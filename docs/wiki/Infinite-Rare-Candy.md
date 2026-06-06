# Infinite Rare Candy

This page documents the Infinite Rare Candy project for the Matroskin pkNX fork.

The short version: I wanted a Run & Bun-style infinite Rare Candy quality-of-life item for Pokemon Royal Sword, but with Royal Sword's own story level caps. I did not want a pile of Rare Candies in the normal Items pocket. I wanted a key item-style tool that could be used forever, route through the real Rare Candy level-up flow, and stop at the current story cap.

That sounds simple until Sword/Shield has opinions.

The final working path proved that I can:

- create a custom level-up item shell in RomFS;
- route item id `1128` through the Rare Candy item-use path;
- keep the item in Key Items;
- stop it from decrementing;
- give it a virtual inventory count so the bag UI remains usable;
- bypass the Exp Candy XL fixed-EXP behavior that normally owns item id `1128`;
- compute a runtime story cap from flag/work state;
- grant the item during the fresh-new-game Bag pickup event through a patched AMX script;
- write a patched `exefs/main` NSO back out as LayeredFS output;
- bring the workflow into pkNX through Royal editor tools.

The public project name is Infinite Rare Candy. Some code and generated notes still use `RoyalCandy` or Royal Sword-branded class names because those were the internal implementation names during the research phase.

## Wiki Navigation

- [Home](Home.md)
- [Development History](Development-History.md)
- [Repository](https://github.com/KotMatrosk1n/pkNX-Fixes)

## Why I Built This

Pokemon Run & Bun was the inspiration. Run & Bun has a convenience item that removes the boring part of level management while preserving the intended challenge. I wanted that same idea for Royal Sword: do not make the player grind, do not ask them to manage stacks of consumables, and do not let them blow past the hack's intended cap ladder.

The design target was:

1. The item should be reusable.
2. The item should live in Key Items.
3. The item should use the vanilla Rare Candy level-up ceremony where possible.
4. The item should respect a custom Royal Sword level cap ladder.
5. The cap ladder should use the hack's trainer/story order, not vanilla badge obedience caps.
6. The output should be a normal LayeredFS patch.
7. The implementation should be documented well enough that future ExeFS edits are not blind magic.

## The Important Constraint

The hardest part was that Sword/Shield item behavior is not only in `item.dat`.

`item.dat` can say an item is usable on Pokemon. It can say the item has a level-up flag. It can put the item in a pouch. It can copy Rare Candy metadata. But it cannot express:

- "this Key Item should open the same party target flow as Rare Candy";
- "this item should not decrement";
- "this item should compute a cap from story flags";
- "this item should use Rare Candy exact-to-next-level behavior instead of an Exp Candy fixed value";
- "this item should show a virtual count even though it is not a normal consumable stack."

Those behaviors are decided in executable code. That is why this project became an ExeFS project.

## Files and Formats I Had To Understand

### RomFS Item Data

The item shell starts in:

- `romfs/bin/pml/item/item.dat`
- `romfs/bin/message/*/common/itemname*.dat`
- `romfs/bin/message/*/common/iteminfo.dat`
- `romfs/bin/appli/shop/bin/shop_data.bin`

`item.dat` is not just a flat array of fixed item rows. Sword/Shield uses an index table plus raw item rows. Several item IDs can point at the same raw row. That mattered because some unused-looking items are only unused as IDs; editing the shared raw row could accidentally mutate other placeholder IDs.

The working generator therefore:

1. reads `item.dat` through `Item8.GetArray`;
2. clones a template row;
3. mutates the selected item's metadata;
4. writes the normal item array back;
5. appends a new raw item row;
6. points only item id `1128` at the appended row.

That last step is one of the early RomFS breakthroughs. It made the selected item unique instead of accidentally editing a shared dummy row.

### RomFS Text

The name and description use pkNX's `TextFile` support with the Sword/Shield text config. The generator rewrites the selected item line across every available language folder it can find:

- `itemname.dat`
- plural/classified `itemname*.dat` variants
- `iteminfo.dat`

The current generated prototype text is:

- name: `Royal Candy`
- plural: `Royal Candies`
- description: `Raises one Pokemon's level up to the current Royal Candy cap.`

That text can be renamed later without changing the ExeFS logic. The item id and route are the important parts.

### Shop Data

Shop data is FlatBuffer-backed. The generator opens `shop_data.bin`, finds the early Poke Mart inventories, and inserts item id `1128` for testing and acquisition. This is not the deep technical part of the project, but it matters because a generated mod is much easier to test when the item is obtainable in a normal place.

### ExeFS `main`

The executable is an NSO file:

- header;
- compressed or uncompressed `.text`;
- compressed or uncompressed `.ro`;
- compressed or uncompressed `.data`;
- segment hashes;
- build ID;
- LZ4 segment compression.

pkNX already had `pkNX.Containers.NSO`, which was a major advantage. It can read `main`, decompress the executable segments, and write a recompressed NSO with updated segment hashes. That meant the problem became "find and safely patch the right ARM64 instructions," not "write an NSO packer from scratch."

## Research Tools I Used

### pkNX and FlatSharp

pkNX provided the project structure, RomFS/ExeFS paths, SWSH file mappings, item parsing, text parsing, NSO reading, and WinForms editor framework. FlatSharp generated the SWSH FlatBuffer classes used for shop data and many existing pkNX editors.

This project would have been much slower without those libraries. I still had to add the Royal-specific reasoning layer, but I did not have to reinvent basic file loading.

### PKHeX.Core

PKHeX.Core became important when I needed save inspection. The cap ladder is only useful if I can check whether a save has the expected flags/work values set. The Royal Sword Save Inspector uses PKHeX.Core to open a Sword/Shield `main` save, then evaluates the milestone ladder against save blocks.

### LLVM

LLVM became useful once I needed real ARM64 disassembly. The workflow was:

1. export the decompressed `.text` segment from `exefs/main`;
2. wrap the raw binary as an AArch64 ELF with `llvm-objcopy`;
3. inspect windows of code with `llvm-objdump`;
4. compare the disassembly against patch offsets and generated branch targets.

The generic shape is:

```powershell
llvm-objcopy -I binary -O elf64-littleaarch64 --rename-section .data=.text,alloc,load,code,readonly main.text.bin main.text.elf
llvm-objdump -d --start-address=0x1410B80 --stop-address=0x1411638 main.text.elf
```

I used this to verify that the flagwork accessors, Rare Candy route checks, and code caves were what I thought they were. The important part was not just seeing bytes. It was seeing control flow.

### Ghidra

Ghidra helped with the same ExeFS question from another angle: xrefs, function boundaries, and local code neighborhoods. Raw command-line disassembly is fast for specific windows; Ghidra is better when I need to understand "what calls this" or "which nearby code path owns this branch."

Ghidra did not magically name everything. Sword/Shield's executable still required manual reasoning, but it made the ARM64 hunting much less blind.

### Custom Royal Sword Prototype Tools

Before the work went into pkNX, I split the prototype out of one giant `Program.cs` into Royal Sword-branded tools. The important prototype files were:

- `RoyalSwordItemRomFsTool.cs`
- `RoyalSwordExeFsPatchTool.cs`
- `RoyalSwordPatchNotesTool.cs`
- `RoyalSwordExeFsResearchTool.cs`
- `RoyalSwordRomFsResearchTool.cs`
- `RoyalSwordScriptAmxTool.cs`
- `RoyalSwordTrainerResearchTool.cs`
- `RoyalSwordFlagworkTool.cs`
- `RoyalSwordSaveResearchTool.cs`
- `RoyalSwordSharedTool.cs`

Those tools were not meant to be the final UI. They were research instruments. They let me dump trainer summaries, scan flagwork tables, inspect AMX scripts, search raw RomFS/ExeFS bytes, patch candidate outputs, generate capped probes, and inspect generated files quickly.

## The Main Dead Ends

### Dead End 1: `item.dat` Alone

The first obvious attempt was to clone Rare Candy into a new item row and change its pouch to Key Items. This created a visible item shell, but it did not reliably enter the full Rare Candy behavior path.

Why it failed:

- Sword/Shield has item metadata in RomFS, but the bag UI and item-use dispatch still have executable-side item routing.
- Key Items and consumables go through different UI expectations.
- Rare Candy has hardcoded checks in `main`.
- The item table cannot express "also treat this key item as Rare Candy for this specific use path."

This was the first major lesson: the RomFS item row can create the item, but ExeFS decides the ceremony.

### Dead End 2: Any Unused Item ID Should Work

I searched for candidate unused slots and tried to reason from item metadata. The problem is that not every unused item id is equally useful. A random unused id can have a nice item row and still miss the exact bag route needed for Rare Candy behavior.

The final working id was `1128`, which is normally in the Exp Candy family range. That was not perfect either, because Exp Candy XL has fixed EXP behavior. But it was close enough to the level-up item pipeline that I could patch the remaining wrong parts.

Why this mattered:

- I stopped treating "unused item" as the main requirement.
- I started treating "can reach the right executable path with minimal patching" as the main requirement.

### Dead End 3: Story Trainer IDs Are Not Save Flags

For the cap ladder, I first wanted to map trainer IDs directly to "defeated" state. The user supplied the trainer milestones, and the natural assumption was that the save had a defeated-trainer bit for each trainer row.

That was too simple.

What I found:

- regular route trainers do appear in placement data;
- story/rival/gym trainers often do not appear as ordinary placement trainers;
- trainer table rows contain team/class/item/money/AI data, not a persistent defeated flag;
- trainer hash tables map trainer IDs and names, but they do not directly give the saved "I beat this fight" value;
- story fights are usually driven by scripts and story progress, not only the trainer row.

This changed the cap research from "find trainer row defeated bit" to "find reliable story progress flags and works near each battle."

### Dead End 4: Search Scripts For Trainer Hashes

I searched AMX scripts and RomFS for trainer IDs and trainer hashes. The result was mostly negative.

What this showed:

- scripts do not store every trainer row hash in plain text;
- scripts do not embed the 64-bit flagwork hashes directly in the obvious way;
- Pawn AMX files import natives by hashed identifiers or indices, not friendly names;
- the runtime likely resolves trainer battle setup and flagwork access through engine-native calls.

That negative result was still useful. It told me not to build the cap ladder on fake precision. If I could not prove a direct trainer defeated value, I needed a safer progress marker.

### Dead End 5: License Flags Are Not Trainer Defeat Flags

The flagwork dump showed useful system flags such as `FSYS_TRLICENCE_EV0570_END`, `FSYS_TRLICENCE_EV0700_END`, and `FSYS_TRLICENCE_EV1260_END`. These were tempting because they looked like clean progress gates.

They are useful, but they are not universal trainer defeat flags. They are license/event gates. I kept them as research clues rather than pretending they answered the whole ladder.

### Dead End 6: "Victory Count" Tables

`system_works.tbl` contains work values that sound like victory counts, especially around postgame tournaments and stadium rematches. Those are real game-state values, but they are not the story-trainer ladder I needed.

Again, this was a useful dead end. It separated postgame/rematch state from the story progress values that actually matter for Infinite Rare Candy.

## Major Breakthroughs

### Breakthrough 1: The Item Shell Was Valid

The RomFS item shell worked well enough to prove that item id `1128` could be renamed, described, shown in shops, and shaped into a Royal Sword item. That meant the problem was no longer "can a custom item exist?" It could.

The problem became "how do I route this item through the exact executable behavior I want?"

### Breakthrough 2: ExeFS Was Editable

The first successful `exefs/main` patch changed the project completely.

Once I could:

- decompress `.text`;
- validate expected instructions;
- write ARM64 branches and comparisons;
- use zero-filled code caves for tiny stubs;
- recompress and write `main`;
- load the output in-game;

the possibility space opened up. This was no longer only an item edit. It became a framework for controlled executable patching.

That is the biggest technical lesson from the project. ExeFS is not off limits. It is risky, and every patch needs build/signature validation, but it is editable.

### Breakthrough 3: The Confirmed Rare Candy UI Route

The confirmed route hook is at:

- compare offset: `text+0x007BC1F8`
- item register: `w8`
- vanilla comparison: `CMP w8, #50`

The patch preserves Rare Candy id `50` and adds item id `1128` through a stub:

1. keep the vanilla Rare Candy comparison;
2. if the item is not Rare Candy, branch to a code cave;
3. compare the same item register against `1128`;
4. if equal, branch into the Rare Candy pass path;
5. otherwise branch to the normal fail path.

That was the first clean proof that a second item could enter the Rare Candy UI route without replacing Rare Candy.

### Breakthrough 4: Item `1128` Needed The Exp Candy Bypass

Item `1128` is normally Exp Candy XL. That helped route it into a useful family, but it also meant the executable tried to treat it like a fixed EXP candy.

The patch changes the Exp Candy upper-bound checks:

- `text+0x007BC1BC`
- `text+0x007BC1C4`

Those checks originally allow Exp Candy index `4`. The patch lowers the upper bound to `3`, so item `1128` no longer enters the fixed 30000 EXP table and can continue into the exact next-level behavior.

This was the moment item `1128` stopped being "an Exp Candy I renamed" and became the working Infinite Rare Candy candidate.

### Breakthrough 5: Non-Consumption Was Executable-Side

The non-consumption patch targets the quantity move at:

- `text+0x007B1F20`

The patch routes the selected use quantity through a stub. If the item id is `1128`, the game receives zero as the decrement quantity. Other items keep the vanilla selected quantity.

This preserves the reusable behavior without needing to fake a massive item stack.

### Breakthrough 6: Virtual Count Made The UI Behave

Even if an item does not decrement, the bag UI still wants a quantity-like value in some paths. The virtual count patch targets the item-count helper at:

- `text+0x01421090`

For item id `1128`, the helper returns a configured virtual count, currently `999`. Other items run the original helper. This gives the UI a stable count without requiring a real consumable stack.

### Breakthrough 7: Story Caps Needed Flagwork And Works

The level cap ladder could not be based only on badge flags or trainer IDs. The final model uses a mix of:

- named gym clear flags, such as `FE_GC_KUSA_CLEAR`;
- story/event flags, such as the early Hop win candidate;
- `WK_SCENE_MAIN_MASTER` thresholds for story progress points where a named post-fight flag was not proven.

The runtime helper checks milestones from highest cap down. The first unlocked milestone wins. That makes later story state override earlier state naturally.

### Breakthrough 8: The Bag Event Can Grant A Real Key Item

The acquisition problem was separate from the item behavior problem. Virtual ownership and virtual count could make the bag helpers claim Royal Candy existed, but the Key Items pocket still needed a concrete saved item entry if I wanted normal inventory persistence.

The working acquisition hook is in `main_event_0020.amx`, the Bag pickup event. I patched that script so a fresh new game receives Royal Candy as part of the Bag event sequence.

The first attempt failed in a useful way. I redirected a no-op call to cell `3881`, which looked unused from a direct-call scan. The game froze before the Bag text box. Later inspection showed why: cell `3881` was not dead code. It was a live dispatcher procedure with `GENARRAY`, `SWITCH`, and `CASETBL` structure. It could be reached through AMX switch/event flow even though my simple direct-call search did not see it.

The fixed version appends a new procedure at the old code/data boundary instead of overwriting a suspicious existing procedure. The Bag event already had a no-op local call at cell `5020`. I retarget that call to the appended procedure, then let the vanilla script continue. The appended procedure calls the same add-item native shape used by `init_scr.amx` for starter inventory:

```text
PROC
PUSHM.P.C 1
PUSHM.P.C 1128
SYSREQ.N 0x8D631FFE, parameter bytes 16
ZERO.pri
RETN
```

That was tested in-game. Loading an older pre-Bag save did not show the item, likely because that save was already inside or past the relevant event state. Starting a completely fresh new game did show Royal Candy after the Bag event. That proves the AMX grant works for the intended new-game flow.

## AMX Script Research

Sword/Shield story scripts are Pawn AMX files. That mattered because I could not treat them like plain text bytecode.

The useful AMX facts were:

- AMX files have a real header;
- the header points to code, data, public, native, and name tables;
- readable script text is separate from compiled AMX logic;
- native calls are not friendly names in the compiled files;
- flagwork values often appear through script/native indirection rather than plain 64-bit hash literals.

I parsed enough AMX structure to stop treating the files as random bytes. That helped explain why simple `rg` searches for trainer names, trainer hashes, or full flag hashes did not find the whole answer.

The key lesson was that story event names and script IDs are still useful even when the exact native calls are not immediately readable. `main_event_0110`, `main_event_0280`, and later event IDs line up with known progression flags and trainer sequences. That gave me a story timeline to compare against saves and flagwork.

### AMX Progress

The AMX tooling is no longer only a reader. It can now do a limited, validated compact-AMX rewrite for the Bag event.

The current AMX patcher can:

- read the AMX header;
- detect 64-bit compact Pawn AMX;
- expand compact code/data memory into cells;
- read native import hashes;
- validate expected cells and native imports;
- append new code cells before the data section;
- adjust `DAT`, `HEA`, and `STP`;
- recompress compact AMX;
- re-expand the generated file and verify the expanded memory matches what I intended to write.

This is still not a full script decompiler. It does not assign friendly names to every native call, reconstruct high-level Pawn source, or understand every public/event entry path. But it is enough for controlled patches where I know the script, cells, native import table, and exact insertion point.

### Cells, Code Spacing, And Landing Pads

AMX code is cell-based. In Sword/Shield's 64-bit AMX files, one cell is eight bytes. Some instructions are a single cell. Others use one or more following cells as operands. Packed instructions can store an opcode and operand inside the same 64-bit cell.

That means "I need eight cells" is different from "I need eight bytes." The Royal Candy Bag grant needs eight cells, or `0x40` bytes of expanded AMX memory:

```text
0: PROC
1: PUSHM.P.C 1
2: PUSHM.P.C 1128
3: SYSREQ.N
4: native index
5: parameter byte count
6: ZERO.pri
7: RETN
```

A landing pad is only safe if the script runtime can branch or call into it and the cells there really are executable code for that purpose. The unsafe cell `3881` looked like an unused `PROC`, but it was actually reachable through switch/dispatcher control flow. That made it a bad landing pad.

The safer landing pad is appended at the end of the code section. I am not stealing existing script code. I add new cells where no old instruction can already depend on them, then retarget a known vanilla no-op call to the new procedure.

### AMX Header Size Limits

The AMX header has explicit bounds:

- `COD`: start of code;
- `DAT`: start of data;
- `HEA`: heap start/end of initialized data;
- `STP`: stack top;
- `CIP`: initial instruction pointer;
- public/native/library/pubvar/tag/name tables before `COD`.

For the Bag patch, I do not change `COD`, `CIP`, or the native table layout. I append code cells at the old `DAT`, then move `DAT`, `HEA`, and `STP` forward by `0x40`.

That means the patch changes the expanded AMX memory layout, but in a very small and controlled way. I am not aware of a practical header size limit hit by this patch. The important constraint is not "can the header represent this?" It can. The important constraint is whether code/data references remain valid after shifting the data section. The Bag event patch was tested in-game and did not break the fresh-new-game event path, but broader AMX insertion should still be treated carefully because data references may exist in other scripts.

### Why Existing Save Tests Can Mislead

The pre-Bag save test did not prove the Bag patch failed. The fresh-new-game test proved the opposite: the patch works when the event runs from the beginning.

Story scripts can have state already queued, cached, or partially advanced in a save. A save that visually appears to be "before Bag pickup" can still resume after the exact call I patched. For acquisition testing, a clean new game is the better proof for this specific hook.

## Flagwork Research

The flagwork tables were smaller and more named than expected. That was good news.

The important tables live under:

- `romfs/bin/flagwork/*.tbl`

They contain named flags/works and hashes. These names made it possible to identify:

- gym clear flags;
- event win flags;
- license/event progression gates;
- system works;
- scene progress works.

The Royal Sword Flagwork Browser now exposes this directly in pkNX. It lists table, index, kind, name, 64-bit hash, and low 32-bit save key evidence. This is not only for Infinite Rare Candy. It is a general Sword/Shield research tool.

## Save Research

The save side was necessary because a cap ladder is only as good as the values it checks.

The Save Inspector opens a Sword/Shield `main` save, evaluates the milestone ladder, and shows:

- save metadata;
- current computed Infinite Rare Candy cap;
- each milestone's cap;
- whether that milestone is unlocked;
- the hash/key being checked;
- the raw saved value;
- notes about why the milestone exists.

This was how I could test probe builds. For example, generating a max-cap probe that only includes milestones up to Bede or Milo made it possible to load a save in Eden and confirm whether the item stopped at the expected cap.

## The Current Cap Ladder

The ladder uses the user's hack caps, not vanilla level caps. Trainer IDs were used to identify the story milestone order, but the runtime ladder uses the best proven flags/works.

Current milestone examples:

| Cap | Marker Type | Meaning |
| --- | --- | --- |
| 16 | flag | Hop 007/008/009 endorsement battle clear, `FE_EV0280_WIN` |
| 20 | work threshold | Hop 191/192/193 Motostoke post-battle progress, `WK_SCENE_MAIN_MASTER >= 530` |
| 23 | work threshold | Bede 195 Galar Mine clear, `WK_SCENE_MAIN_MASTER >= 550` |
| 25 | flag | Milo gym clear, `FE_GC_KUSA_CLEAR` |
| 28 | work threshold | Hop 121/122/123 Hulbury clear |
| 30 | flag | Nessa gym clear, `FE_GC_MIZU_CLEAR` |
| 32 | work threshold | Bede 240 Galar Mine No. 2 clear |
| 36 | work threshold | Marnie 196 Budew Drop Inn clear |
| 38 | flag | Kabu gym clear, `FE_GC_HONO_CLEAR` |
| 40 | work threshold | Hop 124/125/126 Stow-on-Side clear |
| 42 | flag | Bea gym clear, Sword |
| 44 | work threshold | Bede 133 Stow-on-Side mural clear |
| 47 | flag | Opal gym clear |
| 50 | work threshold | Hop 127/128/129 Route 7 clear |
| 52 | flag | Gordie gym clear, Sword |
| 54 | work threshold | Hop 202/203/204 Hero's Bath clear |
| 55 | work threshold | Marnie 138 Route 9/Spikemuth clear |
| 60 | flag | Piers gym clear |
| 65 | flag | Raihan gym clear |
| 70 | work threshold | Hop 130/131/132 Semifinals clear |
| 75 | work threshold | Oleana 143 clear |
| 80 | work threshold | Raihan 213 finals clear |
| 85 | work threshold | Rose 175 clear |
| 90 | work threshold | Leon 149/189/190 clear |

The two confirmed in-game probes so far were important:

- a Bede probe stopped at cap 23;
- a Milo probe stopped at cap 25.

That confirmed the core ladder mechanism and the `>=` logic. The runtime helper checks from highest cap down, so a later progress value wins over earlier values. The `>=` comparison is intentional for work values because story progress works are monotonic thresholds.

## ExeFS Patch Architecture

The ExeFS patcher uses small ARM64 stubs in zero-filled code caves. The pattern is:

1. validate the expected vanilla instruction at a known offset;
2. find an aligned zero run large enough for a stub;
3. patch the original instruction or branch to reach the stub;
4. write a tiny sequence of ARM64 instructions;
5. branch back to the vanilla pass/fail/resume path;
6. write the NSO back with updated hashes.

This is why validation is non-negotiable. If the user's `main` is a different build or already patched, the expected instruction may not match. A blind write at a hardcoded offset could corrupt the executable. The Patch Manager exists to make that risk visible before a mutating build.

### Candy Creation Hook

The "creation" side is really a RomFS plus ExeFS pair.

RomFS creates the Royal Candy item shell:

- item id `1128` is cloned from a safe template;
- it is moved to Key Items;
- it gets Rare Candy-style usable-on-Pokemon metadata;
- it receives unique item-row storage instead of sharing a dummy row;
- message files are patched to show `Royal Candy`;
- test shop inventories can include the item.

ExeFS then teaches the game what that shell should do. The Rare Candy route hook checks for item id `1128` alongside vanilla Rare Candy id `50`. The Exp Candy bypass keeps id `1128` out of the fixed Exp Candy XL behavior. The non-consumption hook makes the use quantity zero for Royal Candy. The cap helper computes the current story cap at runtime.

That is why I think of this as a behavior hook, not only an item edit. The item row is the shell. The executable patches are the behavior.

### Candy Appearing In Bag Hook

The "appearing in bag" hook is the AMX Bag event patch, not the ExeFS route hook.

The ExeFS route hook makes Royal Candy usable once it exists. It does not by itself create a saved inventory entry. The Bag event patch creates the saved entry by calling the script add-item native during the fresh-new-game Bag pickup event.

The current build still keeps virtual ownership/count support because it helps the UI and protects the item-use flow. But the real acquisition proof is the AMX grant in `main_event_0020.amx`.

### ExeFS Cramping

The hardest practical problem in `exefs/main` is not always finding logic. Sometimes it is finding space.

The `.text` segment is full of real executable code. I cannot simply make a function longer in place unless I move surrounding code, rebuild branch targets, and understand relocation-like references. For this project, I chose the safer pattern: overwrite only a tiny anchor instruction with a branch, then place custom code in zero-filled code caves.

The cap ladder is cave-hungry. Every milestone needs several small chunks because each chunk must fit branch ranges and keep registers/flow predictable. As the number of milestones grew, the available nearby caves became cramped. That forced the custom code to stay small, split into chained chunks, and reuse patterns aggressively.

### Identifying Caves

The patcher searches for aligned zero-filled runs inside the decompressed `.text` segment. A cave must be:

- zero-filled;
- four-byte aligned;
- large enough for the planned ARM64 instruction count;
- within branch range of the anchor when a conditional branch needs to reach it;
- not already reserved by an earlier patch chunk in the same build.

The cave allocator marks used caves with real instructions or `NOP`s so later allocations do not accidentally overlap them. That matters because the Infinite Rare Candy patch uses many tiny caves. Overlapping two stubs would produce a build that might still write successfully but behave unpredictably.

### Maintaining Cave-Friendly Custom Code

The custom ARM64 code is intentionally small and repetitive.

I try to keep stubs cave-friendly by:

- using short dispatch stubs that compare one item id and branch;
- replaying overwritten vanilla instructions in tiny trampoline caves;
- avoiding large embedded tables when a chain of small checks will do;
- splitting the cap ladder into milestone chunks;
- using `MOVZ`/`MOVK` only where a full 64-bit hash is required;
- validating every anchor instruction before patching;
- writing generated notes that list which offsets were used.

This is not the prettiest way to write code if I were compiling from source. It is the safer way to write patches into an existing game binary.

### `main` File Size Limits

For the current ExeFS path, I do not append to `main`. I rewrite bytes inside the decompressed `.text` segment, then let the NSO writer recompress the segment and update hashes.

That avoids a large class of file-size problems. The custom code has to fit inside existing zero-filled space in `.text`. If there are no caves left, the patch fails instead of increasing the executable layout.

There may be practical compressed-size and loader constraints for larger NSO rewrites, but Infinite Rare Candy does not rely on growing the executable. The working rule is simpler: if the cave allocator cannot find enough safe space, the build should stop and say so.

### Important ARM64 Helpers

The patcher writes instructions such as:

- `CMP wN, #imm`
- conditional branches (`B.EQ`, `B.NE`, `B.HS`, `B.HI`)
- direct branches (`B`)
- branch with link (`BL`)
- `MOVZ` and `MOVK`
- register moves;
- compare-and-branch;
- conditional select;
- `RET`;
- `NOP`.

These are encoded manually because the patcher writes raw instructions into `.text`. LLVM and Ghidra were used to verify that these helpers generated the intended control flow.

### Runtime Cap Helper

The story cap helper is the most advanced part of the patch.

It writes a chain of code-cave chunks. Each milestone:

1. loads the flagwork global;
2. loads the flagwork object;
3. loads the 64-bit flag/work hash;
4. calls either the flag getter or work getter;
5. compares the result;
6. returns the milestone cap if unlocked;
7. branches to the next milestone if locked.

The helper checks milestones from highest cap to lowest cap. If nothing is unlocked, it returns level 10.

Key runtime anchors:

- flagwork global address: `0x02610798`
- flagwork object offset: `0x1B8`
- flag getter: `text+0x01410F00`
- work getter: `text+0x014114C0`

The helper is shared by:

- the use gate, which decides whether the selected Pokemon can use Infinite Rare Candy;
- the quantity max path, which computes how many levels are useful up to the current cap.

## What The pkNX Integration Added

The project started as a prototype CLI because I needed fast experiments. It ended as a Royal editor suite inside pkNX.

### Royal Editor Mode

The normal pkNX Options menu now exposes `Display Royal Editors`. When enabled, the main editor surface shows only Royal tools. This was necessary because the Royal tools are not ordinary generic editors. They are investigation and patch workflows.

Human-facing Royal buttons are short enough for the fixed pkNX launcher buttons:

- `Candy Builder`
- `Flagwork`
- `Story Events`
- `Dialogue Map`
- `Trainer Map`
- `Save Inspector`
- `Patch Manager`

### Dialogue Map

Dialogue Map started as a broader text editor improvement, then moved to the Royal side because it is directly useful for story-event research. It combines Common and Script text, supports search, and gives better script context than a raw text file list.

### Flagwork

Flagwork is the searchable hash/name browser for `romfs/bin/flagwork/*.tbl`. It exists because flags and works are the backbone of reliable story progress checks.

### Story Events

Story Events connects `main_event_####` message files, script IDs, and AMX presence/metadata. It exists because story fights are often script/event driven rather than placement-trainer driven.

### Trainer Map

Trainer Map correlates:

- trainer IDs;
- trainer names and classes;
- trainer hash names;
- trainer team summaries;
- placement references;
- Royal ladder IDs.

This tool exists because "trainer row" and "story progress marker" are separate systems. I needed one place to see both.

### Save Inspector

Save Inspector opens a Sword/Shield save and evaluates the Infinite Rare Candy milestone ladder. It is the practical verification tool for probe builds and future cap research.

### Patch Manager

Patch Manager is a read-only ExeFS validator. It opens `exefs/main`, reads NSO metadata, checks segment hashes, validates patch anchors, scans for code caves, and warns if the executable already looks patched.

This exists to keep ExeFS editing from becoming a reckless hardcoded-offset workflow.

### Candy Builder

Candy Builder is the mutating editor. It can validate or build a LayeredFS output with:

- Royal Candy item data;
- item names and description;
- shop data;
- patched `exefs/main`;
- Infinite Rare Candy UI route hook;
- Exp Candy XL bypass;
- non-consumption behavior;
- virtual count;
- story-cap ladder;
- Bag pickup AMX grant for fresh-new-game acquisition;
- optional max-cap probe mode;
- generated notes and README.

This is the current user-facing output tool.

## PR Timeline For This Project

The Infinite Rare Candy project overlaps the Royal editor integration PRs:

| PR | Title | Why It Mattered |
| --- | --- | --- |
| #19 | Improve SWSH item and placement labels | Made item fields clearer and added more hash labels from flagwork/trainer tables. |
| #20 | Add Royal Sword tools mode | Added the Royal-only toolset concept and first dashboard entries. |
| #21 | Make Royal editors the Options toggle | Changed the visible Options entry to `Display Royal Editors`. |
| #22 | Move Dialogue Map to Royal editors | Put the story-text map where the Royal research tools live. |
| #23 | Add Royal Sword Flagwork browser | Added the flag/work hash browser needed for cap research. |
| #24 | Add Royal Sword Story Events inspector | Added the story-event and AMX-context inspector. |
| #25 | Add Royal Sword Trainer Map editor | Added trainer/team/placement correlation for milestone research. |
| #26 | Add Royal Sword Save Inspector | Added save-state validation for the cap ladder. |
| #27 | Add Royal Sword Patch Manager | Added read-only ExeFS validation for patch anchors and code caves. |
| #28 | Add Royal Sword Candy Builder | Added the mutating LayeredFS builder for Infinite Rare Candy output. |
| #29 | Document Infinite Rare Candy wiki | Added the first long-form technical wiki page for this project. |

## Why This Was Difficult

The difficulty was not one big unknown. It was a stack of smaller unknowns that all depended on each other.

RomFS could create the item, but not the behavior.

The item behavior was in ExeFS, but ExeFS needed NSO decompression, segment hashing, ARM64 offsets, code caves, and instruction validation.

The cap ladder needed trainer order, but trainer rows did not directly reveal persistent defeated state.

The scripts described story flow, but AMX bytecode did not expose everything as plain names.

Flagwork had names and hashes, but not every promising flag meant what I first wanted it to mean.

Save inspection could prove values, but only after the ladder had candidate markers.

Every breakthrough narrowed the problem. Every dead end removed a false assumption. That is why the final result looks obvious in hindsight: item id `1128`, one confirmed UI hook, an Exp Candy bypass, a non-consumption patch, a virtual count, a story-cap helper. Getting there required proving that each of those pieces was actually the right piece.

## Why ExeFS Editing Matters Beyond This Item

This project proved that the fork can safely approach executable-side behavior changes when RomFS is not enough.

That opens possibilities such as:

- custom item behaviors;
- runtime story-state checks;
- quality-of-life patches that preserve vanilla systems;
- UI route fixes for items that RomFS metadata cannot fully express;
- safer patch validation tools for build-specific executables;
- future Royal Sword systems that depend on live save flags/works rather than static data tables.

The key word is "safely." ExeFS patching should not become random byte writes. The pattern I want is:

1. understand the data path;
2. validate the executable build and anchor instructions;
3. generate patches with clear notes;
4. expose read-only diagnostics before write tools;
5. keep the original game behavior intact where possible;
6. document every offset and assumption.

Infinite Rare Candy is the first full proof of that pattern.

## Current Limits And Future Work

The core item works, but the project still has open technical questions.

The cap ladder should continue to be tested against saves and in-game checkpoints, especially around story/rival milestones that use `WK_SCENE_MAIN_MASTER` thresholds rather than named win flags.

The UI text still uses Royal Candy naming in the generated output. That can be renamed for the public hack once the final player-facing name is decided.

Patch Manager is still read-only. That is intentional for now. Candy Builder writes the known Infinite Rare Candy output, while Patch Manager validates anchors and build state. Later, Patch Manager can become a broader ExeFS patch enable/disable surface.

The AMX tooling is not a complete script decompiler. It is now good enough to inspect headers, native imports, script presence, references, and perform the specific validated Bag-event grant patch. Full Pawn bytecode decompilation would still be a separate project.

The current ExeFS patch is build-specific. If another Sword/Shield build changes instruction offsets, the validator should fail rather than write.

## Final Takeaway

This started as a hunt for an infinite Rare Candy key item.

It became a technical map of how Sword/Shield item behavior crosses RomFS metadata, message files, shop data, flagwork tables, story scripts, save blocks, and executable code. The most important result is not only that Infinite Rare Candy works. The important result is that I now have a repeatable way to investigate and patch executable-side behavior when pkNX's normal data editors cannot express the change.
