# RimWorld reference: XML, selected C# source, and assembly archives

This repository is a versioned reference for RimWorld mod development. It contains loose XML, selected C# source files, and ZIP archives labelled as game DLLs. Start with [INDEX.md](INDEX.md) to find the right version and subsystem.

For RimWorld 1.6 work, search `1.6/` first. The version directories are snapshot labels; the exact game build and whether the source and DLLs came from the same build have not been recorded.

## Start here

| Document | Purpose |
| --- | --- |
| [INDEX.md](INDEX.md) | Version matrix, Core/DLC locations, and topic navigation |
| [Docs/Common-Locations.md](Docs/Common-Locations.md) | Frequent modding entry points and the complete list of 43 supplied 1.6 C# files |
| [Docs/Reference-Workflow.md](Docs/Reference-Workflow.md) | Scoped searches, DLL extraction, and evidence checks |
| [Docs/Snapshot-Manifest.md](Docs/Snapshot-Manifest.md) | Inspected commit, file counts, archive identity, and unknown provenance |
| [AGENTS.md](AGENTS.md) | Instructions for coding agents working with this reference |

## What is available?

| Snapshot | Loose data | Selected C# files | DLL archive |
| --- | --- | ---: | --- |
| [1.4](1.4/) | Core only | 44 | `1.4/Rimworld 1.4 DLLs (All).zip` |
| [1.5](1.5/) | Core only | 44 | `1.5/Rimworld 1.5 DLLs.zip` |
| [1.6](1.6/) | Core, Royalty, Ideology, Biotech, Anomaly, Odyssey | 43 | `1.6/Rimworld 1.6 DLLs.zip` |

There is also a root-level `Harmony DLL.zip`. Its bundled Harmony version is unverified.

`Source/` is a selection, not a complete game source tree or a standalone build project. If a class is absent, inspect the matching DLL rather than substituting a different game version. Archive contents and assembly identities must be checked after extraction.

The existing paths are preserved so current mod projects and reference prompts can keep using them. XML is already loose and searchable; DLL ZIPs can be extracted once into an ignored or external workspace without reorganizing the repository.
