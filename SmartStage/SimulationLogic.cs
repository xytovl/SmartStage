using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SmartStage
{
	class StageDescription
	{
		public double activationTime;
		public List<Part> stageParts;
		public StageDescription(double activationTime)
		{
			this.activationTime = activationTime;
			stageParts = new List<Part>();
		}
	}

	struct Sample
	{
		public double time;
		public double mass;
		public double altitude;
		public double velocity;
		public double acceleration;
		public double throttle;
	}

	class SimulationLogic
	{
		const double simulationStep = 0.1;

		readonly bool advancedSimulation;

		public List<StageDescription> stages = new List<StageDescription>();
		public List<Sample> samples = new List<Sample>();

		private SimulationState state;

		// Removes all descendants of the given part from the shipParts dictionary
		public void dropPartAndChildren(Part part)
		{
			state.availableNodes.Remove(part);
			foreach(Part child in part.children)
			{
				if (state.availableNodes.ContainsKey(child))
					dropPartAndChildren(child);
			}
		}

		public DState RungeKutta(ref SimulationState s, double dt)
		{
			DState ds1 = s.derivate();
			DState ds2 = s.increment(ds1, dt / 2).derivate();
			DState ds3 = s.increment(ds2, dt / 2).derivate();
			DState ds4 = s.increment(ds3, dt).derivate();

			s = s.increment(ds1, dt/6).increment(ds2, dt/3).increment(ds3, dt/3).increment(ds4, dt/6);
			return ds1;
		}

		public SimulationLogic(List<Part> parts, CelestialBody planet, double departureAltitude, double maxAcceleration, bool advancedSimulation, Vector3d forward)
		{
			this.advancedSimulation = advancedSimulation;
			state = new SimulationState(planet, departureAltitude, forward);
			state.maxAcceleration = maxAcceleration != 0 ? maxAcceleration : double.MaxValue;

			//Initialize first stage with available engines and launch clamps
			stages.Add(new StageDescription(0));
			foreach (Part p in parts)
			{
				if (p.Modules.OfType<LaunchClamp>().Count() > 0)
					stages[0].stageParts.Add(p);
				else
					state.availableNodes.Add(p, new Node(p, forward));
			}
			foreach (CompoundPart p in parts.OfType<CompoundPart>())
			{
				if (p.Modules.OfType<CompoundParts.CModuleFuelLine>().Count() > 0
					&& state.availableNodes.ContainsKey(p.target))
					state.availableNodes[p.target].linkedParts.Add(p.parent);
			}
			stages[0].stageParts.AddRange(state.updateEngines());
		}

		public void computeStages()
		{
			#if DEBUG
			DateTime startTime = DateTime.Now;
			#endif
			double elapsedTime = 0;
			while (state.availableNodes.Count() > 0)
			{
				if (advancedSimulation)
				{
					state.m = state.availableNodes.Sum(p => p.Value.mass);
					// Compute flow for active engines
					foreach (EngineWrapper e in state.activeEngines)
						e.evaluateFuelFlow(state.atmDensity, state.machNumber, state.throttle, false);
				}
				else
				{
					// Compute flow for active engines, in vacuum
					foreach (EngineWrapper e in state.activeEngines)
						e.evaluateFuelFlow(1, 1, 1, false);
				}

				double step = Math.Max(state.availableNodes.Min(node => node.Value.getNextEvent()), 1E-100);

				// Quit if there is no other event
				if (step == Double.MaxValue && state.throttle > 0)
					break;

				if (advancedSimulation)
				{
					if (step > simulationStep)
						step = Math.Max(simulationStep, (elapsedTime + step - stages.Last().activationTime) / 100);

					if (state.throttle == 0)
						step = simulationStep;

					float throttle = state.throttle;
					var savedState = state;
					DState dState = RungeKutta(ref state, step);
					while (Math.Abs(state.throttle - throttle) > 0.05 && step > 1e-3)
					{
						state = savedState;
						step /= 2;
						dState = RungeKutta(ref state, step);
					}
					Sample sample;
					sample.time = elapsedTime + step;
					sample.velocity = state.v_surf;
					sample.altitude = state.r - state.planet.Radius;
					sample.mass = state.m;
					sample.acceleration = Math.Sqrt(dState.ax_nograv * dState.ax_nograv + dState.ay_nograv * dState.ay_nograv);
					sample.throttle = state.throttle;
					if (samples.Count == 0 || samples.Last().time + simulationStep <= sample.time)
						samples.Add(sample);
				}
				elapsedTime += step;

				// Burn the fuel !
				bool eventHappens = false;
				foreach (Node node in state.availableNodes.Values)
				{
					eventHappens |= node.applyFuelConsumption(step);
				}

				if (!eventHappens)
					continue;

				// Add all decouplers in a new stage
				StageDescription dpStage = new StageDescription(elapsedTime);
				foreach (Node node in state.availableNodes.Values)
				{
					ModuleDecouple decoupler = node.part.Modules.OfType<ModuleDecouple>().FirstOrDefault();
					ModuleAnchoredDecoupler aDecoupler= node.part.Modules.OfType<ModuleAnchoredDecoupler>().FirstOrDefault();
					if ((decoupler != null || aDecoupler != null)&& !node.hasFuelInChildren(state.availableNodes))
					{
						dpStage.stageParts.Add(node.part);
					}
				}

				if (dpStage.stageParts.Count > 0)
				{
					stages.Add(dpStage);
					List<Part> activableChildren = new List<Part>();

					// Remove all decoupled elements, fire sepratrons and parachutes
					foreach (Part part in dpStage.stageParts)
					{
						if (state.availableNodes.ContainsKey(part))
						{
							activableChildren.AddRange(state.availableNodes[part].getRelevantChildrenOnDecouple(state.availableNodes));
							dropPartAndChildren(part);
						}
					}
					dpStage.stageParts.AddRange(activableChildren);
				}

				// Update available engines and fuel flow
				List<Part> activeEngines = state.updateEngines();

				StageDescription newStage = new StageDescription(elapsedTime);
				newStage.stageParts.AddRange(activeEngines);
				stages.Add (newStage);
			}

			// Put all remaining items (parachutes?) in a separate 0 stage
			foreach(var stage in stages)
			{
				foreach (var part in stage.stageParts)
					state.availableNodes.Remove(part);
			}

			// Put fairings in a separate stage before decoupling
			List<StageDescription> newStages = new List<StageDescription>();
			for (int i = 0; i < stages.Count; i++)
			{
				var fairings = stages[i].stageParts.Where(p => p.Modules.OfType<ModuleProceduralFairing>().Count() != 0);
				var notfairings = stages[i].stageParts.Where(p => p.Modules.OfType<ModuleProceduralFairing>().Count() == 0);

				if (fairings.Count() != 0)
				{
					StageDescription fairingStage = new StageDescription(stages[i].activationTime);
					fairingStage.stageParts.AddRange(fairings);
					newStages.Add(fairingStage);
				}

				StageDescription notfairingStage = new StageDescription(stages[i].activationTime);
				notfairingStage.stageParts.AddRange(notfairings);
				newStages.Add(notfairingStage);
			}
			stages = newStages;

			int initialStage = 0;
			foreach (Part part in state.availableNodes.Keys)
			{
				if (part.hasStagingIcon)
				{
					part.inverseStage = 0;
					initialStage = 1;
				}
			}

			// Set stage number correctly
			Staging.SetStageCount(stages.Count);
			for (int stage = stages.Count - 1 ; stage >= 0; stage--)
			{
				var currentStage = stages[stage];
				foreach(Part part in currentStage.stageParts)
				{
					part.inverseStage = stages.Count - 1 - stage + initialStage;
				}
			}

			Staging.SortIcons();

			#if DEBUG
			var compTime = DateTime.Now - startTime;
			Debug.Log("Staging computed in " + compTime.TotalMilliseconds + "ms");
			if (samples.Count() > 0)
			{
				string result = "time;altitude;velocity;acceleration;mass;throttle\n";
				foreach (var sample in samples)
				{
					result += sample.time + ";"+sample.altitude+";"+sample.velocity+";"+sample.acceleration+";"+sample.mass+";"+sample.throttle+"\n";
				}
				Debug.Log(result);
			}
			#endif
		}

		public static void inFlightComputeStages()
		{
			var vessel = FlightGlobals.ActiveVessel;
			(new SimulationLogic(vessel.parts, FlightGlobals.currentMainBody, vessel.altitude, 0, false, vessel.upAxis)).computeStages();
		}
	}
}

