using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using KSP.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KERBALISM.KsmGui
{
	public class KsmGuiTooltipController : MonoBehaviour
	{
		public static KsmGuiTooltipController Instance { get; private set; }
		public bool IsVisible { get; private set; }
		public KsmGuiTooltipBase CurrentTooltip { get; private set; }
		public RectTransform TopTransform { get; private set; }
		public RectTransform ContentTransform { get; private set; }
		public TextMeshProUGUI TextComponent { get; private set; }

		private GameObject tooltipObject;
		private VerticalLayoutGroup backgroundLayout;
		private RectTransform textTransform;
		private RectTransform tooltipRect;
		private ContentSizeFitter topFitter;
		private CanvasGroup canvasGroup;
		private Coroutine coroutine;

		private void Awake()
		{
			Instance = this;

			tooltipObject = new GameObject("KsmGuiTooltip");

			TopTransform = tooltipObject.AddComponent<RectTransform>();
			// default of 0, 1 mean pivot is at the window top-left corner
			// pivotX = 0 => left, pivotX = 1 => right
			// pivotY = 0 => bottom, pivotY = 1 => top
			TopTransform.pivot = new Vector2(0f, 0f);
			// distance in pixels between the pivot and the center of the screen
			TopTransform.anchoredPosition = new Vector2(0f, 0f);

			TopTransform.sizeDelta = new Vector2(KsmGuiStyle.tooltipMaxWidth, 0f); // max width of tooltip, text wrap will occur if larger.

			// set the parent canvas
			// render order of the various UI canvases (lower value = on top)
			// maincanvas => Z 750
			// appCanvas => Z 625
			// actionCanvas => Z 500
			// screenMessageCanvas => Z 450
			// dialogCanvas => Z 400
			// dragDropcanvas => Z 333
			// debugCanvas => Z 315
			// tooltipCanvas => Z 300
			TopTransform.SetParentFixScale(UIMasterController.Instance.tooltipCanvas.transform);

			tooltipObject.AddComponent<CanvasRenderer>();

			canvasGroup = tooltipObject.AddComponent<CanvasGroup>();
			canvasGroup.interactable = false;
			canvasGroup.blocksRaycasts = false;

			VerticalLayoutGroup toplayout = tooltipObject.AddComponent<VerticalLayoutGroup>();
			toplayout.childAlignment = TextAnchor.UpperLeft;
			toplayout.childControlHeight = true;
			toplayout.childControlWidth = true;
			toplayout.childForceExpandHeight = false;
			toplayout.childForceExpandWidth = false;

			topFitter = tooltipObject.AddComponent<ContentSizeFitter>();
			topFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
			topFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

			// first child : 1px white border
			GameObject border = new GameObject("KsmGuiTooltipBorder");
			tooltipRect = border.AddComponent<RectTransform>();
			tooltipRect.SetParentFixScale(TopTransform);
			border.AddComponent<CanvasRenderer>();

			VerticalLayoutGroup borderLayout = border.AddComponent<VerticalLayoutGroup>();
			borderLayout.padding = new RectOffset(1,1,1,1);
			borderLayout.childAlignment = TextAnchor.UpperLeft;
			borderLayout.childControlHeight = true;
			borderLayout.childControlWidth = true;
			borderLayout.childForceExpandHeight = false;
			borderLayout.childForceExpandWidth = false;

			Image borderImage = border.AddComponent<Image>();
			borderImage.color = KsmGuiStyle.tooltipBorderColor;
			borderImage.raycastTarget = false;

			// 2nd child : black background
			GameObject background = new GameObject("KsmGuiTooltipBackground");
			ContentTransform = background.AddComponent<RectTransform>();
			ContentTransform.SetParentFixScale(tooltipRect);
			background.AddComponent<CanvasRenderer>();

			backgroundLayout = background.AddComponent<VerticalLayoutGroup>();
			backgroundLayout.padding = new RectOffset(5, 5, 2, 2);
			backgroundLayout.childAlignment = TextAnchor.UpperLeft;
			backgroundLayout.childControlHeight = true;
			backgroundLayout.childControlWidth = true;
			backgroundLayout.childForceExpandHeight = false;
			backgroundLayout.childForceExpandWidth = false;

			Image backgroundImage = background.AddComponent<Image>();
			backgroundImage.color = KsmGuiStyle.tooltipBackgroundColor;
			backgroundImage.raycastTarget = false;

			// last child : text
			GameObject textObject = new GameObject("KsmGuiTooltipText");
			textTransform = textObject.AddComponent<RectTransform>();
			textTransform.SetParentFixScale(ContentTransform);
			textObject.AddComponent<CanvasRenderer>();

			TextComponent = textObject.AddComponent<TextMeshProUGUI>();
			TextComponent.raycastTarget = false;
			TextComponent.color = KsmGuiStyle.textColor;
			TextComponent.font = KsmGuiStyle.textFont;
			TextComponent.fontSize = KsmGuiStyle.textSize;
			TextComponent.alignment = TextAlignmentOptions.Top;

			tooltipObject.SetLayerRecursive(5);
			//KsmGuiBase.ApplyCanvasScalerScale(TopTransform); 
			tooltipObject.SetActive(false);
			IsVisible = false;
		}

		public void SetMaxWidth(int width)
		{
			if (width <= 0)
			{
				topFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
			}
			else
			{
				topFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
				TopTransform.sizeDelta = new Vector2(width, TopTransform.sizeDelta.y);
			}
		}

		public void ShowTooltip(KsmGuiTooltipBase tooltip)
		{
			HideTooltip();
			CurrentTooltip = tooltip;
			coroutine = StartCoroutine(TooltipUpdate());
		}

		public void HideTooltip()
		{
			tooltipObject.SetActive(false);
			IsVisible = false;

			if (CurrentTooltip != null)
			{
				if (coroutine != null)
				{
					StopCoroutine(coroutine);
					coroutine = null;
				}

				CurrentTooltip.OnHideTooltip();
				CurrentTooltip = null;
			}
		}

		private IEnumerator TooltipUpdate()
		{
			canvasGroup.alpha = 0f;

			// wait a tiny bit before creating the tooltip, so rapid mouse movement over items
			// doesn't reult in flashing tooltips everywhere (and prevent instantiating useless things)
			yield return new WaitForSecondsRealtime(0.1f);
			CurrentTooltip.OnShowTooltip();
			tooltipObject.SetActive(true);
			IsVisible = true;

			// wait one frame before making the tooltip visible, so the RectTransform size is updated according to the layout
			yield return null;

			while (IsVisible)
			{
				CurrentTooltip.OnTooltipUpdate();

				if (!CurrentTooltip.TooltipEnabled)
				{
					canvasGroup.alpha = 0f;
				}
				else
				{
					canvasGroup.alpha = 1f;

					Vector3 mouseWorldPos;
					Vector3 position = new Vector3();
					RectTransformUtility.ScreenPointToWorldPointInRectangle(TopTransform, Input.mousePosition, UIMasterController.Instance.uiCamera, out mouseWorldPos);

					position.x = mouseWorldPos.x - (tooltipRect.rect.width * tooltipRect.lossyScale.x * 0.5f);
					position.y = mouseWorldPos.y + 15f;

					if (position.x < -0.5f * Screen.width)
						position.x = -0.5f * Screen.width;
					else if (position.x + tooltipRect.rect.width * tooltipRect.lossyScale.x > 0.5f * Screen.width)
						position.x = 0.5f * Screen.width - tooltipRect.rect.width * tooltipRect.lossyScale.x;

					if (position.y + tooltipRect.rect.height * tooltipRect.lossyScale.y > 0.5f * Screen.height)
						position.y = mouseWorldPos.y - (tooltipRect.rect.height * tooltipRect.lossyScale.y) - 20f;

					TopTransform.position = position;
				}

				yield return null;
			}
		}
	}
}
