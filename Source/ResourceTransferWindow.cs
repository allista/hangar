//This code is based on code from ExLaunchPads mod, BuildWindow class.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AtHangar
{
	public class ResourceTransferWindow : MonoBehaviour
	{
		private List<ResourceManifest> transfer_list;
		private bool link_lfo_sliders = true;
		public bool transferNow = false;
		
		private GUIStyle fracStyle(float frac)
		{
			if(frac < 0.1) return Styles.red;
			if(frac < 0.5) return Styles.yellow;
			if(frac < 0.8) return Styles.white;
			return Styles.green;
		}
		
		float ResourceLine(string label, string resourceName, float fraction, 
		                   double pool, double minAmount, double maxAmount, double capacity)
		{
			GUILayout.BeginHorizontal ();

			// Resource name
			GUILayout.Box(label, Styles.white, GUILayout.Width (120), GUILayout.Height (40));

			// Fill amount
			// limit slider to 0.5% increments
			GUILayout.BeginVertical ();
			fraction = GUILayout.HorizontalSlider(fraction, 0.0F, 1.0F,
												  Styles.slider,
												  GUI.skin.horizontalSliderThumb,
												  GUILayout.Width (300),
												  GUILayout.Height (20));
			
			fraction = (float)Math.Round (fraction, 3);
			fraction = (Mathf.Floor (fraction * 200)) / 200;
			if(fraction*maxAmount < minAmount) fraction = (float)(minAmount/maxAmount);
			GUILayout.Box ((fraction * 100).ToString () + "%",
						   Styles.sliderText, GUILayout.Width (300),
						   GUILayout.Height (20));
			GUILayout.EndVertical ();

			// amount and capacity
			GUILayout.Box((Math.Round(pool-fraction*maxAmount, 2)).ToString (),
						   Styles.white, GUILayout.Width (75),
						   GUILayout.Height(40));
			GUILayout.Box((Math.Round(fraction*maxAmount, 2)).ToString (),
						   fracStyle(fraction), GUILayout.Width (75),
						   GUILayout.Height(40));
			GUILayout.Box((Math.Round(capacity, 2)).ToString (),
						   Styles.yellow, GUILayout.Width (75),
						   GUILayout.Height(40));

			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
			return fraction;
		}
		
		private void TransferWindow(int windowId)
		{
			
			GUILayout.BeginVertical();
			link_lfo_sliders = GUILayout.Toggle(link_lfo_sliders, "Link LiquidFuel and Oxidizer sliders");
			
			foreach (var r in transfer_list) 
			{
				float frac = (float)(r.amount/r.maxAmount);
				frac = ResourceLine(r.name, r.name, frac, r.pool+r.offset, r.minAmount, r.maxAmount, r.capacity);
				if (link_lfo_sliders
					&& (r.name == "LiquidFuel" || r.name == "Oxidizer")) 
				{
					string other;
					if(r.name == "LiquidFuel") other = "Oxidizer";
					else other = "LiquidFuel";
					var or = transfer_list.Find(res => res.name == other);
					if (or != null) or.amount = or.maxAmount * frac;
				}
				r.amount = frac * r.maxAmount;
			} 
			if(GUILayout.Button("Transfer now", GUILayout.ExpandWidth(true))) transferNow = true;
			GUILayout.EndVertical();
			GUI.DragWindow(new Rect(0, 0, Screen.width, 30));
		}
		
		public Rect Draw(List<ResourceManifest> resourceTransferList, Rect windowPos)
		{
			if(resourceTransferList.Count == 0) return windowPos;
			transfer_list = resourceTransferList;
			windowPos = GUILayout.Window(GetInstanceID(), 
										 windowPos, TransferWindow,
										 "Transfer resources to the launched vessel",
										 GUILayout.Width(360));
			return windowPos;
		}
	}
}

