using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace SmartStage
{

	[KSPAddon(KSPAddon.Startup.EditorVAB, false)]
	public class SmartStage : MonoBehaviour
	{

		private IButton stageButton;

		public SmartStage()
		{
			if (ToolbarManager.ToolbarAvailable)
			{
				stageButton = ToolbarManager.Instance.add("SmartStage", "stageButton");
				stageButton.TexturePath = "SmartStage/SmartStage";
				stageButton.ToolTip = "Automatically calculate ship stages";
				stageButton.OnClick += (e) => computeStages();
			}
		}

		internal void onDestroy()
		{
			if (stageButton != null)
				stageButton.Destroy();
		}

		void OnGUI()
		{
			// Do not draw button ourself if toolbar is available
			if (ToolbarManager.ToolbarAvailable)
				return;
			if (GUI.Button(new Rect(Screen.width - 100, 50, 100, 20), "Smart stage"))
			{
				computeStages();
			}
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

		public void computeStages()
		{
			int requestId = 0;
			Dictionary<Part,Node> availableNodes = new Dictionary<Part, Node>();
			foreach (Part p in EditorLogic.fetch.ship.parts)
				availableNodes.Add(p, new Node(p, availableNodes));

			List<StageDescription> stages = new List<StageDescription>();
			double elapsedTime = 0;

			//Initialize first stage with available engines and launch clamps
			stages.Add(new StageDescription(0));
			foreach(Node node in availableNodes.Values)
			{
				if ((node.isActiveEngine() && ! isSepratron(node.part))
					|| node.part.Modules.OfType<LaunchClamp>().Count() > 0)
					stages[0].stageParts.Add(node.part);

			}

			while (true)
			{
				// Reset all propellant flow
				foreach (Node node in availableNodes.Values)
					node.resetFlow();

				// Compute flow for active engines
				foreach (Node node in availableNodes.Values)
					requestId = node.evaluateFuelFlow(requestId);

				// Find out when next event happens
				if (availableNodes.Count() == 0)
					break;
				double nextEvent = availableNodes.Min(node => node.Value.getNextEvent());

				// Quit if there is no other event
				if (nextEvent == Double.MaxValue)
					break;

				elapsedTime += nextEvent;

				// Burn the fuel !
				foreach (Node node in availableNodes.Values)
				{
					node.applyFuelConsumption(nextEvent);
				}

				// Add all decouplers in a new stage
				StageDescription newStage = new StageDescription(elapsedTime);
				foreach (Node node in availableNodes.Values)
				{
					ModuleDecouple decoupler = node.part.Modules.OfType<ModuleDecouple>().FirstOrDefault();
					ModuleAnchoredDecoupler aDecoupler= node.part.Modules.OfType<ModuleAnchoredDecoupler>().FirstOrDefault();
					if ((decoupler != null || aDecoupler != null)&& !node.hasFuelInChildren())
					{
						newStage.stageParts.Add(node.part);
						// TODO: add sepratrons here
					}
				}

				if (newStage.stageParts.Count > 0)
					stages.Add(newStage);
				List<Part> sepratrons = new List<Part>();

				// Remove all decoupled elements, fire sepratrons
				foreach(Part part in newStage.stageParts)
				{
					if (availableNodes.ContainsKey(part))
					{
						sepratrons.AddRange(availableNodes[part].getSepratronChildren());
						availableNodes[part].dropSelfAndChildren();
					}
				}
				newStage.stageParts.AddRange(sepratrons);

				// Fire some engines
				foreach(Node node in availableNodes.Values)
				{
					if (node.isActiveEngine() && ! isSepratron(node.part))
						newStage.stageParts.Add(node.part);
				}

			}

			// Set stage number correctly
			Staging.SetStageCount(stages.Count);
			for (int stage = 0 ; stage < stages.Count; stage++)
			{
				var currentStage = stages[stages.Count - stage - 1];
				foreach(Part part in currentStage.stageParts)
				{
					part.inverseStage = stage;
				}
			}
			Staging.SortIcons();
		}

		private class StageDescription
		{
			public double activationTime;
			public List<Part> stageParts;
			public StageDescription(double activationTime)
			{
				this.activationTime = activationTime;
				stageParts = new List<Part>();
			}
		}

		private class Node
		{
			// Identifier of the request to check if we have already been visited
			int requestId;
			public Part part;

			private Dictionary<int, double> resourceMass;
			private Dictionary<int, double> resourceFlow;

			private Dictionary<Part,Node> shipParts;

			public Node(Part part, Dictionary<Part,Node> shipParts)
			{
				this.part = part;
				this.shipParts = shipParts;
				requestId = -1;
				// TODO: enlever les resources désactivées
				resourceMass = part.Resources.list.ToDictionary(x => x.info.id, x => x.info.density * x.amount);
				resetFlow();
			}

			//Helper method to unify ModuleEngines and ModuleEnginesFX
			private int evaluateFuelFlow(List<Propellant> propellants, float maxThrust, FloatCurve atmosphereCurve, int currentRequestIdentifier)
			{
				// For each relevant propellant, get the list of tanks the engine will drain resources
				Dictionary<Propellant, List<Node>> resources = 
					propellants.FindAll (
						prop => PartResourceLibrary.Instance.GetDefinition(prop.id).density > 0 && prop.name != "IntakeAir")
						.ToDictionary (
							prop => prop,
							prop => GetTanks (currentRequestIdentifier++, prop.id));

				// Check if we have all required propellants
				if (resources.All(pair => pair.Value.Count > 0))
				{
					foreach (KeyValuePair<Propellant, List<Node>> pair in resources)
					{
						double rate = maxThrust * atmosphereCurve.Evaluate(0) * pair.Key.ratio / pair.Value.Count;
						pair.Value.ForEach(tankNode => tankNode.resourceFlow[pair.Key.id] += rate);
					}
				}
				return currentRequestIdentifier;
			}

			// Follows fuel flow for each engine propellant and adjust consumption rate in the affected tanks
			// Must be provided an unused request identifier, which will be incremented and returned
			public int evaluateFuelFlow(int currentRequestIdentifier)
			{
				if (!isActiveEngine())
					return currentRequestIdentifier;
				foreach(ModuleEngines engine in part.Modules.OfType<ModuleEngines>())
					currentRequestIdentifier = evaluateFuelFlow(engine.propellants, engine.maxThrust, engine.atmosphereCurve, currentRequestIdentifier);
				foreach(ModuleEnginesFX engine in part.Modules.OfType<ModuleEnginesFX>())
					currentRequestIdentifier = evaluateFuelFlow(engine.propellants, engine.maxThrust, engine.atmosphereCurve, currentRequestIdentifier);
				return currentRequestIdentifier;
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
			public void applyFuelConsumption(double time)
			{
				foreach (KeyValuePair<int,double> flowPair in resourceFlow)
				{
					resourceMass[flowPair.Key] -= flowPair.Value * time;
					if (resourceMass[flowPair.Key] < Double.Epsilon)
					{
						resourceMass[flowPair.Key] = 0d;
					}
				}
			}

			// Removes all descendants of the given part from the shipParts dictionary
			public void dropSelfAndChildren()
			{
				shipParts.Remove(part);
				foreach(Part child in part.children)
				{
					if (shipParts.ContainsKey(child))
						shipParts[child].dropSelfAndChildren();
				}
			}

			// Sets fuel consumption to 0 on the given part
			public void resetFlow()
			{
				resourceFlow = part.Resources.list.ToDictionary(x => x.info.id, x => 0d);
			}

			// Returns true if any of the descendant still in the shipParts dictionary has fuel and is not a sepratron
			public bool hasFuelInChildren()
			{
				if (resourceMass.Any(massPair => massPair.Value > 0.0001d) && ! isSepratron(part))
					return true;
				return part.children.Any(child => shipParts.ContainsKey(child) && shipParts[child].hasFuelInChildren());
			}

			// Returns the list of descendants in the shipParts dictionary that are considered sepratrons
			public List<Part> getSepratronChildren()
			{
				List<Part> result = new List<Part>();
				if (isSepratron(part))
					result.Add(part);
				foreach(Part child in part.children)
				{
					if (shipParts.ContainsKey(child))
						result.AddRange(shipParts[child].getSepratronChildren());
				}
				return result;
			}

			// Follows fuel flow for a given propellant (by name) and returns the list of parts from which resources
			// will be drained
			private List<Node> GetTanks(int currentRequestIdentifier, int propellantId)
			{
				if (PartResourceLibrary.Instance.GetDefinition(propellantId).resourceFlowMode == ResourceFlowMode.NO_FLOW)
				{
					if (resourceMass.ContainsKey(propellantId) && resourceMass[propellantId] <= 0)
						return new List<Node>();
					return new List<Node> {this};
				}

				List<Node> result = new List<Node>();

				// Rule 1
				if (currentRequestIdentifier == requestId)
					return result;

				requestId = currentRequestIdentifier;

				// Rule 2
				foreach (Part p in shipParts.Keys)
				{
					if (p is FuelLine && ((FuelLine)p).target == part)
					{
						result.AddRange(shipParts[p.parent].GetTanks(currentRequestIdentifier, propellantId));
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
							shipParts.ContainsKey(i.attachedPart) &&
							i.nodeType == AttachNode.NodeType.Stack &&
							i.id != "Strut" &&
							!(part.NoCrossFeedNodeKey.Length > 0 && i.id.Contains(part.NoCrossFeedNodeKey)))
						{
							result.AddRange(shipParts[i.attachedPart].GetTanks(currentRequestIdentifier, propellantId));
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
							shipParts.ContainsKey(i.attachedPart))
						{
							return shipParts[i.attachedPart].GetTanks(currentRequestIdentifier, propellantId);
						}
					}
				}

				// Rule 8
				return new List<Node>();
			}
		
			//Returns true if the part is an engine and should be turned on according to remaining parts
			public bool isActiveEngine()
			{
				if (part.Modules.OfType<ModuleEngines>().Count() == 0 && part.Modules.OfType<ModuleEnginesFX>().Count() == 0)
					return false;
				return part.attachNodes.All(x => (x.id != "bottom" || x.attachedPart == null || !shipParts.ContainsKey(x.attachedPart)));
			}
		}
	}
}

