# Development History for the Matroskin pkNX Fork

This page documents the first major editor-improvement line merged into `KotMatrosk1n/pkNX-Fixes`. The goal is to explain what changed, why it changed, how the implementation works, what files were created or modified, and what still needs research.

The short version is that this fork has been moving pkNX's Sword/Shield editors away from raw generated-object inspection and toward focused tools that understand the game data. A lot of the old editors technically exposed the data, but they exposed it in the shape FlatSharp generated from the FlatBuffer schemas. That meant users saw raw hashes, `Field##` names, integer arrays, duplicate backing fields, and stock WinForms UI behavior. My work has mostly been about putting a translation layer between "how the game stores this" and "how a user needs to edit this without guessing."

This page covers the Matroskin branch merges from PR #1 through PR #16. The later Royal editor and Infinite Rare Candy work is large enough that I split it into its own page: [Infinite Rare Candy](Infinite-Rare-Candy.md).

## Wiki Navigation

- [Home](Home.md)
- [Infinite Rare Candy](Infinite-Rare-Candy.md)
- [Repository](https://github.com/KotMatrosk1n/pkNX-Fixes)

## Big Architectural Direction

The recurring root cause across the fork was not that pkNX lacked access to the data. It had plenty of access. The problem was that most editors were binding raw data shapes directly into WinForms controls, especially `PropertyGrid`. A generated FlatBuffer class is a storage contract, not a user interface. If I bind it directly, the user gets the storage contract: raw `ulong` hashes, generated type names, anonymous fields, numeric species IDs, and arrays that say `0, 0, 0, 0` instead of "these are IV sentinels" or "these are star-rank reward quantities."

The pattern I introduced is:

1. Keep the serialized model intact.
2. Add lightweight wrapper properties on the generated partial classes when the data has a known gameplay meaning.
3. Add `PropertyDescriptor` and `TypeConverter` layers so the generic editor can display friendly labels, categories, descriptions, dropdown values, and searchable pickers.
4. Hide duplicate raw fields when a safer wrapper exists.
5. Keep raw/unknown fields visible when I cannot prove what they mean. I would rather leave a field honestly unknown than write confident nonsense into the UI.

The key reusable files in this direction are:

- `pkNX.WinForms/Subforms/GenericEditor/TypeRegistrationHelper.cs`
- `pkNX.WinForms/Subforms/GenericEditor/WinFormsTheme.cs`
- `pkNX.WinForms/Subforms/GenericEditor/ThemedConfirmationDialog.cs`
- `pkNX.WinForms/Subforms/GenericEditor/SearchableStandardValuesUITypeEditor.cs`
- `pkNX.WinForms/Subforms/GenericEditor/SearchableComboBoxBehavior.cs`
- The descriptor files under `pkNX.WinForms/Subforms/GenericEditor/*PropertyGridDescriptors.cs`

`TypeRegistrationHelper` is the hub. It registers dynamic type description providers for runtime object types. Those providers rewrite how `PropertyGrid` sees an object: properties can be hidden, renamed, recategorized, given descriptions, converted through a friendly `TypeConverter`, or given a custom editor. This lets me keep the underlying data classes serializable and close to the FlatBuffer schema while still presenting something usable.

`WinFormsTheme` is the shared dark-mode treatment. It applies the same color palette to forms, buttons, combo boxes, grids, property grids, menus, and nested controls. It is intentionally centralized because manually restyling every editor creates little visual mismatches everywhere. The theme does not solve every WinForms limitation, because WinForms enjoys reminding us it is old enough to vote, but it gives every editor the same baseline.

`ThemedConfirmationDialog` is the shared safety prompt. A lot of editor actions are dangerous in quiet ways: save writes data, randomize can mutate many rows, dump can overwrite clipboard contents, and closing a form can discard the current session. The confirmations are not there to annoy users. They are there because "oops, I clicked Randomize" is a bad way to spend an evening.

The two searchable dropdown systems solve slightly different problems:

- `SearchableStandardValuesUITypeEditor` is for `PropertyGrid` cells backed by a `TypeConverter` with standard values. It opens a dark dropdown, filters by prefix, highlights rows, supports IDs in labels, and avoids the native white WinForms autocomplete popup.
- `SearchableComboBoxBehavior` is for standalone combo boxes like trainer selectors. It keeps typed search, deletion, wheel handling, and dropdown sizing predictable.

That dropdown behavior became one of the most important pieces of polish. A lot of pkNX data sets are long: Pokemon, moves, items, raids, trainers, text files. If the dropdown is slow, white, flickery, or loses the real source index, the editor feels broken even when serialization is correct.

## Merge Overview

| PR | Branch | Title | Main Focus |
| --- | --- | --- | --- |
| #1 | `KM/shop-editor-improvements` | Improve shop inventory editing | Rebuilt shop editing around tables and modal item lists. |
| #2 | `KM/pokemon-editor-ui` | Improve Pokemon editor UI and evolution saving | Fixed evolution persistence and made Pokemon selection/theme behavior safer. |
| #3 | `KM/placement-editor` | Improve Placement editor readability | Added zone-first placement browsing and many readable placement summaries. |
| #4 | `KM/item-editor` | Improve item editor machine editing | Restored real TM/TR taught-move editing through the machine table in `item.dat`. |
| #5 | `KM/max-raid-editor` | Improve Max Raid and Dynamax Adventure editors | Added raid descriptors, DA field clarity, and reusable searchable property-grid dropdowns. |
| #6 | `KM/move-editor` | Improve move editor field clarity | Added move descriptors, flag descriptions, stat-change clarity, and tooltips. |
| #7 | `KM/raid-bonus-rewards` | Improve raid bonus reward editor | Fixed reward list casting and clarified reward table usage/quantities. |
| #8 | `KM/rental-editor` | Improve rental editor fields | Added rental Pokemon wrappers and descriptors. |
| #9 | `KM/shiny-rate-editor` | Improve shiny rate editor | Dark-themed shiny rate patching and fixed state detection. |
| #10 | `KM/static-encounter-editor` | Improve static encounter editor fields | Added static encounter wrappers and descriptors. |
| #11 | `KM/symbol-behavior-editor` | Improve Symbol Behavior editor labels | Labeled behavior profiles and made species/behavior selection usable. |
| #12 | `KM/in-game-trades-editor` | Improve in-game trades editor | Added trade wrappers, IV sentinel clarity, and trade dialogue refresh. |
| #13 | `KM/trainer-editor` | Polish trainer editor | Dark-themed trainer editor, saved trainer class edits, class balls, payout display, and faster selection. |
| #14 | `KM/main-window-ui` | Polish main window layout | Added shared branding/title path and made Wild visible in the launcher. |
| #15 | `KM/wild-editor` | Polish Wild Editor layout | Dark-themed and resized Wild Editor encounter/randomization tabs. |
| #16 | `KM/text-editor` | Add Dialogue Map editor for Common and Script text | Added a combined searchable Common/Script text map with syntax helpers and undo/redo. |

## PR #1 - Improve Shop Inventory Editing

Branch: `KM/shop-editor-improvements`

Merged as: `d95bc59494f2e872c7276efe23f90e7b21301077`

### Files I Created

- `pkNX.WinForms/Subforms/GenericEditor/ShopItemListUITypeEditor.cs`
- `pkNX.WinForms/Subforms/GenericEditor/ShopItemNameFormatter.cs`
- `pkNX.WinForms/Subforms/GenericEditor/ShopPropertyGridObjectFactory.cs`
- `pkNX.WinForms/Subforms/GenericEditor/ShopTableView.cs`
- `pkNX.WinForms/Subforms/GenericEditor/ThemedConfirmationDialog.cs`
- `pkNX.WinForms/Subforms/GenericEditor/WinFormsTheme.cs`

### Files I Modified

- `FlatBuffers/SWSH/Gen8/Other/ShopInventory.cs`
- `pkNX.Structures/Converters/ItemConverter.cs`
- `pkNX.WinForms/MainEditor/EditorGG.cs`
- `pkNX.WinForms/MainEditor/EditorSWSH.cs`
- `pkNX.WinForms/Subforms/GenericEditor/GenericEditor.Designer.cs`
- `pkNX.WinForms/Subforms/GenericEditor/GenericEditor.cs`
- `pkNX.WinForms/Subforms/GenericEditor/TypeRegistrationHelper.cs`

### What I Was Fixing

The shop editor was the first big proof that raw `PropertyGrid` binding was the wrong user experience for this fork. The newer fork could open shop data, but the editor showed internal hash fields and nested inventory structures rather than a practical table. Users had to expand `Inventories`, then an inventory index, then `Items`, then numbered integer values. For a user who just wants to change "Potion" to "Great Ball", that is absurdly indirect.

There was also a persistence problem. FlatSharp-generated lists do not always behave like ordinary mutable WinForms-bound lists. Editing an item in place could update what the grid displayed without guaranteeing the data was written back in the shape the serializer expected. The fix needed to treat the inventory list as data to read, edit in a controlled copy, and set back into the owning object.

### How the New Shop Editing Works

`ShopTableView` became the main UI for shop contents. Instead of a raw property grid, it renders each shop inventory as a row with:

- an inventory label,
- an item count,
- a readable item summary,
- an always-visible `Edit...` button.

For single-inventory shops, that table usually has one row. For multi-inventory shops, such as badge-gated marts, each inventory row is visible at once. This matters because users can scan all badge tiers without drilling through nested expanders.

`ShopPropertyGridObjectFactory` builds wrapper objects around the underlying shop data. The wrapper objects expose only the data users need to interact with, while hiding implementation details like the shop hash. The key trick is that `ShopInventoryPropertyGridObject` uses delegates: a getter pulls the current `IList<int>` and a setter writes the edited list back. That makes the UI work with friendly copies but still commit changes to the real FlatSharp object.

`ShopItemListUITypeEditor` is the modal item editor. It opens a dedicated form where each row represents one shop item. Users can add, remove, reorder, and choose items through dropdowns. The modal returns the edited list, then the wrapper setter commits the list back to the inventory.

`ShopItemNameFormatter` centralizes how item IDs become readable text. It is used by the table summaries and the editor rows. This later became important for TM/TR taught moves, because shop labels needed to show `TM94 - False Swipe (619)` rather than just `619` or `TM94`.

`ThemedConfirmationDialog` and `WinFormsTheme` were introduced here because the editor needed consistent dark mode and safer prompts for Save, Dump, Randomize, and close. This was the first pass at the shared UI language the rest of the fork now uses.

### Why This Merge Mattered

This branch established the fork's core editing philosophy. I stopped treating generated data as the UI and started adding editor-specific presentation layers. Shop editing became a normal workflow: choose a shop, click `Edit...`, pick item names, save.

## PR #2 - Improve Pokemon Editor UI and Evolution Saving

Branch: `KM/pokemon-editor-ui`

Merged as: `9a88351fc790172097e960855f57fee010451444`

### Files I Modified

- `pkNX.WinForms/Controls/EvolutionRow.cs`
- `pkNX.WinForms/Subforms/Gen7b/PokeDataUI.cs`
- `pkNX.WinForms/Subforms/GenericEditor/WinFormsTheme.cs`

### What I Was Fixing

The Pokemon editor had two separate categories of problems.

The first was a real data-loss bug in the Evolve tab. If a user changed an evolution method for one Pokemon, then moved to another Pokemon and edited it too, only the first change reliably saved. The workaround was awful: edit one Pokemon, save, close pkNX, reopen, edit the next Pokemon. The root cause was WinForms control state. The active edit still lived in the UI control when the editor switched species, so the underlying evolution data was not always updated before `LoadIndex` replaced the active Pokemon context.

The second issue was the Pokemon selector. Native combo-box autocomplete could update the visible text without actually moving the editor's loaded species index. That created the weird behavior where the dropdown said "Alakazam" but the stats still showed Bulbasaur. Then scrolling nudged the real index and the editor jumped to Kadabra or something nearby. That kind of mismatch is exactly the sort of bug that makes users stop trusting an editor.

### How the Fix Works

The evolution fix ensures active evolution UI state is committed before changing Pokemon and before saving. Conceptually, I made the editor flush the row/control state into the model at the boundary where a species change or save can happen. That way, switching Pokemon is not allowed to discard an in-progress row edit.

The selector fix moved away from trusting raw combo-box text as the source of truth. The selector uses stable entries that preserve the real species index, then applies selection changes through that index. The text shown to the user and the data loaded into the editor now move together.

I also themed the Personal, Learnset, Evolve, Enhancements, and nested Enhancement tabs. The layout itself was intentionally preserved because the user liked the Pokemon editor layout. This was a polish pass, not a redesign.

### UI Details

The Pokemon selector kept typed search and autocomplete, but the filtering became prefix-based and predictable. I had to fight native WinForms autocomplete a bit here. The native dropdown wanted to be white, flicker, and separate text from selection. The final behavior uses a darker custom list and applies selected entries by model index, not by whatever text the control happens to display.

Tab clipping was also addressed by resizing and custom-drawing tab headers. This was one of the first times high-DPI and default WinForms rendering showed up as recurring enemies. Not dramatic enemies, but persistent ones.

## PR #3 - Improve Placement Editor Readability

Branch: `KM/placement-editor`

Merged as: `83b50a2dabc372cf6159b2cebb4c2ea90196a610`

### Files I Created

- `FlatBuffers/SWSH/Gen8/Placement/Zone/Holders/PlacementZoneHolderSummaries.cs`
- `pkNX.WinForms/Subforms/GenericEditor/PlacementPropertyGridDescriptors.cs`
- `pkNX.WinForms/Subforms/GenericEditor/PlacementTableView.cs`

### Files I Modified

- `FlatBuffers/SWSH/Gen8/Placement/Zone/Holders/PlacementZoneFishingPointHolder.cs`
- `FlatBuffers/SWSH/Gen8/Placement/Zone/Holders/PlacementZoneMovementPathHolder.cs`
- `FlatBuffers/SWSH/Gen8/Placement/Zone/Holders/PlacementZoneOtherNPCHolder.cs`
- `FlatBuffers/SWSH/Gen8/Placement/Zone/Holders/PlacementZoneParticleHolder.cs`
- `FlatBuffers/SWSH/Gen8/Placement/Zone/Holders/PlacementZoneStaticObjectsHolder.cs`
- `FlatBuffers/SWSH/Gen8/Placement/Zone/Holders/PlacementZoneTrainerHolder.cs`
- `FlatBuffers/SWSH/Gen8/Placement/Zone/Holders/PlacementZoneUnitObjectHolder.cs`
- `FlatBuffers/SWSH/Gen8/Placement/Zone/Holders/PlacementZoneWarpHolder.cs`
- `FlatBuffers/SWSH/Gen8/Placement/Zone/PlacementZone8.cs`
- `pkNX.WinForms/MainEditor/EditorSWSH.cs`
- `pkNX.WinForms/Subforms/GenericEditor/GenericEditor.cs`
- `pkNX.WinForms/Subforms/GenericEditor/TypeRegistrationHelper.cs`

### What I Was Fixing

Placement was almost pure FlatSharp output before this merge. The first screen showed filenames and raw hashes. Opening a zone showed a property grid full of `Field##` names, raw object paths, full model paths, generated type names, and unresolved hashes. It technically displayed data, but it did not explain the map.

Placement is complicated because it is not one data type. A zone can contain field items, hidden items, NPCs, travel points, warps, static objects, symbols, trainers, triggers, particles, movement paths, ladders, berry trees, and assorted still-unmapped holders. A single generic view was never going to make that pleasant.

### How the Zone Table Works

`PlacementTableView` provides a zone-first view. The main Placement editor no longer drops users directly into every raw property. It shows a table where each zone has:

- a zone index,
- a readable zone label when known,
- spawn/object summary counts,
- item summary counts,
- NPC/travel summary counts,
- an `Edit...` action.

This lets users answer the first important question: "Which zone am I looking at?" Once the right zone is chosen, the detail editor still uses a property grid, but with better descriptors and summaries.

### How Placement Summaries Work

`PlacementZoneHolderSummaries.cs` is a large set of partial-class `ToString()` and summary helpers. This is the translation layer between generated holder objects and human-readable rows. It contains `PlacementZoneLabelProvider` and `PlacementZoneSummaryUtil`.

`PlacementZoneLabelProvider` holds lookup tables for things like:

- zone hashes,
- object hashes,
- item hashes,
- static encounter hashes,
- known NPC/model names.

`PlacementZoneSummaryUtil` formats repeated concepts such as coordinates, model paths, object identifiers, and unresolved hashes. A raw path like `bin/field/model/unit_obj/unit_obj_otoku01/unit_obj_otoku01.gfbmdl` can be summarized as `unit_obj_otoku01`. Coordinates are shown as `@ (x, y, z)`. Unknown hashes are still shown, but with context.

The placement holder partials summarize common categories:

- field items show item names, quantities, object names, and positions,
- hidden items show item names, chances, and positions,
- fishing points show fishing object placement,
- flight anchors show fly point unlocks,
- NPC holders show NPC models and coordinates,
- trainer tips and advanced tips show sign/object labels,
- static objects show linked static encounter summaries when the hash can be resolved,
- warps show destination object/path hints.

### Descriptor Layer

`PlacementPropertyGridDescriptors.cs` adds a placement-specific `PropertyDescriptor` layer. It does three important things:

1. It renames fields that are known.
2. It cleans values that are noisy but meaningful, such as full asset paths.
3. It leaves unknowns visible when I do not have a safe name.

I intentionally did not over-label every `Field##`. Some placement values are clearly coordinates, model names, toggles, arrays, or hashes. Others still need in-game tests. The wiki should say this bluntly: a wrong label is worse than an unknown label because it sends modders in the wrong direction.

### Remaining Work

Placement still has a lot of research left. Trainer placement in particular still needs deeper decoding. The editor now shows model, battle hash, movement path, placement, ranges, and settings, but the actual `TrainerID` linkage is still not fully resolved. Field items also deserve a dedicated item picker later; right now the summaries are much better, but editing still uses the property grid.

## PR #4 - Improve Item Editor Machine Editing

Branch: `KM/item-editor`

Merged as: `224c3e23ca1a989c44b7875c170e2f27629e83f8`

### Files I Created

- `pkNX.Structures/Item/Item8MachineTable.cs`

### Files I Modified

- `pkNX.Structures/Item/Item8.cs`
- `pkNX.WinForms/MainEditor/EditorSWSH.cs`
- `pkNX.WinForms/Subforms/GenericEditor/GenericEditor.Designer.cs`
- `pkNX.WinForms/Subforms/GenericEditor/GenericEditor.cs`
- `pkNX.WinForms/Subforms/GenericEditor/ShopItemNameFormatter.cs`

### What I Was Fixing

This was one of the biggest functional breakthroughs in the fork: TM/TR taught-move editing.

The item editor could edit visible item metadata, but changing `GroupType` or `GroupIndex` was not enough to change what a TM/TR teaches in Sword/Shield. The real taught move lives in a machine table embedded inside `item.dat`. Without decoding that table, the editor could look like it was changing machine data while the actual taught move stayed vanilla.

There was also a nasty selector bug around TM/TR scrolling. Filtered combo-box entries were using temporary filtered indexes instead of source item indexes. TM/TR entries made the bug obvious because their logical order (`TM00`, `TM01`, `TR00`, etc.) is not the same as raw item ID order. Scrolling could skip entries, jump to unrelated items, or flicker the native autocomplete list.

### How `Item8MachineTable` Works

`Item8MachineTable` decodes and writes the 200-entry Sword/Shield machine table from `item.dat`.

The lookup I implemented is:

- The table offset is stored in `item.dat` at byte offset `2`.
- That stored value is read as little-endian `ushort`.
- The actual byte offset is `ReadUInt16LE(item.dat[2..]) * 2 + 0x44`.
- There are 200 machine slots.
- Each entry is 4 bytes.
- The taught move is the little-endian `ushort` at entry offset `+2`.

The slots are mapped as:

- slot `0` through `99`: TMs, with `TM00` at slot `0`;
- slot `100` through `199`: TRs, with `TR00` at slot `100`.

`Item8MachineTable.TryGetMachineSlotForItem` maps actual item IDs to machine slots using `Legal.Pouch_TM_SWSH` and `Legal.Pouch_TR_SWSH`. `TryGetMoveForItem` maps an item ID to the taught move. `SetMove` updates the in-memory table. `WriteTo` writes the edited machine table back into the outgoing `item.dat` byte array.

`Item8.GetArray` now constructs `Item8` instances with a shared machine table. `Item8.Write` writes normal item records and then writes the table through the first item's shared table. That is important: the machine table is not per item; it is a shared table in the file.

### New Item Wrapper Properties

`Item8.cs` gained machine-facing properties:

- `IsTechnicalMachine`
- `TechnicalMachineType`
- `TechnicalMachineNumber`
- `TechnicalMachineSlot`
- `TechnicalMachineMove`

These wrapper properties let the property grid present "Machine Type", "Machine Number", and "Teaches Move" instead of raw `GroupType`, `GroupIndex`, and table bytes. `TechnicalMachineMove` uses a move converter so the user can select any move in the SWSH move list.

### UI and Search Fixes

The generic editor selector now stores both display text and original source index. That prevents filtered rows from becoming fake indexes. For TM/TR scrolling, the editor handles machine ordering explicitly so one wheel notch moves from `TM01` to `TM02`, not `TM03` or some unrelated item.

`ShopItemNameFormatter` was updated to read the same machine table. That means shop displays reflect edited TM/TR moves. If a user changes TM28 to teach Flamethrower, the shop editor's item label follows the edited machine table rather than hardcoded vanilla legal arrays.

### Current Limitation

This changes the move taught by the TM/TR item. It does not change Pokemon learnability. TM/TR compatibility is slot-based. If `TM28` is edited to teach Flamethrower, Pokemon that can learn `TM28` will learn Flamethrower unless compatibility data is edited separately later.

## PR #5 - Improve Max Raid and Dynamax Adventure Editors

Branch: `KM/max-raid-editor`

Merged as: `3aa7bc5baffe53c512b6801ff8fac791834c48a0`

### Files I Created

- `pkNX.WinForms/Subforms/GenericEditor/RaidPropertyGridDescriptors.cs`
- `pkNX.WinForms/Subforms/GenericEditor/SearchableStandardValuesUITypeEditor.cs`

### Files I Modified

- `FlatBuffers/SWSH/Gen8/Nest/EncounterNestArchive.cs`
- `FlatBuffers/SWSH/Gen8/Nest/EncounterUndergroundArchive.cs`
- `FlatBuffers/SWSH/Schemas/NestHoleUndergroundArchive.fbs`
- `pkNX.WinForms/MainEditor/EditorSWSH.cs`
- `pkNX.WinForms/Subforms/GenericEditor/GenericEditor.cs`
- `pkNX.WinForms/Subforms/GenericEditor/TypeRegistrationHelper.cs`

### What I Was Fixing

The Max Raid editor originally only exposed a tiny subset of raid tables in the dropdown. The older pkNX editor showed tables up through 196, but this fork only showed the current table. The first bug was UI-side: the top selector treated the currently selected text as an active search filter when opening the dropdown. If the text was `Sword - 0`, the list filtered itself down to `Sword - 0`.

The second issue was meaning. Raid data was still raw generated data: species IDs, move IDs, ability values, star probability arrays, table hashes, and generated object names. The user needed den tables, slot meaning, and Dynamax Adventures fields to be readable.

There was also a real randomization bug in Dynamax Adventures. The old randomizer used `Random.Next(1, 4)`, generating raw values `1`, `2`, and `3`. I inspected the shipped `underground_exploration_poke.bin` data and found 273 entries using only `0`, `1`, and `2`. Raw `3` has meaning elsewhere as `Ability1Or2`, but it is not used by the base DA table.

### Raid Descriptor Layer

`RaidPropertyGridDescriptors.cs` became the central place for raid-specific presentation logic. It adds:

- named categories,
- readable labels,
- descriptions,
- type converters for species, moves, abilities, gender, game version, IV sentinels, table IDs, and reward entries,
- derived property rows such as placement usage and reward table usage.

For Max Raid slots, the descriptor layer summarizes species, form, star range, and star probabilities. Instead of seeing an array like `35, 0, 0, 0, 0`, the user sees star-oriented fields and summaries.

For Dynamax Adventures, `EncounterUndergroundArchive.cs` exposes wrapper properties:

- `SpeciesID`,
- `MoveSlot1` through `MoveSlot4`,
- enums for Gigantamax state,
- version,
- shiny roll,
- and clearer ability behavior.

The DA editor now shows entries by species and index instead of just raw numbers.

### Searchable PropertyGrid Dropdowns

`SearchableStandardValuesUITypeEditor.cs` was introduced here. It is now the reference pattern for property-grid dropdowns with many possible values.

The editor works by:

1. Asking the field's `TypeConverter` for its standard values.
2. Converting those values to display strings.
3. Showing them in a custom dark `UserControl` hosted by `IWindowsFormsEditorService`.
4. Filtering by prefix as the user types.
5. Matching numeric IDs in parentheses when useful.
6. Resizing to the number of visible matches.
7. Returning the selected object value, not just display text.

This matters because property grids normally use stock dropdowns. Stock dropdowns are fine for five values and painful for 900 moves.

### DA Ability Randomization

The fix uses named raid ability roll constants:

- `0`: Ability 1,
- `1`: Ability 2,
- `2`: Hidden Ability.

It no longer rolls raw `3` for the base DA table. I documented that raw `3` is still meaningful as `Ability1Or2` in broader raid data, but it should not be randomly introduced into DA encounters unless a future research pass proves the DA table expects it.

## PR #6 - Improve Move Editor Field Clarity

Branch: `KM/move-editor`

Merged as: `430e1cff8b0ec686d0d1df62e2fb4d8a1b90b489`

### Files I Created

- `pkNX.WinForms/Subforms/GenericEditor/MovePropertyGridDescriptors.cs`

### Files I Modified

- `FlatBuffers/SWSH/Gen8/Waza/Waza.cs`
- `pkNX.WinForms/MainEditor/EditorSWSH.cs`
- `pkNX.WinForms/Subforms/GenericEditor/GenericEditor.cs`
- `pkNX.WinForms/Subforms/GenericEditor/TypeRegistrationHelper.cs`

### What I Was Fixing

The Move editor exposed the `Waza` FlatBuffer fields directly. Fields like `Type`, `Category`, `Target`, `Inflict`, `Stat1`, `Stat1Percent`, and `Stat1Stage` were editable but cryptic. Move flags were worse: generated names like `FlagMetronome` or `FlagProtect` were not always phrased as user-facing behavior. Some fields also appeared twice because wrapper-style values existed alongside raw backing fields.

### How the Move Wrapper Works

`Waza.cs` gained typed wrappers for known move concepts. The descriptor layer in `MovePropertyGridDescriptors.cs` then hides the duplicate raw fields and displays friendlier properties.

The move editor groups fields into categories such as:

- identity,
- core stats,
- targeting/timing,
- secondary effects,
- stat changes,
- flags,
- raw/unknown.

Stat changes were especially important. The UI now presents three explicit stat-change slots. Each slot has:

- affected stat,
- chance,
- stage delta.

Stage values are signed in gameplay terms: positive stages raise stats and negative stages lower stats. The descriptors make that relationship clearer instead of leaving users to infer it from three unrelated raw fields.

### Tooltips

This merge also improved the generic property grid so it can show hover tooltips from descriptor descriptions. That is valuable for move flags because names alone are not always enough. For example:

- `Makes Contact` means the move can trigger contact effects.
- `Blocked By Protect` means Protect-like moves can block it.
- `Recharge Turn` marks Hyper Beam-style recharge behavior.
- `Sound Move` marks sound-based move behavior.
- `Callable By Metronome` controls whether Metronome can call the move.

Unknown fields stayed visible under raw/unknown categories. Again, I did not want to make up fake certainty.

## PR #7 - Improve Raid Bonus Reward Editor

Branch: `KM/raid-bonus-rewards`

Merged as: `3b2a7bc194f0864486bdbf7d4a40892fdf37f276`

### Files I Modified

- `FlatBuffers/SWSH/Gen8/Nest/NestHoleDistributionRewardArchive.cs`
- `FlatBuffers/SWSH/Gen8/Nest/NestHoleRewardArchive.cs`
- `pkNX.WinForms/MainEditor/EditorSWSH.cs`
- `pkNX.WinForms/Subforms/GenericEditor/RaidPropertyGridDescriptors.cs`
- `pkNX.WinForms/Subforms/GenericEditor/TypeRegistrationHelper.cs`

### What I Was Fixing

The Raid Bonus Rewards editor had a broken `Rewards` row. It could show a FlatSharp/list-cast error instead of reward data. The root cause was generic list invariance in C#.

The generated reward table held an `IList<NestHoleReward>`, while the editor wanted an `IList<INestHoleReward>`. You cannot directly cast `IList<NestHoleReward>` to `IList<INestHoleReward>` because generic lists are invariant. Even though every `NestHoleReward` implements `INestHoleReward`, the list type itself is not substitutable.

### How the Cast Fix Works

The fix projects the list:

- `Entries.Cast<INestHoleReward>().ToList()`

That gives the editor an interface list it can inspect safely. It is a small technical fix, but it is exactly the kind of thing that turns "Specified cast is not valid" into a usable editor.

### Reward Table Improvements

`RaidPropertyGridDescriptors.cs` was expanded to understand reward tables. Reward rows now show:

- reward index,
- item name and ID,
- star-bucket quantities or chances,
- nonzero buckets in collapsed summaries,
- reward table usage.

For bonus rewards, values are labeled as quantities by star rank:

- `1-Star Quantity`,
- `2-Star Quantity`,
- `3-Star Quantity`,
- `4-Star Quantity`,
- `5-Star Quantity`.

For normal raid rewards, the same structure is treated as drop-chance percentages. The UI names differ because the gameplay meaning differs.

The bottom `Reward Table ID` row was renamed to `Internal ID` and shown with hex. I kept it editable for advanced users, but documented that changing it without updating raid slots that reference it can disconnect the table.

### Usage Summaries

The editor derives "Used By" summaries from `nest_hole_encount.bin`. That lets table labels say things like:

- game version,
- slot count,
- den count,
- star range,
- species count.

The top dropdown was shortened from giant repeated examples to compact table summaries. Long dropdown names are not documentation; they are just UI clutter with ambition.

## PR #8 - Improve Rental Editor Fields

Branch: `KM/rental-editor`

Merged as: `9cbbed1c78425ff33051bb724f5b5db2d1ae0d69`

### Files I Created

- `pkNX.WinForms/Subforms/GenericEditor/RentalPropertyGridDescriptors.cs`

### Files I Modified

- `FlatBuffers/SWSH/Gen8/Other/Rental.cs`
- `pkNX.WinForms/MainEditor/EditorSWSH.cs`
- `pkNX.WinForms/Subforms/GenericEditor/TypeRegistrationHelper.cs`

### What I Was Fixing

The Rental editor used internal hashes and raw numeric IDs. Rentals had enough structure to show Pokemon, moves, items, ability, ball, nature, gender, EVs, and IVs, but the editor was presenting the generated archive shape instead of the Pokemon-level meaning.

### How the Rental Wrappers Work

`Rental.cs` gained writable wrapper properties like:

- `SpeciesID`,
- `AbilitySlot`,
- `MoveSlot1`,
- `MoveSlot2`,
- `MoveSlot3`,
- `MoveSlot4`.

These wrappers read and write the underlying raw integer fields. The serialized layout does not change; only the property-grid view changes.

`RentalPropertyGridDescriptors.cs` adds converters and field metadata for species, moves, held items, balls, natures, gender, ability slot, EVs, and IVs. It hides duplicate raw fields when a wrapper exists.

The top selector now identifies rental entries by index, species, level, and short move preview. A rental entry is no longer just a mysterious hash.

### Known Unknowns

`Hash1` and `Hash2` remain unresolved internal rental references. I kept them visible as advanced fields and formatted them as hashes. `TrainerID` also remains cautiously labeled because the base data appears to leave it unused or at least not straightforwardly user-facing.

## PR #9 - Improve Shiny Rate Editor

Branch: `KM/shiny-rate-editor`

Merged as: `3681d46afa5ed4fa5e7294811d6b9af9a8346f80`

### Files I Modified

- `pkNX.Game/Editors/ShinyRate/ShinyRateSWSH.cs`
- `pkNX.WinForms/Subforms/Gen7b/ShinyRate.Designer.cs`
- `pkNX.WinForms/Subforms/Gen7b/ShinyRate.cs`
- `pkNX.WinForms/Subforms/GenericEditor/WinFormsTheme.cs`

### What I Was Fixing

The shiny rate editor worked, but it looked and felt like an older light-mode utility dialog. More importantly, it exposed the patch as a reroll count without enough explanation. Users needed to know what the reroll count meant, what chance it produced, and how to pick a reasonable target.

There was also a real state-detection bug in `ShinyRateSWSH.IsDefault`. It returned `!IsAlways && !IsAlways`, which means it checked the same condition twice and failed to distinguish a fixed reroll patch from default state.

### How the Editor Works Now

The editor now presents three modes:

- default,
- fixed reroll count,
- always shiny.

It explains that the patch changes the number of PID generation rolls, not the game's `IsShiny` logic itself. That distinction matters because the patch increases opportunities for a shiny PID rather than rewriting the shiny check.

The UI displays:

- current roll count,
- approximate chance,
- odds text,
- target percentage helper,
- quick presets,
- save confirmation.

The target helper converts a desired overall shiny chance into the closest supported PID roll count. The save prompt warns that this edits the ExeFS `main` patch.

### Why the State Fix Matters

If the editor misdetects a fixed reroll patch as default, users can reopen the editor and think their patch disappeared. The data may still be patched, but the UI lies. That is a nasty category of bug because it erodes trust. Fixing the boolean logic lets the UI report default, fixed, and always-shiny states correctly.

## PR #10 - Improve Static Encounter Editor Fields

Branch: `KM/static-encounter-editor`

Merged as: `0c54a6ed1689c29bd4bb9268f9027716a21fba5f`

### Files I Created

- `pkNX.WinForms/Subforms/GenericEditor/StaticEncounterPropertyGridDescriptors.cs`

### Files I Modified

- `FlatBuffers/SWSH/Gen8/Static/EncounterStaticArchive.cs`
- `pkNX.WinForms/MainEditor/EditorSWSH.cs`
- `pkNX.WinForms/Subforms/GenericEditor/TypeRegistrationHelper.cs`

### What I Was Fixing

Static encounters were mostly raw IDs. Users saw species numbers, item numbers, move numbers, nature values, ability values, shiny lock values, and internal hashes. Several useful wrapper-style values were also read-only before this pass, so the editor could display a friendly value but not edit it safely.

### How the Static Encounter Wrappers Work

`EncounterStaticArchive.cs` gained writable wrappers:

- `SpeciesID`,
- `GenderType`,
- `AbilitySlot`,
- `MoveSlot1` through `MoveSlot4`,
- `Moves`.

Those wrappers read/write the raw serialized fields, preserving the underlying FlatBuffer data shape.

`StaticEncounterPropertyGridDescriptors.cs` handles field grouping and converters. It gives dropdowns for:

- species,
- held item,
- nature,
- gender,
- ability slot,
- shiny lock,
- moves,
- IV values.

The top dropdown now identifies encounters by species, level, scenario, and a short move preview. That makes finding an encounter much less like hunting through a list of numbers.

### Unknowns

`Field0A` and `Field0C` remain unmapped. I left them editable under raw/unknown fields. They need in-game testing before I would rename them.

## PR #11 - Improve Symbol Behavior Editor Labels

Branch: `KM/symbol-behavior-editor`

Merged as: `9a5863db844f978fc056d23c0b432052aecbd698`

### Files I Created

- `pkNX.WinForms/Subforms/GenericEditor/SymbolBehaviorPropertyGridDescriptors.cs`

### Files I Modified

- `pkNX.WinForms/MainEditor/EditorSWSH.cs`
- `pkNX.WinForms/Subforms/GenericEditor/TypeRegistrationHelper.cs`

### What I Was Fixing

Symbol Behavior controls overworld Pokemon behavior profiles: whether a Pokemon approaches, stares, runs away, uses special movement, or follows a unique species pattern. The editor exposed behavior strings, but those strings were internal labels like `Maggyo`, `Haneru`, `Massuguma`, and `Ziguzaguma`. They are meaningful if you already know the Japanese/internal naming patterns, but not great for normal editing.

### What I Mapped

I compared the behavior strings against actual SWSH symbol behavior data and common species usage. The descriptor layer now gives better descriptions where I could identify patterns, for example:

- `Appeal`: attention/approach behavior,
- `Approach`: direct approach behavior,
- `Escape`: flees from the player,
- `Haneru`: splash/flop style behavior,
- `Maggyo`: Stunfisk-style trap behavior,
- `WaterDash`: Sharpedo-style dash/homing behavior,
- species-flavored behavior names like Diglett-style burrow/pop-up and Zigzagoon-style movement where applicable.

Some broad profiles remain conservative because they are shared by many species and likely represent general AI profiles rather than one exact action.

### How the Editor Changed

The top selector now shows:

- entry index,
- localized species name,
- form suffix when present,
- behavior profile.

The behavior field uses the searchable dark dropdown, but still allows custom/internal strings for advanced experimentation. That is important because behavior strings are not necessarily a closed set forever; DLC or unused profiles may exist.

Unknown tuning fields remain visible as advanced/raw values. I hid noisy defaults only where they were clearly not useful as normal settings.

## PR #12 - Improve In-Game Trades Editor

Branch: `KM/in-game-trades-editor`

Merged as: `63b7a5761255d5c128a30467643a2f89e3e30f36`

### Files I Created

- `pkNX.WinForms/Subforms/GenericEditor/TradePropertyGridDescriptors.cs`

### Files I Modified

- `FlatBuffers/SWSH/Gen8/Trade/EncounterTradeArchive.cs`
- `pkNX.WinForms/MainEditor/EditorSWSH.cs`
- `pkNX.WinForms/Subforms/GenericEditor/StaticEncounterPropertyGridDescriptors.cs`
- `pkNX.WinForms/Subforms/GenericEditor/TypeRegistrationHelper.cs`

### What I Was Fixing

In-game trades had raw species IDs, required species IDs, ball IDs, ability numbers, relearn move IDs, shiny locks, nature values, and IV sentinel values. The editor also saved the trade archive without refreshing related `field_trade` dialogue, so a user could change a trade and leave the in-game prompt describing the old Pokemon.

### Trade Wrapper Properties

`EncounterTradeArchive.cs` gained Pokemon-facing wrappers for:

- received species,
- requested species,
- held item,
- received nature,
- required nature,
- gender,
- shiny lock,
- ability slot,
- relearn moves,
- ball.

`TradePropertyGridDescriptors.cs` adds dropdowns for all the obvious Pokemon data:

- species,
- items,
- moves,
- natures,
- gender,
- ability slot,
- shiny lock,
- balls,
- IVs.

The top selector now shows the requested Pokemon, received Pokemon, and level. That answers the only question users care about when choosing a trade: "Which trade is this?"

### IV Sentinel Clarification

The HP IV value `-4` is special. It is not a literal IV and does not mean HP itself is `-4`. It acts as the sentinel for "three randomly chosen perfect IVs" in the existing dump/interpretation logic. I exposed it as `3 Random Perfect IVs (-4)` and only offered it where the existing data path expects it.

Other IVs use:

- `Random (-1)`,
- fixed `0` through `31`.

### Dialogue Refresh

Manual save now refreshes `field_trade` dialogue lines for every in-game trade. It uses the edited requested species/form and received species/form before serializing. This keeps data and text aligned, which is critical because trade prompts are part of the user-facing game behavior.

## PR #13 - Polish Trainer Editor

Branch: `KM/trainer-editor`

Merged as: `7a2aa8ebabacf7b34d2fb06544757e717da8ed4a`

### Files I Created

- `pkNX.WinForms/Subforms/GenericEditor/SearchableComboBoxBehavior.cs`

### Files I Modified

- `pkNX.Game/Editors/TrainerEditor.cs`
- `pkNX.Structures/VsTrainer/Base/TrainerClass.cs`
- `pkNX.WinForms/Controls/StatEditor.cs`
- `pkNX.WinForms/Dumping/Gen8/GameDumperSWSH.cs`
- `pkNX.WinForms/Subforms/Gen7b/BTTE.cs`
- `pkNX.WinForms/Subforms/GenericEditor/WinFormsTheme.cs`

### What I Was Fixing

The trainer editor was already one of the more functional editors, but it still needed polish and a few real fixes.

The biggest data fix was trainer class saving. I added a Class Ball dropdown for unique trainer classes, but trainer class data was cached without a proper write-back path. That meant class-level edits could appear to work in the UI and then disappear after saving.

The money field was also confusing. The stored trainer value is a rate/multiplier, but users care about the actual payout. The payout depends on trainer money rate and the highest level Pokemon in the trainer's party.

The UI had layout issues after adding Class Ball. Money, Mode, and Class Ball were cramped and overlapping. Checkboxes also looked bad in dark mode until their colors were corrected.

### Trainer Class Saving

`TrainerClass.cs` now exposes:

- `Write() => (byte[])Data.Clone()`

`TrainerEditor.Save()` now writes cached trainer class data back through that payload. This means class-level edits survive saving. Cancel behavior rolls those edits back with the rest of the trainer cache.

### Class Ball Logic

Class Ball is class-level, not per-Pokemon and not necessarily per individual trainer. That is why I intentionally limited the dropdown to unique trainer classes: cases where one distinct trainer name owns the class, such as important named trainers. I did not want the UI to imply a per-trainer ball override when changing the field would affect every trainer sharing that class.

### Money Display

The editor still stores the game's raw money rate internally, but the dropdown displays the calculated payout. The calculation uses the trainer's highest-level Pokemon and the Sword/Shield payout formula. The dumper was updated to show both raw rate and computed payout, so output stays honest for technical users.

### Search and Theme

`SearchableComboBoxBehavior.cs` provides a standalone dark searchable dropdown behavior. It supports:

- type-to-search,
- prefix filtering,
- deletion/backspace without getting stuck,
- dark popup rendering,
- selection by real source index,
- predictable wheel behavior,
- proper focus behavior.

The trainer editor got a full dark-theme pass across:

- main tabs,
- Pokemon tabs,
- dropdowns,
- checkboxes,
- context menus,
- stat colors,
- team sprites,
- confirmation prompts.

I also cached trainer Pokemon sprites so switching trainers does less repeated image work. Tiny performance improvements add up when the user is jumping through many trainers.

## PR #14 - Polish Main Window Layout

Branch: `KM/main-window-ui`

Merged as: `e32531c7aa945fe8be1a1c8fd71dd6f0d169cb7d`

### Files I Created

- `pkNX.WinForms/Branding.cs`

### Files I Modified

- `pkNX.WinForms/Main.cs`
- `pkNX.WinForms/UI/MainWindow.xaml.cs`

### What I Was Fixing

The main launcher window hid the Wild button at startup unless the user manually resized the window. Wild was sorted near the bottom because the launcher sorted buttons by title, not by editing priority. The title also needed a consistent shared path so attribution and active game context would not be scattered across multiple places.

### What Changed

`Branding.cs` centralizes title/branding strings. `Main.cs` and `MainWindow.xaml.cs` use that shared branding path so the window title consistently shows the active pkNX/game context and attribution.

`MainWindow.xaml.cs` also changed the editor button sort key. Wild is sorted next to Items, and Dialogue Map later gets sorted next to Trainers. The launcher now uses a cleaner five-column grid. That made Wild visible immediately and created room for future editor buttons.

This was not a deep data change, but it mattered. The first window is the user's front door. If a major editor button is hidden on launch, the front door has a chair in front of it.

## PR #15 - Polish Wild Editor Layout

Branch: `KM/wild-editor`

Merged as: `fb08f037be75651b9cc07ba77e5e4f90b4c76f38`

### Files I Modified

- `pkNX.WinForms/Controls/EncounterList8.Designer.cs`
- `pkNX.WinForms/Controls/EncounterList8.cs`
- `pkNX.WinForms/Subforms/Gen8/SSWE.Designer.cs`
- `pkNX.WinForms/Subforms/Gen8/SSWE.cs`

### What I Was Fixing

The Wild Editor had two layout problems. On encounter tabs, the table could be tiny while the form had huge empty margins. On the randomization tab, the options were cramped despite available space. High-DPI scaling also caused clipping around the Save button and header.

This was a weird UI fight because fixing one size often broke another. If the form was sized for randomization, the encounter grid looked stranded. If it was sized for encounters, randomization felt squeezed. The final answer was to size the form around the active tab's actual content instead of letting designer-time sizes dominate everything.

### Encounter Tab Changes

`EncounterList8` now uses:

- dark-themed grid rendering,
- fixed sensible column widths,
- compact species column,
- stable row heights,
- left weather tabs with dark styling,
- no unnecessary vertical scrollbar in the normal encounter layout,
- a runtime-sized parent form.

The table is wide enough for Sprite, Species, Form, and Chance, but not so wide that Species becomes a runway. The lower blank extension was removed by making the window follow the content.

### Randomization Tab Changes

The randomization tab keeps the wider layout it needs for options. It uses the space more deliberately, with the randomize button, fill-empty toggle, level multiplier, and property-grid options spaced out instead of jammed together.

### Safety Prompts

Save, randomization, and close now use the same themed confirmation prompts as the other updated editors.

## PR #16 - Add Dialogue Map Editor for Common and Script Text

Branch: `KM/text-editor`

Merged as: `46a8dbbbde923425f4ec8d0cff6708b8ab2dc9c2`

### Files I Created

- `pkNX.WinForms/Subforms/DialogueMapEditor.cs`
- `pkNX.WinForms/Subforms/TextSyntaxHelper.cs`

### Files I Modified

- `pkNX.WinForms/MainEditor/EditorSWSH.cs`
- `pkNX.WinForms/Subforms/TextContainer.cs`
- `pkNX.WinForms/Subforms/TextEditor.cs`
- `pkNX.WinForms/UI/MainWindow.xaml.cs`

### What I Was Building

Dialogue Map is the first WIP pass at turning Common and Script text into connected editable game data. The old Text Editor showed one text file at a time as a line table. That is useful if the user already knows the file and line. It is much less useful when the user is asking, "Which NPC says this?" or "Where is this script text used?"

The new editor combines Common and Script text into one searchable map. It does not fully solve NPC/location/trigger mapping yet, but it creates the foundation:

- source,
- file,
- line,
- likely owner,
- label,
- context,
- readable text,
- raw text,
- friendly editing,
- variable insertion,
- undo/redo.

This is one of the bigger future-facing additions. There is a lot more we can build on top of it.

### How Dialogue Map Loads Text

`EditorSWSH.EditDialogueMap()` loads:

- Common text from `GameFile.GameText`,
- Script text from `GameFile.StoryText`,
- a shared `TextConfig`,
- ROMFS path for script metadata lookup.

It constructs two `TextContainer` instances and passes them into `DialogueMapEditor`.

`TextContainer` gained `GetFilePath(int i)`, which exposes the backing file path when the container is folder-based. Dialogue Map needs that path so it can correlate `.dat` files with `.tbl` labels and other metadata.

### Entry Model

`DialogueMapEditor` builds a list of `DialogueMapEntry` records. Each entry stores:

- source (`Common` or `Script`),
- owning `TextContainer`,
- file index,
- line index,
- file name,
- label,
- likely owner,
- context,
- raw text getter/setter.

The raw text setter is careful about label/line counts. If a `.tbl` label points beyond the current line array length, the setter can resize the array on write. This prevents an edit from failing just because metadata knows about a line that is not currently present in the same shape.

### Label and Script Metadata

Dialogue Map reads `.tbl` labels where available. These labels are important because text lines often have names like `msg_ui_pw_title_00` that explain UI context better than the file name alone.

For Script text, Dialogue Map also inspects:

- `bin/script/param/script_id/script_id_record.bin`

It deserializes that through the existing FlatBuffer pipeline using `FlatBufferConverter.DeserializeFrom<ScriptMeta>`. That lets the editor infer AMX/script context when the metadata exists.

The likely owner/context field is partly heuristic. It looks at filenames and labels for patterns such as:

- `rival`,
- `hop`,
- `bede`,
- `marnie`,
- `shop`,
- `nurse`,
- `gym`,
- `sign`,
- and similar context clues.

This is not perfect and I do not want to pretend it is. It is a practical first pass, and future work should connect it to Placement zones, NPC holders, signs, triggers, and script call sites.

### Performance Design

Dialogue Map originally loaded too slowly when treated like a normal eager grid. The current implementation uses:

- a backing `Entries` list,
- a filtered `VisibleEntries` list,
- `DataGridView.VirtualMode`,
- `CellValueNeeded`,
- a 200 ms `FilterTimer` debounce,
- on-demand readable text generation.

The grid does not eagerly create and populate every cell. Instead, it asks for values as rows become visible. Filtering is also split between cheap metadata checks and text checks. For search terms of length 0 or 1, it avoids expensive text decoding. For longer searches, it can inspect readable/raw text.

This is the difference between a useful map and a "click button, go make coffee" feature.

### Text Syntax Helper

`TextSyntaxHelper.cs` handles readable previews and friendly/raw conversion. It recognizes:

- `\n` as line break,
- `\r` as wait plus scroll,
- `\c` as wait plus clear,
- `[WAIT n]`,
- `[~ n]` null/linked line markers,
- `{base|ruby}` ruby/furigana style markup,
- `[VAR code(args)]` variable calls.

It also contains known variable descriptions and `TextVariableDefinition` entries grouped by:

- Pokemon,
- Item,
- Move,
- Number.

Examples include Pokemon species variables, Pokemon nickname variables, player/trainer name variables, item name variables, move name variables, formatted number variables, gender/plural selectors, and text style/color markers. Some descriptions are still intentionally broad because variable behavior depends on script runtime arguments.

### Friendly and Raw Editing

Dialogue Map provides two editing paths:

- `Apply Friendly`: converts readable helper syntax back into game syntax.
- `Apply Raw`: writes the raw syntax exactly as entered.

Both actions ask for confirmation before applying. I did that because text lines can be used by scripts and UI flows in ways that are not always obvious from a single row. A friendly helper is nice; a confirmation is nicer when the user is about to modify live text data.

The variable buttons open a dark themed picker interface. The picker lists variables, descriptions, default arguments, and the generated token. Users can edit arguments before inserting. That means users do not need to memorize `[VAR 0107(0000)]` just to insert a move-name variable.

### Undo and Redo

Dialogue Map has session undo/redo:

- `UndoHistory`
- `RedoHistory`
- `OriginalTextByEntry`

Each applied change records the entry, old text, and new text. Undo can roll back all applied changes to the state from when the editor opened. Redo can reapply changes forward to the newest edit. The memory is intentionally session-scoped and cleared when the form closes.

If undo/redo affects an entry hidden by filters, the editor can focus the affected entry by adjusting/clearing filters so the user sees what changed. That little bit matters because an undo stack that changes invisible rows feels haunted. Not literally haunted, just rude.

### Text Editor Improvements

The existing Common/Script Text Editor also got a dark-mode and safety pass:

- readable preview column,
- syntax tooltips,
- save confirmation,
- import confirmation,
- randomize confirmation,
- insert/remove line warnings,
- close-without-saving warning.

Line insertion and deletion are especially dangerous because they shift all following line indexes. Script and UI references can point at different text afterward. The prompts spell that out.

### Current Limitations

Dialogue Map is intentionally WIP. The major missing piece is exact ownership mapping. The editor can infer likely owner/context from labels and script metadata, but it does not yet fully connect text to:

- exact NPCs,
- exact placement zones,
- exact sign objects,
- exact triggers,
- exact shops,
- exact event flow nodes.

Future passes should wire Dialogue Map into Placement and Script data more deeply. It should eventually be possible to click a text entry and see where it appears in the world. That is the dream version.

## Cross-Cutting Files and Why They Matter

### `TypeRegistrationHelper.cs`

This file is the central registry for dynamic property-grid behavior. It recursively registers object types and list element types so generated FlatSharp objects can be displayed with custom descriptors. It wraps raw properties in specialized descriptors:

- `PlacementPropertyDescriptor`
- `RaidPropertyDescriptor`
- `MovePropertyDescriptor`
- `RentalPropertyDescriptor`
- `StaticEncounterPropertyDescriptor`
- `SymbolBehaviorPropertyDescriptor`
- `TradePropertyDescriptor`
- dynamic list descriptors

It also handles list display. Instead of showing a list as a generated object, `ListTypeConverter<T>` exposes indexed rows using `ListItemPropertyDescriptor<T>`. For shop item lists, it can return `ShopItemListUITypeEditor` so the property grid opens the modal item editor instead of expanding raw integers.

This is the plumbing that lets one generic editor support many specialized views.

### `GenericEditor.cs`

`GenericEditor<T>` is the shared editor shell for many SWSH data types. I modified it repeatedly because it owns:

- entry selection,
- data cache loading,
- table view overrides,
- property grid setup,
- save/dump/randomize actions,
- dark theme application,
- custom selector behavior,
- type registration before binding.

Several specialized table views plug into it. For example, shops can use `ShopTableView`, and placement can use `PlacementTableView`, while other editors still use the property grid with descriptor wrappers.

### `WinFormsTheme.cs`

This file is the shared visual baseline. It applies dark colors, grid colors, button styles, menu colors, combo-box colors, property-grid colors, and nested-control styling. Without this, each editor would end up with slightly different grays, button sizes, and control behavior. That is the kind of visual drift that makes software feel patched together.

### `ThemedConfirmationDialog.cs`

This dialog standardizes user confirmation for risky actions. It is intentionally used in many editors because the same verbs mean similar things everywhere:

- Save commits the editor session.
- Dump writes exported data.
- Randomize changes many fields at once.
- Close can discard edits.

The warnings are editor-specific, but the presentation is consistent.

### `SearchableStandardValuesUITypeEditor.cs`

This is the reusable property-grid dropdown. It is now the standard for any field with many choices. It avoids the native WinForms autocomplete popup, supports dark mode, supports prefix filtering, and returns actual typed values.

### `SearchableComboBoxBehavior.cs`

This is the standalone combo-box equivalent. It was added during the Trainer editor pass after similar selector problems appeared in other editors. The main lesson: combo boxes must preserve source indexes. Display text is not data identity.

## Known Remaining Research Areas

I intentionally left several things open for future work.

Placement needs deeper mapping between zones, NPCs, triggers, signs, scripts, and text. The summaries are much better now, but exact owner relationships are not fully solved.

Dialogue Map needs stronger cross references. It can combine Common and Script text, show labels, infer owners, and edit syntax safely, but it does not yet prove exactly which NPC or trigger owns every line.

TM/TR taught moves are editable, but compatibility is still slot-based. A later editor should expose TM/TR learnability data so users can change both taught moves and which Pokemon can learn each slot.

Several descriptor layers still contain raw/unknown fields. These are not failures; they are honest markers for data that needs controlled testing. The next contributor should treat those fields as research targets, not UI polish tasks.

Reward table usage is based on known base-game raid references. Event-only or external reward references may need separate mapping.

Trainer Class Ball is class-level and intentionally restricted to unique classes. If we later prove a per-trainer ball override exists elsewhere, that should be a separate editor field, not a reinterpretation of Class Ball.

Text variable documentation is incomplete. The current helper covers common and observed variables, but uncommon control codes, style/color codes, gender/plural branches, and language-specific formatting need more samples.

## Validation Pattern I Used

For nearly every merge, I validated with some combination of:

- `git diff --check`
- `dotnet build pkNX.WinForms\pkNX.WinForms.csproj`
- `dotnet build pkNX.sln`
- `dotnet publish pkNX.WinForms\pkNX.WinForms.csproj -c Debug -o pkNX-local-test`
- manual local testing through the published `pkNX-local-test\pkNX.exe`

The local test publish folder is intentionally not committed. It exists so I can hand-test the Windows UI without polluting source history.

## Final Notes

The main direction of this fork is now clear: keep the power of pkNX, but stop making users edit raw storage formats when the editor can explain the data. I added wrappers when the data meaning was understood, descriptors when a property grid needed translation, table views when a property grid was the wrong shape entirely, and confirmation prompts when an action could surprise the user.

There is still plenty to do, especially in Placement and Dialogue Map. But the fork now has a reusable technical foundation for turning more generated data into real editors. That is the important part. Once the pattern exists, each new editor pass becomes less like inventing a UI from scratch and more like teaching pkNX one more piece of what the game data means.
