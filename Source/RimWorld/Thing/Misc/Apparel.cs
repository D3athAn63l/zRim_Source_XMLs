using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using LudeonTK;

namespace RimWorld
{

public class Apparel : ThingWithComps
{
    //Working vars
    private bool wornByCorpseInt;
    private List<Ability> abilities;

    //Properties
    public Pawn Wearer => ParentHolder is Pawn_ApparelTracker apparelTracker ? apparelTracker.pawn : null;

    public bool WornByCorpse
    {
        get => wornByCorpseInt;
        set => wornByCorpseInt = value;
    }
    public string WornGraphicPath
    {
        get
        {
            if (StyleDef != null && !StyleDef.wornGraphicPath.NullOrEmpty())
                return StyleDef.wornGraphicPath;

            if (!def.apparel.wornGraphicPaths.NullOrEmpty())
                return def.apparel.wornGraphicPaths[thingIDNumber % def.apparel.wornGraphicPaths.Count];

            return def.apparel.wornGraphicPath;
        }
    }
    public override string DescriptionDetailed
    {
        get
        {
            string descr = base.DescriptionDetailed;
            if( WornByCorpse )
                descr += "\n" + "WasWornByCorpse".Translate();
            
            return descr;
        }
    }
    public override Color DrawColor
    {
        get
        {
            Color col;

            if (StyleDef != null && StyleDef.color != default)
                col = StyleDef.color;
            else
                col = base.DrawColor;

            if (WornByCorpse)
                col = PawnRenderUtility.GetRottenColor(col);

            if (Wearer is { Drawer: { renderer: { StatueColor: { } statueColor } } })
                //If our wearer is a statue, we should use the statue color
                col = statueColor;

            return col;
        }
    }
    public Color? DesiredColor
    {
        get => GetComp<CompColorable>()?.DesiredColor;
        set
        {
            var colorable = GetComp<CompColorable>();
            if (colorable != null)
                colorable.DesiredColor = value;
            else
                Log.Error("Tried setting " + nameof(Apparel) + "." + nameof(DesiredColor) + " without having " + nameof(CompColorable) + " comp!");
        }
    }

    public IEnumerable<Ability> AllAbilitiesForReading
    {
        get
        {
            if (abilities == null && def.apparel.abilities != null)
                FillOrUpdateAbilities();

            if (abilities == null)
                yield break;

            foreach (var ability in abilities)
            {
                yield return ability;
            }
        }
    }

    public override string GetInspectStringLowPriority()
    {
        string str = base.GetInspectStringLowPriority();

        if (StyleDef != null)
        {
            if (!str.NullOrEmpty())
                str += "\n";
            str += "VariantOf".Translate().CapitalizeFirst() + ": " + def.LabelCap;
        }

        if( ModsConfig.BiotechActive )
        {
            if (!str.NullOrEmpty())
                str += "\n";

            str += "WearableBy".Translate() + ": " + def.apparel.developmentalStageFilter.ToCommaList().CapitalizeFirst();
        }

        return str;
    }

    public bool PawnCanWear(Pawn pawn, bool ignoreGender = false)
    {
        if (!def.IsApparel)
            return false;

        if (!def.apparel.PawnCanWear(pawn, ignoreGender))
            return false;

        return true;
    }

    public Color GetColorIgnoringTainted()
    {
        if (StyleDef != null && StyleDef.color != default)
            return StyleDef.color;
        
        //Specifically *base* DrawColor, not DrawColor, so we don't get the tainted color
        return base.DrawColor;
    }

    public void Notify_PawnKilled()
    {
        if( def.apparel.careIfWornByCorpse )
            wornByCorpseInt = true;

        foreach (var c in AllComps)
        {
            c.Notify_WearerDied();
        }
    }

    public void Notify_PawnResurrected(Pawn pawn)
    {
        if (pawn.IsMutant && pawn.mutant.Def.isConsideredCorpse)
            return;

        wornByCorpseInt = false;
    }

    public override void Notify_ColorChanged()
    {
        if (Wearer != null)
            Wearer.apparel.Notify_ApparelChanged();
        
        base.Notify_ColorChanged();
    }

    public override void Notify_Equipped(Pawn pawn)
    {
        base.Notify_Equipped(pawn);

        foreach (var ability in AllAbilitiesForReading)
        {
            ability.pawn = pawn;
            ability.verb.caster = pawn;
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look( ref wornByCorpseInt, "wornByCorpse" );
        Scribe_Collections.Look(ref abilities, "abilities", LookMode.Deep);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            FillOrUpdateAbilities();
        }
    }

    public void FillOrUpdateAbilities()
    {
        if (def.apparel.abilities == null)
            return;

        if (abilities == null)
        {
            abilities = new List<Ability>();

            foreach (var def in def.apparel.abilities)
            {
                abilities.Add(AbilityUtility.MakeAbility(def, Wearer));
            }
        }

        foreach (var ability in abilities)
        {
            ability.pawn = Wearer;
            ability.verb.caster = Wearer;
        }
    }

    public virtual void DrawWornExtras()
    {
        var comps = AllComps;
        for(var i = 0; i < comps.Count; i++)
        {
            comps[i].CompDrawWornExtras();
        }
    }

    public virtual bool CheckPreAbsorbDamage(DamageInfo dinfo)
    {
        var comps = AllComps;
        for(var i = 0; i < comps.Count; i++)
        {
            comps[i].PostPreApplyDamage(ref dinfo, out var absorbed);
            
            if (absorbed)
                return true;
        }

        return false;
    }

    public virtual bool AllowVerbCast(Verb verb)
    {
        var comps = AllComps;
        for(var i = 0; i < comps.Count; i++)
        {
            if(!comps[i].CompAllowVerbCast(verb))
                return false;
        }

        return true;
    }

    public virtual IEnumerable<Gizmo> GetWornGizmos()
    {
        var comps = AllComps;
        for( int i = 0; i < comps.Count; i++ )
        {
            var comp = comps[i];
            foreach (var g in comp.CompGetWornGizmosExtra())
            {
                yield return g;
            }
        }
    }

    public override IEnumerable<StatDrawEntry> SpecialDisplayStats()
    {
        foreach (var s in base.SpecialDisplayStats())
        {
            yield return s;
        }

        RoyalTitleDef maxSatisfiedTitle = DefDatabase<FactionDef>.AllDefsListForReading
            .SelectMany(f => f.RoyalTitlesAwardableInSeniorityOrderForReading)
            .Where(t => t.requiredApparel != null && t.requiredApparel.Any(req => req.ApparelMeetsRequirement(def, false)))
            .OrderByDescending(t => t.seniority)
            .FirstOrDefault();
        
        if (maxSatisfiedTitle != null)
        {
            yield return new StatDrawEntry(StatCategoryDefOf.Apparel, 
                "Stat_Thing_Apparel_MaxSatisfiedTitle".Translate(), 
                maxSatisfiedTitle.GetLabelCapForBothGenders(), 
                "Stat_Thing_Apparel_MaxSatisfiedTitle_Desc".Translate(), 
                StatDisplayOrder.Thing_Apparel_MaxSatisfiedTitle, 
                null, 
                new [] { new Dialog_InfoCard.Hyperlink(maxSatisfiedTitle) });
        }
    }

    public override string GetInspectString()
    {
        var s = base.GetInspectString();

        if( WornByCorpse )
        {
            if( s.Length > 0 )
                s += "\n";

            s += "WasWornByCorpse".Translate();
        }

        return s;
    }

    public virtual float GetSpecialApparelScoreOffset()
    {
        var score = 0f;

        var comps = AllComps;
        for(var i = 0; i < comps.Count; i++)
        {
            score += comps[i].CompGetSpecialApparelScoreOffset();
        }

        return score;
    }
    
    [DebugOutput]
    private static void ApparelValidLifeStages()
    {
        List<TableDataGetter<ThingDef>> getters = new List<TableDataGetter<ThingDef>>();
        getters.Add(new TableDataGetter<ThingDef>("name", t => t.LabelCap ));
        getters.Add(new TableDataGetter<ThingDef>("valid life stage", t => t.apparel.developmentalStageFilter.ToCommaList() ));


        DebugTables.MakeTablesDialog(DefDatabase<ThingDef>.AllDefs.Where(t => t.IsApparel), getters.ToArray() );
    }
}
}
