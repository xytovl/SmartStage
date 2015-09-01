using System;

namespace SmartStage
{
	public static class SmartStage
	{
		// Compatibility with autoasparagus, using reflection
		public static void computeStages()
		{
			(new SimulationLogic(EditorLogic.fetch.ship.parts, Planetarium.fetch.Home, 68, false, 0, false, Vector3d.up)).computeStages();
		}
	}
}

