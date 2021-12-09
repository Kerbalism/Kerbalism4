using MultipleModuleInPartAPI;
using System;
using System.Reflection;
using UnityEngine;

namespace KERBALISM
{
	public abstract class KsmPartModule : PartModule, IModuleInfo, IMultipleModuleInPart
	{
		public const string AvailablePartKsmModuleInfo = "KsmInfoIdx@";

		/// <summary>
		/// Module definition
		/// </summary>
		[KSPField] public string definition = KsmModuleDefinitionLibrary.DEFAULT_LOCAL_DEFINITION;


		[KSPField(isPersistant = true)]
		public string modulePartConfigId = string.Empty;
		public string ModulePartConfigId => modulePartConfigId;

		[KSPField] public bool showModuleInfo = true;

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
			if (ModuleHandler.UIActivation.HasFlag(UIContext.EditorPartTooltip) && showModuleInfo)
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
