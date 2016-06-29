//   ResourcePump.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using UnityEngine;
using AT_Utils;

namespace AtHangar
{
	public class ResourcePump
	{
		const float eps = 1e-7f;
		const float min_request = 1e-5f;
		float request;

		readonly Part part;

		public readonly PartResourceDefinition Resource;
		public float Requested { get; private set; }
		public float Result    { get; private set; }
		public float Ratio     { get { return Result/Requested; } }
		public bool  PartialTransfer { get { return Mathf.Abs(Requested)-Mathf.Abs(Result) > eps; } }
		public bool  Valid     { get { return part != null; } }

		public ResourcePump(Part part, int res_ID)
		{ 
			Resource = PartResourceLibrary.Instance.GetDefinition(res_ID);
			if(Resource != null) this.part = part;
			else Utils.Log("WARNING: Cannot find a resource with '{0}' ID in the library.", res_ID);
		}

		public ResourcePump(Part part, string res_name)
		{
			Resource = PartResourceLibrary.Instance.GetDefinition(res_name);
			if(Resource != null) this.part  = part;
			else Utils.Log("WARNING: Cannot find '{0}' in the resource library.", res_name);
		}

		public void RequestTransfer(float dR) { request += dR; }

		public bool TransferResource()
		{
			if(Mathf.Abs(request) <= min_request) return false;
			Result    = part.RequestResource(Resource.id, request);
			Requested = request;
			request   = 0;
			return true;
		}

		public void Clear()
		{ request = Requested = Result = 0; }

		public void Revert()
		{
			if(Result.Equals(0)) return;
			part.RequestResource(Resource.id, -Result);
			request = Result; Requested = Result = 0;
		}
	}

	public class ResourceLine : ResourceWrapper<ResourceLine>
	{
		/// <summary>
		/// Gets the density in tons/unit.
		/// </summary>
		public float Density { get { return Resource.density; } }

		/// <summary>
		/// Gets conversion rate in tons/sec.
		/// </summary>
		public float Rate    { get; private set; }
		float base_rate;

		/// <summary>
		/// Gets conversion rate in units/sec.
		/// </summary>
		public float URate   { get; private set; } //u/sec

		public ResourcePump Pump { get; private set; }

		public ResourceLine() {}
		public ResourceLine(Part part, PartResourceDefinition res_def, float rate)
		{ 
			Resource = res_def; 
			Rate = base_rate = rate;
			if(res_def != null) 
			{
				Pump = new ResourcePump(part, res_def.id);
				URate = rate/res_def.density;
			}
		}

		public void InitializePump(Part part, float rate_multiplier)
		{ 
			Pump  = new ResourcePump(part, Resource.id);
			Rate  = base_rate * rate_multiplier;
			URate = Rate/Resource.density;
		}

		public override void LoadDefinition(string resource_definition)
		{
			var rate = load_definition(resource_definition);
			if(!Valid) return;
			Rate  = base_rate = rate;
			URate = rate/Resource.density;
		}

		public bool TransferResource(float rate = 1f)
		{
			Pump.RequestTransfer(rate*URate*TimeWarp.fixedDeltaTime);
			return Pump.TransferResource();
		}

		public bool PartialTransfer { get { return Pump.PartialTransfer; } }

		public string Info
		{ get { return string.Format("{0}: {1}/sec", Resource.name, Utils.formatUnits(URate)); } }
	}
}

