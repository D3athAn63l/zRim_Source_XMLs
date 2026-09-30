using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Verse;


namespace RimWorld{
public class Spark : Projectile
{
	protected override void Impact(Thing hitThing, bool blockedByShield = false)
	{
		var map = Map; // before Impact!

        base.Impact(hitThing, blockedByShield);

        var instigator = launcher;
        if (launcher is Fire fire)
            instigator = fire.instigator;

		FireUtility.TryStartFireIn(Position, map, Fire.MinFireSize, instigator);
	}	
}}
