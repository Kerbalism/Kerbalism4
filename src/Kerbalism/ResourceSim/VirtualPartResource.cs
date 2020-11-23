using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
	public class VirtualPartResource : PartResourceWrapper
	{
		public const string NODENAME_VIRTUAL_RESOURCES = "VIRTUAL_RESOURCES";

		private VirtualResourceDefinition definition;

		public override string ResName => definition.name;

		public override int ResId => definition.id;

		public int ContainerIndex { get; private set; }

		private double amount;
		public override double Amount
		{
			get => amount;
			set
			{
				amount = value < 0.0 ? 0.0 : value > capacity ? capacity : value;
			}
		}

		private double capacity;
		public override double Capacity
		{
			get => capacity;
			set
			{
				if (value < 0.0)
					value = 0.0;

				if (value > amount)
					amount = value;

				capacity = value;
			}
		}

		public override bool FlowState { get; set; } = true;

		public override double Level => Capacity > 0.0 ? Amount / Capacity : 0.0;

		public VirtualPartResource(VirtualResourceDefinition definition, int containerIndex, double amount = 0.0, double capacity = 0.0)
		{
			this.definition = definition;
			Capacity = capacity;
			Amount = amount;
			ContainerIndex = containerIndex;
		}

		public static void LoadPartResources(PartData pd, ConfigNode partDataNode)
		{
			ConfigNode resTopNode = partDataNode.GetNode(NODENAME_VIRTUAL_RESOURCES);
			if (resTopNode == null)
				return;

			foreach (ConfigNode node in resTopNode.nodes)
			{
				pd.virtualResources.AddResource(node.name,
					Lib.ConfigValue(node, "amount", 0.0),
					Lib.ConfigValue(node, "capacity", 0.0),
					Lib.ConfigValue(node, "index", 0));
			}
		}

		public static bool SavePartResources(PartData pd, ConfigNode partDataNode)
		{
			if (pd.virtualResources.Count == 0)
				return false;

			ConfigNode resTopNode = partDataNode.AddNode(NODENAME_VIRTUAL_RESOURCES);

			foreach (VirtualPartResource res in pd.virtualResources)
			{
				ConfigNode resNode = resTopNode.AddNode(res.ResName);
				resNode.AddValue("amount", res.amount);
				resNode.AddValue("capacity", res.capacity);
				resNode.AddValue("index", res.ContainerIndex);
			}
			return true;
		}
	}
}
