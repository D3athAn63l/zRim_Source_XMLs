# Working with this RimWorld reference

Read [INDEX.md](INDEX.md) before broad searches. See [Docs/Reference-Workflow.md](Docs/Reference-Workflow.md) for commands and [Docs/Snapshot-Manifest.md](Docs/Snapshot-Manifest.md) for provenance limits.

- This is a reference repository. For a mod implementation task, edit the target mod repository; use this repository to investigate definitions and runtime APIs.
- Use the game version requested by the task. If the target is unclear, inspect the mod's supported versions and build references first. For the owner's RimWorld 1.6 projects, begin in `1.6/`.
- Scope `rg` to that version and relevant data pack. Search another version only for an explicit comparison, and label it as such.
- Preserve reference paths and supplied XML, C# files, and archives unless the task explicitly calls for changing the snapshot.
- `1.4/Data/` and `1.5/Data/` contain Core only. `1.6/Data/` contains Core and five DLC packs. Search DLC patches as well as Defs when resolving a value.
- `Source/` is incomplete: 44 C# files each in 1.4 and 1.5, and 43 in 1.6 at the documented snapshot. A missing class or symbol is not evidence that it does not exist in the game.
- Do not infer a namespace from a folder or prior memory. Read the file declaration or inspect the assembly. For example, the supplied `CompRottable.cs` declares `namespace RimWorld`, and `Toils_Recipe.cs` declares `namespace Verse.AI`.
- Inspect DLL archive member names before extraction. Extract into a temporary/external directory or an ignored local directory. Discover actual assembly paths rather than assuming the ZIP layout.
- Verify the selected assembly's identity and relevant signatures before implementing runtime patches. Exact game builds, source/binary alignment, and Harmony version are currently unverified.
- Report concrete file paths and relevant members for runtime findings. Distinguish supplied source evidence, assembly evidence, and behavior still requiring an in-game test.
- Keep documentation links aligned with actual repository paths. Update the snapshot manifest when reference content changes.
