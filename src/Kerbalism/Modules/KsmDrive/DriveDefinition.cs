namespace KERBALISM
{
	public class DriveDefinition : KsmModuleDefinition
	{
		[CFGValue] public double FilesCapacity { get; private set; }
		[CFGValue] public double SamplesCapacity { get; private set; }
		[CFGValue] public int MaxSamples { get; private set; }
	}
}

