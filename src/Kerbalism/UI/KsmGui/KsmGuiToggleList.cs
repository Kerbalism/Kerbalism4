using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace KERBALISM.KsmGui
{

	public class KsmGuiToggleList<T> : KsmGuiBase
	{
		public ToggleGroup ToggleGroupComponent { get; private set; }
		public UnityAction<T, bool> OnToggleSelectedChange { get; set; }
		public List<KsmGuiToggleListElement<T>> ChildToggles { get; private set; } = new List<KsmGuiToggleListElement<T>>();
		public HorizontalOrVerticalLayoutGroup LayoutGroup { get; private set; }

		public KsmGuiToggleList(KsmGuiBase parent, KsmGuiLib.Orientation orientation, UnityAction<T, bool> onToggleSelectedChange) : base(parent)
		{
			switch (orientation)
			{
				case KsmGuiLib.Orientation.Vertical: LayoutGroup = TopObject.AddComponent<VerticalLayoutGroup>(); break;
				case KsmGuiLib.Orientation.Horizontal: LayoutGroup = TopObject.AddComponent<HorizontalLayoutGroup>(); break;
			}

			LayoutGroup.spacing = 2f;
			LayoutGroup.padding = new RectOffset(0, 0, 0, 0);
			LayoutGroup.childControlHeight = true;
			LayoutGroup.childControlWidth = true;
			LayoutGroup.childForceExpandHeight = false;
			LayoutGroup.childForceExpandWidth = false;
			LayoutGroup.childAlignment = TextAnchor.UpperLeft;

			ToggleGroupComponent = TopObject.AddComponent<ToggleGroup>();
			OnToggleSelectedChange = onToggleSelectedChange;
		}
	}

	public class KsmGuiToggleListElement<T> : KsmGuiHorizontalLayout, IKsmGuiInteractable, IKsmGuiText, IKsmGuiToggle
	{
		public KsmGuiText TextObject { get; private set; }
		public Toggle ToggleComponent { get; private set; }
		public T ToggleId { get; private set; }
		private KsmGuiToggleList<T> parent;

		public KsmGuiToggleListElement(KsmGuiToggleList<T> parent, T toggleId, string text) : base(parent)
		{
			ToggleComponent = TopObject.AddComponent<Toggle>();
			ToggleComponent.transition = Selectable.Transition.None;
			ToggleComponent.navigation = new Navigation() { mode = Navigation.Mode.None };
			ToggleComponent.isOn = false;
			ToggleComponent.toggleTransition = Toggle.ToggleTransition.Fade;
			ToggleComponent.group = parent.ToggleGroupComponent;

			this.parent = parent;
			parent.ChildToggles.Add(this);
			ToggleId = toggleId;
			ToggleComponent.onValueChanged.AddListener(NotifyParent);

			Image image = TopObject.AddComponent<Image>();
			image.color = KsmGuiStyle.boxColor;

			SetLayoutElement(false, false, -1, -1, -1, 14);

			KsmGuiHorizontalLayout highlightImage = new KsmGuiHorizontalLayout(this);
			Image bgImage = highlightImage.TopObject.AddComponent<Image>();
			bgImage.color = KsmGuiStyle.selectedBoxColor;
			bgImage.raycastTarget = false;
			ToggleComponent.graphic = bgImage;

			TextObject = new KsmGuiText(highlightImage, text);
			TextObject.SetLayoutElement(true);
		}

		private void NotifyParent(bool selected)
		{
			if (parent.OnToggleSelectedChange != null)
			{
				parent.OnToggleSelectedChange(ToggleId, selected);
			}
		}

		public bool Interactable
		{
			get => ToggleComponent.interactable;
			set => ToggleComponent.interactable = value;
		}

		public string Text
		{
			get => TextObject.Text;
			set => TextObject.Text = value;
		}

		public void SetToggleOnChange(UnityAction<bool> action)
		{
			ToggleComponent.onValueChanged.AddListener(action);
		}
	}
}
