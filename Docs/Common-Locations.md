# Common modding locations (RimWorld 1.6)

All links use the supplied `1.6/` snapshot. See [INDEX.md](../INDEX.md) for DLC directories and [Reference-Workflow.md](Reference-Workflow.md) for searches and DLL inspection.

## Frequent investigations

| Investigation | Supplied evidence | Further runtime lookup |
| --- | --- | --- |
| GM21 / Parametric skill changes | [Skills.xml](../1.6/Data/Core/Defs/SkillDefs/Skills.xml) | Locate `SkillRecord` and `Pawn_SkillTracker` in the matching gameplay assembly; their C# files are absent here |
| Cooking and recipe output | [Recipes_Meals.xml](../1.6/Data/Core/Defs/RecipeDefs/Recipes_Meals.xml), [Items_Food.xml](../1.6/Data/Core/Defs/ThingDefs_Items/Items_Food.xml), [Toils_Recipe.cs](../1.6/Source/Verse/AI/JobDrivers/DoBill/Toils_Recipe.cs) | Inspect recipe/output handling beyond the supplied toils as needed |
| Meal freshness and stacking | [CompRottable.cs](../1.6/Source/RimWorld/ThingComps/CompRottable.cs) | Inspect Thing stacking/splitting and food-poisoning components in the assembly; those implementations are absent here |
| Combat and beam parry | [weapon XML](../1.6/Data/Core/Defs/ThingDefs_Misc/Weapons/), [Toils_Combat.cs](../1.6/Source/Verse/AI/JobDrivers/Toils/Toils_Combat.cs), [Bullet.cs](../1.6/Source/RimWorld/Thing/Projectile/Bullet.cs) | `Verb_ShootBeam`, base Verb lifecycle, and `StunHandler` require assembly lookup |
| Surgery and hediff behavior | [surgery XML](../1.6/Data/Core/Defs/RecipeDefs/Recipes_Surgery_Misc.xml), [HediffDefs](../1.6/Data/Core/Defs/HediffDefs/), [Bodies](../1.6/Data/Core/Defs/Bodies/) | Recipe workers, pawn health trackers, and hediff runtime classes require assembly lookup |
| The Network factions and custody | [FactionDef.cs](../1.6/Source/RimWorld/Defs/DefTypes/FactionDef.cs), [faction XML](../1.6/Data/Core/Defs/FactionDefs/), [Ideology prisoner modes](../1.6/Data/Ideology/Defs/PrisonerInteractionModeDefs/) | Inspect Pawn faction transitions, `Pawn_GuestTracker`, world-pawn ownership, and age tracking in the assembly |
| The Network quests and visitors | [QuestScriptDefs](../1.6/Data/Core/Defs/QuestScriptDefs/), [TraderKindDefs](../1.6/Data/Core/Defs/TraderKindDefs/), [Sites](../1.6/Data/Core/Defs/Sites/) | Quest parts, pawn generation, caravans, and trader arrival behavior require assembly lookup |
| Hauling optimization | [JobDriver_HaulToCell.cs](../1.6/Source/Verse/AI/JobDrivers/Haul/JobDriver_HaulToCell.cs), [JobDriver_HaulToContainer.cs](../1.6/Source/Verse/AI/JobDrivers/Haul/JobDriver_HaulToContainer.cs), [Building_Storage.cs](../1.6/Source/RimWorld/Thing/Building/Storage/Building_Storage.cs) | Inspect any referenced trackers/utilities absent from the selection |
| VFX and sound | [Effects](../1.6/Data/Core/Defs/Effects/), [SoundDefs](../1.6/Data/Core/Defs/SoundDefs/) | Check the actual SoundDef and matching sound API when choosing one-shot versus sustained playback |

Names in the final column are lookup targets, not verified API signatures or assumed namespaces. A folder named `Source/RimWorld/` is not enough to establish a particular member's declaration.

Two declarations checked directly in the supplied source: `CompRottable.cs` uses `namespace RimWorld`; `Toils_Recipe.cs` uses `namespace Verse.AI`. Always read the declaration before adding a `using` or patch target.

## Complete supplied 1.6 C# file list

There are 43 files in this selection. This inventory establishes file availability; it does not verify every implementation against the DLL archive. There is no `SteamGeyser.cs` in the 1.6 selection, although it is present in the 1.4 and 1.5 selections.

### Definition classes

| File | Repository path |
| --- | --- |
| FactionDef.cs | [1.6/Source/RimWorld/Defs/DefTypes/FactionDef.cs](../1.6/Source/RimWorld/Defs/DefTypes/FactionDef.cs) |
| JobDef.cs | [1.6/Source/Verse/Defs/DefTypes/JobDef.cs](../1.6/Source/Verse/Defs/DefTypes/JobDef.cs) |
| RoofDef.cs | [1.6/Source/Verse/Defs/DefTypes/RoofDef.cs](../1.6/Source/Verse/Defs/DefTypes/RoofDef.cs) |
| ThingDef.cs | [1.6/Source/Verse/Defs/DefTypes/ThingDef.cs](../1.6/Source/Verse/Defs/DefTypes/ThingDef.cs) |
| WeatherDef.cs | [1.6/Source/Verse/Defs/DefTypes/WeatherDef.cs](../1.6/Source/Verse/Defs/DefTypes/WeatherDef.cs) |

### Jobs and toils

| File | Repository path |
| --- | --- |
| JobDriver_Equip.cs | [1.6/Source/Verse/AI/JobDrivers/Basics/JobDriver_Equip.cs](../1.6/Source/Verse/AI/JobDrivers/Basics/JobDriver_Equip.cs) |
| JobDriver_Goto.cs | [1.6/Source/Verse/AI/JobDrivers/Basics/JobDriver_Goto.cs](../1.6/Source/Verse/AI/JobDrivers/Basics/JobDriver_Goto.cs) |
| JobDriver_Wait.cs | [1.6/Source/Verse/AI/JobDrivers/Basics/JobDriver_Wait.cs](../1.6/Source/Verse/AI/JobDrivers/Basics/JobDriver_Wait.cs) |
| JobDriver_AttackStatic.cs | [1.6/Source/Verse/AI/JobDrivers/Casting/JobDriver_AttackStatic.cs](../1.6/Source/Verse/AI/JobDrivers/Casting/JobDriver_AttackStatic.cs) |
| JobDriver_Kill.cs | [1.6/Source/Verse/AI/JobDrivers/Casting/JobDriver_Kill.cs](../1.6/Source/Verse/AI/JobDrivers/Casting/JobDriver_Kill.cs) |
| JobDriver_UseVerb.cs | [1.6/Source/Verse/AI/JobDrivers/Casting/JobDriver_UseVerb.cs](../1.6/Source/Verse/AI/JobDrivers/Casting/JobDriver_UseVerb.cs) |
| JobDriver_DoBill.cs | [1.6/Source/Verse/AI/JobDrivers/DoBill/JobDriver_DoBill.cs](../1.6/Source/Verse/AI/JobDrivers/DoBill/JobDriver_DoBill.cs) |
| Toils_Recipe.cs | [1.6/Source/Verse/AI/JobDrivers/DoBill/Toils_Recipe.cs](../1.6/Source/Verse/AI/JobDrivers/DoBill/Toils_Recipe.cs) |
| JobDriver_HaulToCell.cs | [1.6/Source/Verse/AI/JobDrivers/Haul/JobDriver_HaulToCell.cs](../1.6/Source/Verse/AI/JobDrivers/Haul/JobDriver_HaulToCell.cs) |
| JobDriver_HaulToContainer.cs | [1.6/Source/Verse/AI/JobDrivers/Haul/JobDriver_HaulToContainer.cs](../1.6/Source/Verse/AI/JobDrivers/Haul/JobDriver_HaulToContainer.cs) |
| ToilFailConditions.cs | [1.6/Source/Verse/AI/JobDrivers/Toils/ToilFailConditions.cs](../1.6/Source/Verse/AI/JobDrivers/Toils/ToilFailConditions.cs) |
| ToilJumpConditions.cs | [1.6/Source/Verse/AI/JobDrivers/Toils/ToilJumpConditions.cs](../1.6/Source/Verse/AI/JobDrivers/Toils/ToilJumpConditions.cs) |
| Toils_Combat.cs | [1.6/Source/Verse/AI/JobDrivers/Toils/Toils_Combat.cs](../1.6/Source/Verse/AI/JobDrivers/Toils/Toils_Combat.cs) |
| Toils_Effects.cs | [1.6/Source/Verse/AI/JobDrivers/Toils/Toils_Effects.cs](../1.6/Source/Verse/AI/JobDrivers/Toils/Toils_Effects.cs) |
| Toils_General.cs | [1.6/Source/Verse/AI/JobDrivers/Toils/Toils_General.cs](../1.6/Source/Verse/AI/JobDrivers/Toils/Toils_General.cs) |
| Toils_Jump.cs | [1.6/Source/Verse/AI/JobDrivers/Toils/Toils_Jump.cs](../1.6/Source/Verse/AI/JobDrivers/Toils/Toils_Jump.cs) |
| Toils_Reserve.cs | [1.6/Source/Verse/AI/JobDrivers/Toils/Toils_Reserve.cs](../1.6/Source/Verse/AI/JobDrivers/Toils/Toils_Reserve.cs) |

### Incident workers

| File | Repository path |
| --- | --- |
| IncidentWorker_AnimalInsanity.cs | [1.6/Source/RimWorld/Game/Storyteller/Incidents/Workers/Map/IncidentWorker_AnimalInsanity.cs](../1.6/Source/RimWorld/Game/Storyteller/Incidents/Workers/Map/IncidentWorker_AnimalInsanity.cs) |
| IncidentWorker_CrashedShipPart.cs | [1.6/Source/RimWorld/Game/Storyteller/Incidents/Workers/Map/IncidentWorker_CrashedShipPart.cs](../1.6/Source/RimWorld/Game/Storyteller/Incidents/Workers/Map/IncidentWorker_CrashedShipPart.cs) |
| IncidentWorker_CropBlight.cs | [1.6/Source/RimWorld/Game/Storyteller/Incidents/Workers/Map/IncidentWorker_CropBlight.cs](../1.6/Source/RimWorld/Game/Storyteller/Incidents/Workers/Map/IncidentWorker_CropBlight.cs) |
| IncidentWorker_HeatWave.cs | [1.6/Source/RimWorld/Game/Storyteller/Incidents/Workers/Map/IncidentWorker_HeatWave.cs](../1.6/Source/RimWorld/Game/Storyteller/Incidents/Workers/Map/IncidentWorker_HeatWave.cs) |
| IncidentWorker_ResourcePodCrash.cs | [1.6/Source/RimWorld/Game/Storyteller/Incidents/Workers/Map/IncidentWorker_ResourcePodCrash.cs](../1.6/Source/RimWorld/Game/Storyteller/Incidents/Workers/Map/IncidentWorker_ResourcePodCrash.cs) |

### Things and components

| File | Repository path |
| --- | --- |
| Building_Storage.cs | [1.6/Source/RimWorld/Thing/Building/Storage/Building_Storage.cs](../1.6/Source/RimWorld/Thing/Building/Storage/Building_Storage.cs) |
| Building_Door.cs | [1.6/Source/RimWorld/Thing/Building/Various/Building_Door.cs](../1.6/Source/RimWorld/Thing/Building/Various/Building_Door.cs) |
| Fire.cs | [1.6/Source/RimWorld/Thing/Fire/Fire.cs](../1.6/Source/RimWorld/Thing/Fire/Fire.cs) |
| Apparel.cs | [1.6/Source/RimWorld/Thing/Misc/Apparel.cs](../1.6/Source/RimWorld/Thing/Misc/Apparel.cs) |
| Plant.cs | [1.6/Source/RimWorld/Thing/Plant/Plant.cs](../1.6/Source/RimWorld/Thing/Plant/Plant.cs) |
| Bullet.cs | [1.6/Source/RimWorld/Thing/Projectile/Bullet.cs](../1.6/Source/RimWorld/Thing/Projectile/Bullet.cs) |
| Spark.cs | [1.6/Source/RimWorld/Thing/Projectile/Spark.cs](../1.6/Source/RimWorld/Thing/Projectile/Spark.cs) |
| CompArt.cs | [1.6/Source/RimWorld/ThingComps/CompArt.cs](../1.6/Source/RimWorld/ThingComps/CompArt.cs) |
| CompExplosive.cs | [1.6/Source/RimWorld/ThingComps/CompExplosive.cs](../1.6/Source/RimWorld/ThingComps/CompExplosive.cs) |
| CompForbiddable.cs | [1.6/Source/RimWorld/ThingComps/CompForbiddable.cs](../1.6/Source/RimWorld/ThingComps/CompForbiddable.cs) |
| CompGatherSpot.cs | [1.6/Source/RimWorld/ThingComps/CompGatherSpot.cs](../1.6/Source/RimWorld/ThingComps/CompGatherSpot.cs) |
| CompRottable.cs | [1.6/Source/RimWorld/ThingComps/CompRottable.cs](../1.6/Source/RimWorld/ThingComps/CompRottable.cs) |
| Building.cs | [1.6/Source/Verse/Thing/Building/Building.cs](../1.6/Source/Verse/Thing/Building/Building.cs) |
| Corpse.cs | [1.6/Source/Verse/Thing/Corpse.cs](../1.6/Source/Verse/Thing/Corpse.cs) |
| Projectile_Explosive.cs | [1.6/Source/Verse/Thing/Projectile_Explosive.cs](../1.6/Source/Verse/Thing/Projectile_Explosive.cs) |
| CompLifespan.cs | [1.6/Source/Verse/ThingComps/CompLifespan.cs](../1.6/Source/Verse/ThingComps/CompLifespan.cs) |

## Other versions

For 1.4 or 1.5 work, enumerate the relevant source selection rather than mechanically replacing the version in a 1.6 link:

```bash
rg --files 1.4/Source -g '*.cs'
rg --files 1.5/Source -g '*.cs'
```
