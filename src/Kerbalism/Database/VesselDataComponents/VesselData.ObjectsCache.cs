using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
    public partial class VesselDataBase
    {
		public ObjectsCacheBase ObjectsCache { get; protected set; } = new ObjectsCacheBase();

        public class ObjectsCacheBase
		{
			public List<RadiationCoilData> RadiationArrays { get; private set; } = new List<RadiationCoilData>();

            public List<PartRadiationData> RadiationEmitters { get; private set; } = new List<PartRadiationData>();

			public virtual void Save(ConfigNode vesselDataNode) { }

			public virtual void Load(ConfigNode vesselDataNode) { }

			public virtual void Update(VesselDataBase vd)
			{
				RadiationArrays.Clear();
				RadiationEmitters.Clear();

				foreach (PartData partData in vd.Parts)
				{
					if (partData.radiationData.IsEmitter)
					{
						RadiationEmitters.Add(partData.radiationData);
					}

					foreach (ModuleData moduleData in partData.modules)
					{
						if (moduleData is RadiationCoilData coilData && coilData.effectData != null)
						{
							RadiationArrays.Add(coilData);
						}
					}
				}
			}
		}

		public class ObjectsCacheVessel : ObjectsCacheBase
		{
			private const string NODENAME_FOREIGN_EMITTERS = "FOREIGN_EMITTERS";

			private List<uint> foreignEmitterPartsIds = new List<uint>();

			public override void Update(VesselDataBase vd)
			{
				base.Update(vd);

				if (vd.LoadedOrEditor)
				{
					foreignEmitterPartsIds.Clear();

					foreach (Vessel loadedVessel in FlightGlobals.VesselsLoaded)
					{
						if (DB.TryGetVesselData(loadedVessel, out VesselData loadedVesselData) && loadedVesselData != vd)
						{
							// ignore vessels that are more than 500m away
							if ((loadedVessel.GetWorldPos3D() - FlightGlobals.ActiveVessel.GetWorldPos3D()).sqrMagnitude > 500f * 500f)
								continue;

							// only grab non foreign emitters in the other vessel
							int nonForeignEmittersCount = loadedVesselData.ObjectsCache.RadiationEmitters.Count - ((ObjectsCacheVessel)loadedVesselData.ObjectsCache).foreignEmitterPartsIds.Count;
							for (int i = 0; i < nonForeignEmittersCount; i++)
							{
								RadiationEmitters.Add(loadedVesselData.ObjectsCache.RadiationEmitters[i]);
								// only persist emitters on landed vessels
								// rationale : non landed vessels have unstable relative positions
								// and will likely quickly drift away one from another
								if (vd.EnvLanded && loadedVesselData.EnvLanded)
								{
									foreignEmitterPartsIds.Add(loadedVesselData.ObjectsCache.RadiationEmitters[i].PartData.flightId);
								}
							}
						}
					}
				}
				else
				{
					for (int i = foreignEmitterPartsIds.Count - 1; i >= 0; i--)
					{
						if (PartData.TryGetPartData(foreignEmitterPartsIds[i], out PartData emitter) && emitter.radiationData.IsEmitter)
						{
							RadiationEmitters.Add(emitter.radiationData);
						}
					}
				}
			}

			public override void Save(ConfigNode vesselDataNode)
			{
				if (foreignEmitterPartsIds.Count == 0)
					return;

				ConfigNode node = vesselDataNode.AddNode(NODENAME_FOREIGN_EMITTERS);
				foreach (uint emitterId in foreignEmitterPartsIds)
				{
					node.AddValue("e", emitterId);
				}
			}

			public override void Load(ConfigNode vesselDataNode)
			{
				ConfigNode node = vesselDataNode.GetNode(NODENAME_FOREIGN_EMITTERS);
				if (node == null)
					return;

				foreach (ConfigNode.Value cfgValue in node.values)
				{
					foreignEmitterPartsIds.Add(uint.Parse(cfgValue.value));
				}
			}
		}

	}
}
