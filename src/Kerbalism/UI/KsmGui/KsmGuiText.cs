using TMPro;

namespace KERBALISM.KsmGui
{
	public class KsmGuiText : KsmGuiBase, IKsmGuiText
	{
		public TextMeshProUGUI TextComponent { get; private set; }
		

		private TextAlignmentOptions savedAlignement;
		private bool useEllipsisWithTooltip = false;

		public KsmGuiText(
			KsmGuiBase parent,
			string text = null,
			TextAlignmentOptions alignement = TextAlignmentOptions.TopLeft,
			bool wordWrap = true,
			TextOverflowModes overflowMode = TextOverflowModes.Overflow
			) : base(parent)
		{
			savedAlignement = alignement;
			TextComponent = TopObject.AddComponent<TextMeshProUGUI>();
			TextComponent.color = KsmGuiStyle.textColor;
			TextComponent.font = KsmGuiStyle.textFont;
			TextComponent.fontSize = KsmGuiStyle.textSize;
			TextComponent.alignment = alignement;
			TextComponent.enableWordWrapping = wordWrap;
			TextComponent.overflowMode = overflowMode;

			if (!string.IsNullOrEmpty(text))
				TextComponent.text = text;

			SetLayoutElement(true);
		}

		// note : this only works reliably with the ellipsis mode, not with the truncate mode...
		public void UseEllipsisWithTooltip()
		{
			TextComponent.overflowMode = TextOverflowModes.Ellipsis;
			TextComponent.enableWordWrapping = true;
			useEllipsisWithTooltip = true;
			SetTooltip(string.Empty);
		}

		public string Text
		{
			get => TextComponent.text;
			set
			{
				if (value == null)
					value = string.Empty;

				// DON'T USE SetText() !!!!
				// SetText() won't check :
				// - if the text has actually changed
				// - if the changed text is actually causing a size change
				// This reduce occurences of the canvas being marked dirty by 99+%,
				// For complex windows, the savings can be several ms per update !
				TextComponent.text = value;

				if (useEllipsisWithTooltip)
				{
					TextComponent.ForceMeshUpdate();
					if (TextComponent.isTextTruncated)
					{
						SetTooltip(value);
					}
					else
					{
						SetTooltip(string.Empty);
					}
				}
			}
		}

		// workaround for a textmeshpro bug :
		// https://forum.unity.com/threads/textmeshprougui-alignment-resets-when-enabling-disabling-gameobject.549784/#post-3901597
		public override bool Enabled
		{
			get => base.Enabled;
			set
			{
				base.Enabled = value;
				TextComponent.alignment = savedAlignement;
			}
		}

	}
}
