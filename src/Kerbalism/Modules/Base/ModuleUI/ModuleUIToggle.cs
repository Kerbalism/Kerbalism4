using HarmonyLib;
using System;
using System.Reflection;
using KSP.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.Object;

namespace KERBALISM.ModuleUI
{
	public abstract class ModuleUIToggle<THandler> : ModuleInteractableBase<THandler>, IModuleUIToggle where THandler : ModuleHandler
	{
		[UI_KSMToggle] private static object dummyField;
		private static UI_KSMToggle ui_control = new UI_KSMToggle();
		private static FieldInfo fieldInfo = AccessTools.Field(typeof(ModuleUIToggle<THandler>), nameof(dummyField));

		protected override FieldInfo DummyFieldInfo => fieldInfo;
		protected override UI_Control UI_Control => ui_control;

		public abstract bool State { get; }
		public abstract void OnToggle();
	}

	public class UI_KSMToggle : UI_Control { }

	[UI_KSMToggle]
	public class UIPartActionKsmToggle : UIPartActionFieldItem
	{
		public static UIPartActionKsmToggle CreatePrefab(UIPartActionToggle uiToggle)
		{
			GameObject prefab = Instantiate(uiToggle.gameObject);
			prefab.name = nameof(UIPartActionKsmToggle);
			UIPartActionToggle stockToggleComponent = prefab.GetComponent<UIPartActionToggle>();
			DestroyImmediate(stockToggleComponent.GetComponent<UIMarquee_PAW>()); // destroy the useless status scroller component
			DestroyImmediate(stockToggleComponent.tipText.gameObject); // destroy the additional line thing
			DestroyImmediate(stockToggleComponent.fieldStatus.transform.parent.gameObject); // destroy the status top object ("marqueeHolder"). We use a single label.


			// destroy the stock toggle component, but get useful references from it first
			UIPartActionKsmToggle ksmToggle = prefab.AddComponent<UIPartActionKsmToggle>();
			ksmToggle.label = stockToggleComponent.fieldName;
			ksmToggle.toggle = stockToggleComponent.toggle;
			DestroyImmediate(stockToggleComponent);

			// setup label transform to stretch in parent
			RectTransform labelTransform = (RectTransform) ksmToggle.label.transform;
			labelTransform.pivot = new Vector2(0f, 0.5f);
			labelTransform.anchorMin = new Vector2(0f, 1f);
			labelTransform.anchorMax = new Vector2(1f, 1f);
			labelTransform.anchoredPosition = new Vector2(42f, -7f);
			labelTransform.sizeDelta = new Vector2(-42f, 14f);

			// tweak stuff
			ksmToggle.label.lineSpacing = 0f; // allow same line left/right rtf alignement tricks to work
			prefab.GetComponent<LayoutElement>().preferredHeight = 14f; // fix default height being set to 28
			ksmToggle.toggle.toggleImage.color = new Color(0f, 0.631f, 0.725f, 1f); // fix color mismatch between toggles and buttons

			// register the prefab and store it
			DontDestroyOnLoad(prefab);
			prefab.transform.SetParent(Loader.KerbalismPrefabs.transform);
			return ksmToggle;
		}

		public TextMeshProUGUI label;
		public UIButtonToggle toggle;

		private void Start()
		{
			toggle.onToggle.AddListener(OnToggle);
		}

		public override void UpdateItem()
		{
			if (this == null)
				return;

			label.text = ((IModuleUILabel)field.host).GetLabel();
			toggle.SetState(((IModuleUIToggle)field.host).State);
			toggle.interactable = ((IModuleUIInteractable)field.host).IsInteractable;
		}

		private void OnToggle()
		{
			Mouse.Left.ClearMouseState();
			((IModuleUIToggle)field.host).OnToggle();
		}

		public override bool IsItemValid()
		{
			return base.IsItemValid() && ((ModuleUIBase)field.host).IsEnabled;
		}
	}
}
