using System.Collections.Generic;

namespace KERBALISM.VesselLogic
{
	// This class is responsible for handling the radiation evaluation on all the vessel parts
	// The goal is to have part count agnostic performance, by only performing a single part update every FixedUpdate()
	// This is needed because the amount of occlusion raycast to perform is equal to the count of receivers + receivers*emitters
	public class VesselRadiation
	{
		private const string NODENAME_UNLOADED_EMITTERS = "UNLOADED_EMITTERS";
		Queue<PartRadiationData.RaycastTask> raycastTasks = new Queue<PartRadiationData.RaycastTask>();

		//int partToUpdate = -1;
		int partToUpdate = 0;

		public void FixedUpdate(PartDataCollectionBase parts, bool loaded, double elapsedSec)
		{
			// Skip the first FixedUpdate
			// Due to inter-vessel dependencies and the various caching behavior optimizations, we need every vessel
			// to have gone trough at least one fixedUpdate before attempting to update radiation.
			// Specifically, since emitters and coil array can be on other vessels, and we rely on VesselData.ObjectCache
			// to find them, some emitters and arrays will be missing from the ObjectCache, causing them to be cleared on
			// the PartRadiationData side, loosing persisted data that we can't recalculate on unloaded vessels.
			if (partToUpdate == -1)
			{
				partToUpdate = 0;
				return;
			}

			for (int i = 0; i < parts.Count; i++)
			{
				PartRadiationData radiationData = parts[i].radiationData;
				// keep track of elapsed time since that part was last updated
				radiationData.AddElapsedTime(elapsedSec);
				// watch for part renderers changes (used for occlusion cross-section computations)
				radiationData.UpdateRenderers();

				// we update a single part per fixedupdate
				if (i == partToUpdate)
				{
					// Update() summary :
					// if the part is an occluder: 
					// - recompute occlusion stats (check resource amounts and mass changes)
					// - sychronize known active shields from the ObjectCache
					// if the part is a receiver :
					// - sychronize known emitters from the ObjectCache, and prepare an EmitterRaycastTask for each emitter
					// - compute radiation from environment, storms, emitters and substract shield effects
					radiationData.Update();

					if (loaded && radiationData.IsReceiver)
					{
						// when the vessel is loaded, add all raycast tasks for this receiver
						// to the queue of tasks to be processed :
						// - the SunRaycastTask (storms)
						// - every EmitterRaycastTask
						radiationData.EnqueueRaycastTasks(raycastTasks);
					}
				}
			}

			// get next part index
			partToUpdate = parts.Count > 0 ? (partToUpdate + 1) % parts.Count : 0;

			// process a single raycast task per fixedUpdate
			// TODO: I haven't done a lot of testing, but on vessels with a large amount of local emitters it is likely
			// that the storm raytask tasks update frequency will become too low to catch up reliably with the vessel orientation
			// changes. And on the other hand, emitters raycast tasks don't require frequent updating. It might prove necessary
			// to implement a separate queue for storm raytask tasks, with either some "load balancing" between the two queues
			// or just performing 2 raycasts per update, one for emitters and one for sun/storm
			if (raycastTasks.Count > 0)
			{
				PartRadiationData.RaycastTask task = raycastTasks.Dequeue();
				task.Raycast(raycastTasks.Count > 0 ? raycastTasks.Peek() : null);
			}
		}

	}
}
