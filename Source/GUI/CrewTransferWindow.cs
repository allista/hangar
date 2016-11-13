//   CrewTransferWindow.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System.Collections.Generic;
using UnityEngine;
using AT_Utils;

namespace AtHangar
{
	class CrewTransferWindow : GUIWindowBase
	{
		int CrewCapacity;
		List<ProtoCrewMember> crew;
		List<ProtoCrewMember> selected;

		public CrewTransferWindow()
		{
			width = 250; height = 150;
		}
		
		Vector2 scroll_view = Vector2.zero;
        void TransferWindow(int windowId)
        {
			scroll_view = GUILayout.BeginScrollView(scroll_view, GUILayout.Width(width), GUILayout.Height(height));
            GUILayout.BeginVertical();
            foreach(ProtoCrewMember kerbal in crew)
            {
				int ki = selected.FindIndex(cr => cr.name == kerbal.name);
				if(Utils.ButtonSwitch(kerbal.name, ki >= 0, "", GUILayout.ExpandWidth(true)))
				{
					if(ki >= 0) selected.RemoveAt(ki);
					else selected.Add(kerbal);
				}
            }
            GUILayout.EndVertical();
            GUILayout.EndScrollView();
			TooltipsAndDragWindow(WindowPos);
        }
		
		public void Draw(List<ProtoCrewMember> _crew, 
		                 List<ProtoCrewMember> _selected, 
		                 int _crew_capacity)
		{
			crew = _crew;
			selected = _selected;
			CrewCapacity = _crew_capacity;
			LockControls();
			WindowPos = GUILayout.Window(GetInstanceID(), 
			                             WindowPos, TransferWindow,
										 string.Format("Vessel Crew {0}/{1}", selected.Count, CrewCapacity),
			                             GUILayout.Width(width), GUILayout.Height(height)).clampToScreen();
		}
	}
}

