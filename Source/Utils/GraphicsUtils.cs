using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AtHangar
{
	public class Triangle : IEnumerable<int>
	{
		readonly protected int i1, i2, i3;

		public Triangle(int i1, int i2, int i3) //indecies need to be clockwise
		{ this.i1 = i1; this.i2 = i2; this.i3 = i3; }

		public virtual IEnumerator<int> GetEnumerator()
		{
			yield return i1;
			yield return i2;
			yield return i3;
		}

		IEnumerator IEnumerable.GetEnumerator()
		{ return GetEnumerator(); }
	}

	public class Quad : Triangle
	{
		readonly int i4;

		public Quad(int i1, int i2, int i3, int i4) //indecies need to be clockwise
			: base(i1, i2, i3) { this.i4 = i4; }

		public override IEnumerator<int> GetEnumerator ()
		{
			yield return i1;
			yield return i2;
			yield return i3;

			yield return i3;
			yield return i4;
			yield return i1;
		}
	}

	public class Basis
	{
		public readonly Vector3 x, y, z;
		public Basis(Vector3 x, Vector3 y, Vector3 z)
		{ this.x = x; this.y = y; this.z = z; }
	}
}

