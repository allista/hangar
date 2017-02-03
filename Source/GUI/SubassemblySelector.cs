//   SubassemblySelector.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2017 Allis Tauri
using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using AT_Utils;

namespace AtHangar
{
	public class SubassemblySelector : GUIWindowBase
	{
		List<ShipTemplate> subs = new List<ShipTemplate>();
		List<GUIContent> buttons = new List<GUIContent>();
		Action<ShipTemplate> load_ship = delegate {};

		public SubassemblySelector()
		{
			width = 400;
			height = 200;
			WindowPos = new Rect(Screen.width/2-width/2, 100, width, 100);
		}

		public override void Awake()
		{
			base.Awake();
			Show(false);
		}

		public void RefreshSubassemblies()
		{
			subs.Clear();
			buttons.Clear();
			var path = KSPUtil.ApplicationRootPath + "saves/" + HighLogic.SaveFolder + "/Subassemblies/";
			Directory.CreateDirectory(path);
			var directoryInfo = new DirectoryInfo(path);
			FileInfo[] files = directoryInfo.GetFiles("*.craft");
			for(int i = 0, len = files.Length; i < len; i++)
			{
				var sub = ShipConstruction.LoadTemplate(files[i].FullName);
				if(sub == null || sub.partCount == 0 || !sub.shipPartsUnlocked) continue;
				var label = string.Format("<color=yellow><b>{0}</b></color>\n" +
				                          "<color=silver>mass:</color> {1:F1}t " +
                                          "<color=silver>cost:</color> {2:F0} " +
                                          "<color=silver>size:</color> {3}", 
				                          sub.shipName, sub.totalMass, sub.totalCost, 
				                          Utils.formatDimensions(sub.GetShipSize()));
				var button = string.IsNullOrEmpty(sub.shipDescription)? new GUIContent(label) : new GUIContent(label, sub.shipDescription);
				buttons.Add(button);
				subs.Add(sub);
			}
		}

		public override void Show(bool show)
		{
			base.Show(show);
			if(show) RefreshSubassemblies();
		}

		Vector2 scroll = Vector2.zero;
		void DrawWindow(int windowID)
		{
			GUILayout.BeginVertical();
			scroll = GUILayout.BeginScrollView(scroll);
			ShipTemplate toLoad = null;
			for(int i = 0, count = buttons.Count; i < count; i++)
			{
				var button = buttons[i];
				GUILayout.BeginHorizontal();
				if(GUILayout.Button(button, Styles.boxed_label, GUILayout.ExpandWidth(true)))
					toLoad = subs[i];
				GUILayout.EndHorizontal();
			}
			GUILayout.EndScrollView();
			if(toLoad != null && load_ship != null) 
			{
				load_ship(toLoad);
				Show(false);
			}
			if(GUILayout.Button("Close", Styles.close_button, GUILayout.ExpandWidth(true)))
				Show(false);
			GUILayout.EndVertical();
			TooltipsAndDragWindow();
		}

		public void Draw(Action<ShipTemplate> loadShip)
		{
			if(doShow)
			{
				LockControls();
				load_ship = loadShip;
				WindowPos = GUILayout.Window(GetInstanceID(),
				                             WindowPos,
				                             DrawWindow,
				                             "Select Subassembly",
				                             GUILayout.Width(width),
				                             GUILayout.Height(height))
					.clampToScreen();
			}
			else UnlockControls();
		}
	}
}

