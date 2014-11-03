using System;
using System.Collections.Generic;
using UnityEngine;
using KSPAPIExtensions;

namespace AtHangar
{
	public class SimpleTextureSwitcher : PartModule
	{
		/// <summary>
		/// The folder in which textures are locatged. 
		/// Relative to GameData folder.
		/// </summary>
		[KSPField] public string RootFolder = string.Empty;

		/// <summary>
		/// The name of the material which texture should be replaced.
		/// </summary>
		[KSPField] public string AffectedMaterial = string.Empty;
		readonly List<Renderer> renderers = new List<Renderer>();

		/// <summary>
		/// Names of the textures to switch. 
		/// All materials that use this texture will be affected.
		/// </summary>
		[KSPField] public string Textures = string.Empty;
		readonly List<string> textures = new List<string>();

		/// <summary>
		/// The texture currently in use.
		/// </summary>
		[KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Texture")]
		[UI_ChooseOption(scene = UI_Scene.Editor)]
		public string CurrentTexture = string.Empty;
		string last_texture = string.Empty;

		public override void OnStart(StartState state)
		{
			//prepare root folder path
			if(!string.IsNullOrEmpty(RootFolder))
				RootFolder = RootFolder.TrimEnd('/')+"/";
			setup_material();
			setup_textures();
			set_texture();
			//setup UI
			if(state == StartState.Editor && textures.Count > 1)
			{
				var _textures = textures.ToArray();
				HangarGUI.SetupChooser(_textures, _textures, Fields["CurrentTexture"]);
				HangarGUI.EnableField(Fields["CurrentTexture"]);
				StartCoroutine(slow_update());
			}
			else HangarGUI.EnableField(Fields["CurrentTexture"], false);
		}

		void setup_material()
		{
			renderers.Clear();
			if(string.IsNullOrEmpty(AffectedMaterial)) return;
			foreach(var r in part.FindModelComponents<Renderer>())
			{
				if(r == null || !r.enabled) continue;
				var m_name = r.sharedMaterial.name.Replace("(Instance)", "").Trim();
				if(m_name == AffectedMaterial) renderers.Add(r);
			}
			if(renderers.Count == 0)
				this.Log("Material {0} was not found", AffectedMaterial);
		}

		void setup_textures()
		{
			textures.Clear();
			if( renderers.Count == 0 ||
				string.IsNullOrEmpty(Textures)) return;
			//parse textures
			foreach(var t in Textures.Split(new []{','}, 
				StringSplitOptions.RemoveEmptyEntries))
			{
				var tex = t.Trim();
				if(GameDatabase.Instance.ExistsTexture(RootFolder+tex))
				{
					try { textures.Add(tex); }
					catch { this.Log("Duplicate texture in the replacement list: {0}", tex); }
				}
				else this.Log("No such texture: {0}", RootFolder+tex);
			}
			if(textures.Count > 0 && 
				(CurrentTexture == string.Empty || 
				!textures.Contains(CurrentTexture)))
				CurrentTexture = textures[0];
		}

		void set_texture()
		{
			if(textures.Count == 0) return;
			var texture = GameDatabase.Instance.GetTexture(RootFolder+CurrentTexture, false);
			foreach(var r in renderers)
				r.material.mainTexture = texture;
			last_texture = CurrentTexture;
		}

		IEnumerator<YieldInstruction> slow_update()
		{
			while(true)
			{
				if(	CurrentTexture != last_texture)
					set_texture();
				yield return new WaitForSeconds(0.1f);
			}
		}
	}
}

