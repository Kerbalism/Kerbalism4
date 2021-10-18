using System.Collections.Generic;

namespace KERBALISM
{
	public class ResourceHVLDefinition
	{
		/// <summary>
		/// Half-Value Layer (meters) : the thickness of water required to divide 1 MeV gamma radiation by two.
		/// </summary>
		public const double waterHVL_Gamma1MeV = 0.10;

		public const double waterHVL_Gamma25MeV = 0.40;

		/// <summary>
		/// Half-Value Layer (meters) : the thickness of aluminium required to divide 1 MeV gamma radiation by two.
		/// </summary>
		public const double aluminiumHVL_Gamma1MeV = 0.04;

		public const double aluminiumHVL_Gamma25MeV = 0.12;

		public static Dictionary<int, ResourceHVLDefinition> definitions = new Dictionary<int, ResourceHVLDefinition>();

		public static void ParseDefinitions(ConfigNode[] configDefinitions)
		{
			foreach (ConfigNode node in configDefinitions)
			{
				if (TryParseResourceOcclusion(node, out ResourceHVLDefinition resourceOcclusion))
				{
					definitions[resourceOcclusion.StockDefinition.id] = resourceOcclusion;
				}
			}

			foreach (PartResourceDefinition resource in PartResourceLibrary.Instance.resourceDefinitions)
			{
				if (!definitions.ContainsKey(resource.id))
				{
					definitions[resource.id] = new ResourceHVLDefinition(resource);
				}
			}
		}

		public static ResourceHVLDefinition GetResourceOcclusion(int resourceId) => ResourceHVLDefinition.definitions[resourceId];
		public static ResourceHVLDefinition GetResourceOcclusion(PartResourceDefinition resource) => ResourceHVLDefinition.definitions[resource.id];

		public static ResourceHVLDefinition GetResourceOcclusion(string resourceName)
		{
			PartResourceDefinition resource = PartResourceLibrary.Instance.GetDefinition(resourceName);
			if (resource == null)
				return null;

			return definitions[resource.id];
		}

		public PartResourceDefinition StockDefinition { get; private set; }
		public bool IsWallResource { get; private set; }
		public double LowHVL { get; private set; }
		public double HighHVL { get; private set; }

		private ResourceHVLDefinition() { }

		public ResourceHVLDefinition(PartResourceDefinition stockDefinition)
		{
			StockDefinition = stockDefinition;
			IsWallResource = false;
			LowHVL = waterHVL_Gamma1MeV / StockDefinition.VolumetricMassDensity();
			HighHVL = waterHVL_Gamma25MeV / StockDefinition.VolumetricMassDensity();
		}

		public static bool TryParseResourceOcclusion(ConfigNode definitionNode, out ResourceHVLDefinition resourceOcclusion)
		{
			string resName = Lib.ConfigValue(definitionNode, "name", string.Empty);
			PartResourceDefinition stockDefinition = PartResourceLibrary.Instance.GetDefinition(resName);
			if (stockDefinition == null)
			{
				resourceOcclusion = null;
				return false;
			}

			resourceOcclusion = new ResourceHVLDefinition
			{
				StockDefinition = stockDefinition,
				IsWallResource = Lib.ConfigValue(definitionNode, "isWallResource", false),
				LowHVL = Lib.ConfigValue(definitionNode, "lowHVL", 1.0),
				HighHVL = Lib.ConfigValue(definitionNode, "highHVL", 1.0)
			};
			return true;
		}
	}
}
