using System;

namespace SmartStage
{
	public class DefaultAscentPath
	{
		private CelestialBody planet;
		private double autoTurnPerc = 0.1;
		private double turnShapeExponent = 0.4;
		public DefaultAscentPath (CelestialBody planet)
		{
			this.planet = planet;
		}
		public double autoTurnStartAltitude
		{
			get
			{
				return planet.atmosphere ? planet.maxAtmosphereAltitude * autoTurnPerc : 25;
			}
		}

		public double autoTurnEndAltitude
		{
			get
			{
				return planet.atmosphere ? planet.maxAtmosphereAltitude : 30000;
			}
		}

		public double FlightPathAngle(double altitude)
		{
			if (altitude < autoTurnStartAltitude) return 0;

			if (altitude > autoTurnEndAltitude) return Math.PI/2;

			return Math.Pow((altitude - autoTurnStartAltitude)/(autoTurnEndAltitude - autoTurnStartAltitude), turnShapeExponent) * Math.PI / 2;
		}
	}
}

