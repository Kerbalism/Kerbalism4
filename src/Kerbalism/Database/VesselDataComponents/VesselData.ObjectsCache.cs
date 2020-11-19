using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
    public partial class VesselDataBase
    {
		public ObjectsCacheBase ObjectsCache { get; protected set; }

        public class ObjectsCacheBase
		{
			protected List<PartRadiationData> radiationEmitters = new List<PartRadiationData>();

			protected List<RadiationCoilData> radiationCoilDatas = new List<RadiationCoilData>();

			/// <summary> all emitters on the vessel, plus emitters on nearby vessels (500m max) </summary>
			public virtual IEnumerable<PartRadiationData> AllRadiationEmitters => radiationEmitters;

			public virtual int AllRadiationEmittersCount => radiationEmitters.Count;

			public virtual PartRadiationData RadiationEmitterAtIndex(int index) => radiationEmitters[index];

			/// <summary> all active shield arrays on the vessel, plus arrays on nearby vessels (250m max) </summary>
			public virtual IEnumerable<RadiationCoilData> AllRadiationCoilDatas => radiationCoilDatas;

			public virtual int AllRadiationCoilDatasCount => radiationCoilDatas.Count;

			public virtual RadiationCoilData CoilDataAtIndex(int index) => radiationCoilDatas[index];

			public virtual void Update(VesselDataBase vd)
			{
				if (!vd.LoadedOrEditor)
					return;

				radiationCoilDatas.Clear();
				radiationEmitters.Clear();

				foreach (PartData partData in vd.Parts)
				{
					if (partData.radiationData.IsEmitter)
					{
						radiationEmitters.Add(partData.radiationData);
					}

					foreach (ModuleData moduleData in partData.modules)
					{
						if (moduleData is RadiationCoilData coilData && coilData.effectData != null)
						{
							radiationCoilDatas.Add(coilData);
						}
					}
				}
			}
		}

		public class ObjectsCacheVessel : ObjectsCacheBase
		{
			private List<PartRadiationData> foreignRadiationEmitters = new List<PartRadiationData>();
			private List<RadiationCoilData> foreignRadiationCoilDatas = new List<RadiationCoilData>();

			public override IEnumerable<PartRadiationData> AllRadiationEmitters
			{
				get
				{
					foreach (PartRadiationData emitter in radiationEmitters)
					{
						yield return emitter;
					}
					foreach (PartRadiationData foreignEmitter in foreignRadiationEmitters)
					{
						yield return foreignEmitter;
					}
				}

			}

			public override int AllRadiationEmittersCount => radiationEmitters.Count + foreignRadiationEmitters.Count;

			public override PartRadiationData RadiationEmitterAtIndex(int index)
			{
				if (index < radiationEmitters.Count)
				{
					return radiationEmitters[index];
				}
				return foreignRadiationEmitters[index - radiationEmitters.Count];
			}

			public override IEnumerable<RadiationCoilData> AllRadiationCoilDatas
			{
				get
				{
					foreach (RadiationCoilData coilData in radiationCoilDatas)
					{
						yield return coilData;
					}
					foreach (RadiationCoilData foreignCoilData in foreignRadiationCoilDatas)
					{
						yield return foreignCoilData;
					}
				}
			}

			public override int AllRadiationCoilDatasCount => radiationCoilDatas.Count + foreignRadiationCoilDatas.Count;

			public override RadiationCoilData CoilDataAtIndex(int index)
			{
				if (index < radiationCoilDatas.Count)
				{
					return radiationCoilDatas[index];
				}
				return foreignRadiationCoilDatas[index - radiationCoilDatas.Count];
			}

			public override void Update(VesselDataBase vd)
			{
				base.Update(vd);

				if (!vd.LoadedOrEditor)
					return;

				foreignRadiationEmitters.Clear();
				foreignRadiationCoilDatas.Clear();

				foreach (Vessel loadedVessel in FlightGlobals.VesselsLoaded)
				{
					if (DB.TryGetVesselData(loadedVessel, out VesselData loadedVesselData) && loadedVesselData != vd)
					{
						double vesselSeparation = (loadedVessel.GetWorldPos3D() - FlightGlobals.ActiveVessel.GetWorldPos3D()).sqrMagnitude;
						ObjectsCacheVessel foreignVesselObjects = (ObjectsCacheVessel)loadedVesselData.ObjectsCache;

						// ignore emitters for vessels that are more than 500m away
						if (vesselSeparation < 500.0 * 500.0)
						{
								
							foreach (PartRadiationData foreignEmitter in foreignVesselObjects.radiationEmitters)
							{
								foreignRadiationEmitters.Add(foreignEmitter);
							}
						}

						// ignore shields for vessels that are more than 250m away
						if (vesselSeparation < 250.0 * 250.0)
						{
							foreach (RadiationCoilData foreignCoilData in foreignVesselObjects.radiationCoilDatas)
							{
								foreignRadiationCoilDatas.Add(foreignCoilData);
							}
						}
					}
				}
			}
		}
	}
}
