using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KERBALISM.KsmGui
{
	public class KsmGuiTooltipBase : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
	{
		protected bool IsTooltipOverThis = false;

		protected virtual string Text => string.Empty;
		protected TextAlignmentOptions textAlignement;
		protected float width;
		protected KsmGuiBase content;

		public void OnPointerEnter(PointerEventData eventData)
		{
			KsmGuiTooltipController.Instance.ShowTooltip(Text, textAlignement, width, content);
			IsTooltipOverThis = true;
		}

		public void OnPointerExit(PointerEventData eventData)
		{
			KsmGuiTooltipController.Instance.HideTooltip();
			IsTooltipOverThis = false;
		}
	}


	public class KsmGuiTooltipStatic : KsmGuiTooltipBase
	{
		private string tooltipText;
		protected override string Text => tooltipText;

		public void SetTooltipText(string text, TextAlignmentOptions textAlignement = TextAlignmentOptions.Top, float width = -1f, KsmGuiBase content = null)
		{
			this.textAlignement = textAlignement;
			this.width = width;
			this.content = content;

			tooltipText = text;
			if (IsTooltipOverThis)
				KsmGuiTooltipController.Instance.SetTooltipText(text);
		}
	}

	public class KsmGuiTooltipDynamic : KsmGuiTooltipBase
	{
		private Func<string> textFunc;

		protected override string Text => textFunc();

		public void SetTooltipText(Func<string> textFunc, TextAlignmentOptions textAlignement = TextAlignmentOptions.Top, float width = -1f, KsmGuiBase content = null)
		{
			this.textAlignement = textAlignement;
			this.width = width;
			this.content = content;

			this.textFunc = textFunc;

			if (IsTooltipOverThis)
				KsmGuiTooltipController.Instance.SetTooltipText(Text);
		}
	}
}
