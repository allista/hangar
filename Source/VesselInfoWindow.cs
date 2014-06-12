using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using KSP.IO;

namespace AtHangar
{
	[KSPAddon (KSPAddon.Startup.EditorAny, false)]
	public class VesselInfoWindow : AddonWindowBase<VesselInfoWindow>
	{
		private double vessel_volume = 0;
		private new string window_name = "Hangar info";
		private float next_update = 0;
		private static float update_interval = 0.2f;
		
		private double vesselVolume()
		{
			//get ship parts
			List<Part> parts = new List<Part>{};
			try { parts = EditorLogic.SortedShipList; }
			catch (NullReferenceException) { return 0; }
			if(parts.Count < 1) return 0;
			//calculate ship's volume
			double vol = 0;
			foreach(Part p in parts) vol += Hangar.PartVolume(p);
			return vol;
		}
		
		public void Update() 
		{ 
			if(Time.time > next_update)
			{
				vessel_volume = vesselVolume();
				next_update += update_interval;
			}
		}
			
		new void Awake() { base.Awake(); next_update = Time.time; }
		
		//draw main window
		override public void WindowGUI(int windowID)
		{ 
			GUILayout.BeginVertical();
			GUILayout.Label("Vessel Volume: "+Utils.formatVolume(vessel_volume), GUILayout.ExpandWidth(true));
			GUILayout.EndVertical();
			GUI.DragWindow(new Rect(0, 0, 10000, 20));
		}
		
		//main GUI callback
		override public void OnGUI()
		{
			if (Event.current.type != EventType.Layout) return;
			windowPos = GUILayout.Window(GetInstanceID(),
										 windowPos, WindowGUI,
										 window_name,
										 GUILayout.Width(200));
		}
	}
}

