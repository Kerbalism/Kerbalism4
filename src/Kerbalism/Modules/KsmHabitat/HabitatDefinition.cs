using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static KERBALISM.HabitatHandler;

namespace KERBALISM
{
	public class HabitatDefinition : KsmModuleDefinition
	{
		[CFGValue] public bool canPressurize = true;              // can the habitat be pressurized ?
		[CFGValue] public double maxShieldingFactor = 1.0;        // how much shielding can be applied, in % of the habitat surface (can be > 1.0)
		[CFGValue] public double reclaimFactor = 0.6;             // % of atmosphere that will be recovered when depressurizing (producing "reclaimResource" back)
		[CFGValue] public double reclaimStorageFactor = 0.0;      // Amount of nitrogen storage, in % of the amount needed to pressurize the part
		[CFGValue] public bool canRetract = true;                 // if false, can't be retracted once deployed
		[CFGValue] public bool deployWithPressure = false;        // if true, deploying is done by pressurizing
		[CFGValue] public double depressurizeECRate = 0.5;        // EC/s consumed while depressurizing and reclaiming the reclaim resource
		[CFGValue] public double deployECRate = 1.0;              // EC/s consumed while deploying / inflating
		[CFGValue] public double accelerateECRate = 5.0;          // EC/s consumed while accelerating a centrifuge (note : decelerating is free)
		[CFGValue] public double rotateECRate = 2.0;              // EC/s consumed to sustain the centrifuge rotation
		[CFGValue] public double centrifugeGravity = 0.3;

		// volume / surface config
		[CFGValue] public double volume = 0.0;  // habitable volume in m^3, deduced from model if not specified
		[CFGValue] public double surface = 0.0; // external surface in m^2, deduced from model if not specified
		[CFGValue] public PartVolumeAndSurface.Method volumeAndSurfaceMethod = PartVolumeAndSurface.Method.Best;
		[CFGValue] public bool substractAttachementNodesSurface = true;

		// resources config
		[CFGValue] public string reclaimResource = "Nitrogen"; // Nitrogen
		[CFGValue] public string shieldingResource = "KsmShielding"; // KsmShielding

		// animations config
		[CFGValue] public string deployAnim = string.Empty; // deploy / inflate animation, if any
		[CFGValue] public bool deployAnimReverse = false;   // deploy / inflate animation is reversed

		[CFGValue] public string rotateAnim = string.Empty;        // rotate animation, if any
		[CFGValue] public bool rotateIsReversed = false;           // inverse rotation direction
		[CFGValue] public bool rotateIsTransform = false;          // rotateAnim is not an animation, but a transform
		[CFGValue] public Vector3 rotateAxis = Vector3.forward;    // axis around which to rotate (transform only)
		[CFGValue] public float rotateSpinRate = 30.0f;            // centrifuge rotation speed (deg/s)
		[CFGValue] public float rotateAccelerationRate = 1.0f;     // centrifuge transform acceleration (deg/s/s)
		[CFGValue] public bool rotateIVA = true;                   // should the IVA rotate with the transform ?

		[CFGValue] public string counterweightAnim = string.Empty;        // inflate animation, if any
		[CFGValue] public bool counterweightIsReversed = false;           // inverse rotation direction
		[CFGValue] public bool counterweightIsTransform = false;          // rotateAnim is not an animation, but a Transform
		[CFGValue] public Vector3 counterweightAxis = Vector3.forward;    // axis around which to rotate (transform only)
		[CFGValue] public float counterweightSpinRate = 60.0f;            // counterweight rotation speed (deg/s)
		[CFGValue] public float counterweightAccelerationRate = 2.0f;     // counterweight acceleration (deg/s/s)

		// ModuleDockingNode handling
		[CFGValue] public bool controlModuleDockingNode = false;     // should all ModuleDockingNode on the part be controlled by us and made dependant on the deployed state

		public List<ComfortValue> comforts = new List<ComfortValue>();

		// fixed caracteristics (some determined at prefab compilation from the module OnLoad())
		public bool isDeployable = false;
		public bool isCentrifuge = false;
		public bool hasShielding;
		public double depressurizationSpeed = -1.0;

		public override void OnLoad(ConfigNode node)
		{
			// Parse comforts

			foreach (ConfigNode comfort in node.GetNodes("COMFORT"))
			{
				ComfortValue instance = ComfortValue.Load(comfort);

				if (instance == null)
					continue;

				comforts.Add(instance);
			}

			if (node.HasValue("depressurizationDuration"))
			{
				// parse config defined depressurization duration
				if (!Lib.ConfigDuration(node, "depressurizationDuration", false, out depressurizationSpeed))
					depressurizationSpeed = -1.0;
			}


			// should we add the shielding resource to the part ?
			hasShielding = Features.Radiation && maxShieldingFactor > 0.0;
		}
	}
}
