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
  - writes `royal_candy_source_cleanup_notes.txt` into generated output so the exact cleanup actions are inspectable.
- Reduced trainer editor startup work:
  - delays full level-up learnset loading until a move-fill or trainer-randomizer action actually needs it;
  - removes duplicate trainer-class string loading during construction;
  - batches trainer and trainer-class dropdown population to avoid per-item UI churn;
  - keeps the SWSH class-ball safety rule but replaces per-class owner `HashSet` allocation with a lighter first-owner/multiple-owner scan.
- Re-ran the builder probe for both unlimited and custom-limit modes:
  - unlimited mode generates the Royal Candy item, text, source cleanup, Bag-event grant, and ExeFS infinite-use/virtual-count/UI-route patches;
  - custom-limit mode generates the same base output plus the story-cap use gate, quantity max, clamp bypass, and shared cap helper patches;
  - both modes report 1 shop removal, 18 raid reward replacements, and 10 placement pickup replacements.
