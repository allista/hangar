using System.Linq;
using UnityEngine;

namespace AtHangar
{
	class AnimatedNode
	{
		readonly AttachNode node;
		readonly Transform nT;
		readonly Transform pT;
		readonly Part part;
		Part attached_part;
		AttachNode attached_node;

		public AnimatedNode(AttachNode node, Transform node_transform, Part part)
		{ 
			this.node = node;
			this.part = part;
			nT        = node_transform; 
			pT        = part.partTransform; 
		}

		//move parts proportionally to the masses
		void UpdatePartsPos()
		{
			if(attached_part == null || attached_node == null) return;
			var dp =
				part.transform.TransformPoint(node.position) -
				attached_part.transform.TransformPoint(attached_node.position);
			Part root  = part.RootPart();
			Vessel vsl = root.vessel;
			float total_mass = root.MassWithChildren();
			float this_mass, attached_mass;
			if(attached_part == part.parent) 
			{
				this_mass = part.MassWithChildren();
				part.transform.position -= dp;
				if(vsl != null) vsl.SetPosition(vsl.transform.position+dp*(this_mass/total_mass));
				else root.transform.position += dp*(this_mass/total_mass);
			} 
			else 
			{
				attached_mass = attached_part.MassWithChildren();
				attached_part.transform.position += dp;
				if(vsl != null) vsl.SetPosition(vsl.transform.position-dp*(attached_mass/total_mass));
				else root.transform.position -= dp*(attached_mass/total_mass);
			}
		}

		bool UpdateJoint()
		{
			if(attached_part == null || attached_node == null) return false;
			ConfigurableJoint joint = part.GetComponents<ConfigurableJoint>().FirstOrDefault(j => j.connectedBody == attached_part.Rigidbody);
			bool update_anchor = true;
			if(joint == null) 
			{
				joint = attached_part.GetComponents<ConfigurableJoint>().FirstOrDefault(j => j.connectedBody == part.Rigidbody);
				update_anchor = false;
			}
			if(joint == null) return false; 
			//setup joint
			joint.xMotion = ConfigurableJointMotion.Limited;
			joint.yMotion = ConfigurableJointMotion.Limited;
			joint.zMotion = ConfigurableJointMotion.Limited;
			//move anchors
			if(update_anchor)
			{ 
				joint.anchor = node.position;
				joint.connectedAnchor = attached_node.position;
			}
			else 
			{ 
				joint.connectedAnchor = node.position;
				joint.anchor = attached_node.position;
			}
			return true;
		}

		public void UpdateNode()
		{
			//update node
			node.size = 0; //force node size to be zero; otherwise the Kraken comes when inflating
			node.position = pT.InverseTransformPoint(nT.position);
			node.originalPosition = node.position;
			//update attached parts
			attached_part = node.attachedPart;
			if(attached_part != null) 
				attached_node = attached_part.findAttachNodeByPart(part);
			if(!UpdateJoint()) UpdatePartsPos();
		}

		#if DEBUG
		public void DrawAnchor()
		{
			Part a_part = node.attachedPart;
			if(a_part == null) return;
			Joint joint = part.GetComponents<Joint>().FirstOrDefault(j => j.connectedBody == a_part.Rigidbody);
			bool update_anchor = true;
			if(joint == null) 
			{
				joint = a_part.GetComponents<Joint>().FirstOrDefault(j => j.connectedBody == part.Rigidbody);
				update_anchor = false;
			}
			if(joint == null) return;
			if(update_anchor)
			{
				Metric.DrawPoint(joint.anchor, pT, Color.red);
				Metric.DrawPoint(joint.connectedAnchor, a_part.transform, Color.green);
			}
			else
			{
				Metric.DrawPoint(joint.connectedAnchor, pT, Color.red);
				Metric.DrawPoint(joint.anchor, a_part.transform, Color.green);
			}
		}
		#endif
	}
}

