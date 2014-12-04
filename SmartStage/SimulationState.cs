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
		private float minThrust;
		private float maxThrust;
		public float throttle;
		public List<EngineWrapper> activeEngines;
		public Dictionary<Part,Node> availableNodes;

		public bool limitToTerminalVelocity;
		public double maxAcceleration;

		public SimulationState increment(DState delta, double dt)
		{
			SimulationState res = (SimulationState) MemberwiseClone();
			res.x += dt * delta.vx;
			res.z += dt * delta.vz;
			res.vx += dt * delta.ax;
			res.vz += dt * delta.az;
			res.m += dt * delta.dm;
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
			minThrust = activeEngines.Sum(e => e.thrust(0));
			maxThrust = activeEngines.Sum(e => e.thrust(1));
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
			float pressure = planet.pressureCurve.Evaluate(altitude);

			double theta = 0; // FIXME: thrust direction

			// unit vectors
			double u_x = x / r;
			double u_z = z / r;

			// gravity
			double grav_acc = -planet.gravParameter / (r * r);

			// drag
			double Cx = 0.2;
			double rho = FlightGlobals.getAtmDensity(pressure);
			double drag_acc_over_velocity = -0.5 * rho * velocity * Cx * FlightGlobals.DragMultiplier;

			float desiredThrust = float.MaxValue;

			throttle = (desiredThrust - minThrust) / (maxThrust - minThrust);
			throttle = Math.Max(0, Math.Min(1, throttle));

			// Effective thrust
			double F = activeEngines.Sum(e => e.thrust(throttle));

			// Propellant mass variation
			res.dm = - activeEngines.Sum(e => e.evaluateFuelFlow(pressure, throttle));

			theta += Math.Atan2(u_x, u_z);
			res.ax = grav_acc * u_x + drag_acc_over_velocity * vx + F / m * Math.Sin(theta);
			res.az = grav_acc * u_z + drag_acc_over_velocity * vz + F / m * Math.Cos(theta);

			return res;
		}
	}
}

