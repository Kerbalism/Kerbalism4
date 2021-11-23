using System;
using System.Reflection;
using UnityEngine;

namespace KERBALISM
{
	public abstract class KsmPartModule : PartModule, IModuleInfo
	{
		public const string AvailablePartKsmModuleInfo = "KsmInfoIdx@";

		/// <summary>
		/// For B9PS module switching : allow changing the module configuration from the DATA{} node in a specific B9PS subtype
		/// </summary>
		[KSPField] public string definition = KsmModuleDefinitionLibrary.DEFAULT_LOCAL_DEFINITION;

		/// <summary>
		/// For B9PS module switching : set it from the B9PS suybtype DATA{} node to enable/disable the module for that subtype.
		/// This is a functional replacement for the vanilla "moduleActive" field of B9PS, which we don't support.
		/// </summary>
		[KSPField] public bool switchModuleEnabled = true;
		public bool switchLastModuleEnabled = true;

		/// <summary>
		/// Doesn't have any functional purpose in code, this is for easily identifying target modules in B9PS IDENTIFIER{} nodes
		/// </summary>
		[KSPField] public string switchId = string.Empty;          // this is for identifying the module with B9PS

		public abstract ModuleHandler ModuleHandler { get; set; }

		public abstract Type ModuleDataType { get; }

		/// <summary>
		/// Must be used in place of PartModule.OnStart() : <br/>
		/// - Garanteed to be executed **after** ModuleHandler.FirstSetup() and ModuleHandler.OnStart() <br/>
		/// - Will be called consistently when a previoulsy disabled module is enabled by B9PS
		/// </summary>
		public virtual void KsmStart() { }

		#region IModuleInfo

		public override string GetModuleDisplayName()
		{
			return ModuleHandler.ModuleTitle;
		}

		public override string GetInfo()
		{
			// if we should have a part tooltip module info widget, only add a widget if the module isn't switched with B9PS
			if (ModuleHandler.UIActivation.HasFlag(UIContext.EditorPartTooltip) && switchId.Length == 0)
			{
				// if this is the prefab compilation
				if (HighLogic.LoadedScene == GameScenes.LOADING)
				{
					// This will be parsed on the AvailablePart in the PartPrefabsPostCompilation call.
					return AvailablePartKsmModuleInfo + part.Modules.IndexOf(this) + "@" + GetType().Name;
				}
				// The only other (stock) case where this called is if the module is using stock upgrades.
				else
				{
					return ModuleHandler.ModuleDescription;
				}
			}

			return string.Empty;
		}

		public string GetModuleTitle()
		{
			return ModuleHandler.ModuleTitle;
		}

		public Callback<Rect> GetDrawModulePanelCallback()
		{
			return null;
		}

		public string GetPrimaryField()
		{
			return null;
		}

		#endregion
	}

	public abstract class KsmPartModule<TModule, THandler, TDefinition> : KsmPartModule
		where TModule : KsmPartModule<TModule, THandler, TDefinition>
		where THandler : KsmModuleHandler<TModule, THandler, TDefinition>
		where TDefinition : KsmModuleDefinition
	{
		public THandler moduleHandler;

		public TDefinition Definition => moduleHandler.definition;

		public override ModuleHandler ModuleHandler { get => moduleHandler; set => moduleHandler = (THandler)value; }

		public override Type ModuleDataType => typeof(THandler);

		/// <summary>
		/// Base override to instantiate a ModuleHandler and ModuleDefinition for the part prefab <br/>
		/// The purpose is to have the handler and definition available at prefab compilation 
		/// for some KSP interfaces like IModuleInfo or IMultipleDragCube <br/>
		/// IMPORTANT : if you override OnLoad() in a derived module, make sure you call base.OnLoad() ! <br/>
		/// Note that that only the prefab reference will be set for ModuleHandler, everything else (PartData, VesselData will be missing)<br/>
		/// Also note that in case that module is B9Switched, you won't have access to the ModuleDefinition of other variants
		/// </summary>
		public override void OnLoad(ConfigNode node)
		{
			if (HighLogic.LoadedScene != GameScenes.LOADING)
				return;

			ModuleHandler.NewForPrefab(this);
		}
	}
}
