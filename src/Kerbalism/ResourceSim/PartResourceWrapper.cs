using System;

namespace KERBALISM
{
	/// <summary> Wrapper for manipulating the stock PartResource / ProtoPartResourceSnapshot objects without having to use separate code </summary>
	public class PartResourceWrapper
	{
		private PartResourceWrapperBase wrapper;

		public virtual string ResName => wrapper.ResName;
		public virtual int ResId => wrapper.ResId;
		public virtual double Amount { get => wrapper.Amount; set => wrapper.Amount = value; }
		public virtual double Capacity { get => wrapper.Capacity; set => wrapper.Capacity = value; }
		public virtual bool FlowState { get => wrapper.FlowState; set => wrapper.FlowState = value; }
		public virtual double Level => wrapper.Level;

		public Action<string> OnResourceRemoved;

		public PartResourceWrapper() { }

		public PartResourceWrapper(PartResource loadedResource)
		{
			wrapper = new LoadedPartResourceWrapper(loadedResource);
		}

		public PartResourceWrapper(ProtoPartResourceSnapshot unloadedResource)
		{
			wrapper = new ProtoPartResourceWrapper(unloadedResource);
		}

		public void Mutate(PartResource loadedResource)
		{
			wrapper = new LoadedPartResourceWrapper(loadedResource);
		}

		public void Mutate(ProtoPartResourceSnapshot unloadedResource)
		{
			wrapper = new ProtoPartResourceWrapper(unloadedResource);
		}

		private abstract class PartResourceWrapperBase
		{
			public abstract string ResName { get; }
			public abstract int ResId { get; }
			public abstract double Amount { get; set; }
			public abstract double Capacity { get; set; }
			public abstract bool FlowState { get; set; }
			public double Level => Capacity > 0.0 ? Amount / Capacity : 0.0;
		}

		private class LoadedPartResourceWrapper : PartResourceWrapperBase
		{
			private PartResource partResource;

			public LoadedPartResourceWrapper(PartResource stockResource)
			{
				partResource = stockResource;
			}

			public override string ResName => partResource.resourceName;
			public override int ResId => partResource.info.id;
			public override double Amount { get => partResource.amount; set => partResource.amount = value; }
			public override double Capacity { get => partResource.maxAmount; set => partResource.maxAmount = value; }
			public override bool FlowState { get => partResource.flowState; set => partResource.flowState = value; }
		}

		private class ProtoPartResourceWrapper : PartResourceWrapperBase
		{
			private ProtoPartResourceSnapshot partResource;

			public ProtoPartResourceWrapper(ProtoPartResourceSnapshot stockResource)
			{
				partResource = stockResource;
			}

			public override string ResName => partResource.resourceName;
			public override int ResId => partResource.definition.id;
			public override double Amount { get => partResource.amount; set => partResource.amount = value; }
			public override double Capacity { get => partResource.maxAmount; set => partResource.maxAmount = value; }
			public override bool FlowState { get => partResource.flowState; set => partResource.flowState = value; }
		}
	}


}
