using System;
using RimWorld;
using UnityEngine;

namespace Verse.AI
{
    
public static class Toils_Combat
{
	public static Toil TrySetJobToUseAttackVerb(TargetIndex targetInd)
	{
		var toil = ToilMaker.MakeToil();
		toil.initAction = ()=>
			{
				var actor = toil.actor;
				var curJob = actor.jobs.curJob;
                var allowManualCastWeapons = !actor.IsColonist;

				var verb = actor.TryGetAttackVerb(curJob.GetTarget(targetInd).Thing, allowManualCastWeapons);

				if( verb == null )
				{
					actor.jobs.EndCurrentJob( JobCondition.Incompletable);
					return;
				}

				curJob.verbToUse = verb;
			};
		return toil;
	}
	

	public static Toil GotoCastPosition( TargetIndex targetInd, TargetIndex castPositionInd = TargetIndex.None, bool closeIfDowned = false, float maxRangeFactor = 1f )
	{
		var toil = ToilMaker.MakeToil();
		toil.initAction = ()=>
			{
				var actor = toil.actor;
				var curJob = actor.jobs.curJob;
				var targThing = curJob.GetTarget(targetInd).Thing;
				var targPawn = targThing as Pawn;

                if( actor == targThing ) // we are already there, as in the case of a self-cast verb
                {
                    actor.pather.StopDead();
                    actor.jobs.curDriver.ReadyForNextToil();
                    return;
                }

                if( targThing == null ) // target is a map location not a thing, don't attempt to get closer
                {
                    actor.pather.StopDead();
                    actor.jobs.curDriver.ReadyForNextToil();
                    return;
                }

				//We get closer if the target is downed and we can
				var req = new CastPositionRequest();
				req.caster = toil.actor;
				req.target = targThing;
				req.verb = curJob.verbToUse;
                req.maxRangeFromTarget = (!closeIfDowned||targPawn==null||!targPawn.Downed)
					? Mathf.Max( curJob.verbToUse.verbProps.range * maxRangeFactor, ShootTuning.MeleeRange )
					: Mathf.Min( curJob.verbToUse.verbProps.range, targPawn.RaceProps.executionRange );
				req.wantCoverFromTarget = false;

                if (castPositionInd != TargetIndex.None)
                    req.preferredCastPosition = curJob.GetTarget(castPositionInd).Cell;

				IntVec3 dest;
				if( !CastPositionFinder.TryFindCastPosition( req, out dest ) )
				{
					toil.actor.jobs.EndCurrentJob( JobCondition.Incompletable );
					return;
				}

				toil.actor.pather.StartPath( dest, PathEndMode.OnCell );

				actor.Map.pawnDestinationReservationManager.Reserve( actor, curJob, dest );
			};
		toil.FailOnDespawnedOrNull( targetInd );
		toil.defaultCompleteMode = ToilCompleteMode.PatherArrival;

		return toil;
	}
    
    public static Toil CastVerb(TargetIndex targetInd, bool canHitNonTargetPawns = true)
    {
        return CastVerb(targetInd, TargetIndex.None, canHitNonTargetPawns);
    }
    
	public static Toil CastVerb(TargetIndex targetInd, TargetIndex destInd, bool canHitNonTargetPawns = true)
	{
		var toil = ToilMaker.MakeToil();
		toil.initAction = ()=>
			{
                var target = toil.actor.jobs.curJob.GetTarget(targetInd);
                var dest = destInd != TargetIndex.None ? toil.actor.jobs.curJob.GetTarget(destInd) : LocalTargetInfo.Invalid;
				toil.actor.jobs.curJob.verbToUse.TryStartCastOn(target, dest, canHitNonTargetPawns: canHitNonTargetPawns, preventFriendlyFire: toil.actor.jobs.curJob.preventFriendlyFire);
			};
		toil.defaultCompleteMode = ToilCompleteMode.FinishedBusy;
        toil.activeSkill = () => GetActiveSkillForToil(toil);
		return toil;
	}

    public static SkillDef GetActiveSkillForToil(Toil toil)
    {
        var verb = toil.actor.jobs.curJob.verbToUse;
        if (verb != null && verb.EquipmentSource != null)
        {
            if (verb.EquipmentSource.def.IsMeleeWeapon)
                return SkillDefOf.Melee;
            if (verb.EquipmentSource.def.IsRangedWeapon)
                return SkillDefOf.Shooting;
        }

        return null;
    }

    public static Toil FollowAndMeleeAttack(TargetIndex targetInd, Action hitAction)
    {
        return FollowAndMeleeAttack(targetInd, TargetIndex.None, hitAction);
    }
    public static Toil FollowAndMeleeAttack(TargetIndex targetInd, TargetIndex standPositionInd, Action hitAction)
	{
		// Follow and attack victim
		var followAndAttack = ToilMaker.MakeToil();
        
		followAndAttack.tickIntervalAction = (delta) =>
			{
				var actor = followAndAttack.actor;
				var curJob = actor.jobs.curJob;
				var driver = actor.jobs.curDriver;
                var target = curJob.GetTarget(targetInd);
                var victim = target.Thing;
				var victimPawn = victim as Pawn;
                var checkReachable = actor.IsHashIntervalTick(GenTicks.TickRareInterval, delta);

				if (!victim.Spawned || 
                    (victimPawn != null && victimPawn.IsPsychologicallyInvisible()) ||
                    (checkReachable && !actor.CanReach(victim, PathEndMode.Touch, Danger.Deadly, canBashDoors: false, canBashFences: false)))
				{
					driver.ReadyForNextToil();
					return;
				}

                var pathDest = target;
                var peMode = PathEndMode.Touch;
                if( standPositionInd != TargetIndex.None ) // use requested stand position, if supplied
                {
                    var standPosition = curJob.GetTarget(standPositionInd);
                    if( standPosition.IsValid )
                    {
                        pathDest = standPosition;
                        peMode = PathEndMode.OnCell;
                    }
                }
                
				if (pathDest != actor.pather.Destination || (!actor.pather.Moving && !actor.CanReachImmediate(target, PathEndMode.Touch)))
                {
                    if (actor.CurJob.ensureReachable && !actor.CanReach(target, peMode, Danger.Deadly))
                    {
                        driver.ReadyForNextToil();
                        return;
                    }
                    
                    actor.pather.StartPath(pathDest, peMode);
                }
				else if (actor.CanReachImmediate(target, PathEndMode.Touch))
                {
                    if (victimPawn != null)
                    {
                        var threatWhileDowned = victimPawn.IsMutant && victimPawn.mutant.Def.canAttackWhileCrawling && !victimPawn.ThreatDisabled(null);
						//Do not attack downed people unless the job specifies to do so or they are a threat while downed
                        if( victimPawn.Downed && !threatWhileDowned && !curJob.killIncappedTarget )
						{
							driver.ReadyForNextToil();
							return;
						}
                    }

					//Try to hit them
					hitAction();
				}
            };
        followAndAttack.activeSkill = () => SkillDefOf.Melee;
		followAndAttack.defaultCompleteMode = ToilCompleteMode.Never;
		return followAndAttack;
	}
}}



