namespace KERBALISM
{
	
	public abstract class TypedModuleHandler : ModuleHandler { }

	public abstract class TypedModuleHandler<T> : TypedModuleHandler where T : PartModule
	{
		public T loadedModule;
		public T prefabModule;

		public override PartModule LoadedModuleBase
		{
			get => loadedModule;
			set => loadedModule = (T)value;
		}

		public override PartModule PrefabModuleBase
		{
			get => prefabModule;
			set => prefabModule = (T)value;
		}

		public override string[] ModuleTypeNames { get; } = new string[] { typeof(T).Name };

	}
}
