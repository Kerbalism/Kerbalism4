using System;
using System.Collections;
using System.Collections.Generic;

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
		private static Dictionary<string, int> allResourceIdsByName = new Dictionary<string, int>();
		private static Dictionary<int, ResourceType> allResourceTypesById = new Dictionary<int, ResourceType>();
		private static HashSet<string> allKSPResources = new HashSet<string>();
		
		private static bool isSetupDone = false;
		private const string ECResName = "ElectricCharge";
		private static int ECResId;

		public enum EditorStep { None, Init, Next, Finalize }
		public enum SimulationType { Planner, Vessel }
		public enum ResourceType { KSP, PartVirtual, VesselVirtual }

		public static void SetupDefinitions()
		{
			// note : KSP resources ids are garanteed to be unique because the are stored in a dictionary in
			// PartResourceDefinitionList, but that id is obtained by calling GetHashCode() on the resource name,
			// and there is no check for the actual uniqueness of it. If that happen, the Dictionary.Add() call
			// will just throw an exception, there isn't any handling of it.

			ECResId = PartResourceLibrary.Instance.GetDefinition(ECResName).id;

			foreach (PartResourceDefinition resDefinition in PartResourceLibrary.Instance.resourceDefinitions)
			{
				if (!allResourceIdsByName.ContainsKey(resDefinition.name))
				{
					allResourceIdsByName.Add(resDefinition.name, resDefinition.id);
					allResourceTypesById.Add(resDefinition.id, ResourceType.KSP);
					allKSPResources.Add(resDefinition.name);
				}
			}

			foreach (VirtualResourceDefinition vResDefinition in VirtualResourceDefinition.definitions.Values)
			{
				do vResDefinition.id = Lib.RandomInt();
				while (allResourceTypesById.ContainsKey(vResDefinition.id));

				allResourceIdsByName.Add(vResDefinition.name, vResDefinition.id);
				allResourceTypesById.Add(vResDefinition.id, vResDefinition.resType);
			}

			isSetupDone = true;
		}

		public static int GetVirtualResourceId(string resName, ResourceType resType)
		{
			// make sure we don't affect an id before we have populated the KSP resources ids
			if (!isSetupDone)
				return 0;

			int id;
			do id = Lib.RandomInt();
			while (allResourceTypesById.ContainsKey(id));

			allResourceIdsByName.Add(resName, id);
			allResourceTypesById.Add(id, resType);

			return id;
		}


		private SimulationType simulationType;

		public VesselKSPResource ElectricCharge { get; protected set; }

		private List<Recipe> recipes = new List<Recipe>(4);
		private Dictionary<string, VesselResource> resources = new Dictionary<string, VesselResource>();
		private Dictionary<int, PartResourceWrapperCollection> resourceWrappers = new Dictionary<int, PartResourceWrapperCollection>();
		private VesselDataBase vesselData;

		public VesselResHandler(VesselDataBase vesselData, SimulationType simulationType)
		{
			this.vesselData = vesselData;
			this.simulationType = simulationType;
			AddResourceToHandler(ECResName, ECResId);
			ElectricCharge = (VesselKSPResource)resources[ECResName];
		}

		public IEnumerable<VesselResource> Resources => resources.Values;

		/// <summary>return the VesselResource for this resource or create a VesselVirtualResource if the resource doesn't exists</summary>
		public VesselResource GetResource(string resourceName)
		{
			// try to get the resource
			if (resources.TryGetValue(resourceName, out VesselResource resource))
			{
				return resource;
			}
			// if not found, and it's a KSP resource, create it
			if (allKSPResources.Contains(resourceName))
			{
				AddResourceToHandler(resourceName, allResourceIdsByName[resourceName]);
				resource = resources[resourceName];
			}
			// otherwise create a virtual resource
			else
			{
				resource = AutoCreateVirtualResource(resourceName);
			}

			return resource;
		}

		private PartResourceWrapperCollection AddResourceToHandler(string resourceName, int resourceId)
		{
			PartResourceWrapperCollection wrapper = CreateWrapper();
			resourceWrappers.Add(resourceId, wrapper);
			resources.Add(resourceName, new VesselKSPResource(resourceName, resourceId, wrapper));
			return wrapper;
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

		private VesselResource AutoCreateVirtualResource(string name)
		{
			if (!VirtualResourceDefinition.definitions.TryGetValue(name, out VirtualResourceDefinition definition))
			{
				definition = VirtualResourceDefinition.GetOrCreateDefinition(name, false, ResourceType.VesselVirtual);
			}

			switch (definition.resType)
			{
				case ResourceType.PartVirtual:
					return GetOrCreateVirtualResource<VesselVirtualPartResource>(name);
				case ResourceType.VesselVirtual:
					return GetOrCreateVirtualResource<VesselVirtualResource>(name);
			}
			return null;
		}

		/// <summary>Get the VesselResource for this resource, returns false if that resource doesn't exist or isn't of the asked type</summary>
		public bool TryGetResource<T>(string resourceName, out T resource) where T : VesselResource
		{
			if (resources.TryGetValue(resourceName, out VesselResource baseResource))
			{
				resource = baseResource as T;
				return resource != null;
			}
			resource = null;
			return false;
		}

		/// <summary> Get-or-create a VesselVirtualPartResource or VesselVirtualResource with a random unique name </summary>
		public T GetOrCreateVirtualResource<T>() where T : VesselResource
		{
			string id;
			do id = Guid.NewGuid().ToString();
			while (allResourceIdsByName.ContainsKey(id));

			return GetOrCreateVirtualResource<T>(id);
		}

		/// <summary> Get-or-create a VesselVirtualPartResource or VesselVirtualResource with the specified name </summary>
		public T GetOrCreateVirtualResource<T>(string name) where T : VesselResource
		{
			if (resources.TryGetValue(name, out VesselResource baseExistingResource))
			{
				if (!(baseExistingResource is T existingResource))
				{
					Lib.Log($"Can't create the {typeof(T).Name} `{name}`, a VesselResource of type {baseExistingResource.GetType().Name} with that name exists already", Lib.LogLevel.Error);
					return null;
				}
				else
				{
					return existingResource;
				}
			}
			else
			{
				if (typeof(T) == typeof(VesselVirtualResource))
				{
					VesselResource resource = new VesselVirtualResource(name);
					resources.Add(name, resource);
					return (T)resource;
				}
				else
				{

					PartResourceWrapperCollection wrapper = CreateWrapper();
					VesselVirtualPartResource partResource = new VesselVirtualPartResource(wrapper, name);
					resourceWrappers.Add(partResource.Definition.id, wrapper);
					resources.Add(name, partResource);
					return (T)(VesselResource)partResource;
				}
			}
		}

		public VirtualResourceDefinition AddVirtualPartResourceToHandler(string resName)
		{
			if (resources.TryGetValue(resName, out VesselResource resourceHandler))
			{
				if (resourceHandler is VesselVirtualPartResource virtualResourceHandler)
				{
					return virtualResourceHandler.Definition;
				}
				else
				{
					Lib.Log($"Error trying to add the VirtualPartResource {resName} to {vesselData}, a {resourceHandler.GetType()} with that name exists already", Lib.LogLevel.Error);
					return null;
				}
			}

			VirtualResourceDefinition definition = VirtualResourceDefinition.GetOrCreateDefinition(resName, false, ResourceType.PartVirtual);
			PartResourceWrapperCollection wrapper = CreateWrapper();
			resourceHandler = new VesselVirtualPartResource(wrapper, definition);
			resourceWrappers.Add(definition.id, wrapper);
			resources.Add(resName, resourceHandler);
			return definition;
		}

		/// <summary> record deferred production of a resource (shortcut) </summary>
		/// <param name="broker">short ui-friendly name for the producer</param>
		public void Produce(string resource_name, double quantity, ResourceBroker broker)
		{
			GetResource(resource_name).Produce(quantity, broker);
		}

		/// <summary> record deferred consumption of a resource (shortcut) </summary>
		/// <param name="broker">short ui-friendly name for the consumer</param>
		public void Consume(string resource_name, double quantity, ResourceBroker broker)
		{
			GetResource(resource_name).Consume(quantity, broker);
		}

		/// <summary> record deferred execution of a recipe (shortcut) </summary>
		public void AddRecipe(Recipe recipe)
		{
			recipes.Add(recipe);
		}

		public void ResourceUpdate(double elapsedSec, EditorStep editorStep = EditorStep.None)
		{
			foreach (PartResourceWrapperCollection resourceWrapper in resourceWrappers.Values)
			{
				resourceWrapper.ClearPartResources(editorStep != EditorStep.Next);
			}

			// note : editor handling here is quite a mess :
			// - we reset resource on finalize step because we want the craft amounts to be displayed, instead of the amounts resulting from the simulation
			// - we don't synchronize resources on simulation steps so the sim can be accurate
			// To solve this, we should work on a copy of the part resources, some sort of "simulation snapshot".
			// That would allow to remove all the special handling, and ensure that the editor sim is accurate.

			switch (editorStep)
			{
				case EditorStep.None:
				case EditorStep.Init:
					SyncPartResources();
					break;
				case EditorStep.Finalize:
					SyncPartResources();
					foreach (VesselResource resource in resources.Values)
						resource.EditorFinalize();
					return;
			}

			// execute all recorded recipes
			Recipe.ExecuteRecipes(this, recipes);

			// forget the recipes
			recipes.Clear();

			// apply all deferred requests and synchronize to vessel
			foreach (VesselResource resource in resources.Values)
			{
				// note : we try to exclude resources that aren't relevant here to save some
				// performance, but this might have minor side effects, like brokers not being reset
				// after a vessel part count change for example. 
				if (!resource.NeedUpdate)
					continue;

				if (resource.ExecuteAndSyncToParts(vesselData, elapsedSec) && vesselData.LoadedOrEditor)
					CoherencyWarning(resource.Title);
			}

		}

		/// <summary>
		/// Force synchronization of all resource handlers with the current effective amount/capacity of the vessel.
		/// To be called after VesselData instantation. Can eventually be called if a resource was manually added
		/// and you need to immediately get the VesselRessource reference for it.
		/// </summary>
		public void ForceHandlerSync()
		{
			foreach (PartResourceWrapperCollection resourceWrapper in resourceWrappers.Values)
			{
				resourceWrapper.ClearPartResources(true, false);
			}

			SyncPartResources();
		}

		public void ConvertShipHandlerToVesselHandler(VesselData vd)
		{
			vesselData = vd;
			simulationType = SimulationType.Vessel;

			foreach (string kspResourceName in allKSPResources)
			{
				if (resources.TryGetValue(kspResourceName, out VesselResource resource) && resource is VesselKSPResource kspResource)
				{
					PartResourceWrapperCollection newWrapper = CreateWrapper();
					newWrapper.SyncWithOtherWrapper(kspResource.ResourceWrapper);
					kspResource.SetWrapper(newWrapper);
					resourceWrappers[allResourceIdsByName[kspResourceName]] = newWrapper;
				}
			}
		}

		private void SyncPartResources()
		{
			foreach (PartData part in vesselData.Parts)
			{
				foreach (PartResourceWrapper partResourceWrapper in part.resources)
				{
					if (!partResourceWrapper.FlowState)
						continue;

					if (!resourceWrappers.TryGetValue(partResourceWrapper.ResId, out PartResourceWrapperCollection wrapper))
						wrapper = AddResourceToHandler(partResourceWrapper.ResName, partResourceWrapper.ResId);

					wrapper.AddPartResourceWrapper(partResourceWrapper);
				}

				foreach (VirtualPartResource virtualPartResource in part.virtualResources)
				{
					if (!virtualPartResource.FlowState)
						continue;

					if (!resourceWrappers.TryGetValue(virtualPartResource.ResId, out PartResourceWrapperCollection wrapper))
						wrapper = AddResourceToHandler(virtualPartResource.ResName, virtualPartResource.ResId);

					wrapper.AddPartResourceWrapper(virtualPartResource);
				}
			}
		}

		private void CoherencyWarning(string resourceName)
		{
			Message.Post
			(
				Severity.warning,
				Lib.BuildString("On <b>", vesselData.VesselName, "</b>\n",
				"a producer of <b>", resourceName, "</b> has\n",
				"incoherent behavior at high warp speeds.\n",
				"<i>Please unload the vessel before warping</i>")
			);
			Lib.StopWarp(5);
		}
	}
}
