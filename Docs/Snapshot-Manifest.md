# Snapshot manifest

Documentation inspected the reference tree at commit [`43c5eabbe3001b7032a9880d4eed2db38b5d289d`](https://github.com/D3athAn63l/zRim_Source_XMLs/commit/43c5eabbe3001b7032a9880d4eed2db38b5d289d) on 2026-10-05. Counts below refer to that content snapshot, before these documentation files were added.

## Loose reference inventory

| Version label | Data packs present | XML files under Data | C# files under Source |
| --- | --- | ---: | ---: |
| 1.4 | Core | 555 | 44 |
| 1.5 | Core | 565 | 44 |
| 1.6 | Core, Royalty, Ideology, Biotech, Anomaly, Odyssey | 1,672 | 43 |

XML totals include About metadata and language files, not just gameplay definitions.

### Definition and patch counts

| Version / pack | XML under Defs | XML under Patches |
| --- | ---: | ---: |
| 1.4 / Core | 524 | 0 |
| 1.5 / Core | 534 | 0 |
| 1.6 / Anomaly | 198 | 0 |
| 1.6 / Biotech | 219 | 5 |
| 1.6 / Core | 550 | 0 |
| 1.6 / Ideology | 212 | 1 |
| 1.6 / Odyssey | 231 | 6 |
| 1.6 / Royalty | 148 | 2 |

A zero patch count means no patch XML was supplied at that location. It is not a statement about all game installations or all versions of a DLC.

## Archive identity from Git

| Path | Bytes | Git blob SHA-1 |
| --- | ---: | --- |
| `1.4/Rimworld 1.4 DLLs (All).zip` | 10,469,812 | `deb9b729e22d24de92457cc1a4cb66ad15070681` |
| `1.5/Rimworld 1.5 DLLs.zip` | 11,425,362 | `31ca1158145d4df716f8e98a9110f71bebe40d99` |
| `1.6/Rimworld 1.6 DLLs.zip` | 14,763,110 | `1251ef3b980a1a82190a078da86474d23e0c13b8` |
| `Harmony DLL.zip` | 808,885 | `8cb674b9cc57696a941d969518ddc5e657252d76` |

These are Git blob object identifiers, not SHA-1 checksums of the raw ZIP bytes. For a normal, non-LFS checkout, `git hash-object "<archive-path>"` computes the corresponding Git blob identifier.

Archive identities and paths were read from the repository tree. The archives were not extracted during this documentation pass.

## Provenance status

| Item | Status |
| --- | --- |
| Major/minor game version | Inferred from 1.4, 1.5, 1.6 directory labels; Odyssey About metadata also lists 1.6 |
| Exact game patch/build | Not recorded |
| Original snapshot/extraction date | Not recorded |
| Origin of selected C# files | Not recorded; do not assume a complete decompilation or original full source distribution |
| Source files matching bundled DLLs | Not verified |
| ZIP member paths and bundled DLL identities | Not verified |
| Bundled Harmony version | Not verified |
| DLC/data completeness | Presence enumerated; completeness against an installation not verified |
| Standalone build project | No .csproj or .sln was present in the inspected tree |

No single `VERSION.txt` is added because this repository contains three version labels and no verified exact build numbers.

## Recording future refreshes

For each changed snapshot, record the version/build string from the actual game installation, collection date, source origin or decompiler used, supplied DLC packs, assembly full names, and raw-file SHA-256 checksums. If data and binary snapshots differ, document both builds rather than claiming alignment.

Regenerate counts from the checkout and update the docs when files change. From the repository root:

```python
from pathlib import Path

for version in ("1.4", "1.5", "1.6"):
    base = Path(version)
    print(
        version,
        "XML:", len(list((base / "Data").rglob("*.xml"))),
        "C#:", len(list((base / "Source").rglob("*.cs"))),
    )
```

This command counts files in the local checkout. Run it on a clean checkout of the intended snapshot so extra local files do not inflate the inventory.
