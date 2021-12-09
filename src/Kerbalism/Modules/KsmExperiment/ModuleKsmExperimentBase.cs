using System;
using System.Collections.Generic;
using System.Linq;
using MultipleModuleInPartAPI;
using static KERBALISM.ExperimentHandlerUtils;

namespace KERBALISM
{
	public interface IModuleKsmExperiment
	{
		ExperimentDefinition Definition { get; }
		Part Part { get; }
	}

	public abstract class ModuleKsmExperimentBase<TModule, THandler, TDefinition, TScienceData> :
		KsmPartModule<TModule, THandler, TDefinition>,
		IModuleKsmExperiment, IMultipleDragCube
		where TModule : ModuleKsmExperimentBase<TModule, THandler, TDefinition, TScienceData>
		where THandler : ExperimentHandlerBase<TModule, THandler, TDefinition, TScienceData>
		where TDefinition : ExperimentDefinition
		where TScienceData : KsmScienceData
	{
		ExperimentDefinition IModuleKsmExperiment.Definition => Definition;
		Part IModuleKsmExperiment.Part => part;

		#region FIELDS

		// animations definition
		[KSPField] public string deployAnimation = string.Empty;
		[KSPField] public bool deployAnimationIsReversed = false;

		[KSPField] public string loopAnimation = string.Empty;
		[KSPField] public bool loopAnimationIsReversed = false;

		/// <summary>
		/// if true, deploy/retract animations will managed by the first (by index) found ModuleAnimationGroup
		/// Note that using an animation group is incompatible with using a loop animation
		/// </summary>
		[KSPField] public bool useAnimationGroup = false;

		// optional : custom drag cubes definitions
		[KSPField] public string retractedDragCube = "Retracted";
		[KSPField] public string deployedDragCube = "Deployed";

		// animation handlers
		internal Animator deployAnimator;
		internal Animator loopAnimator;
		internal ModuleAnimationGroup animationGroup;

		#endregion

		#region LIFECYCLE

		public override void OnLoad(ConfigNode node)
		{
			base.OnLoad(node);

			if (HighLogic.LoadedScene == GameScenes.LOADING)
			{
				moduleHandler.Start();
			}

			if (useAnimationGroup)
				animationGroup = part.FindModuleImplementing<ModuleAnimationGroup>();
		}


		public override void KsmStart()
		{
			// create animators
			deployAnimator = new Animator(part, deployAnimation, deployAnimationIsReversed);
			loopAnimator = new Animator(part, loopAnimation, loopAnimationIsReversed);

			// set initial animation states
			if (moduleHandler.IsRunningRequested)
			{
				deployAnimator.Still(1f);
				loopAnimator.Play(false, true);
				SetDragCubes(true);
			}
			else
			{
				deployAnimator.Still(0f);
				SetDragCubes(false);
			}

			if (useAnimationGroup && animationGroup == null)
				animationGroup = part.Modules.OfType<ModuleAnimationGroup>().FirstOrDefault();

			if (animationGroup != null && !animationGroup.isDeployed && moduleHandler.IsRunningRequested)
			{
				animationGroup.DeployModule();
			}
		}

		public void OnDefinitionChanged()
		{
			Actions["StartAction"].guiName = Lib.BuildString(Local.Generic_START, ": ", moduleHandler.definition.ExpInfo.Title);
			Actions["StopAction"].guiName = Lib.BuildString(Local.Generic_STOP, ": ", moduleHandler.definition.ExpInfo.Title);
		}

		#endregion

		#region EVALUATION

		public void Update()
		{
			moduleHandler.shrouded = part.ShieldedFromAirstream;

			if (animationGroup != null && !animationGroup.isDeployed && moduleHandler.IsRunningRequested)
			{
				moduleHandler.Toggle();
			}
		}

		#endregion

		#region USER INTERACTION

		// action groups
		[KSPAction("Start")]
		public void StartAction(KSPActionParam param)
		{
			if (!moduleHandler.IsRunningRequested)
				moduleHandler.Toggle();
		}

		[KSPAction("Stop")]
		public void StopAction(KSPActionParam param)
		{
			if (moduleHandler.IsRunningRequested)
				moduleHandler.Toggle();
		}
		#endregion

		#region DRAG CUBES

		internal void SetDragCubes(bool deployed)
		{
			if (deployAnimator == null)
				return;

			part.DragCubes.SetCubeWeight(retractedDragCube, deployed ? 0f : 1f);
			part.DragCubes.SetCubeWeight(deployedDragCube, deployed ? 1f : 0f);
		}


		public bool IsMultipleCubesActive
		{
			get
			{
				if (deployAnimator == null)
				{
					deployAnimator = new Animator(part, deployAnimation, deployAnimationIsReversed);
				}
				return deployAnimator.IsDefined;
			}
		}

		public string[] GetDragCubeNames() => new string[] { retractedDragCube, deployedDragCube };

		// called at prefab compilation, after OnLoad()
		public void AssumeDragCubePosition(string name)
		{
			if (deployAnimator == null)
			{
				deployAnimator = new Animator(part, deployAnimation, deployAnimationIsReversed);
			}

			if (name == retractedDragCube)
				deployAnimator.Still(0f);
			else if (name == deployedDragCube)
				deployAnimator.Still(1f);
		}

		public bool UsesProceduralDragCubes() => false;

		#endregion
	}
}
