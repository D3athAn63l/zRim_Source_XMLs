using System;
using System.Buffers;
using UnityEngine;
using System.Collections.Generic;
using System.Text;
using Verse;
using LudeonTK;

namespace RimWorld
{

public enum PlantLifeStage : byte
{
    Sowing,
    Growing,
    Mature,
}

public enum PlantDestructionMode
{
    Smash,
    Flame,
    Chop,
    Cut
}

[StaticConstructorOnStartup]
public class Plant : ThingWithComps
{
    //Working vars
    protected float 					growthInt = BaseGrowthPercent; //Start in growing phase by default, set to 0 if sowing
    protected int						ageInt = 0;
    protected int						unlitTicks = 0;
    protected int						madeLeaflessTick = -99999;
    public bool							sown = false;

    //Working vars - cache
    private string						cachedLabelMouseover = null;

    //Fast vars
    private static Color32[]			workingColors = new Color32[4];

    //Constants and content
    public const float					BaseGrowthPercent = 0.15f;
    public const float                  BaseSownGrowthPercent = 0.0001f;
    public const float                  MinGrowthForAnimalIngestion = 0.1f;
    private const float					BaseDyingDamagePerTick = 1f/200f;
    private const float					GridPosRandomnessFactor = 0.30f;
    private const int					TicksWithoutLightBeforeStartDying = (int)(GenDate.TicksPerDay * 7.5f);
    private const int					LeaflessMinRecoveryTicks = 60000;	//Minimum time to not show leafless after being made leafless
    public const float					DefaultMinGrowthTemperature = 0;			//Min temperature at which plant can grow or reproduce
    public const float					DefaultMinOptimalGrowthTemperature = 6f;
    public const float					DefaultMaxOptimalGrowthTemperature = 42f;
    public const float					DefaultMaxGrowthTemperature = 58;			//Max temperature at which plant can grow or reproduce
    private const float					MinLeaflessTemperatureOffset = -18;
    public const float					MaxLeaflessTemperatureOffset = -10;
    public const float					TopVerticesAltitudeBias = 0.1f;
    private static Graphic				GraphicSowing = GraphicDatabase.Get<Graphic_Single>("Things/Plant/Plant_Sowing", ShaderDatabase.Cutout, Vector2.one, Color.white);
    
    private static readonly FloatRange DyingDamagePerTickBecauseExposedToLight = new FloatRange(0.02f / 200f, 0.2f / 200f);
    private static readonly FloatRange PollutionDamagePerTickRange = new FloatRange(1f / GenDate.TicksPerDay, 10f / GenDate.TicksPerDay);
    private const float VacuumDamagePerTick = 1f;
    
    private static readonly Texture2D   CutAllBlightTex = ContentFinder<Texture2D>.Get("UI/Commands/CutAllBlightedPlants");

    [TweakValue("Graphics", -1, 1)]	private static float	LeafSpawnRadius = 0.4f;
    [TweakValue("Graphics", 0, 2)] 	private static float	LeafSpawnYMin = 0.3f;
    [TweakValue("Graphics", 0, 2)] 	private static float	LeafSpawnYMax = 1.0f;

    //Properties
    public virtual float Growth
    {
        get => growthInt;
        set
        {
            growthInt = Mathf.Clamp01(value);
            cachedLabelMouseover = null;
        }
    }
    public virtual int Age
    {
        get => ageInt;
        set
        {
            ageInt = value;
            cachedLabelMouseover = null;
        }
    }
    public virtual bool HarvestableNow => def.plant.Harvestable && growthInt > def.plant.harvestMinGrowth;

    public bool HarvestableSoon
    {
        get
        {
            if( HarvestableNow )
                return true;

            if( !def.plant.Harvestable )
                return false;
            
            var leftGrowth = Mathf.Max(1f - Growth, 0f);
            var leftDays = leftGrowth * def.plant.growDays;
            var leftGrowthAny = Mathf.Max(1f - def.plant.harvestMinGrowth, 0f);
            var leftDaysAny = leftGrowthAny * def.plant.growDays;

            return (leftDays <= 10f || leftDaysAny <= 1f)
                && GrowthRateFactor_Fertility > 0f
                && GrowthRateFactor_Temperature > 0f;
        }
    }
    public virtual bool BlightableNow =>
        !Blighted
        && def.plant.Blightable
        && sown
        && LifeStage != PlantLifeStage.Sowing
        && !Map.wildPlantSpawner.AllWildPlants.Contains(def);

    public Blight Blight
    {
        get
        {
            if( !Spawned || !def.plant.Blightable )
                return null;

            return Position.GetFirstBlight(Map);
        }
    }
    public bool Blighted => Blight != null;

    public override bool IngestibleNow
    {
        get
        {
            if( !base.IngestibleNow )
                return false;

            //Trees are always edible
            // This allows alphabeavers completely destroy the tree ecosystem
            if( def.plant.IsTree )
                return true;

            if( growthInt < def.plant.harvestMinGrowth )
                return false;

            if (growthInt < Plant.MinGrowthForAnimalIngestion)
                return false;

            if( LeaflessNow )
                return false;

            if( Spawned && Math.Max(Position.GetSnowDepth(Map), Position.GetSandDepth(Map)) > def.hideAtSnowOrSandDepth)
                return false;

            return true;
        }
    }
    public virtual float CurrentDyingDamagePerTick
    {
        get
        {
            if( !Spawned )
                return 0f;

            var damage = 0f;

            if( def.plant.LimitedLifespan && ageInt > def.plant.LifespanTicks )
                damage = Mathf.Max(damage, BaseDyingDamagePerTick);

            if( !def.plant.diesToLight && def.plant.dieIfNoSunlight && unlitTicks > TicksWithoutLightBeforeStartDying )
                damage = Mathf.Max(damage, BaseDyingDamagePerTick);

            if( DyingBecauseExposedToLight )
            {
                var glow = Map.glowGrid.GroundGlowAt(Position, ignoreCavePlants: true);
                damage = Mathf.Max(damage, DyingDamagePerTickBecauseExposedToLight.LerpThroughRange(glow));
            }

            if (DyingBecauseExposedToVacuum)
                damage = Mathf.Max(damage, VacuumDamagePerTick * Position.GetVacuum(Map));

            if( DyingFromPollution || DyingFromNoPollution )
                damage = Mathf.Max(damage, PollutionDamagePerTickRange.RandomInRangeSeeded(Position.GetHashCode()));
            
            if(DyingBecauseOfTerrainTags)
                damage = Mathf.Max(damage, BaseDyingDamagePerTick);

            return damage;
        }
    }
    
    public virtual bool DyingBecauseExposedToLight => def.plant.diesToLight && Spawned && Map.glowGrid.GroundGlowAt(Position, ignoreCavePlants: true) > 0f;
    public virtual bool DyingBecauseExposedToVacuum => !def.plant.vacuumResistant && Spawned && Position.GetVacuum(Map) >= VacuumUtility.MinVacuumForDamage;
    public virtual bool DyingBecauseOfTerrainTags => def.plant.WildTerrainTags.Count > 0 && !def.plant.WildTerrainTags.Overlaps(Position.GetTerrain(Map).tags.OrElseEmptyEnumerable());

    public bool Dying => CurrentDyingDamagePerTick > 0f;

    protected virtual bool Resting => GenLocalDate.DayPercent(this) < 0.25f || GenLocalDate.DayPercent(this) > 0.8f;

    public virtual float GrowthRate
    {
        get
        {
            if (Blighted)
                return 0f;

            if (Spawned && !PlantUtility.GrowthSeasonNow(Position, Map, def))
                return 0f;

            return GrowthRateFactor_Fertility * GrowthRateFactor_Temperature * GrowthRateFactor_Light * GrowthRateFactor_NoxiousHaze * GrowthRateFactor_Drought;
        }
    }
    public virtual string GrowthRateCalcDesc
    {
        get
        {
            var sb = new StringBuilder();

            if(GrowthRateFactor_Fertility != 1f)
                sb.AppendInNewLine("StatsReport_MultiplierFor".Translate("FertilityLower".Translate()) + ": " + GrowthRateFactor_Fertility.ToStringPercent());
            
            if(GrowthRateFactor_Temperature != 1f)
                sb.AppendInNewLine("StatsReport_MultiplierFor".Translate("TemperatureLower".Translate()) + ": " + GrowthRateFactor_Temperature.ToStringPercent());
            
            if(GrowthRateFactor_Light != 1f)
                sb.AppendInNewLine("StatsReport_MultiplierFor".Translate("LightLower".Translate()) + ": " + GrowthRateFactor_Light.ToStringPercent());

            if(ModsConfig.BiotechActive && Map.gameConditionManager.ConditionIsActive(GameConditionDefOf.NoxiousHaze) && GrowthRateFactor_NoxiousHaze != 1f)
                sb.AppendInNewLine("StatsReport_MultiplierFor".Translate(GameConditionDefOf.NoxiousHaze.label) + ": " + GrowthRateFactor_NoxiousHaze.ToStringPercent());
            
            if(ModsConfig.OdysseyActive && Map.gameConditionManager.ConditionIsActive(GameConditionDefOf.Drought) && GrowthRateFactor_Drought != 1f)
                sb.AppendInNewLine("StatsReport_MultiplierFor".Translate(GameConditionDefOf.Drought.label) + ": " + GrowthRateFactor_Drought.ToStringPercent());

            return sb.ToString();
        }
    }
    protected float GrowthPerTick
    {
        get
        {
            if (LifeStage != PlantLifeStage.Growing || Resting)
                return 0;
            
            if (!def.plant.vacuumResistant && Position.GetVacuum(Map) >= VacuumUtility.MinVacuumForDamage)
                return 0;
            
            var baseRate = 1 / (GenDate.TicksPerDay * def.plant.growDays);
            return baseRate * GrowthRate;
        }
    }
    public float GrowthRateFactor_Fertility => PlantUtility.GrowthRateFactorFor_Fertility(def, Map.fertilityGrid.FertilityAt(Position));

    public float GrowthRateFactor_Light
    {
        get
        {
            var glow = Map.glowGrid.GroundGlowAt(Position);

            return PlantUtility.GrowthRateFactorFor_Light(def, glow);
        }
    }
    public float GrowthRateFactor_Temperature
    {
        get
        {
            if( !GenTemperature.TryGetTemperatureForCell(Position, Map, out var cellTemp) )
                return 1;

            return PlantUtility.GrowthRateFactorFor_Temperature(def, cellTemp);
        }
    }
    public float GrowthRateFactor_NoxiousHaze
    {
        get
        {
            if(NoxiousHazeUtility.IsExposedToNoxiousHaze(this))
                return GameCondition_NoxiousHaze.PlantGrowthRateFactor;

            return 1f;
        }
    }

    public float GrowthRateFactor_Drought
    {
        get
        {
            if(!ModsConfig.OdysseyActive)
                return 1f; // Droughts are an Odyssey feature
            
            //If we're in hydroponics we're not affected by droughts
            if(CurrentlyCultivated() && Position.GetEdifice(Map)?.def.building.SupportsPlants == true)
                return 1f;

            // Drought doesn't affect cave plants
            if (def.plant.cavePlant)
                return 1f;

            if (Map.gameConditionManager.ConditionIsActive(GameConditionDefOf.Drought))
                return GameCondition_Drought.PlantGrowthRateFactor;
            
            return 1f;
        }
    }
    
    protected int TicksUntilFullyGrown
    {
        get
        {
            if( growthInt > 0.9999f )
                return 0;

            var growthPerTick = GrowthPerTick;

            if( growthPerTick == 0f )
                return int.MaxValue;
            else
                return (int)((1f - growthInt) / growthPerTick);
        }
    }
    protected string GrowthPercentString => (Mathf.Floor((growthInt + 0.0001f) * 100f) / 100f).ToStringPercent();

    public override string LabelMouseover
    {
        get
        {
            if( cachedLabelMouseover == null )
            {
                var sb = new StringBuilder();
                sb.Append(def.LabelCap);
                sb.Append(" (" + "PercentGrowth".Translate(GrowthPercentString));
            
                if( Dying )
                    sb.Append(", " + "DyingLower".Translate() );
            
                sb.Append(")");
                cachedLabelMouseover = sb.ToString();
            }

            return cachedLabelMouseover;
        }
    }
    protected virtual bool HasEnoughLightToGrow => GrowthRateFactor_Light > 0.001f;

    public virtual PlantLifeStage LifeStage
    {
        get
        {
            if( growthInt < 0.0001f )
                return PlantLifeStage.Sowing;

            if( growthInt > 0.999f )
                return PlantLifeStage.Mature;

            return PlantLifeStage.Growing;
        }
    }
    public override Graphic Graphic
    {
        get
        {
            if( LifeStage == PlantLifeStage.Sowing )
                return GraphicSowing;

            if( def.plant.pollutedGraphic != null && PositionHeld.IsPolluted(MapHeld) )
                return def.plant.pollutedGraphic;
            
            if(def.plant.leaflessImmatureGraphic != null && LeaflessNow && !HarvestableNow)
                return def.plant.leaflessImmatureGraphic;
            
            //Note: Plants that you sowed and which are harvestable now never display the leafless graphic
            if( def.plant.leaflessGraphic != null && LeaflessNow && !(sown && HarvestableNow) )
                return def.plant.leaflessGraphic;
            
            if( def.plant.immatureGraphic != null && !HarvestableNow )
                return def.plant.immatureGraphic;

            return base.Graphic;
        }
    }

    public Graphic SnowOverlayGraphic
    {
        get
        {
            if( def.plant.immatureSnowOverlayGraphic != null && !HarvestableNow )
                return def.plant.immatureSnowOverlayGraphic;
            
            if( def.plant.leaflessSnowOverlayGraphic != null && LeaflessNow && !(sown && HarvestableNow) )
                return def.plant.leaflessSnowOverlayGraphic;

            return def.plant.snowOverlayGraphic; //Which may be null
        }
    }
    public bool LeaflessNow
    {
        get
        {
            if( Find.TickManager.TicksGame - madeLeaflessTick < LeaflessMinRecoveryTicks )
                return true;
            else
                return false;
        }
    }
    protected virtual float LeaflessTemperatureThresh => def.plant.minGrowthTemperature + Rand.RangeSeeded(MinLeaflessTemperatureOffset, MaxLeaflessTemperatureOffset, thingIDNumber ^ 838051265);

    public bool IsCrop
    {
        get
        {
            if( !def.plant.Sowable )
                return false;

            if( !Spawned )
            {
                Log.Warning("Can't determine if crop when unspawned.");
                return false;
            }

            return def == WorkGiver_Grower.CalculateWantedPlantDef(Position, Map);
        }
    }
    public bool DyingFromPollution      => def.plant.RequiresNoPollution && Position.IsPolluted(Map);
    public bool DyingFromNoPollution    => def.plant.RequiresPollution && !Position.IsPolluted(Map);

    public override void SpawnSetup(Map map, bool respawningAfterLoad)
    {
        base.SpawnSetup(map, respawningAfterLoad);

        // Don't call during init because indoor warm plants will all go leafless if it's cold outside
        if (Current.ProgramState == ProgramState.Playing && !respawningAfterLoad && !BeingTransportedOnGravship)
            CheckMakeLeafless();
    }

    public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
    {
        var blight = Position.GetFirstBlight(Map);

        base.DeSpawn(mode);

        if (mode != DestroyMode.WillReplace &&  blight != null)
            blight.Notify_PlantDeSpawned();
    }

    public override void ExposeData()
    {
        base.ExposeData();
        
        Scribe_Values.Look(ref growthInt, 	"growth");
        Scribe_Values.Look(ref ageInt, 		"age", 0);
        Scribe_Values.Look(ref unlitTicks,	"unlitTicks", 0 );
        Scribe_Values.Look(ref madeLeaflessTick, "madeLeaflessTick", -99999);
        Scribe_Values.Look(ref sown,		"sown", false);
    }

    public override void PostMapInit()
    {
        CheckMakeLeafless();
    }

    protected override void IngestedCalculateAmounts(Pawn ingester, float nutritionWanted, out int numTaken, out float nutritionIngested)
    {
        float nutritionStat = this.GetStatValue(StatDefOf.Nutrition);
        float nutritionAvailable = growthInt * nutritionStat;
        nutritionIngested = Mathf.Min(nutritionWanted, nutritionAvailable);

        if( nutritionIngested >= nutritionAvailable )
        {
            //Plant completely destroyed
            numTaken = 1;
        }
        else
        {
            //Plant growth reduced but not destroyed
            numTaken = 0;
            growthInt -= nutritionIngested / nutritionStat;
            if( Spawned )
                Map.mapDrawer.MapMeshDirty(Position, MapMeshFlagDefOf.Things);
        }
    }

    public virtual void PlantCollected(Pawn by, PlantDestructionMode plantDestructionMode)
    {
        if( def.plant.HarvestDestroys )
        {
            if( def.plant.IsTree && def.plant.treeLoversCareIfChopped)
            {
                Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf.CutTree, by.Named(HistoryEventArgsNames.Doer)));
                Map.treeDestructionTracker.Notify_TreeCut(by);
            }

            // If blighted check neighbours and assign them to be cut too.
            if (Blighted && plantDestructionMode == PlantDestructionMode.Cut)
            {
                Map.floodFiller.FloodFill(Position, cell =>
                {
                    if (Position == cell)
                        return true;
                    
                    var plant = cell.GetPlant(Map);
                    
                    if (plant == null || Map.designationManager.HasMapDesignationOn(plant))
                        return false;
                    
                    return plant.Blighted;
                }, cell =>
                {
                    if (Position == cell)
                        return;
                    
                    var plant = cell.GetPlant(Map);
                    Map.designationManager.AddDesignation(new Designation(plant, DesignationDefOf.CutPlant));
                    
                    by.jobs?.curJob?.targetQueueA?.Add(plant);
                }, maxCellsToProcess: 100);
            }
            
            var stump = TrySpawnStump(plantDestructionMode);
            var map = Map;
            Destroy(DestroyMode.KillFinalizeLeavingsOnly);

            //If we were cut by the "cut" designator, also try to cut the stump
            if (stump != null && plantDestructionMode == PlantDestructionMode.Cut && by.Faction == Faction.OfPlayer)
            {
                map.designationManager.AddDesignation(new Designation(stump, DesignationDefOf.CutPlant));
                //Add the stump to the cutter's job queue so it's cut without interrupting other jobs.
                by.jobs?.curJob?.targetQueueA?.Add(stump);
            }
        }
        else
        {
            growthInt = def.plant.harvestAfterGrowth;
            Map.mapDrawer.MapMeshDirty(Position, MapMeshFlagDefOf.Things);
        }
    }

    public Thing TrySpawnStump(PlantDestructionMode treeDestructionMode)
    {
        if (!Spawned || LifeStage == PlantLifeStage.Sowing)
            return null;

        //Plants that are not harvestable do not leave stumps (eg young trees)
        if (!HarvestableNow)
            return null;

        ThingDef defToSpawn = null;

        switch (treeDestructionMode)
        {
            case PlantDestructionMode.Smash: defToSpawn = def.plant.smashedThingDef; break;
            case PlantDestructionMode.Flame: defToSpawn = def.plant.burnedThingDef; break;
            case PlantDestructionMode.Chop:
            case PlantDestructionMode.Cut:
                defToSpawn = def.plant.choppedThingDef;
                break;
        }

        if (defToSpawn != null)
        {
            var stump = GenSpawn.Spawn(defToSpawn, Position, Map);
            if (stump is DeadPlant deadPlant)
                deadPlant.Growth = Growth;
            if (Find.Selector.IsSelected(this))
                Find.Selector.Select(stump, playSound: false, forceDesignatorDeselect: false);
            return stump;
        }

        return null;
    }

    public override void Kill(DamageInfo? dinfo = null, Hediff exactCulprit = null)
    {
        if (Spawned && dinfo.HasValue)
        {
            if (dinfo.Value.Def == DamageDefOf.Flame)
                TrySpawnStump(PlantDestructionMode.Flame);
            else
                TrySpawnStump(PlantDestructionMode.Smash);

            if (def.plant.IsTree && def.plant.treeLoversCareIfChopped)
                Map.treeDestructionTracker.Notify_TreeDestroyed(dinfo.Value);
        }

        base.Kill(dinfo, exactCulprit);
    }

    protected virtual void CheckMakeLeafless()
    {
        if (DyingFromPollution)
            MakeLeafless(LeaflessCause.Pollution);
        else if (DyingFromNoPollution)
            MakeLeafless(LeaflessCause.NoPollution);
        else if (this.GetRoom() is { UsesOutdoorTemperature: true } && AmbientTemperature < LeaflessTemperatureThresh)
            MakeLeafless(LeaflessCause.Cold);
    }

    public enum LeaflessCause { Cold, Poison, Pollution, NoPollution };
    public virtual void MakeLeafless(LeaflessCause cause, bool sendMessage = true)
    {
        var changed = !LeaflessNow;
        var map = Map; // before applying damage

        if( cause == LeaflessCause.Poison && def.plant.leaflessGraphic == null )
        {
            //Poisoned a plant without a leafless graphic - we have to kill it

            if (IsCrop && sendMessage && MessagesRepeatAvoider.MessageShowAllowed("MessagePlantDiedOfPoison-"+def.defName, 240f))
                Messages.Message( "MessagePlantDiedOfPoison".Translate(GetCustomLabelNoCount(includeHp: false)), new TargetInfo(Position, map), MessageTypeDefOf.NegativeEvent );

            TakeDamage(new DamageInfo(DamageDefOf.Rotting, 99999));	
        }
        else if( def.plant.dieIfLeafless )
        {
            //Plant dies if ever leafless

            if( IsCrop )
            {
                if( cause == LeaflessCause.Cold )
                {
                    if( sendMessage && MessagesRepeatAvoider.MessageShowAllowed("MessagePlantDiedOfCold-"+def.defName, 240f) )
                        Messages.Message( "MessagePlantDiedOfCold".Translate(GetCustomLabelNoCount(includeHp: false)), new TargetInfo(Position, map), MessageTypeDefOf.NegativeEvent );
                }
                else if ( cause == LeaflessCause.Poison )
                {
                    if( sendMessage && MessagesRepeatAvoider.MessageShowAllowed("MessagePlantDiedOfPoison-"+def.defName, 240f) )
                        Messages.Message( "MessagePlantDiedOfPoison".Translate(GetCustomLabelNoCount(includeHp: false)), new TargetInfo(Position, map), MessageTypeDefOf.NegativeEvent );
                }
                else if( cause == LeaflessCause.Pollution )
                {
                    if( sendMessage && MessagesRepeatAvoider.MessageShowAllowed("MessagePlantDiedOfPollution-"+def.defName, 240f) )
                        Messages.Message( "MessagePlantDiedOfPollution".Translate(GetCustomLabelNoCount(includeHp: false)), new TargetInfo(Position, map), MessageTypeDefOf.NegativeEvent );
                }
                else if( cause == LeaflessCause.NoPollution )
                {
                    if( sendMessage && MessagesRepeatAvoider.MessageShowAllowed("MessagePlantDiedOfNoPollution-"+def.defName, 240f) )
                        Messages.Message( "MessagePlantDiedOfNoPollution".Translate(GetCustomLabelNoCount(includeHp: false)), new TargetInfo(Position, map), MessageTypeDefOf.NegativeEvent );
                }
            }

            TakeDamage(new DamageInfo(DamageDefOf.Rotting, 99999));	
        }
        else
        {
            //Just become visually leafless
            madeLeaflessTick = Find.TickManager.TicksGame;
        }

        if( changed )
            map.mapDrawer.MapMeshDirty( Position, MapMeshFlagDefOf.Things );
    }

    protected override void TickInterval(int delta)
    {
        base.TickInterval(delta);

        if (this.IsHashIntervalTick(GenTicks.TickLongInterval, delta))
            TickLong();
    }

    public override void TickLong()
    {
        CheckMakeLeafless();

        if (Destroyed)
            return;
        
        base.TickLong();

        if (PlantUtility.GrowthSeasonNow(Position, Map, def))
        {
            //Grow
            var prevGrowth = growthInt;
            var wasMature = LifeStage == PlantLifeStage.Mature;
            growthInt += GrowthPerTick * GenTicks.TickLongInterval;

            if( growthInt > 1f )
                growthInt = 1f;

            //Regenerate layers
            if( (!wasMature && LifeStage == PlantLifeStage.Mature)
                || (int)(prevGrowth * 10f) != (int)(growthInt * 10f) )
            {
                if( CurrentlyCultivated() )
                    Map.mapDrawer.MapMeshDirty(Position, MapMeshFlagDefOf.Things);
            }
        }

        var hasLight = HasEnoughLightToGrow;

        //Record light starvation
        if( !hasLight )
            unlitTicks += GenTicks.TickLongInterval;
        else
            unlitTicks = 0;

        //Age
        ageInt += GenTicks.TickLongInterval;

        //Dying
        if( Dying )
        {
            var map = Map;
            var isCrop = IsCrop; // before applying damage!
            var harvestableNow = HarvestableNow;
            var dyingBecauseExposedToLight = DyingBecauseExposedToLight;
            var dyingBecauseExposedToVacuum = DyingBecauseExposedToVacuum;

            var dyingDamAmount = Mathf.CeilToInt(CurrentDyingDamagePerTick * GenTicks.TickLongInterval);
            TakeDamage(new DamageInfo(DamageDefOf.Rotting, dyingDamAmount));

            if( Destroyed && !def.plant.skipDeteriorationMessage )
            {
                if( isCrop && def.plant.Harvestable && MessagesRepeatAvoider.MessageShowAllowed("MessagePlantDiedOfRot-" + def.defName, 240f) )
                {
                    string messageKey;
                    if (harvestableNow)
                    {
                        if (ModsConfig.BiotechActive && Position.IsPolluted(map))
                            messageKey = "MessagePlantDiedOfRot_PollutedTerrain";
                        else
                            messageKey = "MessagePlantDiedOfRot_LeftUnharvested";
                    }
                    else if( dyingBecauseExposedToLight )
                        messageKey = "MessagePlantDiedOfRot_ExposedToLight";
                    else if( dyingBecauseExposedToVacuum )
                        messageKey = "MessagePlantDiedOfRot_ExposedToVacuum";
                    else
                        messageKey = "MessagePlantDiedOfRot";

                    Messages.Message(messageKey.Translate(GetCustomLabelNoCount(includeHp: false)),
                        new TargetInfo(Position, map),
                        MessageTypeDefOf.NegativeEvent);
                }

                return;
            }
        }

        //State has changed, label may have to as well
        //Also, we want to keep this null so we don't have useless data sitting there a long time in plants that never get looked at
        cachedLabelMouseover = null;

        // Drop a leaf
        if( def.plant.dropLeaves )
        {
            var mote = MoteMaker.MakeStaticMote(Vector3.zero, Map, ThingDefOf.Mote_Leaf) as MoteLeaf;
            if( mote != null )
            {
                var size = def.plant.visualSizeRange.LerpThroughRange(growthInt);
                var graphicSize = def.graphicData.drawSize.x * size; //Plants don't support non-square drawSizes

                var disc = Rand.InsideUnitCircleVec3 * LeafSpawnRadius;	// Horizontal alignment
                mote.Initialize(Position.ToVector3Shifted()	// Center of the tile
                        + Vector3.up * Rand.Range(LeafSpawnYMin, LeafSpawnYMax)	// Vertical alignment
                        + disc	// Horizontal alignment
                        + Vector3.forward * def.graphicData.shadowData.offset.z,	// Move to the approximate base of the tree
                    Rand.Value * GenTicks.TickLongInterval.TicksToSeconds(),
                    disc.z > 0,
                    graphicSize
                );
            }
        }
    }

    protected virtual bool CurrentlyCultivated()
    {
        if( !def.plant.Sowable )
            return false;

        if( !Spawned )
            return false;
        
        var z = Map.zoneManager.ZoneAt(Position);
        if (z != null && z is Zone_Growing)
            return true;

        var ed = Position.GetEdifice(Map);
        if( ed != null && ed.def.building.SupportsPlants )
            return true;

        return false;
    }
    public bool DeliberatelyCultivated()
    {
        if( !def.plant.Sowable )
            return false;

        if( !Spawned )
            return false;

        if (Map.zoneManager.ZoneAt(Position) is Zone_Growing growZone && growZone.GetPlantDefToGrow() == def)
            return true;

        var ed = Position.GetEdifice(Map);
        if( ed != null && ed.def.building.SupportsPlants )
            return true;

        return false;
    }

    public virtual bool CanYieldNow()
    {
        if( !HarvestableNow )
            return false;

        //If yield is 0, handle it
        if( def.plant.harvestYield <= 0 )
            return false;

        if( Blighted )
            return false;
        
        return true;
    }

    public virtual int YieldNow()
    {
        if( !CanYieldNow() )
            return 0;

        //Start with max yield
        var yieldFloat = def.plant.harvestYield;

        //Factor for growth
        var growthFactor = Mathf.InverseLerp( def.plant.harvestMinGrowth, 1, growthInt);
        growthFactor = 0.5f + growthFactor * 0.5f;	//Scalebias it to 0.5..1 range.
        yieldFloat *= growthFactor;

        //Factor down for health with a 50% lerp factor
        yieldFloat *=  Mathf.Lerp( 0.5f, 1f,  ((float)HitPoints / (float)MaxHitPoints) );

        //Factor for difficulty
        if (def.plant.harvestYieldAffectedByDifficulty)
            yieldFloat *= Find.Storyteller.difficulty.cropYieldFactor;

        return GenMath.RoundRandom(yieldFloat);		
    }

    public override void Print( SectionLayer layer )
    {
        var trueCenter = this.TrueCenter();
        var wantSnowOverlay = Position.GetSnowDepth(Map) > 0.8f;

        Rand.PushState();
        Rand.Seed = Position.GetHashCode();//So our random generator makes the same numbers every time

        //Determine how many meshes to print
        var meshCount = Mathf.CeilToInt(growthInt * def.plant.maxMeshCount);
        if( meshCount < 1 )
            meshCount = 1;

        //Determine plane size
        var size = def.plant.visualSizeRange.LerpThroughRange(growthInt);
        var graphicSize = def.graphicData.drawSize.x * size; //Plants don't support non-square drawSizes

        //Shuffle up the position indices and place meshes at them
        //We do this to get even mesh placement by placing them roughly on a grid
        var meshesYielded = 0;
        var posIndexList = PlantPosIndices.GetPositionIndices(this);
        var clampedBottomToCellBottom = false;
        for(var i=0; i<posIndexList.Length; i++ )
        {		
            Vector3 adjustedCenter;
            var posIndex = posIndexList[i];

            //Determine center position
            if( def.plant.maxMeshCount == 1 )
            {
                //Determine random local position variance
                const float PositionVariance = 0.05f;

                adjustedCenter = trueCenter + Gen.RandomHorizontalVector(PositionVariance);

                //Clamp bottom of plant to square bottom
                //So tall plants grow upward
                float squareBottom = Position.z;
                if( adjustedCenter.z - size / 2f < squareBottom )
                {
                    adjustedCenter.z = squareBottom + size / 2f;
                    clampedBottomToCellBottom = true;
                }
            }
            else
            {
                //Grid width is the square root of max mesh count
                var gridWidth = 1;
                switch( def.plant.maxMeshCount )
                {
                case 1: gridWidth = 1; break;
                case 4: gridWidth = 2; break;
                case 9: gridWidth = 3; break;
                case 16: gridWidth = 4; break;
                case 25: gridWidth = 5; break;
                default: Log.Error(def + " must have plant.MaxMeshCount that is a perfect square."); break;
                }
                var gridSpacing = 1f / gridWidth; //This works out to give half-spacings around the edges

                adjustedCenter = Position.ToVector3(); //unshifted
                adjustedCenter.y = def.Altitude;//Set altitude

                //Place this mesh at its randomized position on the submesh grid
                adjustedCenter.x += 0.5f * gridSpacing;
                adjustedCenter.z += 0.5f * gridSpacing;
                var xInd = posIndex / gridWidth;
                var zInd = posIndex % gridWidth;
                adjustedCenter.x += xInd * gridSpacing;
                adjustedCenter.z += zInd * gridSpacing;
                
                //Add a random offset
                var gridPosRandomness = gridSpacing * GridPosRandomnessFactor;
                adjustedCenter += Gen.RandomHorizontalVector(gridPosRandomness);
            }

            //Randomize horizontal flip
            var flipped = Rand.Bool;

            var snowOverlayMat = wantSnowOverlay && SnowOverlayGraphic is {} snowOverlayGraphic
                ? snowOverlayGraphic.MatSingleFor(this)
                : null;
            
            var mat = Graphic.MatSingleFor(this);
            // Pulls a random material. This is pretty jank since MatSingleFor is deterministic based on the plant ID but this isn't
            // but we are too close to release to be messing with Graphic_Random so just do this for now
            if (Graphic is Graphic_Random random)
            {
                var graphicCount = random.SubGraphicsCount;
                var randomIndex = Rand.Range(0, graphicCount);
                mat = random.SubGraphicAtIndex(randomIndex).MatSingle;

                if (wantSnowOverlay && SnowOverlayGraphic is Graphic_Random randomSnowOverlayGraphic)
                {
                    //Match the snow overlay material to the random plant material
                    snowOverlayMat = randomSnowOverlayGraphic.SubGraphicAtIndex(randomIndex).MatSingle; //This does a modulo length, so it's safe
                }
            }

            Graphic.TryGetTextureAtlasReplacementInfo(mat, def.category.ToAtlasGroup(), flipped, false, out mat, out var uvs, out _);

            //Set wind exposure value at each vertex by setting vertex color
            PlantUtility.SetWindExposureColors(workingColors, this);

            var planeSize = new Vector2(graphicSize, graphicSize);

            Printer_Plane.PrintPlane( layer, 
                                      adjustedCenter, 
                                      planeSize,	
                                      mat, 
                                      flipUv: flipped, 
                                      uvs: uvs,
                                      colors:  workingColors,
                                      topVerticesAltitudeBias: TopVerticesAltitudeBias,	// need to beat walls corner filler (so trees don't get cut by mountains)
                                      uvzPayload: this.HashOffset() % 1024 );

            if (wantSnowOverlay && snowOverlayMat != null)
            {
                Graphic.TryGetTextureAtlasReplacementInfo(snowOverlayMat, def.category.ToAtlasGroup(), flipped, false, out snowOverlayMat, out uvs, out _);
                
                //Print snow overlay
                Printer_Plane.PrintPlane(layer, adjustedCenter.WithYOffset(Altitudes.AltInc / 100), planeSize, snowOverlayMat, flipUv: flipped, uvs: uvs,
                    colors: workingColors, topVerticesAltitudeBias: TopVerticesAltitudeBias, uvzPayload: this.HashOffset() % 1024);
            }


            meshesYielded++;
            if( meshesYielded >= meshCount )
                break;
        }

        if( def.graphicData.shadowData != null )
        {
            //Start with a standard shadow center
            var shadowCenter = trueCenter + def.graphicData.shadowData.offset * size;

            //Clamp center of shadow to cell bottom
            if( clampedBottomToCellBottom )
                shadowCenter.z = Position.ToVector3Shifted().z + def.graphicData.shadowData.offset.z;

            shadowCenter.y -= Altitudes.AltInc;

            var shadowVolume = def.graphicData.shadowData.volume * size;

            Printer_Shadow.PrintShadow(layer, shadowCenter, shadowVolume, Rot4.North);
        }

        Rand.PopState();
    }

    public override string GetInspectString()
    {
        var sb = new StringBuilder();

        if (def.plant.showGrowthInInspectPane)
        {
            if( LifeStage == PlantLifeStage.Growing )
            {
                sb.AppendLine("PercentGrowth".Translate(GrowthPercentString));
                sb.Append("GrowthRate".Translate() + ": " + GrowthRate.ToStringPercent());
                if( !Blighted )
                {
                    //Trying to avoid unneccessary allocs here
                    var additionalConditions = ArrayPool<string>.Shared.Rent(4);
                    var additionalConditionsCount = 0;
                    
                    static void AddCondition(string[] conditions, ref int count, string condition)
                    {
                        if (count < conditions.Length)
                            conditions[count++] = condition;
                        else
                            Log.Error("Too many conditions for plant growth inspect string");
                    }
                    
                    if( Resting )
                        AddCondition(additionalConditions, ref additionalConditionsCount, "PlantResting".Translate());
                    if( !HasEnoughLightToGrow )
                        AddCondition(additionalConditions, ref additionalConditionsCount, "PlantNeedsLightLevel".Translate() + " " + def.plant.growMinGlow.ToStringPercent());
                    var tempFactor = GrowthRateFactor_Temperature;
                    if( tempFactor < 0.99f )
                    {
                        if (Mathf.Approximately(tempFactor, 0f) || !PlantUtility.GrowthSeasonNow(Position, Map, def))
                            AddCondition(additionalConditions, ref additionalConditionsCount, "OutOfIdealTemperatureRangeNotGrowing".Translate());
                        else
                            AddCondition(additionalConditions, ref additionalConditionsCount, "OutOfIdealTemperatureRange".Translate(Mathf.Max(1, Mathf.RoundToInt(tempFactor * 100f)).ToString())); // print less-than-one as  one, since 0% is misleading
                    }
                    
                    var droughtFactor = GrowthRateFactor_Drought;
                    if (droughtFactor < 0.99f) 
                        AddCondition(additionalConditions, ref additionalConditionsCount, GameConditionDefOf.Drought.label);
                    
                    //Not using GenText.ToCommaList because I'd have to slice the array (which allocates), or make a Span overload, and we don't need the "and" feature, so it's not language-dependent
                    var conditionsString = string.Join(", ", additionalConditions, 0, additionalConditionsCount);
                    ArrayPool<string>.Shared.Return(additionalConditions);

                    if (!conditionsString.NullOrEmpty())
                        sb.Append(" (").Append(conditionsString).Append(')');
                }

                sb.AppendLine();
            }
            else if( LifeStage == PlantLifeStage.Mature )
            {
                sb.AppendLine(HarvestableNow ? "ReadyToHarvest".Translate() : "Mature".Translate());
            }
            
            if( DyingBecauseExposedToLight )
                sb.AppendLine("DyingBecauseExposedToLight".Translate());
            
            if (DyingBecauseExposedToVacuum)
                sb.AppendLine("DyingBecauseExposedToVacuum".Translate());
            
            if(DyingBecauseOfTerrainTags)
                sb.AppendLine("DyingBecauseOfTerrain".Translate());
            
            if (Blighted)
                sb.AppendLine($"{"Blighted".Translate()} ({Blight.Severity.ToStringPercent()})");
        }
        
        var extraCompStrings = InspectStringPartsFromComps();
        
        if (!extraCompStrings.NullOrEmpty())
            sb.Append(extraCompStrings);
        
        return sb.ToString().TrimEndNewlines();
    }
    
    public virtual void CropBlighted()
    {
        if( !Blighted )
            GenSpawn.Spawn(ThingDefOf.Blight, Position, Map);
    }

    public override IEnumerable<Gizmo> GetGizmos()
    {
        foreach( var gizmo in base.GetGizmos() )
        {
            yield return gizmo;
        }

        if( Blighted  )
        {
            var designation = Map.designationManager.DesignationOn(this);

            if( designation == null || designation.def != DesignationDefOf.CutPlant )
            {
                var cut = new Command_Action
                {
                    defaultLabel = "CutAllBlight".Translate(),
                    defaultDesc = "CutAllBlightDesc".Translate(),
                    icon = CutAllBlightTex,
                    action = () =>
                    {
                        foreach( var thing in Map.listerThings.ThingsInGroup(ThingRequestGroup.Plant))
                        {
                            var plant = (Plant) thing;
                        
                            if( plant == null || !plant.Blighted )
                                continue;
                        
                            if( Map.designationManager.HasMapDesignationOn(plant) )
                                continue; // don't over-write existing designations

                            Map.designationManager.AddDesignation(new Designation(plant, DesignationDefOf.CutPlant));
                        }
                    }
                };

                yield return cut;
            }
        }

        if(DebugSettings.ShowDevGizmos)
        {
            if (Blighted)
            {
                yield return new Command_Action()
                {
                    defaultLabel = "DEV: Spread blight",
                    action = () => Blight.TryReproduceNow()
                };
            }
            else
            {
                yield return new Command_Action()
                {
                    defaultLabel = "DEV: Make blighted",
                    action = CropBlighted
                };
            }
        }
    }

    public override IEnumerable<StatDrawEntry> SpecialDisplayStats()
	{
        foreach(var stat in base.SpecialDisplayStats())
            yield return stat;        

        if (def.plant.LimitedLifespan)
        {
            var plantAge = Age.ToStringTicksToPeriod();
            yield return new StatDrawEntry(StatCategoryDefOf.Basics, "Stat_Thing_Plant_Age".Translate(), plantAge, "Stat_Thing_Plant_AgeDesc".Translate(), StatDisplayOrder.Thing_Plant_Age);
        }
        
        //Growth rate
        if(LifeStage == PlantLifeStage.Growing && Spawned)
        {
            var growthRateDesc = "Stat_Thing_Plant_GrowthRate_Desc".Translate();
            
            var growthRateCalc = GrowthRateCalcDesc;
            if(!growthRateCalc.NullOrEmpty())
                growthRateDesc += "\n\n" + growthRateCalc;
            
            growthRateDesc += "\n" + "StatsReport_FinalValue".Translate() + ": " + GrowthRate.ToStringPercent();

            yield return new StatDrawEntry(StatCategoryDefOf.Basics, 
                "Stat_Thing_Plant_GrowthRate".Translate(), 
                GrowthRate.ToStringPercent(), 
                growthRateDesc, 
                StatDisplayOrder.Thing_Plant_GrowthRate);
        }
    }
}

public static class PlantPosIndices
{
    //Cached
    //First index - for maxMeshCount
    //Second index - which of the lists for this maxMeshCount
    //Third index - which index in this list
    private static int[][][] rootList = null;

    //Constants
    private const int ListCount = 8;

    static PlantPosIndices()
    {
        rootList = new int[PlantProperties.MaxMaxMeshCount][][];
        for( var i=0; i<PlantProperties.MaxMaxMeshCount; i++ )
        {
            rootList[i] = new int[ListCount][];
            for( var j=0; j<ListCount; j++ )
            {
                var newList = new int[i+1];
                for( var k=0; k<i; k++ )
                {
                    newList[k] = k;
                }
                newList.Shuffle();

                rootList[i][j] = newList;
            }
        }
    }

    public static int[] GetPositionIndices( Plant p )
    {
        var mmc = p.def.plant.maxMeshCount;
        var index = (p.thingIDNumber^42348528) % ListCount;
        return rootList[mmc-1][ index ];
    }
}

}
