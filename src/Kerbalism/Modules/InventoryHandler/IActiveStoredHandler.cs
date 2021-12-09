namespace KERBALISM
{
	public interface IActiveStoredHandler
	{
		/// <summary>
		/// Should that module be instantiated when its part is stored in an inventory ?
		/// This is called between Load() and Start(). You can access the part/module prefabs,
		/// as well as the KsmModule definition, but both the loaded module and protomodule will be null.
		/// </summary>
		bool IsActiveCargo { get; }

		StoredPartData StoredPart { get; set; }

		/// <summary>
		/// Called when the part is being stored.
		/// Will also be called when an already stored part is instantiated, after the handler Start()/OnStart()
		/// </summary>
		void OnCargoStored();

		void OnCargoUnstored();
	}
}
