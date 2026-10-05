# Reference index

Use one version at a time. Paths in this document are relative to the repository root. [Snapshot details and limits](Docs/Snapshot-Manifest.md) explain what has been verified.

## Version navigation

| Version | XML/data root | Selected source root | Packs present |
| --- | --- | --- | --- |
| 1.4 | [1.4/Data](1.4/Data/) | [1.4/Source](1.4/Source/) | Core |
| 1.5 | [1.5/Data](1.5/Data/) | [1.5/Source](1.5/Source/) | Core |
| 1.6 | [1.6/Data](1.6/Data/) | [1.6/Source](1.6/Source/) | Core + Royalty, Ideology, Biotech, Anomaly, Odyssey |

These labels do not establish an exact patch/build. No complete source tree or project file is supplied.

## RimWorld 1.6 data packs

| Pack | Definitions | Patches | Metadata |
| --- | --- | --- | --- |
| Core | [Defs](1.6/Data/Core/Defs/) | No Patches directory supplied | [About.xml](1.6/Data/Core/About/About.xml) |
| Royalty | [Defs](1.6/Data/Royalty/Defs/) | [Patches](1.6/Data/Royalty/Patches/) | [About.xml](1.6/Data/Royalty/About/About.xml) |
| Ideology | [Defs](1.6/Data/Ideology/Defs/) | [Patches](1.6/Data/Ideology/Patches/) | [About.xml](1.6/Data/Ideology/About/About.xml) |
| Biotech | [Defs](1.6/Data/Biotech/Defs/) | [Patches](1.6/Data/Biotech/Patches/) | [About.xml](1.6/Data/Biotech/About/About.xml) |
| Anomaly | [Defs](1.6/Data/Anomaly/Defs/) | No Patches directory supplied | [About.xml](1.6/Data/Anomaly/About/About.xml) |
| Odyssey | [Defs](1.6/Data/Odyssey/Defs/) | [Patches](1.6/Data/Odyssey/Patches/) | [About.xml](1.6/Data/Odyssey/About/About.xml) |

Each pack also has a `Languages/` directory. For gameplay definitions start in `Defs/`; for localized text start in `Languages/`. Folder presence does not establish that a player's active game has that DLC enabled.

## Topic map for 1.6

| Topic | Start here |
| --- | --- |
| Skills | [SkillDefs](1.6/Data/Core/Defs/SkillDefs/), [WorkTypeDefs](1.6/Data/Core/Defs/WorkTypeDefs/), [WorkGiverDefs](1.6/Data/Core/Defs/WorkGiverDefs/) |
| Crafting and cooking | [RecipeDefs](1.6/Data/Core/Defs/RecipeDefs/), [food items](1.6/Data/Core/Defs/ThingDefs_Items/Items_Food.xml), [bill runtime source](1.6/Source/Verse/AI/JobDrivers/DoBill/) |
| Medicine and body parts | [HediffDefs](1.6/Data/Core/Defs/HediffDefs/), [Bodies](1.6/Data/Core/Defs/Bodies/), [surgery recipes](1.6/Data/Core/Defs/RecipeDefs/Recipes_Surgery_Misc.xml), [PawnCapacityDefs](1.6/Data/Core/Defs/PawnCapacityDefs/) |
| Weapons and damage | [Weapons](1.6/Data/Core/Defs/ThingDefs_Misc/Weapons/), [DamageDefs](1.6/Data/Core/Defs/DamageDefs/), [combat toils](1.6/Source/Verse/AI/JobDrivers/Toils/Toils_Combat.cs) |
| Effects and sound | [Effects](1.6/Data/Core/Defs/Effects/), [SoundDefs](1.6/Data/Core/Defs/SoundDefs/) |
| Factions and pawn generation | [FactionDefs](1.6/Data/Core/Defs/FactionDefs/), [humanlike PawnKindDefs](1.6/Data/Core/Defs/PawnKindDefs_Humanlikes/), [PawnKinds](1.6/Data/Core/Defs/PawnKinds/), [FactionDef source](1.6/Source/RimWorld/Defs/DefTypes/FactionDef.cs) |
| Prisoners and slavery | [Core prisoner interactions](1.6/Data/Core/Defs/InteractionDefs/Interactions_Prisoner.xml), [Ideology interactions](1.6/Data/Ideology/Defs/InteractionDefs/), [Ideology slave modes](1.6/Data/Ideology/Defs/SlaveInteractionModeDefs/) |
| Quests and contractors | [QuestScriptDefs](1.6/Data/Core/Defs/QuestScriptDefs/), [Sites](1.6/Data/Core/Defs/Sites/), [WorldObjectDefs](1.6/Data/Core/Defs/WorldObjectDefs/), [TraderKindDefs](1.6/Data/Core/Defs/TraderKindDefs/) |
| Incidents | [Storyteller](1.6/Data/Core/Defs/Storyteller/), [selected incident workers](1.6/Source/RimWorld/Game/Storyteller/Incidents/Workers/Map/) |
| Hauling and storage | [haul job drivers](1.6/Source/Verse/AI/JobDrivers/Haul/), [Building_Storage](1.6/Source/RimWorld/Thing/Building/Storage/Building_Storage.cs), [ThingCategoryDefs](1.6/Data/Core/Defs/ThingCategoryDefs/) |
| Buildings and research | [ThingDefs_Buildings](1.6/Data/Core/Defs/ThingDefs_Buildings/), [ResearchProjectDefs](1.6/Data/Core/Defs/ResearchProjectDefs/) |
| Stats | [Stats](1.6/Data/Core/Defs/Stats/) |

Folder names are organizational labels, not a guaranteed one-to-one map to Def classes: effects share `Effects/`, many stats use `Stats/`, and Core incidents use `Storyteller/`. Search by XML tag or defName within the selected version when a folder name is unexpected.

## Runtime source and assemblies

Browse [Common-Locations.md](Docs/Common-Locations.md) for all supplied 1.6 C# files. Several frequent targets, including `SkillRecord`, `Verb_ShootBeam`, and `Pawn_GuestTracker`, have no corresponding loose C# file in this snapshot. Check the matching DLL for their implementations.

| Archive path | Intended reference, based on filename |
| --- | --- |
| `1.4/Rimworld 1.4 DLLs (All).zip` | 1.4 assembly bundle |
| `1.5/Rimworld 1.5 DLLs.zip` | 1.5 assembly bundle |
| `1.6/Rimworld 1.6 DLLs.zip` | 1.6 assembly bundle |
| `Harmony DLL.zip` | Harmony bundle, version unknown |

The ZIP member lists and actual assembly identities have not been inspected for this documentation. Use the [extraction workflow](Docs/Reference-Workflow.md) to discover them.

## A quick search

Run from the repository root:

```bash
rg -n -F '<defName>Shooting</defName>' 1.6/Data -g '*.xml'
rg --files 1.6/Source -g '*.cs'
rg -n 'FinishRecipeAndStartStoringProduct|MakeRecipeProducts' 1.6/Source
```

A missing match in this selected source tree calls for assembly inspection. It does not settle whether the API exists.
