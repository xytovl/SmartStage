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

		// List of nodes that point to the current node
		public List<Part> linkedParts = new List<Part>();

		// For engines, parts hit by thrust raycast
		private List<Part> raycastHit = new List<Part>();

		private double baseMass;
		public readonly bool isSepratron;

		public Node(Part part, Vector3d forward)
		{
			this.part = part;
			isSepratron = IsSepratron(forward);
			resourceMass = part.Resources.list.ToDictionary(x => x.info.id, x => x.enabled ? x.info.density * x.amount * 1000 : 0);
			resourceFlow = part.Resources.list.ToDictionary(x => x.info.id, x => 0d);
			if (part.physicalSignificance != Part.PhysicalSignificance.NONE && part.PhysicsSignificance != 1)
				baseMass = 1000 * part.mass + part.Resources.list.Sum(x => x.enabled ? 0 : x.info.density * x.amount * 1000);
			else
				baseMass = 0;

			foreach (var e in part.Modules.OfType<ModuleEngines>())
				computeRaycastHits(e.thrustTransforms);
			foreach (var e in part.Modules.OfType<ModuleEnginesFX>())
				computeRaycastHits(e.thrustTransforms);
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
		public bool hasFuelInChildren(Dictionary<Part,Node> availableNodes, HashSet<Part> visitedParts = null)
		{
			visitedParts = visitedParts ?? new HashSet<Part>();
			if (!visitedParts.Add(part))
				return false;
			if (resourceMass.Any(massPair => massPair.Value > 0.1d
				&& (PartResourceLibrary.Instance.GetDefinition(massPair.Key).resourceFlowMode == ResourceFlowMode.STACK_PRIORITY_SEARCH
					|| PartResourceLibrary.Instance.GetDefinition(massPair.Key).resourceFlowMode == ResourceFlowMode.NO_FLOW)
			)
				&& ! isSepratron)
				return true;
			return part.children.Any(child => availableNodes.ContainsKey(child) && availableNodes[child].hasFuelInChildren(availableNodes, visitedParts));
		}

		// Returns the list of descendants in the shipParts dictionary that should be activated when node is decoupled
		public List<Part> getRelevantChildrenOnDecouple(Dictionary<Part,Node> availableNodes, HashSet<Part> visitedParts = null)
		{
			List<Part> result = new List<Part>();

			// break cycles
			visitedParts = visitedParts ?? new HashSet<Part>();
			if (!visitedParts.Add(part))
				return result;

			if (part.hasStagingIcon)
				result.Add(part);
			foreach(Part child in part.children)
			{
				if (availableNodes.ContainsKey(child))
					result.AddRange(availableNodes[child].getRelevantChildrenOnDecouple(availableNodes, visitedParts));
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
			foreach (Part p in linkedParts)
			{
				if (availableNodes.ContainsKey(p))
					result.AddRange(availableNodes[p].GetTanks(propellantId, availableNodes, visitedTanks));
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
				if (part.attachMode == AttachModes.SRF_ATTACH && part.parent != null && availableNodes.ContainsKey(part.parent))
					return availableNodes[part.parent].GetTanks(propellantId, availableNodes, visitedTanks);
			}

			// Rule 8
			return new List<Node>();
		}

		private void computeRaycastHits(List<Transform> thrusts)
		{
			foreach (var thrust in thrusts)
			{
				var hits = Physics.RaycastAll(thrust.position, thrust.forward, 10f);
				raycastHit.AddRange(
					hits.Select(hit => Part.GetComponentUpwards<Part>(hit.collider.gameObject))
						.Where(x => x != null));
			}
		}

		// Returns true if the exhaust from this engine collides with other parts
		private bool exhaustDamagesAPart(Dictionary<Part,Node> availableNodes)
		{
			if (raycastHit.Count() == 0)
				return false;
			return raycastHit.Any(p => availableNodes.ContainsKey(p));
		}

		//Returns true if the part is an engine and should be turned on according to remaining parts
		public bool isActiveEngine(Dictionary<Part,Node> availableNodes)
		{
			if (part.Modules.OfType<ModuleEngines>().Count() == 0 && part.Modules.OfType<ModuleEnginesFX>().Count() == 0)
				return false;

			if (exhaustDamagesAPart(availableNodes))
				return false;

			return part.attachNodes.All(x => (x.id != "bottom" || x.attachedPart == null || !availableNodes.ContainsKey(x.attachedPart)));
		}

		// Let's say a sepratron is an engine with more than 45° inclination
		bool IsSepratron(Vector3d forward)
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

			return Vector3.Dot(forward, thrust/numTransforms) <= 0.8;
		}
	}
}

