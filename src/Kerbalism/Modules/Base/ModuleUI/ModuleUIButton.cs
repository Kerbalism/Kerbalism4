using HarmonyLib;
using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.Object;

namespace KERBALISM.ModuleUI
{
	public abstract class ModuleUIButton<THandler> : ModuleInteractableBase<THandler>, IModuleUIButton where THandler : ModuleHandler
	{
		[UI_KSMButton] private static object dummyField;
		private static UI_KSMButton ui_control = new UI_KSMButton();
		private static FieldInfo fieldInfo = AccessTools.Field(typeof(ModuleUIButton<THandler>), nameof(dummyField));

		protected override FieldInfo DummyFieldInfo => fieldInfo;
		protected override UI_Control UI_Control => ui_control;

		public abstract void OnClick();
	}

	public class UI_KSMButton : UI_Control { }

	[UI_KSMButton]
	public class UIPartActionKsmButton : UIPartActionFieldItem
	{
		public static UIPartActionKsmButton CreatePrefab(UIPartActionButton uiButton)
		{
			GameObject buttonPrefab = Instantiate(uiButton.gameObject);
			buttonPrefab.name = nameof(UIPartActionKsmButton);

			UIPartActionButton stockButton = buttonPrefab.GetComponent<UIPartActionButton>();
			UIPartActionKsmButton ksmButton = buttonPrefab.AddComponent<UIPartActionKsmButton>();
			ksmButton.label = stockButton.label;
			ksmButton.button = stockButton.button;
			DestroyImmediate(stockButton);

			ksmButton.label.lineSpacing = 0f;

			DontDestroyOnLoad(buttonPrefab);
			buttonPrefab.transform.SetParent(Loader.KerbalismPrefabs.transform);
			return ksmButton;
		}

		public TextMeshProUGUI label;
		public Button button;

		private void Start()
		{
			button.onClick.AddListener(OnClick);
		}

		public override void UpdateItem()
		{
			label.text = ((IModuleUILabel)field.host).GetLabel();
			button.interactable = ((IModuleUIInteractable)field.host).IsInteractable;
		}

		public void OnClick()
		{
			// TODO: should we handle control state lock ? (full / partial / none) ? the stock handling is quite a mess...
			Mouse.Left.ClearMouseState();
			((IModuleUIButton)field.host).OnClick();
		}

		public override bool IsItemValid()
		{
			return base.IsItemValid() && ((ModuleUIBase)field.host).IsEnabled;
		}
	}


}
