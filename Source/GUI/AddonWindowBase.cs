//   AddonWindowBase.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using UnityEngine;
using AT_Utils;

namespace AtHangar
{
	abstract public class HangarWindowBase<T> : AddonWindowBase<T> where T : HangarWindowBase<T>
	{
		//update parameters
		float next_update = 0;
		const float update_interval = 0.5f;
		
		//update-init-destroy
		protected override void UpdateContent() { enabled = do_show; }

		abstract public void OnUpdate();
		
		virtual public void Update() 
		{ 
			if(Time.time > next_update)
			{
				OnUpdate();
				next_update += update_interval;
			}
		}
		
		public override void Awake()
		{
			base.Awake();
			next_update = Time.time; 
		}
		
		virtual public void OnGUI()
		{
			Styles.Init();
		}
	}
}

