using System;
using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KERBALISM.KsmGui
{
	public class KsmGuiTooltipBase : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
	{
		public bool TooltipEnabled { get; set; } = true;

		public virtual void OnShowTooltip() {}

		public virtual void OnHideTooltip() {}

		public virtual void OnTooltipUpdate() {}

		public void OnPointerEnter(PointerEventData eventData)
		{
			KsmGuiTooltipController.Instance.ShowTooltip(this);
		}

		public void OnPointerExit(PointerEventData eventData)
		{
			KsmGuiTooltipController.Instance.HideTooltip();
		}

		private void OnDisable()
		{
			if (KsmGuiTooltipController.Instance.CurrentTooltip == this)
			{
				KsmGuiTooltipController.Instance.HideTooltip();
			}
		}

		private void OnDestroy()
		{
			if (KsmGuiTooltipController.Instance.CurrentTooltip == this)
			{
				KsmGuiTooltipController.Instance.HideTooltip();
			}
		}
	}

	public class KsmGuiTooltipStaticText : KsmGuiTooltipBase
	{
		private string tooltipText;
		private TextAlignmentOptions textAlignement;
		private int maxWidth;

		public void Setup(string text, TextAlignmentOptions textAlignement = TextAlignmentOptions.Top, int maxWidth = 300)
		{
			SetText(text);
			this.textAlignement = textAlignement;
			this.maxWidth = maxWidth;
		}

		public void SetText(string text)
		{
			if (string.IsNullOrEmpty(text))
			{
				TooltipEnabled = false;
			}
			else
			{
				TooltipEnabled = true;
				tooltipText = text;

				if (KsmGuiTooltipController.Instance.CurrentTooltip == this)
				{
					KsmGuiTooltipController.Instance.TextComponent.text = tooltipText;
				}
			}
		}

		public override void OnShowTooltip()
		{
			KsmGuiTooltipController controller = KsmGuiTooltipController.Instance;
			controller.TextComponent.enabled = true;
			controller.TextComponent.alignment = textAlignement;
			controller.TextComponent.text = tooltipText;
			controller.SetMaxWidth(maxWidth);
		}
	}

	public class KsmGuiTooltipDynamicText : KsmGuiTooltipBase
	{
		public Func<string> textFunc;
		private TextAlignmentOptions textAlignement;
		private int maxWidth;
		private string text;

		public void Setup(Func<string> textFunc, TextAlignmentOptions textAlignement = TextAlignmentOptions.Top, int maxWidth = 300)
		{
			this.textFunc = textFunc;
			this.textAlignement = textAlignement;
			this.maxWidth = maxWidth;
		}

		public override void OnShowTooltip()
		{
			KsmGuiTooltipController controller = KsmGuiTooltipController.Instance;
			controller.TextComponent.enabled = true;
			controller.TextComponent.alignment = textAlignement;
			controller.TextComponent.text = textFunc();
			controller.SetMaxWidth(maxWidth);
		}

		public override void OnTooltipUpdate()
		{
			text = textFunc();

			if (string.IsNullOrEmpty(text))
			{
				TooltipEnabled = false;
			}
			else
			{
				TooltipEnabled = true;
			}

			KsmGuiTooltipController.Instance.TextComponent.text = text;
		}
	}

	public class KsmGuiTooltipDynamicContent : KsmGuiTooltipBase
	{
		public Func<KsmGuiBase> contentBuilder;
		public KsmGuiBase content;

		public void Setup(Func<KsmGuiBase> contentBuilder)
		{
			this.contentBuilder = contentBuilder;
		}

		public override void OnShowTooltip()
		{
			KsmGuiTooltipController controller = KsmGuiTooltipController.Instance;
			controller.TextComponent.enabled = false;
			controller.SetMaxWidth(-1);
			content = contentBuilder();
			content.LayoutOptimizer.enabled = false;
			content.TopTransform.SetParentFixScale(controller.ContentTransform);
		}

		public override void OnHideTooltip()
		{
			if (content != null)
			{
				content.TopObject.DestroyGameObject();
				content = null;
			}
		}
	}
}
