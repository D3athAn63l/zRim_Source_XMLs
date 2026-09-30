using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Verse.Sound;
using Verse;

namespace RimWorld
{

public struct BulletImpactData
{
    public IntVec3 impactPosition;
    public Bullet bullet;
    public Thing hitThing;
}

public class Bullet : Projectile
{
    public override bool AnimalsFleeImpact => true;

	protected override void Impact(Thing hitThing, bool blockedByShield = false)
	{
		var map = Map; // before Impact!
        var position = Position;

		base.Impact(hitThing, blockedByShield);
		
		var impact = new BattleLogEntry_RangedImpact(launcher, hitThing, intendedTarget.Thing, equipmentDef, def, targetCoverDef);
		Find.BattleLog.Add(impact);

        NotifyImpact(hitThing, map, position);

		if( hitThing != null )
		{
            var instigatorGuilty = !(launcher is Pawn pawn) || !pawn.Drafted;
			var dinfo = new DamageInfo(DamageDef, DamageAmount, armorPenetration: ArmorPenetration, angle: ExactRotation.eulerAngles.y, instigator: launcher, weapon: equipmentDef, intendedTarget: intendedTarget.Thing, instigatorGuilty: instigatorGuilty);
            dinfo.SetWeaponQuality(equipmentQuality);
			hitThing.TakeDamage(dinfo).AssociateWithLog(impact);

			var hitPawn = hitThing as Pawn;

            hitPawn?.stances?.stagger.Notify_BulletImpact(this);

            if (ExtraDamages != null)
			{
				foreach (var d in ExtraDamages)
				{
					if (Rand.Chance(d.chance))
					{
						var extraDinfo = new DamageInfo(d.def, d.amount, d.AdjustedArmorPenetration(), ExactRotation.eulerAngles.y, launcher, weapon: equipmentDef, intendedTarget: intendedTarget.Thing, instigatorGuilty: instigatorGuilty);
						hitThing.TakeDamage(extraDinfo).AssociateWithLog(impact);
					}
				}
			}
		}
		else
		{
            if (!blockedByShield)
            {
                SoundDefOf.BulletImpact_Ground.PlayOneShot(new TargetInfo(Position, map));

                if( Position.GetTerrain(map).takeSplashes )
                    FleckMaker.WaterSplash(ExactPosition, map, Mathf.Sqrt(DamageAmount) * FleckSplash.SizeGunfire, FleckSplash.VelocityGunfire);
                else
                    FleckMaker.Static(ExactPosition, map, FleckDefOf.ShotHit_Dirt);
            }

            if (Rand.Chance(DamageDef.igniteCellChance))
                FireUtility.TryStartFireIn(Position, map, Rand.Range(0.55f, 0.85f), launcher);
        }

        
    }

    private void NotifyImpact(Thing hitThing, Map map, IntVec3 position)
    {
        var impactNotificationData = new BulletImpactData {bullet = this, hitThing = hitThing, impactPosition = position};

        hitThing?.Notify_BulletImpactNearby(impactNotificationData);

        int cellCount = 9;
        for( int i = 0; i < cellCount; i++ )
        {
            var cell = position + GenRadial.RadialPattern[i];
            if( !cell.InBounds(map) )
                continue;

            var things = cell.GetThingList(map);
            for( int j = 0; j < things.Count; j++ )
            {
                if (things[j] != hitThing)
                    things[j].Notify_BulletImpactNearby(impactNotificationData);
            }
        }
    }
}
}