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
		public SynchronizerBase Synchronizer { get; protected set; }

        public class SynchronizerBase
		{
			protected VesselDataBase vesselData;

			protected List<PartRadiationData> radiationEmitters = new List<PartRadiationData>();

			protected List<RadiationCoilHandler> radiationCoilDatas = new List<RadiationCoilHandler>();

			/// <summary> all emitters on the vessel, plus emitters on nearby vessels (500m max) </summary>
			public virtual IEnumerable<PartRadiationData> AllRadiationEmitters => radiationEmitters;

			public virtual int AllRadiationEmittersCount => radiationEmitters.Count;

			public virtual PartRadiationData RadiationEmitterAtIndex(int index) => radiationEmitters[index];

			/// <summary> all active shield arrays on the vessel, plus arrays on nearby vessels (250m max) </summary>
			public virtual IEnumerable<RadiationCoilHandler> AllRadiationCoilDatas => radiationCoilDatas;

			public virtual int AllRadiationCoilDatasCount => radiationCoilDatas.Count;

			public virtual RadiationCoilHandler CoilDataAtIndex(int index) => radiationCoilDatas[index];

			public SynchronizerBase(VesselDataBase vesselData)
			{
				this.vesselData = vesselData;
			}

			public virtual void Synchronize()
			{
				radiationCoilDatas.Clear();
				radiationEmitters.Clear();

				foreach (PartData partData in vesselData.Parts)
				{
					partData.resources.Synchronize();

					if (vesselData.LoadedOrEditor && partData.radiationData.IsEmitter)
					{
						radiationEmitters.Add(partData.radiationData);
					}

					foreach (ModuleHandler moduleData in partData.modules)
					{
						if (vesselData.LoadedOrEditor && moduleData is RadiationCoilHandler coilData && coilData.effectData != null)
						{
							radiationCoilDatas.Add(coilData);
						}
					}
				}
			}
		}

		public class SynchronizerVessel : SynchronizerBase
		{
			private List<PartRadiationData> foreignRadiationEmitters = new List<PartRadiationData>();
			private List<RadiationCoilHandler> foreignRadiationCoilDatas = new List<RadiationCoilHandler>();

			public SynchronizerVessel(VesselDataBase vesselData) : base(vesselData) { }

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

			public override IEnumerable<RadiationCoilHandler> AllRadiationCoilDatas
			{
				get
				{
					foreach (RadiationCoilHandler coilData in radiationCoilDatas)
					{
						yield return coilData;
					}
					foreach (RadiationCoilHandler foreignCoilData in foreignRadiationCoilDatas)
					{
						yield return foreignCoilData;
					}
				}
			}

			public override int AllRadiationCoilDatasCount => radiationCoilDatas.Count + foreignRadiationCoilDatas.Count;

			public override RadiationCoilHandler CoilDataAtIndex(int index)
			{
				if (index < radiationCoilDatas.Count)
				{
					return radiationCoilDatas[index];
				}
				return foreignRadiationCoilDatas[index - radiationCoilDatas.Count];
			}

			public override void Synchronize()
			{
				base.Synchronize();

				if (!vesselData.LoadedOrEditor)
					return;

				foreignRadiationEmitters.Clear();
				foreignRadiationCoilDatas.Clear();

				foreach (Vessel loadedVessel in FlightGlobals.VesselsLoaded)
				{
					if (DB.TryGetVesselData(loadedVessel, out VesselData loadedVesselData) && loadedVesselData != vesselData)
					{
						double vesselSeparation = (loadedVessel.GetWorldPos3D() - FlightGlobals.ActiveVessel.GetWorldPos3D()).sqrMagnitude;
						SynchronizerVessel foreignVesselObjects = (SynchronizerVessel)loadedVesselData.Synchronizer;

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
							foreach (RadiationCoilHandler foreignCoilData in foreignVesselObjects.radiationCoilDatas)
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
