using System;
using System.Collections;
using System.Collections.Generic;
using Steamworks;

namespace KERBALISM
{
	/*
	 OVERVIEW OF THE RESOURCE SIM :
	- For each vessel or shipconstruct, a VesselResHandler is instantiated
	- It contains a dictionary of all individual resource handlers (one vessel-wide handler per resource)
	- Resource handlers are either a Virtualresource or VesselResource. The IResource interface hide the implementation details
	- The IResource interface provide Consume() and Produce() methods for direct consumptions/productions
	- The VesselResHandler provide a AddRecipe() method for defining input/output processes
	- Each resource handler store consumption/production sum in a "deferred" variable

	IMPLEMENTATION DETAILS :
	- VesselResHandler and VesselResource rely on a PartResourceWrapper abstract class that is used to hide
	  the difference between the KSP PartResource and ProtoPartResourceSnapshot classes
	- One of the reason this implementation is spread over multiple classes is because the KSP PartResourceList class is actually
	  a dictionary, meaning that every iteration over the Part.Resources property instantiate a new list from the
	  dictionary values, which is a huge performance/memory garbage issue since we need at least 2
	  "foreach resource > foreach part > foreach partresource" loops (first to get amount/capacity, then to update amounts).
	  To solve this, we do a single "foreach part > foreach partresource" loop and save a List of PartResource object references,
	  then iterate over that list.
	- Another constraint is that the VesselResHandler must be available from PartModule.Start()/OnStart(), and that
	  the VesselResHandler and resource handlers references must stay the same when the vessel is changing state (loaded <> unloaded)
	

	OVERVIEW OF A SIMULATION STEP :
	- Direct Consume()/Produce() calls from partmodules and other parts of Kerbalism are accumulated in the resource handler "deferred"
	- Recipe objects are created trough AddRecipe() from partmodules and other parts of Kerbalism
	- VesselResHandler.Update() is called : 
	  - All Recipes are executed, and the inputs/outputs added in each resource "deferred".
	  - Each resource amount/capacity (from previous step) is saved
	  - All parts are iterated upon, the KSP part resource object reference is saved in each resource handler,
	    and the new amount and capacity for each resource is calculated from each part resource object.
	  - If amount has changed, this mean there is non-Kerbalism producers/consumers on the vessel
	  - If non-Kerbalism producers are detected on a loaded vessel, we prevent high timewarp rates
	  - For each resource handler :
	    - clamp "deferred" to total amount/capacity
		- distribute "deferred" amongst all part resource
		- add "deferred" to total amount
		- calculate rate of change per-second
		- calculate resource level
		- reset "deferred"

	NOTE
	It is impossible to guarantee coherency in resource simulation of loaded vessels,
	if consumers/producers external to the resource cache exist in the vessel (#96).
	The effect is that the whole resource simulation become dependent on timestep again.
	From the user point-of-view, there are two cases:
	- (A) the timestep-dependent error is smaller than capacity
	- (B) the timestep-dependent error is bigger than capacity
	In case [A], there are no consequences except a slightly wrong computed level and rate.
	In case [B], the simulation became incoherent and from that point anything can happen,
	like for example insta-death by co2 poisoning or climatization.
	To avoid the consequences of [B]:
	- we hacked the solar panels to use the resource cache (SolarPanelFixer)
	- we detect incoherency on loaded vessels, and forbid the two highest warp speeds
	*/

	public class VesselResHandler
	{
		public static Dictionary<int, PartResourceDefinition> allKSPResourcesById = new Dictionary<int, PartResourceDefinition>();
		public static Dictionary<string, PartResourceDefinition> allKSPResourcesByName = new Dictionary<string, PartResourceDefinition>();
		public static Dictionary<string, int> allKSPResourceIdsByName = new Dictionary<string, int>();
		private static HashSet<int> allKSPResourcesIds = new HashSet<int>();
		private static HashSet<int> allResourceIds = new HashSet<int>();

		public static PartResourceDefinition ElectricChargeDefinition { get; private set; }
		public static int ElectricChargeId { get; private set; }

		public enum EditorStep { None, Init, Next, Finalize }
		public enum SimulationType { Planner, Vessel }

		public static void ParseDefinitions()
		{
			// note : KSP resources ids are garanteed to be unique because the are stored in a dictionary in
			// PartResourceDefinitionList, but that id is obtained by calling GetHashCode() on the resource name,
			// and there is no check for the actual uniqueness of it. If that happen, the Dictionary.Add() call
			// will just throw an exception, there isn't any handling of it.

			ElectricChargeDefinition = PartResourceLibrary.Instance.GetDefinition(PartResourceLibrary.ElectricityHashcode);
			ElectricChargeId = ElectricChargeDefinition.id;

			foreach (PartResourceDefinition resDefinition in PartResourceLibrary.Instance.resourceDefinitions)
			{
				allKSPResourceIdsByName.Add(resDefinition.name, resDefinition.id);
				allKSPResourcesById.Add(resDefinition.id, resDefinition);
				allKSPResourcesByName.Add(resDefinition.name, resDefinition);
				allKSPResourcesIds.Add(resDefinition.id);
			}
		}

		// call this from Kerbalism.OnLoad()
		public static void PopulateResourceIdsOnLoad()
		{
			allResourceIds = new HashSet<int>(allKSPResourcesIds);
		}

		private SimulationType simulationType;

		public VesselResourceKSP ElectricCharge { get; protected set; }

		private VesselDataBase vesselData;
		private Dictionary<int, VesselResource> resources = new Dictionary<int, VesselResource>();
		private List<Recipe> requestedRecipes = new List<Recipe>();
		private List<Recipe> executedRecipes = new List<Recipe>();
		public bool ExecutedRecipesAreParsed { get; private set; }

		public string VesselName => vesselData.VesselName;

		public VesselResHandler(VesselDataBase vesselData, SimulationType simulationType)
		{
			this.vesselData = vesselData;
			this.simulationType = simulationType;
			ElectricCharge = AddKSPResourceToHandler(ElectricChargeDefinition);
		}

		public IEnumerable<VesselResource> Resources => resources.Values;


		/// <summary>Get the VesselResource for this resource, returns false if that resource doesn't exist or isn't of the asked type</summary>
		public bool TryGetResource(string resourceName, out VesselResource resource)
		{
			if (!allKSPResourceIdsByName.TryGetValue(resourceName, out int id))
			{
				resource = null;
				return false;
			}

			return resources.TryGetValue(id, out resource);
		}

		/// <summary>Get the VesselResource for this resource, returns false if that resource doesn't exist or isn't of the asked type</summary>
		public bool TryGetResource(int resourceId, out VesselResource resource)
		{
			return resources.TryGetValue(resourceId, out resource);
		}

		/// <summary>Get the VesselResource for this resource, returns false if that resource doesn't exist or isn't of the asked type</summary>
		public bool TryGetResource<T>(string resourceName, out T resource) where T : VesselResource
		{
			if (!allKSPResourceIdsByName.TryGetValue(resourceName, out int id))
			{
				resource = null;
				return false;
			}

			if (resources.TryGetValue(id, out VesselResource baseResource))
			{
				resource = baseResource as T;
				return resource != null;
			}

			resource = null;
			return false;
		}

		/// <summary>Get the VesselResource for this resource, returns false if that resource doesn't exist or isn't of the asked type</summary>
		public bool TryGetResource<T>(int resourceId, out T resource) where T : VesselResource
		{
			if (resources.TryGetValue(resourceId, out VesselResource baseResource))
			{
				resource = baseResource as T;
				return resource != null;
			}

			resource = null;
			return false;
		}

		public VesselResourceKSP GetKSPResource(int resourceId)
		{
			if (resources.TryGetValue(resourceId, out VesselResource resource) && resource is VesselResourceKSP)
			{
				return (VesselResourceKSP)resource;
			}

			if (allKSPResourcesById.TryGetValue(resourceId, out PartResourceDefinition definition))
			{
				return AddKSPResourceToHandler(definition);
			}

			throw new Exception($"No KSP resource found with id {resourceId}");
		}

		internal VesselResource GetResource(int resourceId)
		{
			if (resources.TryGetValue(resourceId, out VesselResource resource))
			{
				return resource;
			}

			if (allKSPResourcesById.TryGetValue(resourceId, out PartResourceDefinition definition))
			{
				return AddKSPResourceToHandler(definition);
			}

			return null;
		}

		public VesselResourceAbstract AddNewAbstractResourceToHandler(int resourceId = 0)
		{
			while (resourceId == 0 || allResourceIds.Contains(resourceId))
			{
				resourceId = Lib.RandomInt();
			}

			VesselResourceAbstract resource = new VesselResourceAbstract(resourceId);

			resources.Add(resourceId, resource);
			allResourceIds.Add(resourceId);

			return resource;
		}

		/// <summary>
		/// Copy an abstract resource to this handler, alongside the resource amount and capacity.
		/// </summary>
		public void CopyAbstractResourceToHandler(VesselResourceAbstract abstractResource, VesselResHandler fromHandler)
		{
			if (resources.TryGetValue(abstractResource.id, out VesselResource existingResource))
			{
				((VesselResourceAbstract)existingResource).SetAmountAndCapacity(abstractResource.Amount, abstractResource.Capacity);
			}
			else
			{
				resources.Add(abstractResource.id, abstractResource);
			}

			fromHandler.resources.Remove(abstractResource.id);
		}

		public void RemoveAbstractResource(int resourceId)
		{
			if (resources.TryGetValue(resourceId, out VesselResource resource) && resource is VesselResourceAbstract)
			{
				resources.Remove(resourceId);
			}
		}

		private VesselResourceKSP AddKSPResourceToHandler(PartResourceDefinition stockDefinition)
		{
			VesselResourceKSP resource = new VesselResourceKSP(stockDefinition, CreateWrapper());
			resources.Add(resource.id, resource);
			return resource;
		}

		private PartResourceWrapperCollection CreateWrapper()
		{
			switch (simulationType)
			{
				case SimulationType.Planner: return new PlannerPartResourceWrapperCollection();
				case SimulationType.Vessel: return new VesselPartResourceWrapperCollection();
				default: return null;
			}
		}

		/// <summary> record deferred execution of a recipe </summary>
		public void RequestRecipeExecution(Recipe recipe)
		{
			requestedRecipes.Add(recipe);
		}

		public void ResourceUpdate(double elapsedSec, EditorStep editorStep = EditorStep.None)
		{
			//foreach (PartResourceWrapperCollection resourceWrapper in resourceWrappers.Values)
			//{
			//	resourceWrapper.ClearPartResources(editorStep != EditorStep.Next);
			//}

			// note : editor handling here is quite a mess :
			// - we reset resource on finalize step because we want the craft amounts to be displayed, instead of the amounts resulting from the simulation
			// - we don't synchronize resources on simulation steps so the sim can be accurate
			// To solve this, we should work on a copy of the part resources, some sort of "simulation snapshot".
			// That would allow to remove all the special handling, and ensure that the editor sim is accurate.

			//switch (editorStep)
			//{
			//	case EditorStep.None:
			//	case EditorStep.Init:
			//		SyncPartResources();
			//		break;
			//	case EditorStep.Finalize:
			//		SyncPartResources();
			//		foreach (VesselResource resource in resources.Values)
			//			resource.EditorFinalize();
			//		return;
			//}

			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.ResHandler.SyncFromPartResources");
			foreach (VesselResource resource in resources.Values)
			{
				if (resource.ResourceWrapper != null)
				{
					resource.ResourceWrapper.SyncFromPartResources(true, true);
				}
			}
			UnityEngine.Profiling.Profiler.EndSample();

			
			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.ResHandler.ExecuteRecipes");

			// execute all recorded recipes
			Recipe.ExecuteRecipes(this, requestedRecipes, elapsedSec);

			// swap the lists
			List<Recipe> justExecutedRecipes = requestedRecipes;
			executedRecipes.Clear();
			requestedRecipes = executedRecipes;
			executedRecipes = justExecutedRecipes;

			UnityEngine.Profiling.Profiler.EndSample();

			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.ResHandler.ExecuteAndSyncToParts");

			bool checkCoherency = Settings.EnforceResourceCoherency && TimeWarp.CurrentRate > 1000.0;

			// apply all deferred requests and synchronize to vessel
			foreach (VesselResource resource in resources.Values)
			{
				resource.ExecuteAndSyncToParts(this, elapsedSec, checkCoherency);

				if (resource.IsSupply)
					resource.Supply.Evaluate(vesselData, resource);
			}

			foreach (Recipe executedRecipe in executedRecipes)
			{
				if (executedRecipe.hasCallback)
				{
					executedRecipe.onRecipeExecutedCallback(elapsedSec);
				}
			}

			ExecutedRecipesAreParsed = false;

			UnityEngine.Profiling.Profiler.EndSample();
		}

		/// <summary>
		/// Force synchronization of all resource handlers with the current effective amount/capacity of the vessel.
		/// To be called after VesselData instantation. Can eventually be called if a resource was manually added
		/// and you need to immediately get the VesselRessource reference for it.
		/// </summary>
		public void ForceHandlerSync()
		{
			//foreach (PartResourceWrapperCollection resourceWrapper in resourceWrappers.Values)
			//{
			//	resourceWrapper.ClearPartResources(true, false);
			//}

			//SyncPartResources();

			foreach (VesselResource resource in resources.Values)
			{
				if (resource.ResourceWrapper != null)
				{
					resource.ResourceWrapper.SyncFromPartResources(true, false);
				}
			}
		}

		public void ConvertShipHandlerToVesselHandler(VesselData vd)
		{
			vesselData = vd;
			simulationType = SimulationType.Vessel;

			foreach (int kspResourceId in allKSPResourcesIds)
			{
				if (resources.TryGetValue(kspResourceId, out VesselResource resource) && resource is VesselResourceKSP kspResource)
				{
					PartResourceWrapperCollection newWrapper = CreateWrapper();
					newWrapper.SyncWithOtherWrapper(kspResource.ResourceWrapper);
					kspResource.SetWrapper(newWrapper);
					//resourceWrappers[allResourceIdsByName[kspResourceName]] = newWrapper;
				}
			}
		}

		internal void ParseExecutedRecipes()
		{
			foreach (Recipe recipe in executedRecipes)
			{
				foreach (RecipeInputBase input in recipe.inputs)
				{
					input.vesselResource.AddExecutedIO(input);
				}

				foreach (RecipeOutputBase output in recipe.outputs)
				{
					output.vesselResource.AddExecutedIO(output);
				}
			}

			ExecutedRecipesAreParsed = true;
		}
	}
}
