using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace AtHangar
{
	//this module adds the ability to store a vessel in a packed state inside
	public class Hangar : PartModule
	{
		//internal properties
		IHangarAnimator hangar_state;
		double usefull_volume_ratio = 0.7;
		double used_volume = 0;
		double total_volume;
		List<Vessel> stored_vessels = new List<Vessel>();
		
		//gui fields
		[KSPField (guiName = "Volume", guiActive = true, guiActiveEditor=true)] public string total_v;
		[KSPField (guiName = "Hangar doors", guiActive = true)] public string doors;
		[KSPField (guiName = "Volume used", guiActive = true)] public string used_v;
		[KSPField (guiName = "Vessels stored", guiActive = true)] public string vessels;
		
		//info string
		//public override string GetInfo () { return ""; }
		
		//all initialization goes here instead of the constructor as documented in Unity API
		public override void OnStart(StartState state)
		{
			base.OnStart(state);
			
			part.force_activate();
            hangar_state = part.Modules.OfType<IHangarAnimator>().SingleOrDefault();
			if (hangar_state == null)
                hangar_state = new DummyHangarAnimator();
            else
            {
                Events["Open"].guiActiveEditor = true;
                Events["Close"].guiActiveEditor = true;
            }
			
			if (HighLogic.LoadedSceneIsEditor) RecalculateVolume();
		}
		
		//calculate part volume as the sum of bounding boxes of its rendered components
		public static double PartVolume(Part p)
		{
			var model = p.FindModelTransform("model");
			if (model == null) 
			{ 
				Debug.Log ("[Hangar] No 'model' tranform in the part.");
				return 0;
			}
			double vol = 0;
			foreach(Renderer r in model.gameObject.GetComponentsInChildren<Renderer>())
			{
				Vector3 s = r.bounds.size;
				vol += s.x*s.y*s.z;
			}
			return vol;
		}
		
		public void RecalculateVolume()
		{
			total_volume = PartVolume(part)*usefull_volume_ratio;
			total_v = String.Format ("{0:F1} m^3", total_volume);
		}
		
		//calculate approximate volume of a vessel 
		//as the sum of volumes of bounding boxes of its parts
		private double VesselVolume(Vessel vsl)
		{
			if (vsl == null) return -1;
			double vol = 0;
			foreach (Part p in vsl.parts)
				vol += PartVolume(p);
			return vol;
		}
		
		//if a vessel can be stored in the hangar
		public bool CanStore(Vessel vsl)
		{
			if(vsl.isEVA) return false;
			double vol = VesselVolume(vsl);
			if(vol < 0 || vol > total_volume-used_volume) return false;
			return true;
		}
		
		//events
		//open event
		[KSPEvent (guiActive = true, guiName = "Open hangar", active = true)]
		public void Open()
		{
			hangar_state.Open();
			Events["Open"].active = false;
			Events["Close"].active = true;
		}
	
		//close event
		[KSPEvent (guiActive = true, guiName = "Close hangar", active = false)]
		public void Close()
		{
			hangar_state.Close();
			Events["Open"].active = true;
			Events["Close"].active = false;
		}
		
		//actions
		[KSPAction("Open hangar")]
        public void OpenHangarAction(KSPActionParam param) { Open (); }
		
		[KSPAction("Close hangar")]
        public void CloseHangarAction(KSPActionParam param) { Close (); }
		
		[KSPAction("Toggle hangar")]
        public void ToggleHangarAction(KSPActionParam param) { hangar_state.Toggle(); }
	
		//save the hangar
		public override void OnSave(ConfigNode node)
		{
			return;
		}
		
		//load the hangar
		public override void OnLoad(ConfigNode node)
		{ 
			
			//always load in closed state
			Close();
		}
		
		
		public override void OnUpdate ()
		{
			doors = hangar_state.CurrentState.ToString();
			used_v = String.Format ("{0:F1} m^3", used_volume);
			vessels = String.Format ("{0}", stored_vessels.Count);
		}
		
	}
}

