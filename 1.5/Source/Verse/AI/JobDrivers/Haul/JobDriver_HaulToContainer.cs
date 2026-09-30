using System.Collections.Generic;
using UnityEngine;
using RimWorld;


namespace Verse.AI
{
    
public class JobDriver_HaulToContainer : JobDriver
{
    // Working vars
    private Effecter graveDigEffect;
    
	// Constants
	protected const TargetIndex CarryThingIndex = TargetIndex.A;
	public const TargetIndex DestIndex = TargetIndex.B;
	protected const TargetIndex PrimaryDestIndex = TargetIndex.C;
    protected const int DiggingEffectInterval = 80;

	public Thing ThingToCarry => (Thing)job.GetTarget(CarryThingIndex);
    
    public Thing Container => (Thing)job.GetTarget(DestIndex);
    
    public ThingDef ThingDef => ThingToCarry.def;
    
    protected virtual int Duration => Container != null && Container is Building b ? b.HaulToContainerDuration(ThingToCarry) : 0;
    
    protected virtual EffecterDef WorkEffecter => null;
    
    protected virtual SoundDef WorkSustainer => null;

    public override string GetReport()
	{
		Thing hauledThing;
        
		if (pawn.CurJob == job && pawn.carryTracker.CarriedThing != null)
			hauledThing = pawn.carryTracker.CarriedThing;
		else
			hauledThing = TargetThingA;

		if (hauledThing == null || !job.targetB.HasThing)
			return "ReportHaulingUnknown".Translate();
		else
		{
			var key = job.GetTarget(DestIndex).Thing is Building_Grave ? "ReportHaulingToGrave" : "ReportHaulingTo"; // Special text for hauling to grave
			return key.Translate(hauledThing.Label, job.targetB.Thing.LabelShort.Named("DESTINATION"), hauledThing.Named("THING"));
		}
	}

	public override bool TryMakePreToilReservations(bool errorOnFailed)
	{
		if (!pawn.Reserve(job.GetTarget(CarryThingIndex), job, errorOnFailed: errorOnFailed) )
			return false;
        
        // Non-haul destinations don't automatically support multiple hauling (biosculptures, ect.)
        if (Container.Isnt<IHaulEnroute>())
        {
            if (!pawn.Reserve(job.GetTarget(DestIndex), job, errorOnFailed: errorOnFailed, stackCount: 1))
                return false;
            
            pawn.ReserveAsManyAsPossible(job.GetTargetQueue(DestIndex), job);
        }
        
        UpdateEnrouteTrackers();
        
        pawn.ReserveAsManyAsPossible(job.GetTargetQueue(CarryThingIndex), job);
		return true;
	}

    protected virtual void ModifyPrepareToil(Toil toil) { }

    private bool TryReplaceWithFrame(TargetIndex index)
    {
        var thing = GetActor().jobs.curJob.GetTarget(index).Thing;
        var edifice = thing.Position.GetEdifice(pawn.Map);
        
        if (edifice != null && thing is Blueprint_Build blueprint &&
            edifice is Frame frame && frame.BuildDef == blueprint.BuildDef)
        {
            job.SetTarget(DestIndex, frame);
            return true;
        }
        
        return false;
    }

	protected override IEnumerable<Toil> MakeNewToils()
	{
		this.FailOnDestroyedOrNull(CarryThingIndex);
		this.FailOn(() => 
        {
            var dest = GetActor().jobs.curJob.GetTarget(DestIndex).Thing;
            var primary = GetActor().jobs.curJob.GetTarget(PrimaryDestIndex).Thing;
            
            if (dest == null)
                return true;

            if (primary != null && primary.Destroyed)
            {
                // Primary target matters, but if it was deleted just continue normally.
                if (!TryReplaceWithFrame(PrimaryDestIndex))
                    job.SetTarget(PrimaryDestIndex, null);
            }
            
            // Try to recover
            if (!dest.Spawned || (dest.Destroyed && !TryReplaceWithFrame(DestIndex)))
            {
                if (job.targetQueueB.NullOrEmpty())
                    return true;
                
                if (!Toils_Haul.TryGetNextDestinationFromQueue(PrimaryDestIndex, DestIndex, ThingDef, job, pawn, out var nextTarget))
                    return true;
                
                job.targetQueueB.RemoveAll(target => target.Thing == nextTarget);
                job.targetB = nextTarget;
            }
            
			var thingOwner = Container.TryGetInnerInteractableThingOwner();
			if (thingOwner != null && !thingOwner.CanAcceptAnyOf(ThingToCarry))
				return true;
            
			// e.g. grave
            if (Container is IHaulDestination haulDestination && !haulDestination.Accepts(ThingToCarry))
				return true;
            
			return false;
		});
        this.FailOnForbidden(DestIndex);
        this.FailOn(() => EnterPortalUtility.WasLoadingCanceled(Container));
        this.FailOn(() => TransporterUtility.WasLoadingCanceled(Container));
        this.FailOn(() => CompBiosculpterPod.WasLoadingCanceled(Container));
        this.FailOn(() => Building_SubcoreScanner.WasLoadingCancelled(Container));
        
        var getToHaulTarget = Toils_Goto.GotoThing(CarryThingIndex, PathEndMode.ClosestTouch, canGotoSpawnedParent: true);
        
		var uninstallIfMinifiable = Toils_Construct.UninstallIfMinifiable(CarryThingIndex)
			.FailOnSomeonePhysicallyInteracting(CarryThingIndex)
            .FailOnDestroyedOrNull(CarryThingIndex);
        
		var startCarryingThing = Toils_Haul.StartCarryThing(CarryThingIndex, subtractNumTakenFromJobCount: true, canTakeFromInventory: true);
		
        var jumpIfAlsoCollectingNextTarget = Toils_Haul.JumpIfAlsoCollectingNextTargetInQueue(getToHaulTarget, CarryThingIndex);
		var carryToContainer = Toils_Haul.CarryHauledThingToContainer();
		
        //Jump moving to and attempting to carry our target if we're already carrying it (e.g. drafted carry of a pawn)
        yield return Toils_Jump.JumpIf(jumpIfAlsoCollectingNextTarget, () => pawn.IsCarryingThing(ThingToCarry));

        yield return getToHaulTarget;
        yield return uninstallIfMinifiable;
        yield return startCarryingThing;
        yield return jumpIfAlsoCollectingNextTarget;
        yield return carryToContainer;
        
		yield return Toils_Goto.MoveOffTargetBlueprint(DestIndex);
		
		// Prepare
		{
			var prepare = Toils_General.Wait(Duration, face: DestIndex);
			prepare.WithProgressBarToilDelay(DestIndex);

            var workEffecter = WorkEffecter;
            if (workEffecter != null)
                prepare.WithEffect(workEffecter, DestIndex);

            var workSustainer = WorkSustainer;
            if (workSustainer != null)
                prepare.PlaySustainerOrSound(workSustainer);
            
            var destThing = job.GetTarget(DestIndex).Thing;
            
            prepare.tickAction = () =>
            {
                if (pawn.IsHashIntervalTick(DiggingEffectInterval) && destThing is Building_Grave)
                {
                    if (graveDigEffect == null)
                    {
                        graveDigEffect = EffecterDefOf.BuryPawn.Spawn();
                        graveDigEffect.Trigger(destThing, destThing);
                    }
                }
                
                graveDigEffect?.EffectTick(destThing, destThing);
            };

            ModifyPrepareToil(prepare);

            yield return prepare;
		}
        
		yield return Toils_Construct.MakeSolidThingFromBlueprintIfNecessary(DestIndex, PrimaryDestIndex);
		
		yield return Toils_Haul.DepositHauledThingInContainer(DestIndex, PrimaryDestIndex);
		
		yield return Toils_Haul.JumpToCarryToNextContainerIfPossible(carryToContainer, PrimaryDestIndex);
	}
    
    private void UpdateEnrouteTrackers()
    {
        var count = job.count;
        
        TryReserveEnroute(TargetThingC, ref count);
        
        if (TargetB != TargetC)
            TryReserveEnroute(TargetThingB, ref count);
        
        if (job.targetQueueB != null)
        {
            foreach (var t in job.targetQueueB)
            {
                if (TargetC.HasThing && t == TargetThingC)
                    continue;
                
                TryReserveEnroute(t.Thing, ref count);
            }
        }
    }

    private void TryReserveEnroute(Thing thing, ref int count)
    {
        if (thing is IHaulEnroute container && !thing.DestroyedOrNull())
            UpdateTracker(container, ref count);
    }
    
    private void UpdateTracker(IHaulEnroute container, ref int count)
    {
        if (ThingToCarry.DestroyedOrNull())
            return;
        
        if (job.playerForced)
        {
            var required = container.GetSpaceRemainingWithEnroute(ThingDef);
            
            // We only interrupt other enroute pawns if they block us from contributing at all. Otherwise let them help.
            if (required == 0)
                container.Map.enrouteManager.InterruptEnroutePawns(container, pawn);
        }
        
        var space = Mathf.Min(count, container.GetSpaceRemainingWithEnroute(ThingDef));
        
        if (space > 0)
            container.Map.enrouteManager.AddEnroute(container, pawn, TargetThingA.def, space);
        
        count -= space;
    }
}

}
