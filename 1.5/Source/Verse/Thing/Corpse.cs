using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse.AI.Group;

namespace Verse
{

public class Corpse : ThingWithComps, IThingHolder, IStrippable, IBillGiver, IObservedThoughtGiver
{
    //Config
    protected ThingOwner<Pawn> innerContainer;

    //Working vars
    public int timeOfDeath = -1;
    private int vanishAfterTimestamp = -1;
    private BillStack operationsBillStack = null;
    public bool everBuriedInSarcophagus;

    [Unsaved] private string cachedLabel = null;

    //Constants
    private const int DontCauseObservedCorpseThoughtAfterRitualExecutionTicks = 1 * GenDate.TicksPerDay;
    private static readonly IntRange ExplodeFilthCountRange = new IntRange(2, 5);

    //Properties
    public Pawn InnerPawn
    {
        get => innerContainer.Count > 0 ? innerContainer[0] : null;
        set
        {
            if( value == null )
                innerContainer.Clear();
            else
            {
                if( innerContainer.Count > 0 )
                {
                    Log.Error("Setting InnerPawn in corpse that already has one.");
                    innerContainer.Clear();
                }

                innerContainer.TryAdd(value);
            }
        }
    }
    public int Age
    {
        get => Find.TickManager.TicksGame - timeOfDeath;
        set => timeOfDeath = Find.TickManager.TicksGame - value;
    }
    public override string LabelNoCount
    {
        get
        {
            if (cachedLabel == null)
            {
                if (Bugged)
                {
                    Log.Error("LabelNoCount on Corpse while Bugged.");
                    return string.Empty;
                }

                cachedLabel = "DeadLabel".Translate(InnerPawn.Label, InnerPawn);
            }

            return cachedLabel;
        }
    }
    public override bool IngestibleNow
    {
        get
        {
            if( Bugged )
            {
                Log.Error("IngestibleNow on Corpse while Bugged.");
                return false;
            }

            if( !base.IngestibleNow )
                return false;

            if( !InnerPawn.RaceProps.IsFlesh )
                return false;

            if( this.GetRotStage() != RotStage.Fresh )
                return false;

            return true;
        }
    }
    public RotDrawMode CurRotDrawMode
    {
        get
        {
            var rottable = GetComp<CompRottable>();

            if( rottable != null )
            {
                switch (rottable.Stage)
                {
                    case RotStage.Dessicated: return RotDrawMode.Dessicated;
                    case RotStage.Rotting: return RotDrawMode.Rotting;
                    case RotStage.Fresh:
                    default: return RotDrawMode.Fresh;
                }
            }

            return RotDrawMode.Fresh;
        }
    }
    private bool ShouldVanish
    {
        get
        {
             return InnerPawn.RaceProps.Animal &&
                    vanishAfterTimestamp > 0 &&
                    Age >= vanishAfterTimestamp &&
                    Spawned &&
                    (this.GetRoom() != null && this.GetRoom().TouchesMapEdge) &&
                    !Map.roofGrid.Roofed(Position);
        }
    }
    public BillStack BillStack => operationsBillStack;
    public IEnumerable <IntVec3> IngredientStackCells { get { yield return InteractionCell; } }
    public bool Bugged
    {
        get
        {
            //This shouldn't ever happen and is purely a bug mitigation
            return innerContainer.Count == 0
                || innerContainer[0]?.def == null
                || innerContainer[0].kindDef == null;
        }
    }


    public Corpse()
    {
        operationsBillStack = new BillStack(this);
        innerContainer = new ThingOwner<Pawn>(this, oneStackOnly: true, contentsLookMode: LookMode.Reference);
    }
    
    public bool CurrentlyUsableForBills()
    {
        return InteractionCell.IsValid;
    }

    public bool UsableForBillsAfterFueling()
    {
        return CurrentlyUsableForBills();
    }
    
    public bool AnythingToStrip()
    {
        return InnerPawn.AnythingToStrip();
    }

    public ThingOwner GetDirectlyHeldThings()
    {
        return innerContainer;
    }

    public void GetChildHolders(List<IThingHolder> outChildren)
    {
        ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
    }

    public override void PostMake()
    {
        base.PostMake();

        timeOfDeath = Find.TickManager.TicksGame;
    }

    public override void SpawnSetup(Map map, bool respawningAfterLoad)
    {
        if( Bugged )
        {
            Log.Error(this + " spawned in bugged state.");
            return;
        }

        base.SpawnSetup(map, respawningAfterLoad);

        InnerPawn.Rotation = Rot4.South; //Fixes drawing errors

        var innerHediffs = InnerPawn.health.hediffSet.hediffs;
        for (int i = 0; i < innerHediffs.Count; i++)
        {
            innerHediffs[i].Notify_PawnCorpseSpawned();
        }
        
        NotifyColonistBar();
    }

    public override void Kill(DamageInfo? dinfo = null, Hediff exactCulprit = null)
    {
        if (dinfo.HasValue && dinfo.Value.Def == DamageDefOf.Bomb)
        {
            ThingDef filth;
            if (GetComp<CompRottable>() is CompRottable rot && rot.Stage == RotStage.Rotting)
                filth = ThingDefOf.Filth_CorpseBile;
            else
                filth = InnerPawn.RaceProps.BloodDef;

            if (filth != null)
            {
                var filthCount = ExplodeFilthCountRange.RandomInRange;
                for (var i = 0; i < filthCount; i++)
                {
                    FilthMaker.TryMakeFilth(PositionHeld, MapHeld, filth);
                }
            }

        }

        base.Kill(dinfo, exactCulprit);
    }

    public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
    {
        base.DeSpawn(mode);

        if( !Bugged )
            NotifyColonistBar();
    }

    public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
    {
        Pawn innerPawn = null;
        if( !Bugged )
        {
            innerPawn = InnerPawn; // store the reference before removing him from the container so we can use it later
            NotifyColonistBar();
            innerContainer.Clear();
        }

        this.GetLord()?.Notify_CorpseLost(this);

        base.Destroy(mode);

        if( innerPawn != null )
            PostCorpseDestroy(innerPawn);
    }

    public static void PostCorpseDestroy(Pawn pawn, bool discarded = false)
    {
        // unclaim grave if we have any
        pawn.ownership?.UnclaimAll();

        // destroy equipment
        pawn.equipment?.DestroyAllEquipment();

        // destroy inventory
        pawn.inventory.DestroyAll();

        pawn.health.Notify_PawnCorpseDestroyed();

        // destroy apparel
        pawn.apparel?.DestroyAll();

        if (!PawnGenerator.IsBeingGenerated(pawn) && !discarded)
            pawn.Ideo?.Notify_MemberCorpseDestroyed(pawn);
    }

    public override void TickRare()
    {
        TickRareInt();
        
        // in case we rot away when ticking base
        if (Destroyed)
            return;
        
        if (Bugged)
        {
            Log.Error(this + " has null innerPawn. Destroying.");
            Destroy();
            return;
        }
        
        var deathRefusal = InnerPawn.health.hediffSet.GetFirstHediff<Hediff_DeathRefusal>();
        
        if (deathRefusal != null)
        {
            deathRefusal.TickRare();
            
            // death refusal destroys corpse upon resurrection
            if (Destroyed) 
                return;
        }

        // Fleshbeasts spawn special filth when dessicated.
        if (ModsConfig.AnomalyActive && InnerPawn.kindDef.IsFleshBeast() && this.GetRotStage() == RotStage.Dessicated)
        {
            FilthMaker.TryMakeFilth(PositionHeld, MapHeld, ThingDefOf.Filth_TwistedFlesh);
            Destroy();
            return;
        }
        
        if (ShouldVanish)
            Destroy();
    }

    protected void TickRareInt()
    {
        if (AllComps != null)
        {
            for( int i = 0, count = AllComps.Count; i < count; i++ )
            {
                AllComps[i].CompTickRare();
            }
        }
        
        if (Destroyed)
            return;
        
        InnerPawn.TickRare();

        // React to gases (Expel rot stink, raise as shambler)
        GasUtility.CorpseGasEffectsTickRare(this);
    }

    protected override void IngestedCalculateAmounts(Pawn ingester, float nutritionWanted, out int numTaken, out float nutritionIngested)
    {
        //Determine part to take
        var part = GetBestBodyPartToEat(nutritionWanted);
        if( part == null )
        {
            Log.Error(ingester + " ate " + this + " but no body part was found. Replacing with core part.");
            part = InnerPawn.RaceProps.body.corePart;
        }

        //Determine the nutrition to gain
        float nut = FoodUtility.GetBodyPartNutrition(this, part);

        //Affect this thing
        //If ate core part, remove the whole corpse
        //Otherwise, remove the eaten body part
        if( part == InnerPawn.RaceProps.body.corePart )
        {
            if( ingester != null && PawnUtility.ShouldSendNotificationAbout(InnerPawn) && InnerPawn.RaceProps.Humanlike )
                Messages.Message("MessageEatenByPredator".Translate(InnerPawn.LabelShort, ingester.Named("PREDATOR"), InnerPawn.Named("EATEN")).CapitalizeFirst(), ingester, MessageTypeDefOf.NegativeEvent);

            numTaken = 1;
        }
        else
        {
            var missing = (Hediff_MissingPart)HediffMaker.MakeHediff(HediffDefOf.MissingBodyPart, InnerPawn, part);
            if (ingester != null)
                missing.lastInjury = HediffDefOf.Bite;
            missing.IsFresh = true;
            InnerPawn.health.AddHediff(missing);

            numTaken = 0;
        }
        
        nutritionIngested = nut;
    }

    public override IEnumerable<Thing> ButcherProducts( Pawn butcher, float efficiency )
    {
        foreach( var t in InnerPawn.ButcherProducts(butcher, efficiency) )
        {
            yield return t;
        }

        //Spread blood
        if( InnerPawn.RaceProps.BloodDef != null )
            FilthMaker.TryMakeFilth(butcher.Position, butcher.Map, InnerPawn.RaceProps.BloodDef, InnerPawn.LabelIndefinite() );

        //Thought/tale for butchering humanlike
        if( InnerPawn.RaceProps.Humanlike )
        {
            Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf.ButcheredHuman, new SignalArgs(butcher.Named(HistoryEventArgsNames.Doer), InnerPawn.Named(HistoryEventArgsNames.Victim))));
            TaleRecorder.RecordTale(TaleDefOf.ButcheredHumanlikeCorpse, butcher);
        }
    }


    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Values.Look( ref timeOfDeath, "timeOfDeath" );
        Scribe_Values.Look(ref vanishAfterTimestamp, "vanishAfterTimestamp");
        Scribe_Values.Look(ref everBuriedInSarcophagus, "everBuriedInSarcophagus");
        Scribe_Deep.Look( ref operationsBillStack, "operationsBillStack", this );
        Scribe_Deep.Look( ref innerContainer, "innerContainer", this );
    }

    public void Strip(bool notifyFaction = true)
    {
        InnerPawn.Strip(notifyFaction);
    }

    public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
    {
        // Don't call base - we don't want to render anything else here.
        
        InnerPawn.DynamicDrawPhaseAt(phase, drawLoc.WithYOffset(InnerPawn.Drawer.SeededYOffset));
    }

    public Thought_Memory GiveObservedThought(Pawn observer) => null;
    
    public HistoryEventDef GiveObservedHistoryEvent(Pawn observer)
    {
        //Non-humanlike corpses never give thoughts
        if( !InnerPawn.RaceProps.Humanlike )
            return null;

        if (InnerPawn.health.killedByRitual && (Find.TickManager.TicksGame - timeOfDeath) < DontCauseObservedCorpseThoughtAfterRitualExecutionTicks)
            return null;

        var storingBuilding = this.StoringThing();
        if( storingBuilding == null )
        {
            //Laying on the ground
            if( this.IsNotFresh() )
                return HistoryEventDefOf.ObservedLayingRottingCorpse;
            else
                return HistoryEventDefOf.ObservedLayingCorpse;
        }
        
        return null;
    }

    public override string GetInspectString()
    {
        var sb = new StringBuilder();

        if( InnerPawn.Faction != null && !InnerPawn.Faction.Hidden)
            sb.AppendLineTagged("Faction".Translate() + ": " + InnerPawn.Faction.NameColored);

        sb.AppendLine("DeadTime".Translate(Age.ToStringTicksToPeriodVague(vagueMax: false)) );

        var percentMissing = 1f - InnerPawn.health.hediffSet.GetCoverageOfNotMissingNaturalParts(InnerPawn.RaceProps.body.corePart);
        
        if (percentMissing >= 0.01f)
        {
            sb.AppendLine("CorpsePercentMissing".Translate() + ": " + percentMissing.ToStringPercent());
        }
        
        var deathRefusal = InnerPawn.health.hediffSet.GetFirstHediff<Hediff_DeathRefusal>();
        if (deathRefusal != null && deathRefusal.InProgress)
            sb.AppendLine("SelfResurrecting".Translate());

        sb.AppendLine(base.GetInspectString());
        return sb.ToString().TrimEndNewlines();
    }

    public override IEnumerable<StatDrawEntry> SpecialDisplayStats()
    {
        foreach( var s in base.SpecialDisplayStats() )
        {
            yield return s;
        }

        yield return new StatDrawEntry(StatCategoryDefOf.Basics, "BodySize".Translate(), InnerPawn.BodySize.ToString("F2"), "Stat_Race_BodySize_Desc".Translate(), StatDisplayOrder.Race_BodySize);

        if( this.GetRotStage() == RotStage.Fresh )
        {
            var meatAmount = StatDefOf.MeatAmount;
            yield return new StatDrawEntry(meatAmount.category, meatAmount, InnerPawn.GetStatValue(meatAmount), StatRequest.For(InnerPawn));

            var leatherAmount = StatDefOf.LeatherAmount;
            yield return new StatDrawEntry(leatherAmount.category, leatherAmount, InnerPawn.GetStatValue(leatherAmount), StatRequest.For(InnerPawn));
        }

        if(ModsConfig.BiotechActive && InnerPawn.RaceProps.IsMechanoid)
            yield return new StatDrawEntry(StatCategoryDefOf.Mechanoid, "MechWeightClass".Translate(), InnerPawn.RaceProps.mechWeightClass.ToStringHuman().CapitalizeFirst(), "MechWeightClassExplanation".Translate(), StatDisplayOrder.Race_Mechanoids_WeightClass);
    }

    public void RotStageChanged()
    {
        // Update wounds
        InnerPawn.Drawer.renderer.SetAllGraphicsDirty();
        InnerPawn.Drawer.renderer.WoundOverlays.ClearCache();
        NotifyColonistBar();
    }

    public BodyPartRecord GetBestBodyPartToEat(float nutritionWanted)
    {
        var candidates = InnerPawn.health.hediffSet.GetNotMissingParts()
            .Where(x => x.depth == BodyPartDepth.Outside && FoodUtility.GetBodyPartNutrition(this, x) > 0.001f);

        if( !candidates.Any() )
            return null;

        // get part which nutrition is the closest to what we want
        return candidates.MinBy(x => Mathf.Abs(FoodUtility.GetBodyPartNutrition(this, x) - nutritionWanted));
    }

    private void NotifyColonistBar()
    {
        if( InnerPawn.Faction == Faction.OfPlayer && Current.ProgramState == ProgramState.Playing )
            Find.ColonistBar.MarkColonistsDirty();
    }

    public void Notify_BillDeleted(Bill bill)
    {
    }

    public override IEnumerable<Gizmo> GetGizmos()
    {
        foreach (var g in base.GetGizmos())
        {
            yield return g;
        }

        if (InnerPawn.HasShowGizmosOnCorpseHediff)
        {
            foreach (var innerGizmo in InnerPawn.GetGizmos())
            {
                yield return innerGizmo;
            }
        }
        
        if (DebugSettings.ShowDevGizmos)
        {
            yield return new Command_Action()
            {
                defaultLabel = "DEV: Resurrect",
                action = () => ResurrectionUtility.TryResurrect(InnerPawn)
            };
            if (ModsConfig.AnomalyActive)
            {
                yield return new Command_Action()
                {
                    defaultLabel = "DEV: Resurrect as shambler",
                    action = () => MutantUtility.ResurrectAsShambler(InnerPawn),
                };
            }
        }
    }
}
}
