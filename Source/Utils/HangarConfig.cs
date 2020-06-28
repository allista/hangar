//   HangarConfig.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2015 Allis Tauri

using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using AT_Utils;

namespace AtHangar
{
    class Globals : PluginGlobals<Globals>
    {
        //store vessel
        [Persistent] public float  MaxSqrRelVelocity     = 1f;    //m/s
        [Persistent] public float  MaxSqrRelAcceleration = 0.01f; //m/s2
        [Persistent] public bool   EnableVesselPacking   = true;
        [Persistent] public bool   UseStockAppLauncher = false;
        [Persistent] public string DontCloneResources  = "ElectricCharge, LiquidFuel, Oxidizer, Ore, XenonGas, MonoPropellant";
        public string[] ResourcesBlacklist { get; private set; }

        public override void Init()
        { 
            ResourcesBlacklist = string.IsNullOrEmpty(DontCloneResources)?
                                       new string[0] : Utils.ParseLine(DontCloneResources, Utils.Comma);
        }
    }
}

