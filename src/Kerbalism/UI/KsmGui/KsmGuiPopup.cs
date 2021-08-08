using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KERBALISM.KsmGui
{
	public class KsmGuiPopup : KsmGuiVerticalLayout
	{
		private class KsmGuiPopupArea : MonoBehaviour, IPointerExitHandler
		{
			public KsmGuiPopup popup;

			public void OnPointerExit(PointerEventData pointerEventData)
			{
				popup.OnPointerExit();
			}
		}

		public override RectTransform ParentTransformForChilds => contentParent?.TopTransform ?? TopTransform;

		private readonly KsmGuiVerticalLayout contentParent;

		public KsmGuiPopup(KsmGuiBase parent, TextAnchor originInParent = TextAnchor.UpperLeft, TextAnchor pivot = TextAnchor.LowerRight) : base(parent, 0, 50, 50, 50, 50)
		{
			TopTransform.SetAnchorsAndPosition(originInParent, pivot, 50, -50);

			KsmGuiPopupArea popupArea = TopObject.AddComponent<KsmGuiPopupArea>();
			popupArea.popup = this;

			RawImage mouseAreaImg = TopObject.AddComponent<RawImage>();
			mouseAreaImg.color = new Color(0f, 0f, 0f, 0f);

			ContentSizeFitter topFitter = TopObject.AddComponent<ContentSizeFitter>();
			topFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize; //.Unconstrained;
			topFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;


			// first child : 1px white border
			KsmGuiVerticalLayout whiteBorder = new KsmGuiVerticalLayout(this, 0, 1, 1, 1, 1);
			Image borderImage = whiteBorder.TopObject.AddComponent<Image>();
			borderImage.color = KsmGuiStyle.tooltipBorderColor;

			// 2nd child : black background
			contentParent = new KsmGuiVerticalLayout(whiteBorder, 0, 5, 5, 2, 2);
			Image backgroundImage = contentParent.TopObject.AddComponent<Image>();
			backgroundImage.color = KsmGuiStyle.tooltipBackgroundColor;
		}

		private void OnPointerExit()
		{
			Destroy();
		}

		public KsmGuiTextButton AddButton(string label, Action callback)
		{
			void CloseCallback()
			{
				callback();
				Destroy();
			}

			return new KsmGuiTextButton(contentParent, label, CloseCallback);
		}
	}
}
