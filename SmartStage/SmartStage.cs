using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SmartStage
{

	[KSPAddon(KSPAddon.Startup.EditorVAB, true)]
	public class SmartStage : MonoBehaviour
	{

		private ApplicationLauncherButton stageButton;

		public SmartStage()
		{
			GameEvents.onGUIApplicationLauncherReady.Add(addButton);
			addButton();
		}

		private void addButton()
		{
			removeButton();

			stageButton = ApplicationLauncher.Instance.AddModApplication(
				() => computeStages(),null,
				null, null,
				null, null,
				ApplicationLauncher.AppScenes.VAB | ApplicationLauncher.AppScenes.SPH,
				GameDatabase.Instance.GetTexture("SmartStage/SmartStage38", false));
		}

		private void removeButton()
		{
			if (stageButton != null)
				ApplicationLauncher.Instance.RemoveModApplication(stageButton);
		}

		private void onDestroy()
		{
			removeButton();
		}

		public static void computeStages()
		{
			Ship ship = new Ship(EditorLogic.fetch.ship, Planetarium.fetch.Home, 68, true, 40);
			ship.computeStages();
		}

		private class Ship
		{
			List<StageDescription> stages = new List<StageDescription>();

			const double simulationStep = 1;
			private SimulationState state;

			List<Sample> samples = new List<Sample>();

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

			public SimulationState RungeKutta(SimulationState s, double dt)
			{
				DState ds1 = s.derivate();
				DState ds2 = s.increment(ds1, dt / 2).derivate();
				DState ds3 = s.increment(ds2, dt / 2).derivate();
				DState ds4 = s.increment(ds3, dt).derivate();

				return s.increment(ds1, dt/6).increment(ds2, dt/3).increment(ds3, dt/3).increment(ds4, dt/6);
			}

			public Ship(ShipConstruct stockShip, CelestialBody planet, double departureAltitude, bool limitToTerminalVelocity, double maxAcceleration)
			{
				state = new SimulationState(planet, departureAltitude);
				state.limitToTerminalVelocity = limitToTerminalVelocity;
				state.maxAcceleration = maxAcceleration;

				//Initialize first stage with available engines and launch clamps
				stages.Add(new StageDescription(0));
				foreach (Part p in stockShip.parts)
				{
					if (p.Modules.OfType<LaunchClamp>().Count() > 0)
						stages[0].stageParts.Add(p);
					else
						state.availableNodes.Add(p, new Node(p));
				}
				stages[0].stageParts.AddRange(state.updateEngines());

			}

			public void computeStages()
			{
				double elapsedTime = 0;
				while (state.availableNodes.Count() > 0)
				{
					state.m = state.availableNodes.Sum(p => p.Value.mass);
					state.Cx = state.availableNodes.Sum(p => p.Value.mass * p.Value.part.maximum_drag) / state.m;
					float altitude = (float) (state.r - state.planet.Radius);
					// Compute flow for active engines
					foreach (EngineWrapper e in state.activeEngines)
						e.evaluateFuelFlow(state.planet.pressureCurve.Evaluate(altitude), state.throttle, false);
						
					double step = Math.Max(state.availableNodes.Min(node => node.Value.getNextEvent()), 1E-100);

					// Quit if there is no other event
					if (step == Double.MaxValue && state.throttle > 0)
						break;

					if (step > simulationStep)
						step = Math.Max(simulationStep, (elapsedTime + step - stages.Last().activationTime) / 100);

					elapsedTime += step;

					double vx = state.vx;
					double vy = state.vy;
					state = RungeKutta(state, step);
					state.derivate(); // Compute updated throttle
					Sample sample;
					sample.time = elapsedTime;
					sample.velocity = Math.Sqrt(state.v_surf_x * state.v_surf_x + state.v_surf_y * state.v_surf_y) ;
					sample.altitude = state.r - state.planet.Radius;
					sample.mass = state.m;
					sample.acceleration = Math.Sqrt((state.vx - vx) * (state.vx - vx) + (state.vy - vy) * (state.vy - vy)) / step;
					sample.throttle = state.throttle;
					samples.Add(sample);

					// Burn the fuel !
					bool eventHappens = false;
					foreach (Node node in state.availableNodes.Values)
					{
						eventHappens |= node.applyFuelConsumption(step);
					}

					if (!eventHappens)
						continue;

					// Add all decouplers in a new stage
					StageDescription newStage = new StageDescription(elapsedTime);
					foreach (Node node in state.availableNodes.Values)
					{
						ModuleDecouple decoupler = node.part.Modules.OfType<ModuleDecouple>().FirstOrDefault();
						ModuleAnchoredDecoupler aDecoupler= node.part.Modules.OfType<ModuleAnchoredDecoupler>().FirstOrDefault();
						if ((decoupler != null || aDecoupler != null)&& !node.hasFuelInChildren(state.availableNodes))
						{
							newStage.stageParts.Add(node.part);
						}
					}

					if (newStage.stageParts.Count > 0)
						stages.Add(newStage);
					List<Part> sepratrons = new List<Part>();

					// Remove all decoupled elements, fire sepratrons
					foreach(Part part in newStage.stageParts)
					{
						if (state.availableNodes.ContainsKey(part))
						{
							sepratrons.AddRange(state.availableNodes[part].getSepratronChildren(state.availableNodes));
							dropPartAndChildren(part);
						}
					}
					newStage.stageParts.AddRange(sepratrons);
					// Update available engines and fuel flow
					List<Part> activeEngines = state.updateEngines();

					if (newStage.stageParts.Count > 0)
						newStage.stageParts.AddRange(activeEngines);

				}

				// Put all parachutes in a separate 0 stage
				int initialStage = 0;
				foreach (Part part in EditorLogic.fetch.ship.parts)
				{
					if (part.Modules.OfType<ModuleParachute>().Count() > 0)
					{
						part.inverseStage = 0;
						initialStage = 1;
					}
				}

				// Set stage number correctly
				Staging.SetStageCount(stages.Count);
				for (int stage = 0 ; stage < stages.Count; stage++)
				{
					var currentStage = stages[stages.Count - stage - 1];
					foreach(Part part in currentStage.stageParts)
					{
						part.inverseStage = stage + initialStage;
					}
				}

				Staging.SortIcons();

				#if DEBUG
				string result = "time;altitude;velocity;acceleration;mass;throttle\n";
				foreach (var sample in samples)
				{
					result += sample.time + ";"+sample.altitude+";"+sample.velocity+";"+sample.acceleration+";"+sample.mass+";"+sample.throttle+"\n";
				}
				Debug.Log(result);
				#endif
			}
		}
	}

	public class StageDescription
	{
		public double activationTime;
		public List<Part> stageParts;
		public StageDescription(double activationTime)
		{
			this.activationTime = activationTime;
			stageParts = new List<Part>();
		}
	}

	public struct Sample
	{
		public double time;
		public double mass;
		public double altitude;
		public double velocity;
		public double acceleration;
		public double throttle;
	}
}

