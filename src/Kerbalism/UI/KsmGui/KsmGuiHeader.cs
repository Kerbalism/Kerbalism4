using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace KERBALISM.KsmGui
{
	public class KsmGuiHeader : KsmGuiHorizontalLayout, IKsmGuiText
	{


		public KsmGuiText TextObject { get; private set; }

		public KsmGuiHeader(KsmGuiBase parent, string title, string tooltip = null, Color backgroundColor = default, int textPreferredWidth = -1)
			: base(parent, 2, 0, 0, 0, 0, TextAnchor.UpperLeft)
		{
			// default : black background
			Image image = TopObject.AddComponent<Image>();
			if (backgroundColor == default)
				image.color = Color.black;
			else
				image.color = backgroundColor;


			TextObject = new KsmGuiText(this, title, TextAlignmentOptions.Center);
			TextObject.TextComponent.fontStyle = FontStyles.UpperCase;
			TextObject.SetLayoutElement(true, false, textPreferredWidth, -1, -1, 16);

			if (tooltip != null)
				TextObject.SetTooltip(tooltip);
		}

		public string Text
		{
			get => TextObject.Text;
			set => TextObject.Text = value;
		}

		public KsmGuiIconButton AddButton(Texture2D texture, UnityAction onClick = null, string tooltip = null, bool leftSide = false)
		{
			KsmGuiIconButton button = new KsmGuiIconButton(this, texture, onClick, 16, 16);
			button.SetLayoutElement(false, false, 16, 16);

			if (leftSide)
				button.MoveAsFirstChild();

			if (tooltip != null)
				button.SetTooltip(tooltip);

			return button;
		}

		public KsmGuiIconToggle AddToggle(Texture2D whenTrueTexture, Texture2D whenFalseTexture, bool initalValue = false, Action<bool> valueChangedAction = null, bool leftSide = false)
		{
			KsmGuiIconToggle toggle = new KsmGuiIconToggle(this, whenTrueTexture, whenFalseTexture, initalValue, valueChangedAction, 16, 16);
			toggle.SetLayoutElement(false, false, 16, 16);
			if (leftSide)
				toggle.MoveAsFirstChild();

			return toggle;
		}

		public KsmGuiIconToggle AddToggle(Texture2D texture, Kolor whenTrue, Kolor whenFalse, bool initalValue = false, Action<bool> valueChangedAction = null, bool leftSide = false)
		{
			KsmGuiIconToggle toggle = new KsmGuiIconToggle(this, texture, whenTrue, whenFalse, initalValue, valueChangedAction, 16, 16);
			toggle.SetLayoutElement(false, false, 16, 16);
			if (leftSide)
				toggle.MoveAsFirstChild();
			return toggle;
		}
	}
}
