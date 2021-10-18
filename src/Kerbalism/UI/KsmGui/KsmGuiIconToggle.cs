using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace KERBALISM.KsmGui
{
	public class KsmGuiIconToggle : KsmGuiImage, IKsmGuiInteractable
	{
		public Button ButtonComponent { get; private set; }
		private Action<bool> onValueChanged;

		private bool value;
		public bool Value
		{
			get => value;
			set
			{
				if (this.value != value)
				{
					this.value = value;
					UpdateState();
					onValueChanged?.Invoke(Value);
				}
			}
		}

		private bool hasStateColors = false;
		private Color whenTrueColor;
		private Color whenFalseColor;

		private bool hasStateTextures = false;
		private Texture2D whenTrueTexture;
		private Texture2D whenFalseTexture;
		

		public KsmGuiIconToggle(KsmGuiBase parent, bool initalValue = false, Action<bool> valueChangedAction = null, int iconWidth = -1, int iconHeight = -1)
			: base(parent, Texture2D.whiteTexture, iconWidth, iconHeight)
		{
			ButtonComponent = TopObject.AddComponent<Button>();
			ButtonComponent.targetGraphic = Image;
			ButtonComponent.interactable = true;
			ButtonComponent.transition = Selectable.Transition.ColorTint;
			ButtonComponent.colors = KsmGuiStyle.iconTransitionColorBlock;
			ButtonComponent.navigation = new Navigation() { mode = Navigation.Mode.None }; // fix the transitions getting stuck
			ButtonComponent.onClick.AddListener(OnClick);

			this.Value = initalValue;
			SetValueChangedAction(valueChangedAction);
		}

		public KsmGuiIconToggle(KsmGuiBase parent, Texture2D whenTrueTexture, Texture2D whenFalseTexture, bool initalValue = false, Action<bool> valueChangedAction = null, int iconWidth = -1, int iconHeight = -1)
			: this(parent, initalValue, valueChangedAction, iconWidth, iconHeight)
		{
			SetStateTexture(whenTrueTexture, whenFalseTexture);
		}

		public KsmGuiIconToggle(KsmGuiBase parent, Texture2D texture, Kolor whenTrue, Kolor whenFalse, bool initalValue = false, Action<bool> valueChangedAction = null, int iconWidth = -1, int iconHeight = -1)
			: this(parent, initalValue, valueChangedAction, iconWidth, iconHeight)
		{
			Image.texture = texture;
			SetStateColors(whenTrue, whenFalse);
		}

		public KsmGuiIconToggle(KsmGuiBase parent, Texture2D texture, Color whenTrue, Color whenFalse, bool initalValue = false, Action<bool> valueChangedAction = null, int iconWidth = -1, int iconHeight = -1)
			: this(parent, initalValue, valueChangedAction, iconWidth, iconHeight)
		{
			Image.texture = texture;
			SetStateColors(whenTrue, whenFalse);
		}


		private void OnClick()
		{
			Value = !Value;
			UpdateState();

			onValueChanged?.Invoke(Value);
		}

		public void UpdateState()
		{
			if (hasStateColors)
			{
				Image.color = Value ? whenTrueColor : whenFalseColor;
			}

			if (hasStateTextures)
			{
				Image.texture = Value ? whenTrueTexture : whenFalseTexture;
			}
		}

		public bool Interactable
		{
			get => ButtonComponent.interactable;
			set => ButtonComponent.interactable = value;
		}

		public void SetValueChangedAction(Action<bool> action)
		{
			onValueChanged = action;
		}

		public void SetStateColors(Color whenTrue, Color whenFalse)
		{
			hasStateColors = true;
			whenTrueColor = whenTrue;
			whenFalseColor = whenFalse;
			UpdateState();
		}

		public void SetStateColors(Kolor whenTrue, Kolor whenFalse)
		{
			hasStateColors = true;
			whenTrueColor = whenTrue.color;
			whenFalseColor = whenFalse.color;
			UpdateState();
		}

		public void SetStateTexture(Texture2D whenTrue, Texture2D whenFalse)
		{
			hasStateTextures = true;
			whenTrueTexture = whenTrue;
			whenFalseTexture = whenFalse;
			UpdateState();
		}
	}
}
