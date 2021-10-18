using KERBALISM.KsmGui;
using KSP.UI.Screens;
using UnityEngine;


namespace KERBALISM
{

	public sealed class Launcher
	{
		// click through locks
		private bool clickThroughLocked = false;
		private const ControlTypes MainGUILockTypes = ControlTypes.MANNODE_ADDEDIT | ControlTypes.MANNODE_DELETE | ControlTypes.MAP_UI |
			ControlTypes.TARGETING | ControlTypes.VESSEL_SWITCHING | ControlTypes.TWEAKABLES | ControlTypes.EDITOR_UI | ControlTypes.EDITOR_SOFT_LOCK | ControlTypes.UI;

		public Launcher()
		{
			GameEvents.onGUIApplicationLauncherReady.Add(Create);
		}


		public void Create()
		{
			// do nothing if button already created
			if (!ui_initialized)
			{
				ui_initialized = true;

				// create the button
				// note: for some weird reasons, the callbacks can be called BEFORE this function return
				vesselListLauncher = ApplicationLauncher.Instance.AddApplication(null, null, null, null, null, null, Textures.applauncher_vessels);

				// enable the launcher button for some scenes
				vesselListLauncher.VisibleInScenes =
					ApplicationLauncher.AppScenes.SPACECENTER
				  | ApplicationLauncher.AppScenes.FLIGHT
				  | ApplicationLauncher.AppScenes.MAPVIEW
				  | ApplicationLauncher.AppScenes.TRACKSTATION
				  | ApplicationLauncher.AppScenes.VAB
				  | ApplicationLauncher.AppScenes.SPH;

				vesselListLauncher.onRightClick = () =>
				{
					if (!Lib.IsEditor)
					{
						KsmGuiWindow window = new KsmGuiWindow(KsmGuiLib.Orientation.Vertical, true, 0.8f, true);
						new VesselsManager(window);
					}
				};
			}

			if (Features.Science)
			{
				if (generalMenuLauncher == null)
				{
					generalMenuLauncher = ApplicationLauncher.Instance.AddApplication(null, null, null, null, null, null, Textures.applauncher_database);
					generalMenuLauncher.VisibleInScenes =
						ApplicationLauncher.AppScenes.SPACECENTER
					  | ApplicationLauncher.AppScenes.FLIGHT
					  | ApplicationLauncher.AppScenes.MAPVIEW
					  | ApplicationLauncher.AppScenes.TRACKSTATION
					  | ApplicationLauncher.AppScenes.VAB
					  | ApplicationLauncher.AppScenes.SPH;

					generalMenuLauncher.onLeftClick = () => ScienceArchiveWindow.Toggle();
				}
			}
			else
			{
				if (generalMenuLauncher != null)
				{
					generalMenuLauncher.onLeftClick = null;
					ApplicationLauncher.Instance.RemoveApplication(generalMenuLauncher);
					generalMenuLauncher = null;
				}
			}
		}

		public void Update()
		{
			// do nothing if GUI has not been initialized
			if (!ui_initialized)
				return;

			// do nothing if the UI is not shown
			if (win_rect.width == 0f)
				return;

			// update planner/monitor content
			if (Lib.IsEditor)
			{
				//Planner.Planner.Update();
			}
			else
			{
				// monitor.Update();
			}
		}


		// called every frame
		public void On_gui()
		{
			// do nothing if GUI has not been initialized
			if (!ui_initialized)
				return;

			// render the window
			if (vesselListLauncher.toggleButton.Value || vesselListLauncher.IsHovering || (win_rect.width > 0f && win_rect.Contains(Mouse.screenPos)))
			{
				// draw tooltip
				tooltip.Draw();
			}
			else
			{
				// set zero area win_rect
				win_rect.width = 0f;
			}

			// get mouse over state
			// bool mouse_over = win_rect.Contains(Event.current.mousePosition);
			bool mouse_over = win_rect.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y));

			// disable camera mouse scrolling on mouse over
			if (mouse_over)
			{
				GameSettings.AXIS_MOUSEWHEEL.primary.scale = 0.0f;
			}

			// Disable Click through
			if (mouse_over && !clickThroughLocked)
			{
				InputLockManager.SetControlLock(MainGUILockTypes, "KerbalismMainGUILock");
				clickThroughLocked = true;
			}
			if (!mouse_over && clickThroughLocked)
			{
				InputLockManager.RemoveControlLock("KerbalismMainGUILock");
				clickThroughLocked = false;
			}
		}


		// initialized flag
		bool ui_initialized;

		// store reference to applauncher button
		ApplicationLauncherButton vesselListLauncher;

		ApplicationLauncherButton generalMenuLauncher;

		// window geometry
		Rect win_rect;

		// tooltip utility
		Tooltip tooltip;
	}


} // KERBALISM
