using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace KERBALISM.KsmGui
{
	public class KsmGuiIconButton : KsmGuiImage, IKsmGuiInteractable, IKsmGuiButton
	{
		public Button ButtonComponent { get; private set; }
		private UnityAction onClick;

		public KsmGuiIconButton(KsmGuiBase parent, Texture2D texture, UnityAction onClick = null, int width = -1, int height = -1)
			: base(parent, texture, iconWidth: width, iconHeight: height)
		{
			ButtonComponent = TopObject.AddComponent<Button>();
			ButtonComponent.targetGraphic = Image;
			ButtonComponent.interactable = true;
			ButtonComponent.transition = Selectable.Transition.ColorTint;
			ButtonComponent.colors = KsmGuiStyle.iconTransitionColorBlock;
			ButtonComponent.navigation = new Navigation() { mode = Navigation.Mode.None }; // fix the transitions getting stuck

			this.onClick = onClick;
			SetButtonOnClick(onClick);
		}

		public bool Interactable
		{
			get => ButtonComponent.interactable;
			set => ButtonComponent.interactable = value;
		}

		public void SetButtonOnClick(UnityAction action)
		{
			if (onClick != null)
				ButtonComponent.onClick.RemoveListener(onClick);

			onClick = action;

			if (action != null)
				ButtonComponent.onClick.AddListener(onClick);
		}
	}
}
