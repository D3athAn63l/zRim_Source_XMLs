using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Verse
{

public class RoofDef : Def
{
	public bool			isNatural = false;
	public bool			isThickRoof = false;
	public ThingDef		collapseLeavingThingDef = null;
	public ThingDef		filthLeaving = null;
	public SoundDef		soundPunchThrough;
    public bool         canCollapse = true;

	public bool VanishOnCollapse { get { return !isThickRoof; } }
}

}

