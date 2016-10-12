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
		
		Vector2 scroll_view = Vector2.zero;
        void TransferWindow(int windowId)
        {
            scroll_view = GUILayout.BeginScrollView(scroll_view, GUILayout.Height(200), GUILayout.Width(300));
            GUILayout.BeginVertical();
            foreach(ProtoCrewMember kerbal in crew)
            {
                GUILayout.BeginHorizontal();
				int ki = selected.FindIndex(cr => cr.name == kerbal.name);
				GUIStyle style = (ki >= 0) ? Styles.green : Styles.normal_button;
				GUILayout.Label(kerbal.name, style, GUILayout.Width(200));
				if(ki >= 0)
                {
                    if(GUILayout.Button("Selected", Styles.green_button, GUILayout.Width(70)))
						selected.RemoveAt(ki);
                }
				else if(selected.Count < CrewCapacity)
				{
					if(GUILayout.Button("Select", style, GUILayout.Width(60)))
						selected.Add(kerbal);
				}
                GUILayout.EndHorizontal();
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
			                             GUILayout.Width(260)).clampToScreen();
		}
	}
}

