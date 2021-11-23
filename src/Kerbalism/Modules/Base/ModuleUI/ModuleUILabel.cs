using HarmonyLib;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.Object;

namespace KERBALISM.ModuleUI
{
	public abstract class ModuleUILabel<THandler> : ModuleUIBase<THandler>, IModuleUILabel where THandler : ModuleHandler
	{
		[UI_KSMLabel] private static object dummyField;
		private static UI_KSMLabel ui_control = new UI_KSMLabel();
		private static FieldInfo fieldInfo = AccessTools.Field(typeof(ModuleUILabel<THandler>), nameof(dummyField));

		protected override FieldInfo DummyFieldInfo => fieldInfo;
		protected override UI_Control UI_Control => ui_control;

		/// <summary>
		/// Label string. Can be multiline
		/// </summary>
		public abstract string GetLabel();
	}

	public class UI_KSMLabel : UI_Control { }

	[UI_KSMLabel]
	public class UIPartActionKsmLabel : UIPartActionLabel
	{
		public static UIPartActionKsmLabel CreatePrefab(UIPartActionLabel uiLabel)
		{
			GameObject labelPrefab = Instantiate(uiLabel.gameObject);
			labelPrefab.name = nameof(UIPartActionKsmLabel);

			UIPartActionLabel stockLabel = labelPrefab.GetComponent<UIPartActionLabel>();
			UIPartActionKsmLabel ksmLabel = labelPrefab.AddComponent<UIPartActionKsmLabel>();
			DestroyImmediate(stockLabel);

			DontDestroyOnLoad(labelPrefab);
			labelPrefab.transform.SetParent(Loader.KerbalismPrefabs.transform);
			return ksmLabel;
		}

		private LayoutElement layoutElement;

		private void Start()
		{
			layoutElement = gameObject.GetComponent<LayoutElement>();
		}

		public override void UpdateItem()
		{
			string text = ((IModuleUILabel)field.host).GetLabel();
			labelText.text = text;
			if (layoutElement != null)
			{
				int lines = 1;
				for (int i = 0; i < text.Length; i++)
				{
					if (text[i] == '\n')
						lines++;
				}

				layoutElement.preferredHeight = lines * 14f;
			}
		}

		public override bool IsItemValid()
		{
			return base.IsItemValid() && ((ModuleUIBase)field.host).IsEnabled;
		}
	}
}
