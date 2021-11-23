using System;

namespace KERBALISM
{
	public class DriveDefinition : KsmModuleDefinition
	{
		/// <summary> Max file size storage </summary>
		[CFGValue] public double FilesCapacity { get; private set; } = 0.0;

		public override void OnLoad(ConfigNode definitionNode)
		{
			// nope, must be done after science DB init...
		}

	}
}

