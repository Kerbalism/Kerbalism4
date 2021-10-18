using System;
using System.Reflection;

namespace KERBALISM
{
	/// <summary> Wrapper for manipulating the stock PartResource / ProtoPartResourceSnapshot objects without having to use separate code </summary>
	public class PartResourceWrapper
	{
		protected PartResourceWrapperBase wrapper;

		public int resId;
		public virtual string ResName => wrapper.ResName;
		public virtual double Amount { get => wrapper.Amount; set => wrapper.Amount = value; }
		public virtual double Capacity { get => wrapper.Capacity; set => wrapper.Capacity = value; }
		public virtual double Level => wrapper.Level;

		public virtual bool FlowState { get => wrapper.FlowState; set => wrapper.FlowState = value; }
		public virtual bool IsTweakable { get => wrapper.IsTweakable; set => wrapper.IsTweakable = value; }
		public virtual bool IsVisible { get => wrapper.IsVisible; set => wrapper.IsVisible = value; }

		public PartResourceWrapper() { }

		public PartResourceWrapper(PartData part, PartResource loadedResource)
		{
			wrapper = new LoadedPartResourceWrapper(loadedResource);
			resId = wrapper.ResId;
			AddToResHandler(part);
		}

		public PartResourceWrapper(PartData part, ProtoPartResourceSnapshot unloadedResource)
		{
			wrapper = new ProtoPartResourceWrapper(unloadedResource);
			resId = wrapper.ResId;
			AddToResHandler(part);
		}

		public void Mutate(PartResource loadedResource)
		{
			wrapper = new LoadedPartResourceWrapper(loadedResource);
			resId = wrapper.ResId;
		}

		public void Mutate(ProtoPartResourceSnapshot unloadedResource)
		{
			wrapper = new ProtoPartResourceWrapper(unloadedResource);
			resId = wrapper.ResId;
		}

		public void AddToResHandler(PartData part)
		{
			part.vesselData.ResHandler.GetKSPResource(resId).ResourceWrapper.AddPartWrapper(this);
		}

		public void RemoveFromResHandler(PartData part)
		{
			part.vesselData.ResHandler.GetKSPResource(resId).ResourceWrapper.RemovePartWrapper(this);
		}

		protected abstract class PartResourceWrapperBase
		{
			public abstract string ResName { get; }
			public abstract int ResId { get; }
			public abstract double Amount { get; set; }
			public abstract double Capacity { get; set; }
			public abstract bool FlowState { get; set; }
			public abstract bool IsTweakable { get; set; }
			public abstract bool IsVisible { get; set; }
			public double Level => Capacity > 0.0 ? Amount / Capacity : 0.0;
		}

		protected sealed class LoadedPartResourceWrapper : PartResourceWrapperBase
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
			public override bool IsTweakable { get => partResource.isTweakable; set => partResource.isTweakable = value; }
			public override bool IsVisible { get => partResource.isVisible; set => partResource.isVisible = value; }
		}

		protected sealed class ProtoPartResourceWrapper : PartResourceWrapperBase
		{
			private static FieldInfo resourceValuesField = typeof(ProtoPartResourceWrapper).GetField("resourceValues", BindingFlags.Instance | BindingFlags.NonPublic);

			private ProtoPartResourceSnapshot partResource;
			private ConfigNode resourceValues;
			private ProtoModuleValueBool isTweakable;
			private ProtoModuleValueBool isVisible;

			public ProtoPartResourceWrapper(ProtoPartResourceSnapshot stockResource)
			{
				partResource = stockResource;
			}

			public override string ResName => partResource.resourceName;
			public override int ResId => partResource.definition.id;
			public override double Amount { get => partResource.amount; set => partResource.amount = value; }
			public override double Capacity { get => partResource.maxAmount; set => partResource.maxAmount = value; }
			public override bool FlowState { get => partResource.flowState; set => partResource.flowState = value; }
			public override bool IsTweakable
			{
				get
				{
					if (isTweakable == null && !ProtoModuleValueBool.TryGet(ResourceValues, "isTweakable", out isTweakable))
						return false;

					return isTweakable.Value;
				}
				set
				{
					if (isTweakable == null && !ProtoModuleValueBool.TryGet(ResourceValues, "isTweakable", out isTweakable))
						return;

					isTweakable.Value = value;
				}
			}

			public override bool IsVisible
			{
				get
				{
					if (isVisible == null && !ProtoModuleValueBool.TryGet(ResourceValues, "isVisible", out isVisible))
						return false;

					return isVisible.Value;
				}
				set
				{
					if (isVisible == null && !ProtoModuleValueBool.TryGet(ResourceValues, "isVisible", out isVisible))
						return;

					isVisible.Value = value;
				}
			}

			private ConfigNode ResourceValues
			{
				get
				{
					if (resourceValues == null)
						resourceValues = (ConfigNode)resourceValuesField.GetValue(partResource);

					return resourceValues;
				}
			}
		}
	}


}
