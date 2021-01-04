using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
	/// <summary> Implemented by ModuleData derivatives that require their part to be radiation-evaluated </summary>
	public interface IRadiationReceiver
	{
		/// <summary>
		/// True if the module actually use the feature.
		/// Set it to false otherwise, to avoid useless computations.
		/// Must be set before the first VesselData update, and must stay constant afterwards.
		/// </summary>
		bool EnableInterface { get; }

		/// <summary> radiation emitted in rad/s </summary>
		PartRadiationData RadiationData { get; }
	}

	/// <summary> Implemented by ModuleData derivatives that emit (or remove) radiation </summary>
	public interface IRadiationEmitter
	{
		/// <summary>
		/// True if the module actually use the feature.
		/// Set to false otherwise, to avoid useless computations.
		/// Must be set before the first VesselData update, and must stay constant afterwards.
		/// </summary>
		bool EnableInterface { get; }

		/// <summary> radiation emitted in rad/s </summary>
		double RadiationRate { get; }

		/// <summary> is the radiation low or high energy (affects material penetration depth) </summary>
		bool HighEnergy { get; }

		/// <summary> set to false if the module isn't currently emitting radiation (performance optimization)</summary>
		bool IsActive { get; }

		/// <summary> reference to the part </summary>
		PartRadiationData RadiationData { get; }

		/// <summary> reference to the module </summary>
		ModuleHandler ModuleData { get; }
	}
}
