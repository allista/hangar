using System.Collections.Generic;
using UnityEngine;

namespace AtHangar
{
	class CrewTransferWindow : MonoBehaviour
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
            GUI.DragWindow(new Rect(0, 0, Screen.width, 20));
        }
		
		public Rect Draw(List<ProtoCrewMember> _crew, 
		                 List<ProtoCrewMember> _selected, 
		                 int _crew_capacity, 
		                 Rect windowPos)
		{
			crew = _crew;
			selected = _selected;
			CrewCapacity = _crew_capacity;
			windowPos = GUILayout.Window(GetInstanceID(), 
										 windowPos, TransferWindow,
										 string.Format("Vessel Crew {0}/{1}", selected.Count, CrewCapacity),
										 GUILayout.Width(260));
			return windowPos;
		}
	}
}

