using System;
using System.Collections.Generic;
using System.Linq;

namespace SmartStage
{

	public struct DState
	{
		public double vx;
		public double vy;
		public double ax;
		public double ay;
		public double dm;
		//For plot
		public double ax_nograv;
		public double ay_nograv;
	}

	public class SimulationState
	{
		public double x;
		public double y;
		public double vx;
		public double vy;
		public double m;
		private float minThrust;
		private float maxThrust;
		public float throttle;
		public List<EngineWrapper> activeEngines;
		public Dictionary<Part,Node> availableNodes;
		public double Cx;
		public CelestialBody planet;
		private DefaultAscentPath ascentPath;

		public bool limitToTerminalVelocity;
		public double maxAcceleration;
		public Vector3d forward;

		public SimulationState(CelestialBody planet, double departureAltitude, Vector3d forward)
		{
			this.planet = planet;
			this.forward = forward;

			x = 0;
			y = planet.Radius + departureAltitude;
			vx = planet.rotates ? (y * 2 * Math.PI / planet.rotationPeriod) : 0;
			vy = 0;
			throttle = 1.0f;
			activeEngines = new List<EngineWrapper>();
			availableNodes = new Dictionary<Part, Node>();
			ascentPath = new DefaultAscentPath(planet);
		}

		public SimulationState increment(DState delta, double dt)
		{
			SimulationState res = (SimulationState) MemberwiseClone();
			res.x += dt * delta.vx;
			res.y += dt * delta.vy;
			res.vx += dt * delta.ax;
			res.vy += dt * delta.ay;
			res.m += dt * delta.dm;
			if (r2 <= res.planet.Radius * res.planet.Radius)
			{
				double r = res.r;
				res.x *= res.planet.Radius / r;
				res.y *= res.planet.Radius / r;
				res.vx = res.y * (planet.rotates ? (2 * Math.PI / planet.rotationPeriod) : 0);
				res.vy = - res.x * (planet.rotates ? (2 * Math.PI / planet.rotationPeriod) : 0);
			}
			return res;
		}

		public List<Part> updateEngines()
		{
			List<Part> activeParts = new List<Part>();
			activeEngines.Clear();
			foreach(Node node in availableNodes.Values)
			{
				if ((node.isActiveEngine(availableNodes) && ! node.isSepratron))
				{
					activeEngines.AddRange(node.part.Modules.OfType<ModuleEngines>().Select(e => new EngineWrapper(e, availableNodes)));
					activeEngines.AddRange(node.part.Modules.OfType<ModuleEnginesFX>().Select(e => new EngineWrapper(e, availableNodes)));
					activeParts.Add(node.part);
				}
			}
			minThrust = activeEngines.Sum(e => e.thrust(0, pressure, machNumber));
			maxThrust = activeEngines.Sum(e => e.thrust(1, pressure, machNumber));
			return activeParts;
		}
		public double r { get { return Math.Sqrt(x * x + y * y);}}
		public double r2 { get { return x * x + y * y;}}

		// unit vectors
		private double u_x { get { return x/r;}}
		private double u_y { get { return y/r;}}

		public double v_surf_x { get { return vx - u_y * (planet.rotates ? (2 * Math.PI * r / planet.rotationPeriod) : 0);}}
		public double v_surf_y { get { return vy + u_x * (planet.rotates ? (2 * Math.PI * r / planet.rotationPeriod) : 0);}}

		public float pressure { get { return (float)FlightGlobals.getStaticPressure(r - planet.Radius, planet);}}

		//FIXME: implement
		public float machNumber { get { return 1;}}

		public DState derivate()
		{
			DState res = new DState();
			res.vx = vx;
			res.vy = vy;

			double r = this.r;
			float altitude = (float) (r - planet.Radius);

			double theta = Math.Atan2(u_x, u_y);
			double thrustDirection = theta + ascentPath.FlightPathAngle(altitude);

			// gravity
			double grav_acc = -planet.gravParameter / (r * r);

			// drag
			// FIXME: implement 1.0 drag model
			double v_surf2 = v_surf_x * v_surf_x + v_surf_y * v_surf_y;
			double v_surf = Math.Sqrt(v_surf2);
			double drag_acc = 0;

			double desiredThrust = double.MaxValue;

			if (v_surf > 0)
			{
				// Projection of acceleration components on vessel forward direction
				double proj = (drag_acc * v_surf_x / v_surf) * Math.Sin(thrustDirection)
					+ (drag_acc * v_surf_y / v_surf) * Math.Cos(thrustDirection);
				// Just ignore orthogonal components for acceleration limitation
				desiredThrust = Math.Min(desiredThrust, (maxAcceleration - proj) * m);
				// Optimal ascent has the vertical component of drag equal to gravity
				double drag_ratio = Math.Abs(drag_acc * (v_surf_x * u_x + v_surf_y * u_y) / (grav_acc * v_surf));
				if (limitToTerminalVelocity && Math.Abs(Math.Cos(theta - thrustDirection)) > 1e-3 && drag_ratio >0.9)
				{
					desiredThrust = Math.Min(desiredThrust,
						-2 /(5 *(drag_ratio - 0.9)) * grav_acc * m / Math.Cos(theta - thrustDirection));
				}
			}
			else
			{
				desiredThrust = Math.Min(desiredThrust, maxAcceleration * m);
			}

			if (maxThrust != minThrust)
				throttle = ((float)desiredThrust - minThrust) / (maxThrust - minThrust);
			else
				throttle = 1;
			throttle = Math.Max(0, Math.Min(1, throttle));

			// Effective thrust
			double F = activeEngines.Sum(e => e.thrust(throttle, pressure, machNumber));

			// Propellant mass variation
			res.dm = - activeEngines.Sum(e => e.evaluateFuelFlow(pressure, machNumber, throttle));

			res.ax_nograv = F / m * Math.Sin(thrustDirection);
			res.ay_nograv = F / m * Math.Cos(thrustDirection);
			if (v_surf != 0)
			{
				res.ax_nograv += drag_acc * v_surf_x/v_surf;
				res.ay_nograv += drag_acc * v_surf_y/v_surf;
			}
			res.ax = res.ax_nograv + grav_acc * u_x;
			res.ay = res.ay_nograv + grav_acc * u_y;

			return res;
		}
	}
}

