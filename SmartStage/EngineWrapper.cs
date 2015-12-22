using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SmartStage
{
	public class EngineWrapper
	{
		ModuleEngines engine;
		Dictionary<Propellant, List<Node>> resources;
		Part part;

		public EngineWrapper(ModuleEngines e, Dictionary<Part,Node> availableNodes)
		{
			engine = e;
			part = e.part;
			updateTanks(availableNodes);
		}

		private float flowMultiplier(float atmDensity, float machNumber)
		{
			float res = 1;
			if (engine.atmChangeFlow)
			{
				if (engine.useAtmCurve)
					res = engine.atmCurve.Evaluate(atmDensity * 0.816326530612245f);
			}
			if (engine.useVelCurve)
				res *= engine.velCurve.Evaluate(machNumber);

			return Math.Max(res, engine.CLAMP);
		}

		public double evaluateFuelFlow(float atmDensity, float machNumber, float throttle, bool simulate = true)
		{
			if (engine.throttleLocked)
				throttle = 1;
			double flow = 0;

			// Check if we have all required propellants
			if (resources.All(pair => pair.Value.Count > 0))
			{
				double ratioSum = resources.Sum(pair => pair.Key.ratio);
				flow = 1000 * Mathf.Lerp(engine.minFuelFlow, engine.maxFuelFlow, throttle * engine.thrustPercentage / 100) * flowMultiplier(atmDensity, machNumber);

				if (! simulate)
				{
					foreach (KeyValuePair<Propellant, List<Node>> pair in resources)
					{
						double resourceFlow = flow * pair.Key.ratio / (ratioSum * pair.Value.Count);
						pair.Value.ForEach(tankNode => tankNode.resourceFlow[pair.Key.id] += resourceFlow);
					}
				}
			}
			return flow;
		}

		private void updateTanks(Dictionary<Part,Node> availableNodes)
		{
			// For each relevant propellant, get the list of tanks the engine will drain resources
			resources = engine.propellants.FindAll (
				prop => PartResourceLibrary.Instance.GetDefinition(prop.id).density > 0 && prop.name != "IntakeAir")
				.ToDictionary (
					prop => prop,
					prop => availableNodes[part].GetTanks(prop.id, availableNodes, new HashSet<Part>()));
		}

		public float thrust(float throttle, float pressurekPa, float machNumber, float atmDensity)
		{
			double fuelFlow = evaluateFuelFlow(atmDensity, machNumber, throttle);
			float isp = engine.atmosphereCurve.Evaluate(pressurekPa / (float)FlightGlobals.GetHomeBody().GetPressure(0));
			return (float)(fuelFlow * isp * 9.82);
		}
	}
}

