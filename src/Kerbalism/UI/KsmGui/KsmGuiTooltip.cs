using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KERBALISM.KsmGui
{
	public class KsmGuiTooltipBase : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
	{
		public virtual string Text => string.Empty;
		protected TextAlignmentOptions textAlignement;
		protected float width;
		protected Func<KsmGuiBase> content;

		public void OnPointerEnter(PointerEventData eventData)
		{
			KsmGuiTooltipController.Instance.ShowTooltip(this, textAlignement, width, content);
		}

		public void OnPointerExit(PointerEventData eventData)
		{
			KsmGuiTooltipController.Instance.HideTooltip();
		}
	}


	public class KsmGuiTooltipStatic : KsmGuiTooltipBase
	{
		private string tooltipText;
		public override string Text => tooltipText;

		public void SetTooltipText(string text, TextAlignmentOptions textAlignement = TextAlignmentOptions.Top, float width = -1f, Func<KsmGuiBase> content = null)
		{
			this.textAlignement = textAlignement;
			this.width = width;
			this.content = content;

			tooltipText = text;
		}
	}

	public class KsmGuiTooltipDynamic : KsmGuiTooltipBase
	{
		private Func<string> textFunc;

		public override string Text => textFunc();

		public void SetTooltipText(Func<string> textFunc, TextAlignmentOptions textAlignement = TextAlignmentOptions.Top, float width = -1f, Func<KsmGuiBase> content = null)
		{
			this.textAlignement = textAlignement;
			this.width = width;
			this.content = content;
			this.textFunc = textFunc;
		}
	}
}
