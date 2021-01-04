namespace KERBALISM
{
	
	public abstract class TypedModuleHandler : ModuleHandler { }

	public abstract class TypedModuleHandler<T> : TypedModuleHandler where T : PartModule
	{
		public T loadedModule;
		public T prefabModule;

		public override PartModule LoadedModuleBase => loadedModule;
		public override PartModule PrefabModuleBase => prefabModule;

		public override string[] ModuleTypeNames { get; } = new string[] { typeof(T).Name };

		public override void SetModuleReferences(PartModule prefabModule, PartModule loadedModule)
		{
			this.prefabModule = (T)prefabModule;
			if (!ReferenceEquals(loadedModule, null)) // bypass unity null equality overload
			{
				this.loadedModule = (T)loadedModule;
			}
		}
	}
}
