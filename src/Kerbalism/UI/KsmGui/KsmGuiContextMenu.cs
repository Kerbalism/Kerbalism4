using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KERBALISM.KsmGui;
using KSP.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KERBALISM
{
	public class KsmGuiContextMenu : MonoBehaviour
	{
		private const float hoverOffset = 25f;

		public static KsmGuiContextMenu Instance { get; private set; }

		private RectTransform topTransform;
		private KsmGuiVerticalLayout content;

		private GameObject menuObject;
		private ContentSizeFitter topFitter;

		private KsmGuiBase target;
		private TextAnchor anchor;

		private Vector3[] corners = new Vector3[4];

		private void Awake()
		{
			Instance = this;

			menuObject = new GameObject("KsmGuiContextMenu");

			topTransform = menuObject.AddComponent<RectTransform>();
			// default of 0, 1 mean pivot is at the window top-left corner
			// pivotX = 0 => left, pivotX = 1 => right
			// pivotY = 0 => bottom, pivotY = 1 => top
			topTransform.pivot = new Vector2(1f, 0f);
			// distance in pixels between the pivot and the center of the screen
			topTransform.anchoredPosition = new Vector2(0f, 0f);

			topTransform.sizeDelta = new Vector2(KsmGuiStyle.tooltipMaxWidth, 0f); // max width of tooltip, text wrap will occur if larger.

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
			topTransform.SetParentFixScale(UIMasterController.Instance.tooltipCanvas.transform);

			menuObject.AddComponent<CanvasRenderer>();

			CanvasGroup group = menuObject.AddComponent<CanvasGroup>();
			group.interactable = true;
			group.blocksRaycasts = true;

			VerticalLayoutGroup toplayout = menuObject.AddComponent<VerticalLayoutGroup>();
			toplayout.childAlignment = TextAnchor.UpperLeft;
			toplayout.childControlHeight = true;
			toplayout.childControlWidth = true;
			toplayout.childForceExpandHeight = false;
			toplayout.childForceExpandWidth = false;
			int offset = (int)hoverOffset;
			toplayout.padding = new RectOffset(offset, offset, offset, offset);

			topFitter = menuObject.AddComponent<ContentSizeFitter>();
			topFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
			topFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

			RawImage mouseAreaImg = menuObject.AddComponent<RawImage>();
			mouseAreaImg.color = new Color(0f, 0f, 0f, 0f);

			menuObject.AddComponent<KsmGuiContextMenuArea>();

			//GameObject mouseOver = new GameObject("KsmGuiMenuMouseOver");
			//RectTransform mouseOverTransform = mouseOver.AddComponent<RectTransform>();
			//mouseOverTransform.SetParentFixScale(topTransform);
			//mouseOverTransform.anchorMin = Vector2.zero;
			//mouseOverTransform.anchorMax = Vector2.one;
			//mouseOverTransform.sizeDelta = new Vector2(25f, 25f);

			//mouseOver.AddComponent<CanvasRenderer>();

			//RawImage mouseAreaImg = mouseOver.AddComponent<RawImage>();
			//mouseAreaImg.color = new Color(0f, 0f, 0f, 0f);

			//mouseOver.AddComponent<KsmGuiContextMenuArea>();

			// first child : 1px white border
			GameObject border = new GameObject("KsmGuiMenuBorder");
			RectTransform borderTransform = border.AddComponent<RectTransform>();
			borderTransform.SetParentFixScale(topTransform);
			border.AddComponent<CanvasRenderer>();

			VerticalLayoutGroup borderLayout = border.AddComponent<VerticalLayoutGroup>();
			borderLayout.padding = new RectOffset(1, 1, 1, 1);
			borderLayout.childAlignment = TextAnchor.UpperLeft;
			borderLayout.childControlHeight = true;
			borderLayout.childControlWidth = true;
			borderLayout.childForceExpandHeight = false;
			borderLayout.childForceExpandWidth = false;

			Image borderImage = border.AddComponent<Image>();
			borderImage.color = KsmGuiStyle.tooltipBorderColor;
			//borderImage.raycastTarget = false;

			// 2nd child : black background
			GameObject background = new GameObject("KsmGuiMenuBackground");
			RectTransform backgroundTranform = background.AddComponent<RectTransform>();
			backgroundTranform.SetParentFixScale(borderTransform);
			background.AddComponent<CanvasRenderer>();

			VerticalLayoutGroup backgroundLayout = background.AddComponent<VerticalLayoutGroup>();
			backgroundLayout.padding = new RectOffset(5, 5, 2, 2);
			backgroundLayout.childAlignment = TextAnchor.UpperLeft;
			backgroundLayout.childControlHeight = true;
			backgroundLayout.childControlWidth = true;
			backgroundLayout.childForceExpandHeight = false;
			backgroundLayout.childForceExpandWidth = false;

			Image backgroundImage = background.AddComponent<Image>();
			backgroundImage.color = KsmGuiStyle.tooltipBackgroundColor;
			//backgroundImage.raycastTarget = false;

			// last child : content
			content = new KsmGuiVerticalLayout(null);
			content.TopTransform.SetParentFixScale(backgroundTranform);
			content.LayoutOptimizer.enabled = false;

			menuObject.SetLayerRecursive(5);
			menuObject.SetActive(false);
		}

		public KsmGuiVerticalLayout Create(KsmGuiBase target, TextAnchor anchor = TextAnchor.UpperLeft)
		{
			this.target = target;
			this.anchor = anchor;

			for (int i = content.TopTransform.childCount - 1; i >= 0; i--)
			{
				content.TopTransform.GetChild(i).gameObject.DestroyGameObject();
			}

			// pivotX = 0 => left, pivotX = 1 => right
			// pivotY = 0 => bottom, pivotY = 1 => top
			topTransform.pivot = new Vector2(1f, 0f);

			switch (anchor)
			{
				case TextAnchor.UpperLeft:    topTransform.pivot = new Vector2(1.0f, 0.0f); break;
				case TextAnchor.UpperCenter:  topTransform.pivot = new Vector2(0.5f, 0.0f); break;
				case TextAnchor.UpperRight:   topTransform.pivot = new Vector2(0.0f, 0.0f); break;
				case TextAnchor.MiddleLeft:   topTransform.pivot = new Vector2(1.0f, 0.5f); break;
				case TextAnchor.MiddleRight:  topTransform.pivot = new Vector2(0.0f, 0.5f); break;
				case TextAnchor.LowerLeft:    topTransform.pivot = new Vector2(1.0f, 1.0f); break;
				case TextAnchor.LowerCenter:  topTransform.pivot = new Vector2(0.5f, 1.0f); break;
				case TextAnchor.LowerRight:   topTransform.pivot = new Vector2(0.0f, 1.0f); break;
			}

			target.TopTransform.GetWorldCorners(corners);

			float offset = hoverOffset * topTransform.lossyScale.x;
			Vector3 position = Vector3.zero;

			switch (anchor)
			{
				case TextAnchor.LowerLeft: position = new Vector3(corners[0].x + offset, corners[0].y + offset); break;
				case TextAnchor.UpperLeft: position = new Vector3(corners[1].x + offset, corners[1].y - offset); break;
				case TextAnchor.UpperRight: position = new Vector3(corners[2].x - offset, corners[2].y - offset); break;
				case TextAnchor.LowerRight: position = new Vector3(corners[3].x - offset, corners[3].y + offset); break;

				case TextAnchor.UpperCenter:
					position = new Vector3((corners[1].x + corners[2].x) * 0.5f, corners[1].y - offset);
					break;
				case TextAnchor.MiddleLeft:
					position = new Vector3(corners[0].x + offset, (corners[0].y + corners[1].y) * 0.5f);
					break;
				case TextAnchor.MiddleRight:
					position = new Vector3(corners[2].x - offset, (corners[2].y + corners[3].y) * 0.5f);
					break;
				case TextAnchor.LowerCenter:
					position = new Vector3((corners[0].x + corners[3].x) * 0.5f, corners[0].y + offset);
					break;
			}

			topTransform.position = position;

			menuObject.SetActive(true);

			return content;
		}

		public void Destroy()
		{
			for (int i = content.TopTransform.childCount - 1; i >= 0; i--)
			{
				content.TopTransform.GetChild(i).gameObject.DestroyGameObject();
			}
			menuObject.SetActive(false);
		}

		public KsmGuiTextButton AddButton(string label, Action callback)
		{
			void CloseCallback()
			{
				callback();
				Destroy();
			}

			return new KsmGuiTextButton(content, label, CloseCallback);
		}

		private void Update()
		{
			if (!menuObject.activeSelf)
				return;

			((RectTransform)UIMasterController.Instance.tooltipCanvas.transform).GetWorldCorners(corners);
			// BL = Bottom Left, TR = Top Right (corners)
			Vector3 containerBL = corners[0], containerTR = corners[2];
			Vector3 containerSize = containerTR - containerBL;
			topTransform.GetWorldCorners(corners);
			Vector3 movableBL = corners[0], movableTR = corners[2];
			Vector3 movableSize = movableTR - movableBL;

			Vector3 position = topTransform.position;
			Vector3 deltaBL = position - movableBL, deltaTR = movableTR - position;
			position.x = movableSize.x < containerSize.x
				? Mathf.Clamp(position.x, containerBL.x + deltaBL.x, containerTR.x - deltaTR.x)
				: Mathf.Clamp(position.x, containerTR.x - deltaTR.x, containerBL.x + deltaBL.x);
			position.y = movableSize.y < containerSize.y
				? Mathf.Clamp(position.y, containerBL.y + deltaBL.y, containerTR.y - deltaTR.y)
				: Mathf.Clamp(position.y, containerTR.y - deltaTR.y, containerBL.y + deltaBL.y);

			topTransform.position = position;
		}

		private class KsmGuiContextMenuArea : MonoBehaviour, IPointerExitHandler
		{
			public void OnPointerExit(PointerEventData pointerEventData)
			{
				KsmGuiContextMenu.Instance.Destroy();
			}
		}
	}
}
