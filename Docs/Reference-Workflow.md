# Reference workflow

Commands below run from the repository root unless stated otherwise. They are investigation examples, not a build setup for a specific mod.

## 1. Select the snapshot

Use the target mod's supported game version and assembly references. Keep XML and assembly investigation scoped to that version. For current 1.6 projects, begin in `1.6/`.

Record the reference checkout when reporting a finding:

```bash
git rev-parse HEAD
```

An exact reference commit makes the finding reproducible. It does not prove that the reference matches the user's installed game build.

## 2. Find XML definitions

Search all 1.6 packs for a defName:

```bash
rg -n -F '<defName>Shooting</defName>' 1.6/Data -g '*.xml'
rg -n 'ParentName=|<statBases>|<comps>|<recipeMaker>' 1.6/Data/Core/Defs/ThingDefs_Items/Items_Food.xml
```

For a value affected by DLC, include that DLC's `Patches/` directory when supplied:

```bash
rg -n 'Slave|Enslave|Prisoner' 1.6/Data/Core/Defs 1.6/Data/Ideology -g '*.xml'
rg --files 1.6/Data -g '*.xml' -g '!**/Languages/**'
```

Follow `ParentName` to the parent definition and read applicable patch operations before treating a raw XML field as the final loaded value. Active DLC and other mods can change the result.

Use [INDEX.md](../INDEX.md) rather than assuming every Def type has a directory with the same name.

## 3. Read selected runtime source

```bash
rg --files 1.6/Source -g '*.cs'
rg -n 'FinishRecipeAndStartStoringProduct|MakeRecipeProducts' 1.6/Source
rg -n 'namespace |class CompRottable|class Toils_Recipe' 1.6/Source/RimWorld/ThingComps/CompRottable.cs 1.6/Source/Verse/AI/JobDrivers/DoBill/Toils_Recipe.cs
```

Read declarations and surrounding call sites. The source selection is incomplete and its provenance is unrecorded. If the relevant class is absent or a signature disagrees with the binary, inspect the matching assembly.

## 4. Inspect and extract assembly archives

List members before extracting:

```bash
python -m zipfile -l "1.6/Rimworld 1.6 DLLs.zip"
python -m zipfile -l "Harmony DLL.zip"
```

Python must be installed. On Windows, `py` may be the available launcher instead of `python`.

Extract once into an external scratch directory. This example creates a new unique directory, so it will not overwrite an existing extraction. Review member paths first.

```python
from pathlib import Path
from tempfile import mkdtemp
from zipfile import ZipFile

output = Path(mkdtemp(prefix="rimworld-reference-1.6-"))
for archive, subdir in (
    (Path("1.6/Rimworld 1.6 DLLs.zip"), "game"),
    (Path("Harmony DLL.zip"), "harmony"),
):
    destination = output / subdir
    destination.mkdir()
    with ZipFile(archive) as bundle:
        bundle.extractall(destination)

print(output)
for dll in sorted(output.rglob("*.dll")):
    print(dll.relative_to(output))
```

Copy the Python block into a scratch script or run it in a Python console from this checkout. `extractall` is intended here for archives whose member paths you have reviewed. The output is temporary; re-extract if the environment is replaced.

Find `Assembly-CSharp.dll`, Unity references, and `0Harmony.dll` by their actual extracted paths. Their presence and placement are not guaranteed by this documentation. Check assembly metadata and required API signatures before using them.

## 5. Inspect missing runtime classes

Open the matching gameplay assembly in an available .NET decompiler, such as ILSpy. Confirm the full type name, namespace, method overloads, and call sites from that binary. Decompile only the needed types into scratch if a small lookup is sufficient.

If `ilspycmd` is already installed, this shows its locally available options:

```bash
ilspycmd --help
```

When a decompiler is unavailable, identify that gap explicitly. Do not fill it with source from 1.4/1.5 or invented 1.6 behavior.

## 6. Apply evidence to the target mod

Use XML for definitions, the verified matching assembly for runtime signatures, and in-game tests for actual behavior and interactions with loaded mods. Describe source/binary disagreements instead of silently assuming one is aligned.

A useful report includes:

- The reference commit and game version label.
- The XML path and defName, or the runtime type/member and assembly inspected.
- The observed behavior or proposed hook.
- Any uncertainty about exact build alignment.
- What was compiled or runtime-tested, and what still needs an in-game test.

## Maintaining the reference

Keep loose XML for routine searches. Preserve the versioned layout and existing ZIP paths unless a separate task approves restructuring. Optional loose DLLs can live in an ignored/external extraction; no recurring ZIP extraction is needed during one session.

When refreshing a snapshot, record the exact game build if known, extraction date, source origin, assembly identity, and checksums. Update [Snapshot-Manifest.md](Snapshot-Manifest.md), the version matrix, and source inventory to reflect the actual files. Do not assign a guessed game build to the existing snapshots.
