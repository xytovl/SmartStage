using System;
using UnityEngine;
using KSP.UI.Screens;

namespace SmartStage
{
	[KSPAddon(KSPAddon.Startup.MainMenu, true)]
	public class Plugin : MonoBehaviour
	{
		public enum state {inactive, active}

		ApplicationLauncherButton vabButton;
		ApplicationLauncherButton flightButton;
		readonly Texture2D[] textures;
		GameScenes currentScene;

		state _state = state.inactive;
		public state State
		{
			get { return _state;}
			set
			{
				if (value == _state)
					return;
				_state = value;
				vabButton?.SetTexture(Texture);
				flightButton?.SetTexture(Texture);
			}
		}
		Texture2D Texture { get { return textures[(int)_state];}}

		bool _showInFlight = false;
		public bool showInFlight
		{
			get { return _showInFlight;}
			set
			{
				if (value == _showInFlight)
					return;
				_showInFlight = value;
			}
		}

		MainWindow gui;

		public Plugin()
		{
			textures = new Texture2D[]{
				GameDatabase.Instance.GetTexture("SmartStage/SmartStage38", false),
				GameDatabase.Instance.GetTexture("SmartStage/SmartStage38-active", false)
			};
		}

		public void Start()
		{
			GameEvents.onGUIApplicationLauncherReady.Add(AddButton);
			GameEvents.onGUIApplicationLauncherDestroyed.Add(RemoveButton);
			GameEvents.onEditorShipModified.Add(onEditorShipModified);
			GameEvents.onLevelWasLoaded.Add(sceneChanged);
			AddButton();
			DontDestroyOnLoad(this);
		}

		void AddButton()
		{
			if (vabButton != null || ! ApplicationLauncher.Ready)
				return;

			vabButton = ApplicationLauncher.Instance.AddModApplication(
				() => gui.ShowWindow = true, () => gui.ShowWindow = false,
				null, null,
				null, null,
				ApplicationLauncher.AppScenes.VAB | ApplicationLauncher.AppScenes.SPH,
				Texture);

			if (showInFlight)
			{
				flightButton = ApplicationLauncher.Instance.AddModApplication(
					SimulationLogic.inFlightComputeStages, SimulationLogic.inFlightComputeStages,
					null, null,
					null, null,
					ApplicationLauncher.AppScenes.FLIGHT,
					Texture);
			}
		}

		void RemoveButton()
		{
			if (flightButton != null)
				ApplicationLauncher.Instance.RemoveModApplication(flightButton);
			flightButton = null;
			if (vabButton != null)
				ApplicationLauncher.Instance.RemoveModApplication(vabButton);
			vabButton = null;
		}

		private void sceneChanged(GameScenes scene)
		{
			if (currentScene == GameScenes.EDITOR)
			{
				gui.Save();
				gui.Dispose();
				gui = null;
			}

			currentScene = scene;

			if (currentScene == GameScenes.EDITOR)
				gui = new MainWindow(this);
			else
				State = state.inactive;
		}

		public void OnDestroy()
		{
			gui.Save();
			gui.Dispose();
			RemoveButton();
			GameEvents.onGUIApplicationLauncherReady.Remove(AddButton);
			GameEvents.onGUIApplicationLauncherDestroyed.Remove(RemoveButton);
			GameEvents.onEditorShipModified.Remove(onEditorShipModified);
		}

		public void OnGUI()
		{
			if (currentScene == GameScenes.EDITOR)
				gui?.OnGUI();
		}

		void onEditorShipModified(ShipConstruct ship)
		{
			gui?.onEditorShipModified(ship);
		}
	}
}

