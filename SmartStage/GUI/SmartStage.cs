using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SmartStage
{

	[KSPAddon(KSPAddon.Startup.EditorVAB, false)]
	public class SmartStage : MonoBehaviour
	{

		private static ApplicationLauncherButton stageButton;
		private static AscentPlot plot;
		private static int windowId = GUIUtility.GetControlID(FocusType.Native);
		private static Rect windowPosition;
		private static bool lockEditor;
		private static bool showWindow = false;
		private static CelestialBody[] planetObjects = FlightGlobals.Bodies.ToArray();
		private static string[] planets = FlightGlobals.Bodies.ConvertAll(b => b.GetName()).ToArray();
		private static int planetId = Array.IndexOf(planetObjects, Planetarium.fetch.Home);
		private static bool limitToTerminalVelocity = true;
		private static EditableDouble maxAcceleration = new EditableDouble(0);
		// Correction factor to limit to terminal velocity closer to MechJeb's limitation
		public static EditableDouble terminalVelocityCorrectionFactor = new EditableDouble(5);
		private static bool advancedSimulation = false;

		public SmartStage()
		{
			GameEvents.onGUIApplicationLauncherReady.Add(addButton);
			addButton();
			// Position will be computed dynamically to be on screen
			windowPosition = new Rect(Screen.width, Screen.height, 0, 0);
		}

		private void addButton()
		{
			plot = null;
			removeButton();

			stageButton = ApplicationLauncher.Instance.AddModApplication(
				computeStages, computeStages,
				() => showWindow = true, null,
				null, null,
				ApplicationLauncher.AppScenes.VAB | ApplicationLauncher.AppScenes.SPH,
				GameDatabase.Instance.GetTexture("SmartStage/SmartStage38", false));
		}

		private void removeButton()
		{
			if (stageButton != null)
				ApplicationLauncher.Instance.RemoveModApplication(stageButton);
		}

		public static void computeStages()
		{
			SimulationLogic ship = new SimulationLogic(EditorLogic.fetch.ship, planetObjects[planetId], 68, limitToTerminalVelocity, maxAcceleration, advancedSimulation);
			ship.computeStages();
			if (advancedSimulation)
				plot = new AscentPlot(ship.samples, ship.stages, 400, 400);
			showWindow = true;
		}

		public void OnGUI()
		{
			lockEditor = ComboBox.DrawGUI();

			if (showWindow)
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
					showWindow = false;
			}
		}

		public void drawWindow(int windowid)
		{
			bool draggable = true;
			GUILayout.BeginVertical();
			advancedSimulation = GUILayout.Toggle(advancedSimulation, "Advanced simulation");
			if (advancedSimulation)
			{
				int oldId = planetId;
				planetId = ComboBox.Box(planetId, planets, planets);

				limitToTerminalVelocity = GUILayout.Toggle(limitToTerminalVelocity, "Limit to terminal velocity");

				GUILayout.BeginHorizontal();
				GUILayout.Label("Max acceleration: ");
				maxAcceleration.text = GUILayout.TextField(maxAcceleration.text);
				GUILayout.EndHorizontal();

				#if DEBUG
				GUILayout.BeginHorizontal();
				GUILayout.Label("lambda: ");
				terminalVelocityCorrectionFactor.text = GUILayout.TextField(terminalVelocityCorrectionFactor.text);
				GUILayout.EndHorizontal();
				#endif

				if (plot != null)
					draggable &= plot.draw();

				if (oldId != planetId)
					computeStages();
			}
			GUILayout.EndVertical();
			if (draggable)
				GUI.DragWindow();
		}

	}
}

