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
				Save();
			}
		}

		bool _autoUpdateStaging = false;
		public bool autoUpdateStaging
		{
			get { return _autoUpdateStaging;}
			set
			{
				if (value == _autoUpdateStaging)
					return;
				_autoUpdateStaging = value;
				State = value ? Plugin.state.active : Plugin.state.inactive;
				Save();
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
			if (KSP.IO.File.Exists<MainWindow>("settings.cfg"))
			{
				try
				{
					var settings = ConfigNode.Load(KSP.IO.IOUtils.GetFilePathFor(typeof(MainWindow), "settings.cfg"));
					autoUpdateStaging = settings.GetValue("autoUpdateStaging") == bool.TrueString;
				}
				catch (Exception) {}
				try
				{
					var settings = ConfigNode.Load(KSP.IO.IOUtils.GetFilePathFor(typeof(MainWindow), "settings.cfg"));
					showInFlight = settings.GetValue("showInFlight") == bool.TrueString;
				}
				catch (Exception) {}
			}
			GameEvents.onGUIApplicationLauncherReady.Add(AddButton);
			GameEvents.onGUIApplicationLauncherDestroyed.Add(RemoveButton);
			GameEvents.onEditorShipModified.Add(onEditorShipModified);
			GameEvents.onLevelWasLoaded.Add(sceneChanged);
			GameEvents.onGameSceneSwitchRequested.Add(sceneSwitchRequested);
			AddButton();
			DontDestroyOnLoad(this);
		}

		void sceneSwitchRequested(GameEvents.FromToAction<GameScenes, GameScenes> ignored)
		{
			RemoveButton();
			gui = null;
			State = state.inactive;
		}

		void sceneChanged(GameScenes scene)
		{
			AddButton();
			if (scene == GameScenes.EDITOR)
			{
				gui = new MainWindow(this);
				if (autoUpdateStaging)
					State = state.active;
			}
		}

		void AddButton()
		{
			if (vabButton != null || ! ApplicationLauncher.Ready)
				return;

			vabButton = ApplicationLauncher.Instance.AddModApplication(
				() => ShowWindow(true),
				() => ShowWindow(false),
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

		void ShowWindow(bool shown)
		{
			if (gui != null)
				gui.ShowWindow = shown;
		}

		public void OnGUI()
		{
			gui?.OnGUI();
		}

		void onEditorShipModified(ShipConstruct ship)
		{
			gui?.onEditorShipModified(ship);
		}

		void Save()
		{
			ConfigNode settings = new ConfigNode("SmartStage");
			settings.AddValue("autoUpdateStaging", autoUpdateStaging);
			settings.AddValue("showInFlight", showInFlight);
			settings.Save(KSP.IO.IOUtils.GetFilePathFor(typeof(MainWindow), "settings.cfg"));
		}
	}
}

