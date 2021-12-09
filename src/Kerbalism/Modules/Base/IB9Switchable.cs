using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
	/// <summary>
	/// Implement this interface on a ModuleHandler derivative to define the module behavior
	/// when its ModuleDefinition is changed by B9PartSwitch
	/// </summary>
	public interface IB9Switchable
	{
		///// <summary>
		///// Will be called when KsmModuleHandler.Definition has been changed following a B9PS subtype switch<br/>*
		///// Note that KsmModuleHandler.OnStart() is garanteed to always run before OnSwitchChangeDefinition()
		///// </summary>
		//void OnSwitchChangeDefinition(KsmModuleDefinition previousDefinition);

		///// <summary>
		///// Will be called if the module is re-enabled by B9PartSwitch. <br/>
		///// You don't need to actually enable the KsmPartModule / ModuleHandler, this will already be done <br/>
		///// Note that depending on the use case, this might be followed by a call to OnSwitchChangeDefinition()
		///// Also note that this will be preceded by a ModuleHandler.OnStart() and then a KsmPartModule.KsmStart()
		///// call if the handler was always disabled since its instantiation.
		///// </summary>
		//void OnSwitchEnable();

		///// <summary>
		///// Will be called if the module is disabled by B9PartSwitch. <br/>
		///// You don't need to actually disable the KsmPartModule / ModuleHandler, this will already be done.<br/>
		///// This doesn't make sense from a functional POV, but we have to handle the possibility of a silly config so
		///// this might be followed by a call to OnSwitchChangeDefinition()
		///// </summary>
		//void OnSwitchDisable();

		/// <summary>
		/// Called once for every prefab, after part prefabs have been compiled and after the science DB initialization<br/>
		/// Should return the UI description for that definition, will appear in the subtype tooltip of the B9 PAW UI<br/>
		/// This is optional, return null if you don't want to append any description.
		/// </summary>
		/// <param name="subTypeDefinition">The definition used by that subtype</param>
		/// <param name="techRequired">The tech node id at which that subtype becomes available. Null if always available</param>
		string GetSubtypeDescription(KsmModuleDefinition subTypeDefinition, string techRequired);

		/// <summary>
		/// You don't need to take care of this, as it already exist in a base KsmModuleHandler
		/// </summary>
		KsmModuleDefinition Definition { get; set; }
	}
}
