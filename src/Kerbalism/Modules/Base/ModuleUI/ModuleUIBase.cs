using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using KERBALISM.Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KERBALISM.ModuleUI
{
	public interface IModuleUILabel
	{
		string GetLabel();
	}

	public interface IModuleUIInteractable
	{
		bool IsInteractable { get; }
	}

	public interface IModuleUIButton
	{
		void OnClick();
	}

	public interface IModuleUIToggle
	{
		bool State { get; }
		void OnToggle();
	}

	public abstract class ModuleUIBase
	{
		[Flags]
		public enum EnabledContext
		{
			Editor = 1 << 0,
			Loaded = 1 << 1,
			Unloaded = 1 << 2,
			Flight = Loaded | Unloaded,
			All = Editor | Loaded | Unloaded
		}

		public virtual int Position => 0;
		public virtual bool IsEnabled => true;
		public virtual EnabledContext Context => EnabledContext.All;
		public virtual bool HasTooltip => false;
		public virtual BasePAWGroup PAWGroup => null;
		public virtual string GetTooltip() => null;

		public bool HasContext(EnabledContext context)
		{
			return (Context & context) == context;
		}

		public BaseField pawField;
		protected abstract FieldInfo DummyFieldInfo { get; }
		protected abstract UI_Control UI_Control { get; }

		protected ModuleHandler handlerBase;

		public virtual void CreatePAWItem(Part part)
		{
			pawField = new BaseField(UI_Control, DummyFieldInfo, this);
			pawField.guiActive = IsEnabled && HasContext(EnabledContext.Flight);
			pawField.guiActiveEditor = IsEnabled && HasContext(EnabledContext.Editor);
			if (handlerBase?.UIGroup != null)
				pawField.group = handlerBase.UIGroup;

			part.Fields.Add(pawField);
		}

		public abstract void SetHandler(ModuleHandler handler);
	}

	public class ModuleUIGroup : BasePAWGroup
	{
		public ModuleUIGroup(string groupName, string groupTitle)
		{
			this.name = groupName;
			this.displayName = groupTitle;
			this.startCollapsed = true;
		}
	}

	public abstract class ModuleUIBase<THandler> : ModuleUIBase where THandler : ModuleHandler
	{
		public THandler handler;

		public override void SetHandler(ModuleHandler handler)
		{
			this.handlerBase = handler;
			this.handler = (THandler) handler;
		}
	}
}
