using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SmartStage
{
	public class Node
	{
		public Part part;

		private Dictionary<int, double> resourceMass;
		public Dictionary<int, double> resourceFlow;

		private double baseMass;

		public Node(Part part)
		{
			this.part = part;
			resourceMass = part.Resources.list.ToDictionary(x => x.info.id, x => x.enabled ? x.info.density * x.amount * 1000 : 0);
			resourceFlow = part.Resources.list.ToDictionary(x => x.info.id, x => 0d);
			baseMass = 1000 * part.mass + part.Resources.list.Sum(x => x.enabled ? 0 : x.info.density * x.amount * 1000);
		}

		public double mass { get
			{
				return baseMass + resourceMass.Sum(p => p.Value);
			}
		}

		// Compute the delay until a depletion event occurs in the node according to the computed fuel flow
		public double getNextEvent()
		{
			if (resourceFlow.Count() == 0)
				return Double.MaxValue;
			return resourceFlow.Min(flowPair => flowPair.Value > Double.Epsilon ?
				resourceMass[flowPair.Key] / flowPair.Value :
				Double.MaxValue);
		}

		// Burn fuel in the part using the stored fuelflow value
		public bool applyFuelConsumption(double time)
		{
			bool depleted = false;
			foreach (KeyValuePair<int,double> flowPair in resourceFlow)
			{
				if (resourceMass[flowPair.Key] == 0 || flowPair.Value == 0)
					continue;
				resourceMass[flowPair.Key] -= flowPair.Value * time;
				if (resourceMass[flowPair.Key] <= 0)
				{
					resourceMass[flowPair.Key] = 0d;
					depleted = true;
				}
			}
			resourceFlow = part.Resources.list.ToDictionary(x => x.info.id, x => 0d);
			return depleted;
		}

		// Returns true if any of the descendant still in the shipParts dictionary has fuel and is not a sepratron
		public bool hasFuelInChildren(Dictionary<Part,Node> availableNodes)
		{
			if (resourceMass.Any(massPair => massPair.Value > 0.1d
				&& (PartResourceLibrary.Instance.GetDefinition(massPair.Key).resourceFlowMode == ResourceFlowMode.STACK_PRIORITY_SEARCH
					|| PartResourceLibrary.Instance.GetDefinition(massPair.Key).resourceFlowMode == ResourceFlowMode.NO_FLOW)
			)
				&& ! isSepratron(part))
				return true;
			return part.children.Any(child => availableNodes.ContainsKey(child) && availableNodes[child].hasFuelInChildren(availableNodes));
		}

		// Returns the list of descendants in the shipParts dictionary that are considered sepratrons
		public List<Part> getSepratronChildren(Dictionary<Part,Node> availableNodes)
		{
			List<Part> result = new List<Part>();
			if (isSepratron(part))
				result.Add(part);
			foreach(Part child in part.children)
			{
				if (availableNodes.ContainsKey(child))
					result.AddRange(availableNodes[child].getSepratronChildren(availableNodes));
			}
			return result;
		}

		// Follows fuel flow for a given propellant (by name) and returns the list of parts from which resources
		// will be drained
		public List<Node> GetTanks(int propellantId, Dictionary<Part,Node> availableNodes, HashSet<Part> visitedTanks)
		{
			if (PartResourceLibrary.Instance.GetDefinition(propellantId).resourceFlowMode == ResourceFlowMode.NO_FLOW)
			{
				if (resourceMass.ContainsKey(propellantId) && resourceMass[propellantId] <= 0)
					return new List<Node>();
				return new List<Node> {this};
			}

			List<Node> result = new List<Node>();

			// Rule 1
			if (visitedTanks.Contains(part))
				return result;

			visitedTanks.Add(part);

			// Rule 2
			foreach (Part p in availableNodes.Keys)
			{
				if (p is FuelLine && ((FuelLine)p).target == part)
				{
					result.AddRange(availableNodes[p.parent].GetTanks(propellantId, availableNodes, visitedTanks));
				}
			}

			if (result.Count > 0)
				return result;

			// Rule 3
			// There is no rule 3

			// Rule 4
			if (part.fuelCrossFeed)
			{
				foreach (AttachNode i in part.attachNodes)
				{
					if (i != null && i.attachedPart != null &&
						availableNodes.ContainsKey(i.attachedPart) &&
						i.nodeType == AttachNode.NodeType.Stack &&
						i.id != "Strut" &&
						!(part.NoCrossFeedNodeKey.Length > 0 && i.id.Contains(part.NoCrossFeedNodeKey)))
					{
						result.AddRange(availableNodes[i.attachedPart].GetTanks(propellantId, availableNodes, visitedTanks));
					}
				}
			}

			if (result.Count > 0)
				return result;

			// Rule 5
			if (resourceMass.ContainsKey(propellantId) && resourceMass[propellantId] > 0)
				return new List<Node> { this };

			// Rule 6
			if (resourceMass.ContainsKey(propellantId) && resourceMass[propellantId] <= 0)
				return new List<Node>();

			// Rule 7
			if (part.fuelCrossFeed)
			{
				foreach (AttachNode i in part.attachNodes)
				{
					if (i != null && i.attachedPart != null &&
						i.attachedPart == part.parent &&
						i.nodeType == AttachNode.NodeType.Surface &&
						availableNodes.ContainsKey(i.attachedPart))
					{
						return availableNodes[i.attachedPart].GetTanks(propellantId, availableNodes, visitedTanks);
					}
				}
			}

			// Rule 8
			return new List<Node>();
		}

		// Returns true if the exhaust from this engine collides with other parts
		private bool exhaustDamagesAPart(List<Transform> thrusts, Dictionary<Part,Node> availableNodes)
		{
			foreach (var thrust in thrusts)
			{
				var hits = Physics.RaycastAll(thrust.position, thrust.forward, 10f);
				foreach (var hit in hits)
				{
					Part target = Part.GetComponentUpwards<Part>(hit.collider.gameObject);
					if (target != null && availableNodes.ContainsKey(target))
						return true;
				}
			}
			return false;
		}

		//Returns true if the part is an engine and should be turned on according to remaining parts
		public bool isActiveEngine(Dictionary<Part,Node> availableNodes)
		{
			if (part.Modules.OfType<ModuleEngines>().Count() == 0 && part.Modules.OfType<ModuleEnginesFX>().Count() == 0)
				return false;

			if (part.Modules.OfType<ModuleEngines>().Any(x => exhaustDamagesAPart(x.thrustTransforms, availableNodes))
				|| part.Modules.OfType<ModuleEnginesFX>().Any(x => exhaustDamagesAPart(x.thrustTransforms, availableNodes)))
				return false;

			return part.attachNodes.All(x => (x.id != "bottom" || x.attachedPart == null || !availableNodes.ContainsKey(x.attachedPart)));
		}

		// Let's say a sepratron is an engine with more than 45° inclination
		public static bool isSepratron(Part part)
		{
			if (part.Modules.OfType<ModuleEngines>().Count() == 0 && part.Modules.OfType<ModuleEnginesFX>().Count() == 0 )
				return false;

			Vector3 thrust = Vector3d.zero;
			int numTransforms = 0;
			foreach (var e in part.Modules.OfType<ModuleEngines>())
			{
				numTransforms += e.thrustTransforms.Count;
				foreach (var t in e.thrustTransforms)
					thrust -= t.forward;
			}
			foreach (var e in part.Modules.OfType<ModuleEnginesFX>())
			{
				numTransforms += e.thrustTransforms.Count;
				foreach (var t in e.thrustTransforms)
					thrust -= t.forward;
			}

			return Vector3.Dot(Vector3d.up, thrust/numTransforms) <= 0.8;
		}
	}
}

