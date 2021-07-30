using System.Collections;
using KERBALISM.KsmGui;
using KSP.UI;
using KSP.UI.Screens;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KERBALISM
{
	public static class EditorUIManager
	{
		private static ApplicationLauncherButton launcherButton;

		private static KsmGuiWindow editorWindow;

		public static bool IsDisplayed { get; private set; } = false;
		public static bool IsPinned { get; private set; } = false;

		public static void OnGUIApplicationLauncherReady(ApplicationLauncherButton launcherButton)
		{
			EditorUIManager.launcherButton = launcherButton;
			launcherButton.toggleButton.onTrue.AddListener(OnLauncherEnable);
			launcherButton.toggleButton.onFalse.AddListener(OnLauncherDisable);
			launcherButton.onHover = OnHoverEnter;
			launcherButton.onHoverOut = OnHoverExitFromLauncher;
		}

		public static void OnGUIApplicationLauncherDestroyed()
		{
			launcherButton.toggleButton.onTrue.RemoveListener(OnLauncherEnable);
			launcherButton.toggleButton.onFalse.RemoveListener(OnLauncherDisable);
			launcherButton.onHover.Clear();
			launcherButton.onHoverOut.Clear();

			editorWindow?.Close();
		}

		private static void OnLauncherEnable(PointerEventData arg0, UIRadioButton.CallType arg1)
		{
			Display(true);
			IsPinned = true;
		}

		private static void OnLauncherDisable(PointerEventData arg0, UIRadioButton.CallType arg1)
		{
			Display(false);
			IsPinned = false;
		}

		private static void OnHoverEnter()
		{
			if (IsPinned)
				return;

			Display(true);
		}

		private static void OnHoverExit()
		{
			if (IsPinned)
				return;

			Display(false);
		}

		private static void OnHoverExitFromLauncher()
		{
			if (IsPinned || !IsDisplayed)
				return;

			editorWindow.StartCoroutine(HoverExitFromLauncherCoroutine());
		}

		private static IEnumerator HoverExitFromLauncherCoroutine()
		{
			yield return null;

			if (editorWindow.IsHovering)
				yield break;

			Display(false);
		}

		public static void Display(bool display)
		{
			if (!Lib.IsEditor || display == IsDisplayed)
				return;

			IsDisplayed = display;

			if (editorWindow == null)
			{
				editorWindow = new KsmGuiWindow(KsmGuiWindow.LayoutGroupType.Vertical, true, 0.8f, false, 0, TextAnchor.UpperLeft, 0f,
					TextAnchor.LowerRight, TextAnchor.LowerRight, 0, 40);

				editorWindow.SetOnPointerEnterAction(OnHoverEnter);
				editorWindow.SetOnPointerExitAction(OnHoverExit);

				new VesselSummaryUI(editorWindow, true, VesselDataShip.Instance);
			}

			editorWindow.Enabled = display;

			SetStageUIPosition(!display);

		}

		static void SetStageUIPosition(bool defaultPositon)
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
