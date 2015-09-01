using System;
using System.Collections.Generic;
using System.Linq;
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
		bool limitToTerminalVelocity = true;
		EditableDouble maxAcceleration = new EditableDouble(0);

		bool _autoUpdateStaging = false;
		bool autoUpdateStaging
		{
			get { return _autoUpdateStaging;}
			set
			{
				if (value == _autoUpdateStaging)
					return;
				_autoUpdateStaging = value;
				plugin.State = value ? Plugin.state.active : Plugin.state.inactive;
			}
		}

		Plugin plugin;

		public MainWindow(Plugin plugin)
		{
			this.plugin = plugin;
			if (KSP.IO.File.Exists<MainWindow>("settings.cfg"))
			{
				try
				{
					var settings = ConfigNode.Load(KSP.IO.IOUtils.GetFilePathFor(typeof(MainWindow), "settings.cfg"));
					autoUpdateStaging = settings.GetValue("autoUpdateStaging") == Boolean.TrueString;
				}
				catch (Exception) {}
			}
			planetId = Array.IndexOf(planetObjects, Planetarium.fetch.Home);
			// Position will be computed dynamically to be on screen
			windowPosition = new Rect(Screen.width, Screen.height, 0, 0);
		}

		public void Save()
		{
			ConfigNode settings = new ConfigNode("SmartStage");
			settings.AddValue("autoUpdateStaging", autoUpdateStaging);
			settings.Save(KSP.IO.IOUtils.GetFilePathFor(typeof(MainWindow), "settings.cfg"));
		}

		public void Dispose()
		{
			autoUpdateStaging = false;
		}

		public void ComputeStages()
		{
			SimulationLogic ship = new SimulationLogic(
				EditorLogic.fetch.ship.parts,
				planetObjects[planetId],
				68,
				limitToTerminalVelocity,
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
			{
				EditorLogic.fetch.Unlock("SmartStage");
				if (Event.current.type == EventType.mouseUp && Event.current.button == 0)
					ShowWindow = false;
			}
		}

		void drawWindow(int windowid)
		{
			bool draggable = true;
			GUILayout.BeginVertical();
			if (GUILayout.Button("Compute stages"))
				ComputeStages();
			autoUpdateStaging = GUILayout.Toggle(autoUpdateStaging, "Automatically recompute staging");

			#if advancedsimulation
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

				limitToTerminalVelocity = GUILayout.Toggle(limitToTerminalVelocity, "Limit to terminal velocity");

				GUILayout.BeginHorizontal();
				GUILayout.Label("Max acceleration: ");
				maxAcceleration.text = GUILayout.TextField(maxAcceleration.text);
				GUILayout.EndHorizontal();

				if (plot != null)
					draggable &= plot.draw();

				if (oldId != planetId)
					computeStages();
			}
			#endif
			GUILayout.EndVertical();
			if (draggable)
				GUI.DragWindow();
		}

		public void onEditorShipModified(ShipConstruct v)
		{
			if (! autoUpdateStaging)
				return;
			try
			{
				ComputeStages();
			}
			catch (Exception) {}
		}

	}
}

