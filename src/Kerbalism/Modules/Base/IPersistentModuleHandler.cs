namespace KERBALISM
{
	/// <summary>
	/// Implemented on the KsmModuleHandler base class and consequentely all derivatives, which 
	/// can override OnLoad()/OnSave() to persist their custom data. <br/>
	/// Can be implemented on a TypedModuleHandler / ForeignModuleHandler derivative to provide
	/// persistence on a supported external module. Note that for having in-flight persistence, 
	/// you must set the derived class ActivationContext to both Loaded and Unloaded.
	/// </summary>
	public interface IPersistentModuleHandler
	{
		ModuleHandler ModuleHandler { get; }

		int FlightId { get; set; }

		int ShipId { get; set; }

		void Load(ConfigNode node);

		void Save(ConfigNode node);
	}
}
