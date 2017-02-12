using System;
using UnityEngine;

namespace SmartStage
{

	public class MainWindow
	{
		int windowId = GUIUtility.GetControlID(FocusType.Native);
		Rect windowPosition;
		bool lockEditor;
		public bool ShowWindow = false;

		bool advancedSimulation = false;
		AscentPlot plot;
		CelestialBody[] planetObjects = FlightGlobals.Bodies.ToArray();
		string[] planets = FlightGlobals.Bodies.ConvertAll(b => b.GetName()).ToArray();
		int planetId;
		EditableDouble maxAcceleration = new EditableDouble(0);

		readonly Plugin plugin;

		public MainWindow(Plugin plugin)
		{
			this.plugin = plugin;
			planetId = Array.IndexOf(planetObjects, Planetarium.fetch.Home);
			// Position will be computed dynamically to be on screen
			windowPosition = new Rect(Screen.width, Screen.height, 0, 0);
		}

		public void ComputeStages()
		{
			SimulationLogic ship = new SimulationLogic(
				EditorLogic.fetch.ship.parts,
				planetObjects[planetId],
				68,
				maxAcceleration,
				advancedSimulation,
				Vector3d.up);
			ship.computeStages();
			if (advancedSimulation)
				plot = new AscentPlot(ship.samples, ship.stages, 400, 400);
		}

		public void OnGUI()
		{
			lockEditor = ComboBox.DrawGUI();

			if (ShowWindow)
			{
				if (Event.current.type == EventType.Layout)
				{
					windowPosition.x = Math.Min(windowPosition.x, Screen.width - windowPosition.width - 50);
					windowPosition.y = Math.Min(windowPosition.y, Screen.height - windowPosition.height - 50);
				}
				windowPosition = GUILayout.Window(windowId, windowPosition,
					drawWindow, "SmartStage");
				lockEditor |= windowPosition.Contains(Event.current.mousePosition);
			}

			if (lockEditor)
				EditorLogic.fetch.Lock(true, true, true, "SmartStage");
			else
				EditorLogic.fetch.Unlock("SmartStage");
		}

		void drawWindow(int windowid)
		{
			bool draggable = true;
			GUILayout.BeginVertical();
			if (GUILayout.Button("Compute stages"))
				ComputeStages();
			plugin.autoUpdateStaging = GUILayout.Toggle(plugin.autoUpdateStaging, "Automatically recompute staging");

			bool newAdvancedSimulation = GUILayout.Toggle(advancedSimulation, "Advanced simulation");
			if (!newAdvancedSimulation && advancedSimulation)
			{
				windowPosition.width = 0;
				windowPosition.height = 0;
			}
			advancedSimulation = newAdvancedSimulation;
			if (advancedSimulation)
			{
				int oldId = planetId;
				planetId = ComboBox.Box(planetId, planets, planets);

				GUILayout.BeginHorizontal();
				GUILayout.Label("Max acceleration: ");
				maxAcceleration.text = GUILayout.TextField(maxAcceleration.text);
				GUILayout.EndHorizontal();

				if (plot != null)
					draggable &= plot.draw();

				if (oldId != planetId)
					ComputeStages();
			}
			plugin.showInFlight = GUILayout.Toggle(plugin.showInFlight, "Show icon in flight");
			GUILayout.EndVertical();
			if (draggable)
				GUI.DragWindow();
		}

		public void onEditorShipModified(ShipConstruct v)
		{
			try
			{
				if (plugin.autoUpdateStaging)
					ComputeStages();
			}
			catch (Exception) {}
		}

	}
}

