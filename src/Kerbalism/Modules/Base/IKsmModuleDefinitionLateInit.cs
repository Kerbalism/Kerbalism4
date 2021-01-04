using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
	/// <summary>
	/// When implemented on a KsmModuleDefinition, called on first game load when the tech tree, the science DB and the bodies are available.
	/// Main purpose is to finish parsing the definition when it require those objects to be available.
	/// </summary>
	public interface IKsmModuleDefinitionLateInit
	{
		/// <summary>
		/// When implemented on a KsmModuleDefinition, called on first game load when the tech tree, the science DB and the bodies are available.
		/// Main purpose is to finish parsing the definition when it require those objects to be available.
		/// </summary>
		void OnLateInit();
	}
}
