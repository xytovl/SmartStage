using System;
using System.Collections.Generic;
using System.Linq;

namespace SmartStage
{

	public struct DState
	{
		public double vx;
		public double vz;
		public double ax;
		public double az;
		public double dm;
	}

	public class SimulationState
	{
		public double x;
		public double z;
		public double vx;
		public double vz;
		public double m;
		public float throttle;
		public List<EngineWrapper> activeEngines;
		public Dictionary<Part,Node> availableNodes;

		public SimulationState increment(DState delta, double dt)
		{
			SimulationState res = new SimulationState();
			res.x = x + dt * delta.vx;
			res.z = z + dt * delta.vz;
			res.vx = vx + dt * delta.ax;
			res.vz = vz + dt * delta.az;
			res.m = m + dt * delta.dm;
			res.throttle = throttle;
			res.activeEngines = activeEngines;
			res.availableNodes = availableNodes;
			return res;
		}

		public List<Part> updateEngines()
		{
			List<Part> activeParts = new List<Part>();
			activeEngines.Clear();
			foreach(Node node in availableNodes.Values)
			{
				if ((node.isActiveEngine(availableNodes) && ! Node.isSepratron(node.part)))
				{
					activeEngines.AddRange(node.part.Modules.OfType<ModuleEngines>().Select(e => new EngineWrapper(e, availableNodes)));
					activeEngines.AddRange(node.part.Modules.OfType<ModuleEnginesFX>().Select(e => new EngineWrapper(e, availableNodes)));
					activeParts.Add(node.part);
				}
			}
			return activeParts;
		}
		public double r { get { return Math.Sqrt(x * x + z * z);}}
		public double velocity { get { return Math.Sqrt(vx * vx + vz * vz);}}


		public DState derivate(CelestialBody planet)
		{
			DState res = new DState();
			res.vx = vx;
			res.vz = vz;

			double r = this.r;
			float altitude = (float) (r - planet.Radius);
			double velocity = this.velocity;

			float throttle = 1; // FIXME: requested throttle
			res.dm = 0;
			// Compute flow for active engines
			foreach (EngineWrapper e in activeEngines)
				res.dm -= e.evaluateFuelFlow(planet.pressureCurve.Evaluate(altitude), throttle);

			double theta = 0; // FIXME: thrust direction

			// unit vectors
			double u_x = x / r;
			double u_z = z / r;

			// gravity
			double grav_acc = -planet.gravParameter / (r * r);

			// drag
			double Cx = 0.2;
			double rho = FlightGlobals.getAtmDensity(planet.pressureCurve.Evaluate(altitude));
			double drag_acc_over_velocity = -0.5 * rho * velocity * Cx * FlightGlobals.DragMultiplier;

			// thrust
			double F = activeEngines.Sum(e => e.thrust(throttle));

			theta += Math.Atan2(u_x, u_z);
			res.ax = grav_acc * u_x + drag_acc_over_velocity * vx + F / m * Math.Sin(theta);
			res.az = grav_acc * u_z + drag_acc_over_velocity * vz + F / m * Math.Cos(theta);

			return res;
		}
	}
}

