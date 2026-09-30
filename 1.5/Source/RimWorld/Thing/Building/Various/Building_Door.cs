using UnityEngine;
using System.Collections.Generic;
using Verse;
using Verse.Sound;
using Verse.AI;
using Verse.AI.Group;

namespace RimWorld{
public class Building_Door : Building
{
	//Links
	public CompPowerTrader	powerComp;

	//Working vars - saved
    protected bool 			openInt = false;
    protected bool			holdOpenInt = false;		//Player has configured door to be/stay open
	private int				lastFriendlyTouchTick = -9999;
    protected int 			ticksUntilClose = 0;
    private Pawn            approachingPawn;
    private int             ticksOpen;

	//Working vars - unsaved
	protected int 			ticksSinceOpen = 0;
	private bool			freePassageWhenClearedReachabilityCache;

	//Constants
	private const float		OpenTicks = 45;
	private const int		CloseDelayTicks = 110;
	private const int		ApproachCloseDelayTicks = 300; //For doors which open before the pawn even arrives, extra long in case pawn is very slow; don't want door to close before they arrive
	private const int		MaxTicksSinceFriendlyTouchToAutoClose = 120;
	private const float		PowerOffDoorOpenSpeedFactor = 0.25f;
	private const float		VisualDoorOffsetStart = 0.0f;
	private const float		VisualDoorOffsetEnd = 0.45f;
    private const float     NotifyFogGridDoorOpenPct = 0.4f;
    private const int       TicksOpenToBreach = GenTicks.TicksPerRealSecond * 10;

	//Properties
    private int CloseDelayAdjusted => Mathf.FloorToInt(CloseDelayTicks * (DoorPowerOn ? def.building.poweredDoorCloseSpeedFactor : def.building.unpoweredDoorCloseSpeedFactor));
    private int WillCloseSoonThreshold => CloseDelayAdjusted + 1;
    
	public bool Open => openInt;
    public bool HoldOpen => holdOpenInt;

    public bool FreePassage
	{
		get
		{
			//Not open - never free passage
			if( !openInt )
				return false;

			return holdOpenInt || !WillCloseSoon;
		}
	}
	public int TicksTillFullyOpened
	{
		get
		{
			int ticks = TicksToOpenNow - this.ticksSinceOpen;
			if (ticks < 0)
				ticks = 0;

			return ticks;
		}
	}
	public bool WillCloseSoon
	{
		get
		{
			if( !Spawned )
				return true;

			//It's already closed
			if( !openInt )
				return true;

			//It's held open -> so it won't be closed soon
			if( holdOpenInt )
				return false;

			//Will close soon
			if( ticksUntilClose > 0 && ticksUntilClose <= WillCloseSoonThreshold && !BlockedOpenMomentary )
				return true;

			//Will close automatically soon
			if( CanTryCloseAutomatically && !BlockedOpenMomentary )
				return true;

			//Check if there is any non-hostile non-downed pawn passing through, he will close the door
            foreach (var cell in this.OccupiedRect())
            {
                for( int i = 0; i < 5; i++ )
			    {
			    	var c = cell + GenAdj.CardinalDirectionsAndInside[i];

			    	if( !c.InBounds(Map) )
			    		continue;

			    	var things = c.GetThingList(Map);
			    	for( int j = 0; j < things.Count; j++ )
			    	{
			    		var p = things[j] as Pawn;

                        if( p == null || p.HostileTo(this) || p.Downed )
			    			continue;

			    		if( p.Position == cell || (p.pather.Moving && p.pather.nextCell == cell) )
			    			return true;
			    	}
			    }
            }

			return false;
		}
	}
    public bool ContainmentBreached => openInt && ticksOpen >= TicksOpenToBreach;
	public bool BlockedOpenMomentary
	{
		get
        {
            if (StuckOpen)
                return true;

            foreach (var c in this.OccupiedRect())
            {
                var thingList = c.GetThingList(Map);
			    for( int i=0; i<thingList.Count; i++ )
			    {
			    	var t = thingList[i];
			    	if( t.def.category == ThingCategory.Item || t.def.category == ThingCategory.Pawn)
			    		return true;
			    }
            }

			return false;
		}
	}

    protected bool StuckOpen
    {
        get
        {
            if (!Spawned || def.size == IntVec2.One)
                return false;

            //Check sides of door, ensure walls surround us.
            foreach (var c in DoorUtility.WallRequirementCells(def, Position, Rotation))
            {
                if (!DoorUtility.EncapsulatingWallAt(c, Map))
                    return true;
            }

            return false;
        }
    }
    public bool DoorPowerOn => powerComp != null && powerComp.PowerOn;
    public bool SlowsPawns => !DoorPowerOn || TicksToOpenNow > 20;
	public int TicksToOpenNow
	{
		get
		{
			float ticks = OpenTicks / this.GetStatValue(StatDefOf.DoorOpenSpeed);

			if( DoorPowerOn )
				ticks *= PowerOffDoorOpenSpeedFactor * def.building.poweredDoorOpenSpeedFactor;
            else
                ticks *= def.building.unpoweredDoorOpenSpeedFactor;

            return Mathf.RoundToInt(ticks);
		}
	}
    private bool CanTryCloseAutomatically => FriendlyTouchedRecently && !HoldOpen;
    private bool FriendlyTouchedRecently => Find.TickManager.TicksGame < lastFriendlyTouchTick + MaxTicksSinceFriendlyTouchToAutoClose;
    public override bool FireBulwark => !Open && base.FireBulwark;
    protected float OpenPct => Mathf.Clamp01(ticksSinceOpen / (float)TicksToOpenNow);	//Needs clamp for after game load	

	public override void PostMake()
	{
		base.PostMake();

		powerComp = GetComp<CompPowerTrader>();
	}

	public override void SpawnSetup(Map map, bool respawningAfterLoad)
	{
		base.SpawnSetup(map, respawningAfterLoad);

		powerComp = GetComp<CompPowerTrader>();
		ClearReachabilityCache(map);
		
		// Open the door if we're spawning on top of something
		if( BlockedOpenMomentary )
			DoorOpen();
	}

	public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
	{
		var map = Map;

		base.DeSpawn(mode);

		ClearReachabilityCache(map);
	}

	public override void ExposeData()
	{
		base.ExposeData();

		Scribe_Values.Look(ref openInt, "open", defaultValue: false);
		Scribe_Values.Look(ref holdOpenInt, "holdOpen", defaultValue: false);
		Scribe_Values.Look(ref lastFriendlyTouchTick, "lastFriendlyTouchTick" );
        Scribe_Values.Look(ref ticksUntilClose, "ticksUntilClose");
        Scribe_References.Look(ref approachingPawn, "approachingPawn");
        Scribe_Values.Look(ref ticksOpen, "ticksOpen");

		if( Scribe.mode == LoadSaveMode.LoadingVars && openInt)
			ticksSinceOpen = TicksToOpenNow;
	}

	public override void SetFaction(Faction newFaction, Pawn recruiter = null)
	{
		base.SetFaction(newFaction, recruiter);

		if( Spawned )
			ClearReachabilityCache(Map);
	}

	public override void Tick()
	{
		base.Tick();

		//Check if we should clear the reachability cache
		if( FreePassage != freePassageWhenClearedReachabilityCache )
			ClearReachabilityCache(Map);

		if( !openInt )
		{
			//Slide door closed
			if( ticksSinceOpen > 0 )
				ticksSinceOpen--;

            ticksOpen = 0;

			//Equalize temperatures
			if (this.IsHashIntervalTick(TemperatureTuning.Door_TempEqualizeIntervalClosed))
				GenTemperature.EqualizeTemperaturesThroughBuilding(this, TemperatureTuning.Door_TempEqualizeRate, twoWay: false);
		}
		else if( openInt )
		{
			//Slide door open
			if( ticksSinceOpen < TicksToOpenNow )
				ticksSinceOpen++;

            ticksOpen++;

			//Check friendly touched
            foreach (var c in this.OccupiedRect())
            {
                var things = c.GetThingList(Map);
                for( int i = 0; i < things.Count; i++ )
			    {
			    	if( things[i] is Pawn p )
			    		CheckFriendlyTouched(p);
			    }
            }

			//Count down to closing
			if( ticksUntilClose > 0 )
			{
				//Pawn moving
                foreach (var c in this.OccupiedRect())
                {
                    if( Map.thingGrid.CellContains( c, ThingCategory.Pawn ) )
                    {
                        //This is important for doors which !SlowsPawns, this will override their default long approach close delay when the pawn actually enters the cell,
                        //note that we do this only if ticksUntilClose is already > 0
                        ticksUntilClose = CloseDelayAdjusted;
                        break;
                    }
                }

				ticksUntilClose--;
				if( ticksUntilClose <= 0 && !holdOpenInt )
				{
					if( !DoorTryClose() )
						ticksUntilClose = 1; //Something blocking - try next tick
				}
			}
			else
			{
				//Not assigned to close, check if we want to close automatically
				if( CanTryCloseAutomatically )
					ticksUntilClose = CloseDelayAdjusted;
			}

			//Equalize temperatures
			if( (Find.TickManager.TicksGame + thingIDNumber.HashOffset()) % TemperatureTuning.Door_TempEqualizeIntervalOpen == 0 )
				GenTemperature.EqualizeTemperaturesThroughBuilding(this, TemperatureTuning.Door_TempEqualizeRate, twoWay: false);

            // Notify fog grid when the door is ~ half open.
            if(OpenPct >= NotifyFogGridDoorOpenPct && approachingPawn != null)
            {
                Map.fogGrid.Notify_PawnEnteringDoor(this, approachingPawn);
                approachingPawn = null;
            }
		}
	}

	public void CheckFriendlyTouched(Pawn p)
	{
		if( !p.HostileTo(this) && PawnCanOpen(p) )
			lastFriendlyTouchTick = Find.TickManager.TicksGame;
	}	
    
	public void Notify_PawnApproaching( Pawn p, float moveCost )
	{
		CheckFriendlyTouched(p);
		bool canOpen = PawnCanOpen(p);
		
		// If we can open the door or it is already opened we notify the fog grid to reveal the area behind it
		if (canOpen || Open)
			approachingPawn = p;
		
			//Open automatically before pawn arrives
		if( canOpen && !SlowsPawns )
		{
			//Make sure it stays open before the pawn reaches it
			DoorOpen(Mathf.Max(ApproachCloseDelayTicks, Mathf.CeilToInt(moveCost) + 1));
		}
	}

	/// <summary>
	/// Returns whether p can physically pass through the door without bashing.
	/// </summary>
	public bool CanPhysicallyPass( Pawn p )
	{
		return FreePassage
			|| PawnCanOpen(p)
			|| (Open && p.HostileTo(this)); // hostile pawns can always pass if the door is open
	}

	/// <summary>
	/// Returns whether p can open the door without bashing.
	/// </summary>
	public virtual bool PawnCanOpen( Pawn p )
	{
        if (ModsConfig.AnomalyActive && p.IsMutant && !p.mutant.Def.canOpenDoors)
            return false;

        if (Map?.Parent != null && Map.Parent.doorsAlwaysOpenForPlayerPawns && p.Faction == Faction.OfPlayer)
            return true;

		var lord = p.GetLord();
		if (lord?.LordJob != null && lord.LordJob.CanOpenAnyDoor(p))
			return true;

		//This is to avoid situations where a wild man is stuck inside the colony forever right after having a mental break
		if( WildManUtility.WildManShouldReachOutsideNow(p) )
			return true;

        // fenceblocked animals can't use doors
        if( p.RaceProps.FenceBlocked && !(
                def.building.roamerCanOpen // except if the door allows it
                || p.roping.IsRopedByPawn && PawnCanOpen(p.roping.RopedByPawn)) // or except if they are roped we let them follow the pawn that roped them
        )
            return false;

        //Door has no faction?
        if( Faction == null )
            return p.RaceProps.canOpenFactionlessDoors;

		if( p.guest != null && p.guest.Released )
			return true;
        
        if (ModsConfig.AnomalyActive)
        {
            // Revenants can use any door
            if (p.kindDef == PawnKindDefOf.Revenant)
                return true;

            if (p.IsMutant && p.mutant.Def.canOpenAnyDoor)
                return true;
        }

		return GenAI.MachinesLike(Faction, p);
	}
	
	public override bool BlocksPawn( Pawn p )
	{
		if( openInt )
			return false;
		else
			return !PawnCanOpen(p);
	}

	protected virtual void DoorOpen(int ticksToClose = CloseDelayTicks)
	{
		if( openInt )
			ticksUntilClose = ticksToClose;
		else //We need to add TicksToOpenNow because this is how long the pawn will wait before the door opens (by using a busy stance)
			ticksUntilClose = TicksToOpenNow + ticksToClose;

		if( !openInt )
		{
			openInt = true;

			CheckClearReachabilityCacheBecauseOpenedOrClosed();

			if( DoorPowerOn )
				def.building.soundDoorOpenPowered.PlayOneShot(this);
			else
				def.building.soundDoorOpenManual.PlayOneShot(this);
		}
	}


	protected bool DoorTryClose()
	{
		if( holdOpenInt || BlockedOpenMomentary )
			return false;

		openInt = false;
		
		CheckClearReachabilityCacheBecauseOpenedOrClosed();

		if( DoorPowerOn )
			def.building.soundDoorClosePowered.PlayOneShot(this);
		else
			def.building.soundDoorCloseManual.PlayOneShot(this);

		return true;
	}

		
	public void StartManualOpenBy( Pawn opener )
	{
		DoorOpen();
	}

	public void StartManualCloseBy( Pawn closer )
	{
		ticksUntilClose = CloseDelayAdjusted;
	}

    protected override void DrawAt(Vector3 drawLoc, bool flip = false)
    {
        DoorPreDraw();

        // Draw the two moving doors
        var offsetDist = VisualDoorOffsetStart + (VisualDoorOffsetEnd-VisualDoorOffsetStart) * OpenPct;	
        DrawMovers(drawLoc, offsetDist, Graphic, AltitudeLayer.DoorMoveable.AltitudeFor(), Vector3.one, Graphic.ShadowGraphic);
    }

    protected void DrawMovers(Vector3 drawPos, float offsetDist, Graphic graphic, float altitude, Vector3 drawScaleFactor, Graphic_Shadow shadowGraphic)
    {
        for (var i = 0; i < 2; i++)
        {
            //Left, then right
            Vector3 offsetNormal;
            Mesh mesh;

            if (i == 0)
            {
                offsetNormal = new Vector3(0, 0, -def.size.x);
                mesh = MeshPool.plane10;
            }
            else
            {
                offsetNormal = new Vector3(0, 0, def.size.x);
                mesh = MeshPool.plane10Flip;
            }
            
            //Work out move direction
            Rot4 openDir = Rotation;
            openDir.Rotate(RotationDirection.Clockwise);
            offsetNormal  = openDir.AsQuat * offsetNormal;

            //Position the door
            var moverPos = drawPos;
            moverPos.y = altitude;
            moverPos += offsetNormal * offsetDist;
            
            //Draw!
            Graphics.DrawMesh(mesh, Matrix4x4.TRS(moverPos, Rotation.AsQuat, new Vector3(def.size.x * drawScaleFactor.x, drawScaleFactor.y, def.size.z * drawScaleFactor.z)), graphic.MatAt(Rotation, this), 0);
            
            //Draw shadow (if defined)
            shadowGraphic?.DrawWorker(moverPos, Rotation, def, this, 0);
        }
    }

	public override IEnumerable<Gizmo> GetGizmos()
	{
		foreach( var g in base.GetGizmos() )
		{
			yield return g;
		}

		if( Faction == Faction.OfPlayer )
		{
			var ro = new Command_Toggle();
			ro.defaultLabel = "CommandToggleDoorHoldOpen".Translate();
			ro.defaultDesc = "CommandToggleDoorHoldOpenDesc".Translate();
			ro.hotKey = KeyBindingDefOf.Misc3;
			ro.icon = TexCommand.HoldOpen;
			ro.isActive = () => holdOpenInt;
			ro.toggleAction = () => holdOpenInt = !holdOpenInt;
			yield return ro;
		}
	}

	private void ClearReachabilityCache(Map map)
	{
		map.reachability.ClearCache();
		freePassageWhenClearedReachabilityCache = FreePassage;
	}

	private void CheckClearReachabilityCacheBecauseOpenedOrClosed()
	{
		if( Spawned )
			Map.reachability.ClearCacheForHostile(this);
	}

    protected void DoorPreDraw()
    {
        //Note: It's a bit odd that I'm changing game variables in Draw
        //      but this is the easiest way to make this always look right even if
        //      conditions change while the game is paused.
        if (def.size == IntVec2.One)
            Rotation = DoorUtility.DoorRotationAt(Position, Map, def.building.preferConnectingToFences);
    }

    public override string GetInspectString()
    {
        var s = base.GetInspectString();
        if (StuckOpen)
        {
            if (!s.NullOrEmpty())
                s += "\n";
            s += "DoorMustBeEnclosedByWalls".Translate().Colorize(ColorLibrary.RedReadable);
        }

        return s;
    }
}

public static class DoorsDebugDrawer
{
	public static void DrawDebug()
	{
		if( !DebugViewSettings.drawDoorsDebug )
			return;

		var visibleRect = Find.CameraDriver.CurrentViewRect;
		var buildings = Find.CurrentMap.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial);

		for( int i = 0; i < buildings.Count; i++ )
		{
			if( !visibleRect.Contains(buildings[i].Position) )
				continue;

			var door = buildings[i] as Building_Door;

			if( door != null )
			{
				Color color;

				if( door.FreePassage )
					color = new Color(0f, 1f, 0f, 0.5f);
				else
					color = new Color(1f, 0f, 0f, 0.5f);

				foreach (var c in door.OccupiedRect())
                {
                    CellRenderer.RenderCell(c, SolidColorMaterials.SimpleSolidColorMaterial(color));
                }
			}
		}
	}
}

}


