# pkNX-Fixes Wiki

Welcome to the wiki for `KotMatrosk1n/pkNX-Fixes`.

This project is my Sword/Shield-focused pkNX fork. I use it to turn raw generated data editors into Royal Sword-aware tools: clearer item fields, safer shop editing, better placement labels, searchable dialogue, Royal-only editor surfaces, and now the Infinite Rare Candy toolchain.

The big theme is that pkNX already knows how to open a lot of Sword/Shield data, but opening data is not the same thing as understanding it. I am building the missing layer between "the game stores this as a hash, row, bytecode file, or ARM64 instruction" and "a modder can safely reason about this."

## Start Here

- [Development History](Development-History.md) explains the earlier pkNX editor work, why I moved away from raw `PropertyGrid` editing, and how the first large editor improvements were built.
- [Infinite Rare Candy](Infinite-Rare-Candy.md) documents the full key-item infinite Rare Candy research project: dead ends, file formats, script research, ExeFS patching, tools created, breakthroughs, and what this makes possible.
- [Repository](https://github.com/KotMatrosk1n/pkNX-Fixes)

## What This Fork Is For

This fork is meant to make Sword/Shield ROM editing less like staring at serialized storage and more like using purpose-built tools.

The current focus areas are:

- readable SWSH editor labels, descriptions, and dropdowns;
- safer item, shop, raid, trainer, placement, text, and encounter editing;
- Royal Sword-specific investigation tools for flagwork, story events, trainers, saves, ExeFS patches, and generated patch output;
- documenting how Sword/Shield stores the data I had to touch, especially when the answer was not obvious from RomFS alone.

I try to keep the underlying data model honest. When I know what a field means, I label it. When I do not know, I leave it visible and say it still needs research. A wrong label is worse than an unknown one because it sends future edits in the wrong direction.

## Current Scope

The current public work covers the Matroskin pkNX fork through the Royal editor integration line:

- PR #1 through PR #16 built the first major quality-of-life editors and generic editor infrastructure.
- PR #17 through PR #19 continued SWSH label and placement readability work.
- PR #20 through PR #28 built the Royal-only editor mode and the Infinite Rare Candy investigation/editor toolchain.

The Royal-only dashboard now includes real editor entries for:

- `Candy Builder`
- `Flagwork`
- `Story Events`
- `Dialogue Map`
- `Trainer Map`
- `Save Inspector`
- `Patch Manager`

## Credits and References

This fork depends on a lot of existing work.

- pkNX is the original ROM editor foundation. I am extending it rather than replacing it.
- PKHeX is the reference for Gen 8 save understanding and save block interpretation.
- FlatSharp and the generated FlatBuffer models provide the structured RomFS data access used throughout the generic editors.
- LLVM tooling and Ghidra were used during the ExeFS research phase to inspect ARM64 code paths and verify patch anchors.
- Pokemon Run & Bun was a workflow inspiration for the Infinite Rare Candy project. Run & Bun has an infinite Rare Candy-style convenience item, and that made me want the same quality-of-life idea for Royal Sword while keeping it tied to the hack's intended level caps.
