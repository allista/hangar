using UnityEngine;

namespace AtHangar
{
	[KSPAddon(KSPAddon.Startup.Instantly, false)]
	public class HangarConfigLoader : MonoBehaviour
	{
		public const string HANGAR_CONFIG  = "HANGAR_CONFIG";
		public const string MESHES_TO_SKIP = "MeshesToSkip";

		public static string GetConfigValue(string cfg_name, string separator = " ")
		{
			string val = "";
			foreach(ConfigNode n in GameDatabase.Instance.GetConfigNodes(HANGAR_CONFIG))
				if(n.HasValue(cfg_name)) 
				{
					if(val != "") val += separator;
					foreach(string v in n.GetValues(cfg_name)) val += v;
				}
			return val;
		}

		public void Start()
		{
			//init meshes names
			string meshes = GetConfigValue(MESHES_TO_SKIP);
			Metric.MeshesToSkip.Clear();
			Metric.MeshesToSkip.AddRange(meshes.Split(' '));
		}
	}

	/// <summary>
	/// Screen messager is an addon that displays on-screen 
	/// messages in the top-center of the screen.
	/// It is a part of the Hangar module.
	/// </summary>
	[KSPAddon(KSPAddon.Startup.EveryScene, false)]
	public class ScreenMessager : MonoBehaviour
	{
		static float  osdMessageTime = 0;
		static string osdMessageText = null;

		public static void showMessage(string msg, params object[] args)
		{ showMessage(3, msg, args);}

		public static void showMessage(float delay, string msg, params object[] args)
		{
			#if DEBUG
			Utils.Log(msg, args);
			#endif
			osdMessageText = string.Format(msg, args);
			osdMessageTime = Time.time + delay;
		}

		public void OnGUI ()
		{
			if (Time.time < osdMessageTime) 
			{
				GUI.skin = HighLogic.Skin;
				GUIStyle style = new GUIStyle("Label");
				style.alignment = TextAnchor.MiddleCenter;
				style.fontSize = 20;
				style.normal.textColor = Color.black;
				GUI.Label (new Rect (2, 2 + (Screen.height / 9), Screen.width, 50), osdMessageText, style);
				style.normal.textColor = Color.yellow;
				GUI.Label (new Rect (0, Screen.height / 9, Screen.width, 50), osdMessageText, style);
			}
		}
	}
}

