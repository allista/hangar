using UnityEngine;

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
			if(Result == 0) return;
			part.RequestResource(Resource.id, -Result);
			request = Result; Requested = Result = 0;
		}
	}
}

