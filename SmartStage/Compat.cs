using System;

namespace SmartStage
{
	public static class SmartStage
	{
		// Compatibility with autoasparagus, using reflection
		public static void computeStages()
		{
			(new SimulationLogic(EditorLogic.fetch.ship, Planetarium.fetch.Home, 68, false, 0, false)).computeStages();
		}
	}
}

