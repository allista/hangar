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
	/// <summary>
	/// Loads hangar configuration presets at game loading
	/// </summary>
	[KSPAddon(KSPAddon.Startup.Instantly, true)]	
	public class HangarGlobalsLoader : MonoBehaviour
	{ public void Start() { Globals.Load(); } }

	class Globals : PluginGlobals<Globals>
	{
		//store vessel
		[Persistent] public float  MaxSqrRelVelocity     = 1f;    //m/s
		[Persistent] public float  MaxSqrRelAcceleration = 0.01f; //m/s2
		[Persistent] public bool   EnableVesselPacking   = true;
		//restore vessel
		[Persistent] public float  MaxSqrAngularVelocity = 0.01f; //5.73 deg/s
		[Persistent] public float  MaxSqrSurfaceVelocity = 0.01f; //m/s
		[Persistent] public float  MaxGeeForce           = 0.2f;  //g
		[Persistent] public float  MaxStaticPressure     = 0.01f; //atm
		//for Metric
		[Persistent] public string MeshesToSkip = string.Empty;
		//misc
		[Persistent] public string KethaneMapCollider  = "MapOverlay collider";
		[Persistent] public bool   UseStockAppLauncher = false;
		[Persistent] public string DontCloneResources  = "ElectricCharge, LiquidFuel, Oxidizer, Ore, XenonGas, MonoPropellant";
		public string[] ResourcesBlacklist { get; private set; }

		public override void Init()
		{ 
			//init meshes names
			if(!string.IsNullOrEmpty(MeshesToSkip))
				Metric.MeshesToSkip.AddUniqueRange(Utils.ParseLine(MeshesToSkip, Utils.Comma));
			ResourcesBlacklist = string.IsNullOrEmpty(DontCloneResources)?
				new string[0] : Utils.ParseLine(DontCloneResources, Utils.Comma);
		}
	}
}

