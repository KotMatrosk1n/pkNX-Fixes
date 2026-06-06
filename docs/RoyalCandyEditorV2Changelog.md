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
