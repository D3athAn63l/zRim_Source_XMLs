using System.Collections.Generic;
using Verse.Sound;
using RimWorld;

namespace Verse.AI
{

public class JobDriver_Equip : JobDriver
{
    private const TargetIndex EquippableIndex = TargetIndex.A;
    private const TargetIndex OutfitStandIndex = TargetIndex.B;
    
    private Thing Target => job.GetTarget(TargetIndex.A).Thing;
    private bool TargetIsOnOutfitStand => Target is { Spawned: false, ParentHolder: Building_OutfitStand };
    
    private Building_OutfitStand OutfitStand => (Building_OutfitStand) job.GetTarget(OutfitStandIndex).Thing;

    public override void Notify_Starting()
    {
        base.Notify_Starting();
        
        if (TargetIsOnOutfitStand)
            job.targetB = (Building_OutfitStand) Target.ParentHolder;
    }

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        // Pawn might have been queued to equip two bond weapons
        if (EquipmentUtility.AlreadyBondedToWeapon(Target, pawn))
            return false;

        int maxPawns = 1, stackCount = ReservationManager.StackCount_All;
        var targetOnStand = TargetIsOnOutfitStand;
        
        if (job.targetA.HasThing && job.targetA.Thing.Spawned && job.targetA.Thing.def.IsIngestible)
        {
            // Special case for ingestibles, beer for example can be equipped
            // In this case we need to only register one item of the stack with the max pawn count used for all ingestibles.
            maxPawns = Toils_Ingest.MaxPawnReservations;
            stackCount = 1;
        }

        if (TargetIsOnOutfitStand)
            return pawn.Reserve((Building_OutfitStand) job.targetA.Thing.ParentHolder, job, maxPawns, stackCount, errorOnFailed: errorOnFailed);

        return pawn.Reserve(job.targetA, job, maxPawns, stackCount, errorOnFailed: errorOnFailed);
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDestroyedOrNull(EquippableIndex);
        this.FailOnBurningImmobile(EquippableIndex);

        //Clear dropped weapon
        yield return Toils_General.Do(() => pawn.mindState.droppedWeapon = null);
        
        var targetOnStand = TargetIsOnOutfitStand;

        //Goto equipment
        {
            var goToToil = Toils_Goto.GotoThing(targetOnStand ? OutfitStandIndex : EquippableIndex, PathEndMode.ClosestTouch);

            //Duels allow picking up forbidden weapons.
            if (job.ignoreForbidden)
                yield return goToToil.FailOnDespawnedOrNull(targetOnStand ? OutfitStandIndex : EquippableIndex);
            else
                yield return goToToil.FailOnDespawnedNullOrForbidden(targetOnStand ? OutfitStandIndex : EquippableIndex);
        }
        
        //Take equipment
        {
            Toil takeEquipment = ToilMaker.MakeToil();
            if (targetOnStand)
            {   
                takeEquipment.initAction = () =>
                {
                    var stand = OutfitStand;
                    var eq = stand.HeldWeapon as ThingWithComps;
                    if (eq == null || eq != Target)
                    {
                        EndJobWith(JobCondition.Errored);
                        return;
                    }
                    
                    //Stands can't have ingestibles so we only copy the logic for weapons
                    //Also the equippable is already despawned
                    if(!stand.RemoveHeldWeapon(eq)) 
                    {
                        EndJobWith(JobCondition.Errored);
                        return;
                    }
                    
                    pawn.equipment.MakeRoomFor(eq);
                    pawn.equipment.AddEquipment(eq);
                    
                    eq.def.soundInteract?.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                };
            }
            else
            {
                takeEquipment.initAction = () =>
                {
                    ThingWithComps eq = ((ThingWithComps)job.targetA.Thing);
                    ThingWithComps toEquip = null;

                    if (eq.def.stackLimit > 1 && eq.stackCount > 1)
                        toEquip = (ThingWithComps)eq.SplitOff(1);
                    else
                    {
                        toEquip = eq;
                        toEquip.DeSpawn();
                    }

                    pawn.equipment.MakeRoomFor(toEquip);
                    pawn.equipment.AddEquipment(toEquip);

                    eq.def.soundInteract?.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                };
            }

            takeEquipment.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return takeEquipment;
        }
    }
}

}