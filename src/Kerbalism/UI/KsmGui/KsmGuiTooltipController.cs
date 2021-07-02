using System;
using System.Collections.Generic;
using KSP.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KERBALISM.KsmGui
{
	public class KsmGuiTooltipController : MonoBehaviour
	{
		public static KsmGuiTooltipController Instance { get; private set; }

		private GameObject tooltipObject;
		private VerticalLayoutGroup backgroundLayout;
		private RectTransform textTransform;
		private TextMeshProUGUI textComponent;
		private ContentSizeFitter topFitter;
		public RectTransform TopTransform { get; private set; }
		public RectTransform ContentTransform { get; private set; }
		public bool IsVisible { get; private set; }

		private RectTransform backgroundTranform;
		private KsmGuiBase content;

		private KsmGuiTooltipBase currentTooltip;

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

			CanvasGroup group = tooltipObject.AddComponent<CanvasGroup>();
			group.interactable = false;
			group.blocksRaycasts = false;

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
			ContentTransform = border.AddComponent<RectTransform>();
			ContentTransform.SetParentFixScale(TopTransform);
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
			backgroundTranform = background.AddComponent<RectTransform>();
			backgroundTranform.SetParentFixScale(ContentTransform);
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
			textTransform.SetParentFixScale(backgroundTranform);
			textObject.AddComponent<CanvasRenderer>();

			textComponent = textObject.AddComponent<TextMeshProUGUI>();
			textComponent.raycastTarget = false;
			textComponent.color = KsmGuiStyle.textColor;
			textComponent.font = KsmGuiStyle.textFont;
			textComponent.fontSize = KsmGuiStyle.textSize;
			textComponent.alignment = TextAlignmentOptions.Top;

			tooltipObject.SetLayerRecursive(5);
			//KsmGuiBase.ApplyCanvasScalerScale(TopTransform); 
			tooltipObject.SetActive(false);
			IsVisible = false;
		}

		public void ShowTooltip(KsmGuiTooltipBase tooltip, TextAlignmentOptions textAlignement = TextAlignmentOptions.Top, float maxWidth = -1f, Func<KsmGuiBase> tooltipContent = null)
		{
			currentTooltip = tooltip;

			if (string.IsNullOrEmpty(currentTooltip.Text) && tooltipContent == null)
			{
				HideTooltip();
				return;
			}

			if (maxWidth == -1f)
			{
				topFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
				
			}
			else if (maxWidth == 0f)
			{
				topFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
				maxWidth = KsmGuiStyle.tooltipMaxWidth;
				TopTransform.sizeDelta = new Vector2(maxWidth, TopTransform.sizeDelta.y);
			}
			else
			{
				topFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
				TopTransform.sizeDelta = new Vector2(maxWidth, TopTransform.sizeDelta.y);
			}

			textComponent.enabled = true;
			textComponent.alignment = textAlignement;
			textComponent.SetText(currentTooltip.Text);

			content?.TopObject.DestroyGameObject();

			if (tooltipContent != null)
			{
				topFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
				content = tooltipContent();
				content.TopTransform.SetParentFixScale(backgroundTranform);
			}

			tooltipObject.SetActive(true);
			LayoutRebuilder.ForceRebuildLayoutImmediate(TopTransform);
			IsVisible = true;
		}

		public void HideTooltip()
		{
			content?.TopObject.DestroyGameObject();
			currentTooltip = null;
			tooltipObject.SetActive(false);
			IsVisible = false;
		}

		private void Update()
		{
			if (IsVisible)
			{
				Vector3 mouseWorldPos;
				Vector3 position = new Vector3();
				RectTransformUtility.ScreenPointToWorldPointInRectangle(TopTransform, Input.mousePosition, UIMasterController.Instance.uiCamera, out mouseWorldPos);

				position.x = mouseWorldPos.x - (ContentTransform.rect.width * ContentTransform.lossyScale.x * 0.5f);
				position.y = mouseWorldPos.y + 15f;

				if (position.x < -0.5f * Screen.width)
					position.x = -0.5f * Screen.width;
				else if (position.x + ContentTransform.rect.width * ContentTransform.lossyScale.x > 0.5f * Screen.width)
					position.x = 0.5f * Screen.width - ContentTransform.rect.width * ContentTransform.lossyScale.x;

				if (position.y + ContentTransform.rect.height * ContentTransform.lossyScale.y > 0.5f * Screen.height)
					position.y = 0.5f * Screen.height - ContentTransform.rect.height * ContentTransform.lossyScale.y;

				TopTransform.position = position;

				textComponent.SetText(currentTooltip.Text);
			}
		}
	}
}
