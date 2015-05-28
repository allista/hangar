using UnityEngine;

namespace AtHangar
{
	class SimpleDialog : MonoBehaviour
	{
		public enum Answer { None, Yes, No }

		const int width = 400;
		Rect windowPos = new Rect(Screen.width/2-width/2, 100, width, 50);

		string message;
		public Answer Result { get; private set; }

		void DialogWindow(int windowId)
		{
			GUILayout.BeginVertical();
			GUILayout.Label(message, Styles.label, GUILayout.Width(width));
			GUILayout.BeginHorizontal();
			Result = Answer.None;
			if(GUILayout.Button("No", Styles.red_button, GUILayout.Width(70))) Result = Answer.No;
			GUILayout.FlexibleSpace();
			if(GUILayout.Button("Yes", Styles.green_button, GUILayout.Width(70))) Result = Answer.Yes;
			GUILayout.EndHorizontal();
			GUILayout.EndVertical();
			GUI.DragWindow(new Rect(0, 0, Screen.width, 20));
		}

		public Rect Show(string message, string title = "Warning")
		{
			this.message = message;
			windowPos = GUILayout.Window(GetInstanceID(), 
				windowPos, DialogWindow,
				title,
				GUILayout.Width(width));
			HangarGUI.CheckRect(ref windowPos);
			return windowPos;
		}
	}
}
