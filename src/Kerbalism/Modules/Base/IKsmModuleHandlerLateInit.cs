using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
	/// <summary>
	/// When implemented on a ModuleHandler, is on first game load, when the tech tree, the science DB and the bodies are available.
	/// Main purpose is to auto-generate part specific KsmModuleDefinitions based on arbitrary conditions. 
	/// </summary>
	public interface IKsmModuleHandlerLateInit
	{
		/// <summary>
		/// When implemented on a ModuleHandler, is on first game load, when the tech tree, the science DB and the bodies are available.
		/// Main purpose is to auto-generate part specific KsmModuleDefinitions based on arbitrary conditions. 
		/// </summary>
		void OnLatePrefabInit(AvailablePart availablePart);
	}
}
