using System;
using UnityEngine;

namespace SmartStage
{
	[KSPAddon(KSPAddon.Startup.EditorVAB, false)]
	public class Plugin : MonoBehaviour
	{
		public enum state {inactive, active}

		ApplicationLauncherButton stageButton;
		readonly Texture2D[] textures;

		state _state;
		public state State
		{
			get { return _state;}
			set
			{
				if (value == _state)
					return;
				_state = value;
				stageButton?.SetTexture(Texture);
			}
		}
		Texture2D Texture { get { return textures[(int)_state];}}

		MainWindow gui;

		public Plugin()
		{
			textures = new Texture2D[]{
				GameDatabase.Instance.GetTexture("SmartStage/SmartStage38", false),
				GameDatabase.Instance.GetTexture("SmartStage/SmartStage38-active", false)
			};
			gui = new MainWindow(this);
			GameEvents.onGUIApplicationLauncherReady.Add(AddButton);
			GameEvents.onGUIApplicationLauncherDestroyed.Add(RemoveButton);
			GameEvents.onEditorShipModified.Add(onEditorShipModified);
			AddButton();
		}

		void AddButton()
		{
			if (stageButton != null || ! ApplicationLauncher.Ready)
				return;

			stageButton = ApplicationLauncher.Instance.AddModApplication(
				() => gui.ShowWindow = true, () => gui.ShowWindow = false,
				null, null,
				null, null,
				ApplicationLauncher.AppScenes.VAB | ApplicationLauncher.AppScenes.SPH,
				Texture);
		}

		void RemoveButton()
		{
			if (stageButton != null)
				ApplicationLauncher.Instance.RemoveModApplication(stageButton);
			stageButton = null;
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
			gui.OnGUI();
		}

		void onEditorShipModified(ShipConstruct ship)
		{
			gui.onEditorShipModified(ship);
		}
	}
}

