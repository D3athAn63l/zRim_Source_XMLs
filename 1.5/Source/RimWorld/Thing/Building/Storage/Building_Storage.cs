using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace RimWorld
{

//Note: "Storage" here means a cell-based storage (e.g. a shelf), it's not a container like graves where the item disappears.
//Maybe we should rename it to Building_CellStorage and add a Building_Storage as a base class for all haul destinations like shelves and graves?
[StaticConstructorOnStartup]
public class Building_Storage : Building, ISlotGroupParent, IStorageGroupMember, IHaulEnroute
{
    //Working vars
    public StorageSettings settings;
    public StorageGroup  storageGroup;
    public string label;

    //Working vars - unsaved
    public SlotGroup        slotGroup;
    private List<IntVec3>   cachedOccupiedCells = null;

    public Building_Storage()
    {
        slotGroup = new SlotGroup(this);
    }

    //=======================================================================
    //====================== IStorageGroupMember interface===================
    //=======================================================================

    StorageGroup IStorageGroupMember.Group
    {
        get => storageGroup;
        set => storageGroup = value;
    }
    bool IStorageGroupMember.DrawConnectionOverlay => Spawned;
    Map IStorageGroupMember.Map => MapHeld;
    string IStorageGroupMember.StorageGroupTag => def.building.storageGroupTag;
    StorageSettings IStorageGroupMember.StoreSettings => GetStoreSettings();
    StorageSettings IStorageGroupMember.ParentStoreSettings => GetParentStoreSettings();
    StorageSettings IStorageGroupMember.ThingStoreSettings => settings; //Our settings, not parent or storage group.
    bool IStorageGroupMember.DrawStorageTab => true;
    bool IStorageGroupMember.ShowRenameButton => Faction == Faction.OfPlayer;

    //=======================================================================
    //========================== SlotGroupParent interface===================
    //=======================================================================

    public bool StorageTabVisible => true;
    public bool IgnoreStoredThingsBeauty => def.building.ignoreStoredThingsBeauty;
    public SlotGroup GetSlotGroup() => slotGroup;

    public virtual void Notify_ReceivedThing(Thing newItem)
    {
        if( Faction == Faction.OfPlayer && newItem.def.storedConceptLearnOpportunity != null )
            LessonAutoActivator.TeachOpportunity(newItem.def.storedConceptLearnOpportunity, OpportunityType.GoodToKnow);
    }

    public virtual void Notify_LostThing(Thing newItem){/*Nothing by default*/}

    public virtual IEnumerable<IntVec3> AllSlotCells()
    {
        if (!Spawned)
            yield break;

        foreach( IntVec3 c in GenAdj.CellsOccupiedBy(this) )
        {
            yield return c;
        }
    }

    public List<IntVec3> AllSlotCellsList()
    {
        return cachedOccupiedCells ?? (cachedOccupiedCells = AllSlotCells().ToList());
    }

    public StorageSettings GetStoreSettings()
    {
        if (storageGroup != null)
            return storageGroup.GetStoreSettings();
        return settings;
    }

    public StorageSettings GetParentStoreSettings()
    {
        var parentSettings = def.building.fixedStorageSettings;
        if (parentSettings != null)
            return parentSettings;

        // if no given fixed config, only allow storable things in storage buildings by default
        return StorageSettings.EverStorableFixedSettings();
    }

    public void Notify_SettingsChanged()
    {
        if (Spawned && slotGroup != null)
            Map.listerHaulables.Notify_SlotGroupChanged(slotGroup);
    }

    public string SlotYielderLabel() => LabelCap; // string.IsNullOrEmpty(label) ? LabelCap : label;
    
    public string GroupingLabel => def.building.groupingLabel;

    public int GroupingOrder => def.building.groupingOrder;

    public bool Accepts(Thing t)
    {
        return GetStoreSettings().AllowedToAccept(t);
    }

    public int SpaceRemainingFor(ThingDef _)
    {
        return slotGroup.HeldThingsCount - def.building.maxItemsInCell * def.Size.Area;
    }
    
    //=======================================================================
    //============================== Other stuff ============================
    //=======================================================================

    public override void PostMake()
    {
        base.PostMake();
        
        settings = new StorageSettings(this);
        
        if (def.building.defaultStorageSettings != null)
            settings.CopyFrom(def.building.defaultStorageSettings);
    }

    public override void SpawnSetup(Map map, bool respawningAfterLoad)
    {
        cachedOccupiedCells = null; // invalidate cache

        base.SpawnSetup(map, respawningAfterLoad);

        if (storageGroup != null && map != storageGroup.Map)
        {
            var oldSettings = storageGroup.GetStoreSettings();
            storageGroup.RemoveMember(this);
            storageGroup = null;
            settings.CopyFrom(oldSettings);
        }
    }

    public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
    {
        base.DeSpawn(mode);
        cachedOccupiedCells = null; // invalidate cache
    }

    public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
    {
        base.Destroy(mode);

        if (storageGroup != null)
        {
            storageGroup?.RemoveMember(this);
            storageGroup = null;
        }

        BillUtility.Notify_ISlotGroupRemoved(slotGroup);
    }

    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Deep.Look(ref settings, "settings", this);
        Scribe_References.Look(ref storageGroup, "storageGroup");
        Scribe_Values.Look(ref label, "label");
    }

    public override void DrawExtraSelectionOverlays()
    {
        base.DrawExtraSelectionOverlays();
        StorageGroupUtility.DrawSelectionOverlaysFor(this);
    }

    private static StringBuilder sb = new StringBuilder();
    public override string GetInspectString()
    {
        sb.Clear();
        sb.Append(base.GetInspectString());

        if (Spawned)
        {
            if (storageGroup != null)
            {
                sb.AppendLineIfNotEmpty();
                sb.Append($"{"StorageGroupLabel".Translate()}: {storageGroup.RenamableLabel.CapitalizeFirst()} ");
                if (storageGroup.MemberCount > 1)
                    sb.Append($"({"NumBuildings".Translate(storageGroup.MemberCount)})");
                else
                    sb.Append($"({"OneBuilding".Translate()})");
            }

            if (slotGroup.HeldThings.Any())
            {
                sb.AppendLineIfNotEmpty();
                sb.Append("StoresThings".Translate());
                sb.Append(": ");
                sb.Append(slotGroup.HeldThings.Select(x => x.LabelShortCap).Distinct().ToCommaList());
                sb.Append(".");
            }
        }

        return sb.ToString();
    }

    public override IEnumerable<Gizmo> GetGizmos()
    {
        foreach( var g in base.GetGizmos() )
        {
            yield return g;
        }

        foreach( var g in StorageSettingsClipboard.CopyPasteGizmosFor(GetStoreSettings()) )
        {
            yield return g;
        }

        if (StorageTabVisible && MapHeld != null)
        {
            foreach (var g in StorageGroupUtility.StorageGroupMemberGizmos(this))
            {
                yield return g;
            }

            if( Find.Selector.NumSelected == 1 )
            {
                //Stored items
                foreach ( var t in slotGroup.HeldThings )
                {
                    yield return ContainingSelectionUtility.CreateSelectStorageGizmo("CommandSelectStoredThing".Translate(t), ("CommandSelectStoredThingDesc".Translate() + "\n\n" + t.LabelCap.Colorize(ColoredText.TipSectionTitleColor) + "\n\n" + t.GetInspectString()).Resolve(), t, t, false);
                }
            }
        }
    }
}

}