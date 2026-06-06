# Royal Candy Editor V2 Changelog

This document tracks the dedicated Royal Candy editor redesign PR.

## Goals

- Replace the advanced patch-builder surface with a player-facing Royal Candy editor.
- Validate that the loaded project is Pokemon Sword or Shield with update 1.3.2 and both DLC content sets available.
- Ask the user whether the dump is Sword or Shield when automatic detection cannot prove the version.
- Offer two editor paths:
  - create an infinite Royal Candy with no level limits;
  - customize level caps by story milestone.
- Keep the proven fresh-new-game Bag-event AMX grant path.
- Keep Royal Candy as a real key item that does not decrement.
- Support custom equal-or-ascending level caps for the known trainer/story milestones.
- Display the Gordie or Melony milestone name according to the selected game version.
- Warn clearly before writing ExeFS/RomFS output that the patch is build-specific and requires a new game.
- Remove the source item being repurposed for Royal Candy from player-accessible acquisition sources.
- Verify that generated output matches the previously tested mod structure.
- Generate output directly into the Sword/Shield title-ID LayeredFS folder, matching normal pkNX editor output.
- Read existing title-ID mod files as higher-priority input over the base dump so Royal Candy can be added to an in-progress mod.
- Preflight existing ExeFS edits before writing and report already-installed Royal Candy output by mode/version.
- Investigate slow editor loading, especially trainer-related editors, and prepare a focused fix.

## Implementation Notes

- PR base starts at `865c9138` (`Add Royal Candy Bag-event grant to builder (#30)`).
- The current editor already has working RomFS item/text/shop output, ExeFS Rare Candy routing, non-consumption behavior, virtual ownership/count, story-cap ladder, and Bag-event AMX grant support.
- The redesign should preserve those proven patch paths while simplifying the user-facing choices.

## Changelog

- Started dedicated PR branch for the Royal Candy editor redesign.
- Replaced the old advanced builder surface with the first Royal Candy V2 flow:
  - validates that RomFS and ExeFS are present before enabling the editor;
  - checks the supported Sword/Shield 1.3.2 full-DLC RomFS file count;
  - detects Sword or Shield from pkNX metadata or `main.npdm` when possible;
  - asks the user to confirm the game version before building;
  - exposes only the two player-facing actions: unlimited Royal Candy or customized Royal Candy limits;
  - keeps the proven Bag-event grant path for fresh new games;
  - warns before writing RomFS, AMX, and ExeFS output;
  - moves the cap ladder into editable milestone definitions with ascending-value validation;
  - displays Gordie or Melony according to the selected game version.
- Added source acquisition cleanup for the repurposed item:
  - removes the original source item from shop inventories;
  - replaces raid bonus reward entries with regular Rare Candy;
  - replaces hidden-item placement hashes with regular Rare Candy while preserving quantity and chance;
  - keeps exact cleanup actions in the editor log so the generated mod folder stays clean.
- Reduced trainer editor startup work:
  - delays full level-up learnset loading until a move-fill or trainer-randomizer action actually needs it;
  - removes duplicate trainer-class string loading during construction;
  - batches trainer and trainer-class dropdown population to avoid per-item UI churn;
  - keeps the SWSH class-ball safety rule but replaces per-class owner `HashSet` allocation with a lighter first-owner/multiple-owner scan.
- Re-ran the builder probe for both unlimited and custom-limit modes:
  - unlimited mode generates the Royal Candy item, text, source cleanup, Bag-event grant, and ExeFS infinite-use/virtual-count/UI-route patches;
  - custom-limit mode generates the same base output plus the story-cap use gate, quantity max, clamp bypass, and shared cap helper patches;
  - both modes report 1 shop removal, 18 raid reward replacements, and 10 placement pickup replacements.
- Replaced the Royal Candy launch file-count fingerprint with direct required-input validation:
  - RomFS file count is now logged as informational only;
  - the editor checks for the item table, item hash table, shop data, raid/placement archives, Bag-event AMX script, ExeFS `main`, and `main.npdm`;
  - message validation now requires at least one language `common` folder with `iteminfo.dat` and `itemname*.dat`;
  - a 50,494-file Sword dump passed the two-mode builder probe after the validation change.
- Updated Royal Candy output and layering behavior:
  - default output now targets the selected game's title-ID folder (`0100ABF008968000` for Sword, `01008DB008C2C000` for Shield);
  - RomFS, AMX, message, item-hash, shop, raid, placement, and ExeFS reads now prefer files already present in the output LayeredFS folder before falling back to the base dump;
  - the Bag-event AMX patcher now accepts the resolved source script path, allowing it to patch an existing layered script instead of silently ignoring it;
  - build preflight detects existing Royal Candy output by scanning layered `exefs/main` patch anchors and reports the installed mode/game before refusing to stack another Royal Candy patch;
  - build preflight dry-runs the ExeFS patch against an existing layered `exefs/main` when present and reports conflicts before the warning/confirm step.
- Re-ran the builder probes after the LayeredFS layering change:
  - fresh unlimited/custom-limit scratch output still generated the expected item/text/source-cleanup/AMX/ExeFS files;
  - existing Royal Candy output was detected as `Unlimited for Sword` and refused before repatching;
  - an existing compatible `exefs/main` overlay was accepted and patched from the overlay source.
- Replaced note-based installed-patch detection with file-scan detection:
  - preflight now opens layered `exefs/main`, decompresses the NSO text segment, decodes ARM64 branch targets, and checks for the real Royal Candy hook anchors;
  - unlimited output is detected from the common ExeFS hooks even if generated marker/readme/note text is deleted;
  - custom-limit output is detected when the common hooks plus story-cap ladder hooks are present;
  - note-free scratch reruns correctly reported `Unlimited for Sword` and `CustomLimits for Sword` from `exefs/main` alone.
- Updated Royal editor usability:
  - the Royal Candy window is larger, its preflight log wraps long executable-scan details, and the result grid wraps long messages instead of cutting them off;
  - the result grid now displays newest entries first with a step marker so current actions are visually separated from older output;
  - the Customize Royal Candy Limits action now runs preflight before opening the cap editor, so an already-installed Royal Candy output is rejected immediately;
  - the Royal Candy status line now uses simple user-facing blocker text such as `Unlimited Royal Candy already installed.` or `Custom Royal Candy already installed.`;
  - Royal Candy, Flagwork, Story Events, Trainer Map, Save Inspector, Patch Manager, and the Royal Candy dialogs now use the same dark WinForms theme as the existing Royal Dialogue Map editor.
- Added signature-gated Royal Candy uninstall support:
  - the editor now exposes an `Uninstall Royal Candy` action next to the unlimited/custom builder actions;
  - uninstall preflight scans layered `exefs/main` and refuses to run unless it matches a registered unlimited or custom-limit Royal Candy signature;
  - unknown ExeFS overlays remain blocked until a signature is added to the library;
  - successful uninstall removes the known Royal Candy LayeredFS output files and prunes empty folders under the selected title-ID output root.
- Changed Royal Candy uninstall to preserve unrelated custom RomFS edits:
  - uninstall now removes Royal Candy-owned ExeFS/generated text files but restores shared RomFS files record-by-record against the base dump;
  - item id `1128` item-table indirection is restored to the vanilla raw row, and the unused appended Royal Candy row is trimmed when it is safe to do so;
  - item name/description text restores only line `1128`;
  - shop inventories, raid rewards, and placement pickups restore only entries whose base value is the repurposed source item and whose layered value matches the Royal Candy cleanup replacement;
  - the Bag-event AMX overlay is removed only when it exactly matches the clean generated Royal Candy patch, leaving custom script overlays untouched.
  - after uninstall restores Royal Candy data, the builder sweeps the selected LayeredFS root and removes any remaining `romfs` or `exefs` overlay file that is byte-identical to the base dump, then prunes the empty folders left behind.
- Consolidated generated text output:
  - new builds write a single `RoyalSword_RoyalCandy.txt` marker at the selected LayeredFS output root;
  - technical patch details stay in the Royal Candy editor log instead of being written as separate note files;
  - uninstall cleans the old generated note/readme files from earlier builder versions, and removes the new marker only when it matches the Royal Sword header.
- Fixed Royal Candy uninstall cleanup for generated RomFS files:
  - item generation no longer mutates the original shared item raw row before appending the Royal Candy row;
  - uninstall restores item `1128` raw-row bytes exactly before trimming/removing overlays;
  - text, raid reward, and placement overlays are removed when they match Royal Candy-generated or restored Royal Candy cleanup states, even when container serialization bytes differ from the original dump;
  - uninstall can now clean orphaned Royal Candy RomFS leftovers after `exefs/main` has already been removed.
