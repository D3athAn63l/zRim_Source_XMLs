using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse.Sound;
using UnityEngine;
using RimWorld;

namespace Verse{

public enum Favorability : byte
{
	VeryBad,
	Bad,
	Neutral,
	Good,
	VeryGood,
}

public class WeatherDef : Def
{
	//When this weather should appear
	public IntRange					durationRange = new IntRange(16000, 160000);
	public bool						repeatable = false;
	public bool						isBad = false;
	public bool						canOccurAsRandomForcedEvent = true;
	public Favorability				favorability = Favorability.Neutral;
	public FloatRange               temperatureRange = new FloatRange(-999f, 999f);
	public SimpleCurve				commonalityRainfallFactor = null;
    public int                      transitionTicksOverride = int.MaxValue;
    public int                      minMonolithLevel;
    public bool                     canOccurInAmbientHorror;
    
    //Letter when this weather starts (for very severe weather)
    [MustTranslate] public string   letterText;
    [MustTranslate] public string   letterLabel;
    public LetterDef                letterDef;

	//Gameplay effects
	public float					rainRate = 0; //1 = raining
	public float					snowRate = 0; //1 = snowing
	public float					sandRate = 0; //1 = snowing
	public float					windSpeedFactor = 1;	//1 = calm weather
	public float					windSpeedOffset = 0f;
	public float					moveSpeedMultiplier = 1f;
	public float					accuracyMultiplier = 1f;
    public float                    maxRangeCap = -1f;
	public float					perceivePriority; //determines which weather is reported during transitions
    public bool                     doToxicBuildup = false;
	public ThoughtDef				weatherThought; // stage 0 = unexposed, stage 1 = exposed
    public float                    maxGlow = 1f;
    public bool                     preventSkygaze = false;
    public bool                     preventsShuttleLaunch = false;
    
	//Audiovisuals
	public List<SoundDef>			ambientSounds = new List<SoundDef>();
	public List<WeatherEventMaker>	eventMakers = new List<WeatherEventMaker>();
	public List<Type>				overlayClasses = new List<Type>();
    
	//Weather core
	public SkyColorSet				skyColorsNightMid;
	public SkyColorSet				skyColorsNightEdge;
	public SkyColorSet				skyColorsDay;
	public SkyColorSet				skyColorsDusk;

	//Calculated
    public Type workerClass = typeof(WeatherWorker);
    [Unsaved] private WeatherWorker workerInt;

	//Properties
	public WeatherWorker Worker
	{
		get
		{
			if( workerInt == null )
				workerInt = (WeatherWorker)Activator.CreateInstance(workerClass, this);
			return workerInt;
		}
	}

	public override IEnumerable<string> ConfigErrors()
	{
		if( skyColorsDay.saturation == 0 || skyColorsDusk.saturation == 0 || skyColorsNightMid.saturation == 0 || skyColorsNightEdge.saturation == 0 )
			yield return "a sky color has saturation of 0";
	}

	public static WeatherDef Named( string defName )
	{
		return DefDatabase<WeatherDef>.GetNamed( defName );
	}
}
}


