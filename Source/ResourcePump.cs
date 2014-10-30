using UnityEngine;

namespace AtHangar
{
	public class ResourcePump
	{
		const float min_request = 1e-5f;
		float request;

		readonly int resourceID;
		readonly Part part;

		public float Requested { get; private set; }
		public float Result    { get; private set; }
		public float Ratio     { get { return Result/Requested; } }
		public bool  PartialTransfer { get { return Mathf.Abs(Result) < Mathf.Abs(Requested); } }

		/// <summary>
		/// Initializes a new instance of the <see cref="AtHangar.ResourcePump"/> class.
		/// </summary>
		/// <param name="part">Part instance.</param>
		/// <param name="res_ID">Resource ID.</param>
		public ResourcePump(Part part, int res_ID)
		{ this.part = part; resourceID = res_ID; }

		public void RequestTransfer(float dR) { request += dR; }

		public bool TransferResource()
		{
			if(Mathf.Abs(request) <= min_request) return false;
			Result    = part.RequestResource(resourceID, request);
			Requested = request;
			request   = 0;
			return true;
		}

		public void Clear()
		{ request = Requested = Result = 0; }

		public void Revert()
		{
			if(Result == 0) return;
			part.RequestResource(resourceID, -Result);
			request = Result; Requested = Result = 0;
		}
	}
}

