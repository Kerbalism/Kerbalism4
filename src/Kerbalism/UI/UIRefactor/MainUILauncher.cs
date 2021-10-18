using System.Collections;
using KERBALISM.KsmGui;
using KSP.UI;
using KSP.UI.Screens;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KERBALISM
{
	public class MainUILauncher
	{
		public static MainUILauncher Instance { get; private set; }

		private ApplicationLauncherButton launcherButton;

		private KsmGuiWindow mainWindow;

		public bool IsDisplayed { get; private set; } = false;
		public bool IsPinned { get; private set; } = false;

		public MainUILauncher()
		{
			Instance = this;

			GameEvents.onGUIApplicationLauncherReady.Add(OnGUIApplicationLauncherReady);
			GameEvents.onGameSceneSwitchRequested.Add(OnSceneSwitch);
		}

		public void OnGUIApplicationLauncherReady()
		{
			if (launcherButton == null)
			{
				launcherButton = ApplicationLauncher.Instance.AddApplication(null, null, null, null, null, null, Textures.applauncher_vessels);

				launcherButton.VisibleInScenes =
					ApplicationLauncher.AppScenes.SPACECENTER
					| ApplicationLauncher.AppScenes.FLIGHT
					| ApplicationLauncher.AppScenes.MAPVIEW
					| ApplicationLauncher.AppScenes.TRACKSTATION
					| ApplicationLauncher.AppScenes.VAB
					| ApplicationLauncher.AppScenes.SPH;

				launcherButton.toggleButton.onTrue.AddListener(OnLauncherEnable);
				launcherButton.toggleButton.onFalse.AddListener(OnLauncherDisable);
				launcherButton.onHover = OnHoverEnter;
				launcherButton.onHoverOut = OnHoverExitFromLauncher;
			}
		}

		public void OnSceneSwitch(GameEvents.FromToAction<GameScenes, GameScenes> data)
		{
			//launcherButton.toggleButton.onTrue.RemoveListener(OnLauncherEnable);
			//launcherButton.toggleButton.onFalse.RemoveListener(OnLauncherDisable);
			//launcherButton.onHover.Clear();
			//launcherButton.onHoverOut.Clear();

			mainWindow?.Close();
			mainWindow = null;
		}

		private void OnLauncherEnable(PointerEventData arg0, UIRadioButton.CallType arg1)
		{
			Display(true);
			IsPinned = true;
		}

		private void OnLauncherDisable(PointerEventData arg0, UIRadioButton.CallType arg1)
		{
			Display(false);
			IsPinned = false;
		}

		private void OnHoverEnter()
		{
			if (IsPinned)
				return;

			Display(true);
		}

		private void OnHoverExit()
		{
			if (IsPinned)
				return;

			Display(false);
		}

		private void OnHoverExitFromLauncher()
		{
			if (IsPinned || !IsDisplayed)
				return;

			mainWindow.StartCoroutine(HoverExitFromLauncherCoroutine());
		}

		private IEnumerator HoverExitFromLauncherCoroutine()
		{
			yield return null;

			if (mainWindow.IsHovering)
				yield break;

			Display(false);
		}

		public void Display(bool display)
		{
			if (display == IsDisplayed)
				return;

			IsDisplayed = display;

			if (mainWindow == null)
			{
				InstantiateWindow();
			}

			mainWindow.Enabled = display;

			if (Lib.IsEditor)
			{
				SetStageUIPosition(!display);
			}
		}

		private void InstantiateWindow()
		{
			mainWindow = new KsmGuiWindow(KsmGuiLib.Orientation.Vertical, true, 0.8f, false, 0, TextAnchor.UpperLeft, 0f,
				ApplicationLauncher.Instance.IsPositionedAtTop ? TextAnchor.UpperRight : TextAnchor.LowerRight,
				ApplicationLauncher.Instance.IsPositionedAtTop ? TextAnchor.UpperRight : TextAnchor.LowerRight,
				ApplicationLauncher.Instance.IsPositionedAtTop ? -40 : 0,
				ApplicationLauncher.Instance.IsPositionedAtTop ? 0 : 40);

			//mainWindow = new KsmGuiScrollableWindow(0.8f, 370, -1, 600,
			//	ApplicationLauncher.Instance.IsPositionedAtTop ? TextAnchor.UpperRight : TextAnchor.LowerRight,
			//	ApplicationLauncher.Instance.IsPositionedAtTop ? TextAnchor.UpperRight : TextAnchor.LowerRight,
			//	ApplicationLauncher.Instance.IsPositionedAtTop ? -40 : 0,
			//	ApplicationLauncher.Instance.IsPositionedAtTop ? 0 : 40);

			mainWindow.TopTransform.sizeDelta = new Vector2(370, 800);

			mainWindow.SetOnPointerEnterAction(OnHoverEnter);
			mainWindow.SetOnPointerExitAction(OnHoverExit);

			if (Lib.IsEditor)
			{
				VesselSummaryUI ui = new VesselSummaryUI(mainWindow, false);
				ui.SetVessel(VesselDataShip.Instance);
			}
			else
			{
				new MainUIFlight(mainWindow, ApplicationLauncher.Instance.IsPositionedAtTop);
			}
		}

		private void SetStageUIPosition(bool defaultPositon)
		{
			RectTransform stagingTopTransform = (RectTransform)StageManager.Instance.transform.parent;

			// Note : Ideally, the "reset" button should stay in place by offsetting it, but the StageGroup.ToggleInfoPanel()
			// method is calling an animator plugin thing that reset all positions, and I can't find a way to reliably override
			// its behaviour.
			// In case we want to try again, this is the transform that need to be moved :
			// RectTransform stagingResetButton = (RectTransform)StageManager.Instance.resetButton.transform.parent;
			// Vector2 stagingResetDefaultAnchoredPosition = new Vector2(80f, -70f);

			if (defaultPositon)
			{
				stagingTopTransform.anchoredPosition = new Vector2(0f, 0f);
				stagingTopTransform.sizeDelta = new Vector2(90f, -25f);
			}
			else
			{
				stagingTopTransform.anchoredPosition = new Vector2(-VesselSummaryUI.Width, 40f);
				stagingTopTransform.sizeDelta = new Vector2(90f, -65f);
			}
		}
	}
}
