using System;
using System.Collections.Generic;
using System.Linq;

namespace SmartStage
{
	public class EngineWrapper
	{
		bool throttleLocked;
		float maxThrust;
		float thrustPercentage;
		FloatCurve atmosphereCurve;
		List<Propellant> propellants;
		Dictionary<Propellant, List<Node>> resources;
		Part part;

		public EngineWrapper(ModuleEngines e, Dictionary<Part,Node> availableNodes)
		{
			throttleLocked = e.throttleLocked;
			maxThrust = e.maxThrust;
			thrustPercentage = e.thrustPercentage;
			atmosphereCurve = e.atmosphereCurve;
			propellants = e.propellants;
			part = e.part;
			updateTanks(availableNodes);
		}
		public EngineWrapper(ModuleEnginesFX e, Dictionary<Part,Node> availableNodes)
		{
			throttleLocked = e.throttleLocked;
			maxThrust = e.maxThrust;
			thrustPercentage = e.thrustPercentage;
			atmosphereCurve = e.atmosphereCurve;
			propellants = e.propellants;
			part = e.part;
			updateTanks(availableNodes);
		}

		public double evaluateFuelFlow(float pressure, float throttle, bool simulate = true)
		{
			double totalRate = 0;

			// Check if we have all required propellants
			if (resources.All(pair => pair.Value.Count > 0))
			{
				double ratioSum = resources.Sum(pair => pair.Key.ratio);
				foreach (KeyValuePair<Propellant, List<Node>> pair in resources)
				{
					// KSP gravity is 9.82 m/s²
					float isp = atmosphereCurve.Evaluate(pressure);
					double rate = thrust(throttle) * pair.Key.ratio / (9.82 * isp * pair.Value.Count * ratioSum);
					totalRate += rate * pair.Value.Count;
					if (! simulate)
						pair.Value.ForEach(tankNode => tankNode.resourceFlow[pair.Key.id] += rate);
				}
			}
			return totalRate;
		}

		private void updateTanks(Dictionary<Part,Node> availableNodes)
		{
			// For each relevant propellant, get the list of tanks the engine will drain resources
			resources = propellants.FindAll (
				prop => PartResourceLibrary.Instance.GetDefinition(prop.id).density > 0 && prop.name != "IntakeAir")
				.ToDictionary (
					prop => prop,
					prop => availableNodes[part].GetTanks(prop.id, availableNodes, new HashSet<Part>()));
			if (resources.Any(p => p.Value.Count == 0))
				maxThrust = 0;
		}

		public float thrust(float throttle)
		{
			if (throttleLocked)
				throttle = thrustPercentage/100;
			return 1000 * maxThrust * throttle;
		}
	}
}

