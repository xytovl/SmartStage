using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace SmartStage
{

	[KSPAddon(KSPAddon.Startup.EditorVAB, false)]
	public class SmartStage : MonoBehaviour
	{

		public SmartStage()
		{
		}

		void OnGUI()
		{
			if (GUI.Button(new Rect(Screen.width - 100, 50, 100, 20), "Smart stage"))
			{
				computeStages();
			}
		}

		// Let's say a sepratron is an engine with more than 45° inclination
		public static bool isSepratron(Part part)
		{
			if (part.Modules.OfType<ModuleEngines>().Count() == 0)
				return false;
			return Quaternion.Angle(part.attRotation, Quaternion.identity) >= 45;
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
				if ((node.part.Modules.OfType<ModuleEngines>().Count() > 0 && node.IsEngineActive() && ! isSepratron(node.part))
					|| node.part.Modules.OfType<LaunchClamp>().Count() > 0)
					stages[0].stageParts.Add(node.part);

			}

			while (true)
			{
				// Reset all propellant flow
				foreach (Node node in availableNodes.Values)
				{
					node.resetFlow();
				}

				// Compute flow for active engines
				foreach (Node node in availableNodes.Values)
				{
					ModuleEngines engine = node.part.Modules.OfType<ModuleEngines>().FirstOrDefault();
					//ModuleEnginesFX enginefx = part.Modules.OfType<ModuleEnginesFX>().Where(e => e.isEnabled).FirstOrDefault();
					if (engine != null && node.IsEngineActive())
					{
						// For each relevant propellant, get the list of tanks the engine will drain resources
						Dictionary<Propellant, List<Node>> resources = 
							engine.propellants.FindAll (
								prop => PartResourceLibrary.Instance.GetDefinition (prop.id).density > 0 && prop.name != "IntakeAir")
								.ToDictionary (
									prop => prop,
									prop => node.GetTanks (requestId++, prop.name));

						// Check if we have all required propellants
						if (resources.All(pair => pair.Value.Count > 0))
						{
							foreach (KeyValuePair<Propellant, List<Node>> pair in resources)
							{
								double rate = engine.maxThrust * engine.atmosphereCurve.Evaluate(0) * pair.Key.ratio / pair.Value.Count;
								pair.Value.ForEach(tankNode => tankNode.resourceFlow[pair.Key.name] += rate);
							}
						}
					}
				}

				// Find out when next event happens
				double nextEvent = Double.MaxValue;
				foreach (Node node in availableNodes.Values)
				{
					foreach (KeyValuePair<String,double> flowPair in node.resourceFlow)
					{
						if (flowPair.Value > Double.Epsilon)
							nextEvent = Math.Min(nextEvent, node.resourceMass[flowPair.Key] / flowPair.Value);
					}
				}

				// Quit if there is no other event
				if (nextEvent == Double.MaxValue)
					break;

				elapsedTime += nextEvent;

				// Burn the fuel !
				foreach (Node node in availableNodes.Values)
				{
					foreach (KeyValuePair<String,double> flowPair in node.resourceFlow)
					{
						node.resourceMass[flowPair.Key] -= flowPair.Value * nextEvent;
						if (node.resourceMass[flowPair.Key] < Double.Epsilon)
						{
							node.resourceMass[flowPair.Key] = 0d;
						}
					}
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
					if (node.part.Modules.OfType<ModuleEngines>().Count() > 0 && node.IsEngineActive() && ! isSepratron(node.part))
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

			public Dictionary<string, double> resourceMass;
			public Dictionary<string, double> resourceFlow;

			private Dictionary<Part,Node> shipParts;

			public Node(Part part, Dictionary<Part,Node> shipParts)
			{
				this.part = part;
				this.shipParts = shipParts;
				requestId = -1;
				// TODO: enlever les resources désactivées
				resourceMass = part.Resources.list.ToDictionary(x => x.resourceName, x => x.info.density * x.amount);
				resetFlow();
			}

			public void dropSelfAndChildren()
			{
				shipParts.Remove(part);
				foreach(Part child in part.children)
				{
					if (shipParts.ContainsKey(child))
						shipParts[child].dropSelfAndChildren();
				}
			}

			public void resetFlow()
			{
				resourceFlow = part.Resources.list.ToDictionary(x => x.resourceName, x => 0d);
			}

			public bool hasFuelInChildren()
			{
				if (resourceMass.Any(massPair => massPair.Value > 0.0001d) && ! isSepratron(part))
					return true;
				return part.children.Any(child => shipParts.ContainsKey(child) && shipParts[child].hasFuelInChildren());
			}

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

			public List<Node> GetTanks(int currentRequestIdentifier, string resource)
			{
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
						result.AddRange(shipParts[p.parent].GetTanks(currentRequestIdentifier, resource));
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
							result.AddRange(shipParts[i.attachedPart].GetTanks(currentRequestIdentifier, resource));
						}
					}
				}

				if (result.Count > 0)
					return result;

				// Rule 5
				if (resourceMass.ContainsKey(resource) && resourceMass[resource] > 0)
					return new List<Node> { this };

				// Rule 6
				if (resourceMass.ContainsKey(resource) && resourceMass[resource] <= 0)
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
							return shipParts[i.attachedPart].GetTanks(currentRequestIdentifier, resource);
						}
					}
				}

				// Rule 8
				return new List<Node>();
			}
		
			public bool IsEngineActive()
			{
				return part.attachNodes.All(x => x.id != "bottom" || x.attachedPart == null || !shipParts.ContainsKey(x.attachedPart));
			}
		}
	}
}

