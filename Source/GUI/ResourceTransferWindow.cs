//This code is based on code from ExLaunchPads mod, BuildWindow class.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace AtHangar
{
	class ResourceTransferWindow : MonoBehaviour
	{
		List<ResourceManifest> transfer_list;
		bool link_lfo_sliders = true;
		public bool transferNow = false;
		
		static float ResourceLine(string label, float fraction, 
		               		      double pool, 
		               		      double minAmount, double maxAmount, 
		               		      double capacity)
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
			GUILayout.Box ((fraction * 100) + "%",
						   Styles.slider_text, GUILayout.Width (300),
						   GUILayout.Height (20));
			GUILayout.EndVertical ();

			// amount and capacity
			GUILayout.Box((Math.Round(pool-fraction*maxAmount, 2)).ToString (),
						   Styles.white, GUILayout.Width (75),
						   GUILayout.Height(40));
			GUILayout.Box((Math.Round(fraction*maxAmount, 2)).ToString (),
						   Styles.fracStyle(fraction), GUILayout.Width (75),
						   GUILayout.Height(40));
			GUILayout.Box((Math.Round(capacity, 2)).ToString (),
						   Styles.yellow, GUILayout.Width (75),
						   GUILayout.Height(40));

			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
			return fraction;
		}
		
		void TransferWindow(int windowId)
		{
			
			GUILayout.BeginVertical();
			link_lfo_sliders = GUILayout.Toggle(link_lfo_sliders, "Link LiquidFuel and Oxidizer sliders");
			
			foreach (var r in transfer_list) 
			{
				float frac = r.maxAmount > 0 ? (float)(r.amount/r.maxAmount) : 0f;
				frac = ResourceLine(r.name, frac, r.pool, r.minAmount, r.maxAmount, r.capacity);
				if (link_lfo_sliders
					&& (r.name == "LiquidFuel" || r.name == "Oxidizer")) 
				{
					string other = r.name == "LiquidFuel" ? "Oxidizer" : "LiquidFuel";
					var or = transfer_list.Find(res => res.name == other);
					if (or != null) or.amount = or.maxAmount * frac;
				}
				r.amount = frac * r.maxAmount;
			} 
			transferNow = GUILayout.Button("Transfer now", GUILayout.ExpandWidth(true));
			GUILayout.EndVertical();
			GUI.DragWindow(new Rect(0, 0, Screen.width, 20));
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

