//   Utils.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2015 Allis Tauri
//
// This work is licensed under the Creative Commons Attribution 4.0 International License. 
// To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/ 
// or send a letter to Creative Commons, PO Box 1866, Mountain View, CA 94042, USA.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AtHangar
{
	// This code is based on Procedural Fairings plug-in by Alexey Volynskov, PMUtils class
	static partial class Utils
	{
		#region Resources
		const string ElectricChargeName = "ElectricCharge";
		static int _electric_charge_id = -1;
		public static int ElectricChargeID
		{ 
			get 
			{ 
				if(_electric_charge_id < 0)
				{
					var _electric_charge = PartResourceLibrary.Instance.GetDefinition(ElectricChargeName);
					if(_electric_charge == null) Log("WARNING: Cannot find '{0}' in the resource library.");
					else _electric_charge_id = _electric_charge.id;
				}
				return _electric_charge_id;
			} 
		}
		#endregion

		#region Misc
		//sound (from the KAS mod; KAS_Shared class)
		public static bool createFXSound(Part part, FXGroup group, string sndPath, bool loop, float maxDistance = 30f)
		{
			group.audio = part.gameObject.AddComponent<AudioSource>();
			group.audio.volume = GameSettings.SHIP_VOLUME;
			group.audio.rolloffMode = AudioRolloffMode.Logarithmic;
			group.audio.dopplerLevel = 0f;
			group.audio.panLevel = 1f;
			group.audio.maxDistance = maxDistance;
			group.audio.loop = loop;
			group.audio.playOnAwake = false;
			if(GameDatabase.Instance.ExistsAudioClip(sndPath))
			{
				group.audio.clip = GameDatabase.Instance.GetAudioClip(sndPath);
				return true;
			}
			ScreenMessager.showMessage(10, "Sound file : " + sndPath + " has not been found, please check your Hangar installation");
			return false;
		}

		public static void DelayPhysicsForSeconds(float dt)
		{ OrbitPhysicsManager.HoldVesselUnpack(Mathf.CeilToInt(dt/TimeWarp.fixedDeltaTime)+1); }

		public static void UpdateEditorGUI()
		{ if(EditorLogic.fetch != null)	GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship); }

		public static bool HasLaunchClamp(IEnumerable<Part> parts)
		{
			foreach(Part p in parts)
			{ if(p.HasModule<LaunchClamp>()) return true; }
			return false;
		}
		#endregion

		#region ControlLock
		//modified from Kerbal Alarm Clock mod
		public static void LockEditor(string LockName, bool Lock=true)
		{
			if(Lock && InputLockManager.GetControlLock(LockName) != ControlTypes.EDITOR_LOCK)
			{
				#if DEBUG
				Log("AddingLock: {0}", LockName);
				#endif
				InputLockManager.SetControlLock(ControlTypes.EDITOR_LOCK, LockName);
				return;
			}
			if(!Lock && InputLockManager.GetControlLock(LockName) == ControlTypes.EDITOR_LOCK) 
			{
				#if DEBUG
				Log("RemovingLock: {0}", LockName);
				#endif
				InputLockManager.RemoveControlLock(LockName);
			}
		}

		public static void LockIfMouseOver(string LockName, Rect WindowRect, bool Lock=true)
		{
			Lock &= WindowRect.Contains(Event.current.mousePosition);
			LockEditor(LockName, Lock);
		}
		#endregion
		
		#region Formatting
		public static string formatMass(float mass)
		{
			if(mass >= 0.1f)
				return mass.ToString("n2") + "t";
			if(mass >= 0.001f)
				return (mass * 1e3f).ToString("n1") + "kg";
			return (mass * 1e6f).ToString("n0") + "g";
		}
		
		public static string formatVolume(double volume)
		{
			if(volume < 1f)
				return (volume * 1e3f).ToString ("n0") + "L";
			return volume.ToString("n1") + "m3";
		}

		public static string formatUnits(float units)
		{
			units = Mathf.Abs(units);
			if(units >= 1f)
				return units.ToString ("n2") + "u";
			if(units >= 1e-3f)
				return (units * 1e3f).ToString ("n1") + "mu";
			if(units >= 1e-6f)
				return (units * 1e6f).ToString ("n1") + "mku";
			if(units >= 1e-9f)
				return (units * 1e9f).ToString ("n1") + "nu";
			if(units >= 1e-13f) //to fully use the last digit 
				return (units * 1e12f).ToString ("n1") + "pu";
			return "0.0u"; //effectivly zero
		}

		public static string formatDimensions(Vector3 size)
		{ return string.Format("{0:F2}m x {1:F2}m x {2:F2}m", size.x, size.y, size.z); }
		
		public static string formatVector(Vector3 v)
		{ return string.Format("({0}, {1}, {2}); |v| = {3}", v.x, v.y, v.z, v.magnitude); }

		public static string formatVector(Vector3d v)
		{ return string.Format("({0}, {1}, {2}); |v| = {3}", v.x, v.y, v.z, v.magnitude); }
		#endregion

		public static void Log(string msg, params object[] args)
		{ 
			for(int i = 0; i < args.Length; i++) 
			{
				if(args[i] is Vector3) args[i] = formatVector((Vector3)args[i]);
				else if(args[i] is Vector3d) args[i] = formatVector((Vector3d)args[i]);
				else if(args[i] == null) args[i] = "null";
			}
			Debug.Log(string.Format("[Hangar] "+msg, args)); 
		}

		//from http://stackoverflow.com/questions/716399/c-sharp-how-do-you-get-a-variables-name-as-it-was-physically-typed-in-its-dec
		//second answer
		public static string PropertyName<T>(T obj) { return typeof(T).GetProperties()[0].Name; }
	}

	class MemoryTimer : IEnumerator<YieldInstruction>
	{
		public delegate void Callback();

		public bool  Active = true;
		public float WaitPeriod = 1f;
		public Callback EndAction = null;

		public YieldInstruction Current
		{
			get
			{
				Active = false;
				return new WaitForSeconds(WaitPeriod);
			}
		}
		object IEnumerator.Current { get { return Current; } }

		public bool MoveNext() 
		{ 
			if(!Active && EndAction != null) 
				EndAction();
			return Active; 
		}

		public void Reset() { Active = true; }

		public void Dispose() {}
	}

	public class State<T>
	{
		T _current, _old;

		public T current 
		{ 
			get	{ return _current; }
			set
			{
				_old = _current;
				_current = value;
			}
		}

		public T old { get { return _old; } }

		public State(T cur, T old = default(T))
		{
			_current = cur;
			_old = EqualityComparer<T>.Default.Equals(old, default(T)) ? cur : old;
		}

		public static implicit operator T(State<T> s) { return s._current; }
	}

	public class ConfigNodeObject : IConfigNode
	{
		public const string NODE_NAME = "NODE";

		virtual public void Load(ConfigNode node)
		{ ConfigNode.LoadObjectFromConfig(this, node); }

		virtual public void Save(ConfigNode node)
		{ ConfigNode.CreateConfigFromObject(this, node); }
	}

	public static class WaitWithPhysics
	{
		public static WaitForSeconds ForSeconds(float dt)
		{
			Utils.DelayPhysicsForSeconds(dt);
			return new WaitForSeconds(dt);
		}

		public static WaitForFixedUpdate ForFixedUpdate()
		{
			OrbitPhysicsManager.HoldVesselUnpack(2);
			return new WaitForFixedUpdate();
		}

		public static YieldInstruction ForNextUpdate()
		{
			Utils.DelayPhysicsForSeconds(TimeWarp.deltaTime);
			return null;
		}
	}
}
