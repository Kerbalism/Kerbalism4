namespace KERBALISM
{
	/// <summary>
	/// Implemented on the KsmModuleHandler base class and consequentely all derivatives, which 
	/// can override OnLoad()/OnSave() to persist their custom data. <br/>
	/// Can be implemented on a TypedModuleHandler / ForeignModuleHandler derivative to provide
	/// persistence on a supported external module. For that to work, you must set the derived
	/// class to use at least the Loaded and Unloaded ActivationContext.
	/// </summary>
	public interface IPersistentModuleHandler
	{
		ModuleHandler ModuleHandler { get; }

		bool ConfigLoaded { get; set; }

		void Load(ConfigNode node);

		void Save(ConfigNode node);
	}
}
