using HarmonyLib;
using static KERBALISM.ModuleHandler;

namespace KERBALISM.Events
{
	[HarmonyPatch(typeof(PartModule))]
	[HarmonyPatch("Save")]
	class PartModule_Save
	{
		static void Postfix(PartModule __instance, ConfigNode node)
		{
			if (loadedHandlersByModuleInstanceId.TryGetValue(__instance.GetInstanceID(), out ModuleHandler handler))
			{
				if (handler is IPersistentModuleHandler persistentHandler)
				{
					ConfigNode moduleNode = node.AddNode(NODENAME_KSMMODULE);
					moduleNode.AddValue(nameof(ModuleHandler.handlerIsEnabled), handler.handlerIsEnabled);
					persistentHandler.Save(moduleNode);
				}
			}
		}
	}

	[HarmonyPatch(typeof(PartModule))]
	[HarmonyPatch("Load")]
	class PartModule_Load
	{
		static void Postfix(PartModule __instance, ConfigNode node)
		{
			if (!handlerTypesByModuleName.TryGetValue(__instance.moduleName, out ModuleHandlerType handlerType))
				return;

			int instanceId = __instance.GetInstanceID();

			if (!loadedHandlersByModuleInstanceId.TryGetValue(instanceId, out ModuleHandler handler))
			{
				handler = handlerType.Instantiate();
				loadedHandlersByModuleInstanceId[instanceId] = handler;
			}

			if (handlerType.isPersistent)
			{
				IPersistentModuleHandler persistentHandler = (IPersistentModuleHandler)handler;

				if (!persistentHandler.ConfigLoaded)
				{
					ConfigNode moduleNode = node.GetNode(NODENAME_KSMMODULE);
					if (moduleNode != null)
					{
						handler.setupDone = true;
						handler.ParseEnabled(__instance, moduleNode);
						persistentHandler.Load(moduleNode);
						persistentHandler.ConfigLoaded = true;
					}
					else if (handlerType.isKsmModule)
					{
						((KsmModuleHandler)handler).Definition = KsmModuleDefinitionLibrary.GetDefinition((KsmPartModule)__instance, null);
					}
				}
			}

			if (handler.LoadedModuleBase == null)
			{
				handler.LoadedModuleBase = __instance;

				if (handlerType.isKsmModule)
				{
					((KsmPartModule)__instance).ModuleHandler = handler;
				}
			}
		}
	}

	[HarmonyPatch(typeof(ProtoPartModuleSnapshot))]
	[HarmonyPatch(MethodType.Constructor, typeof(PartModule))]
	class ProtoPartModuleSnapshot_PMCtor
	{
		static void Postfix(ProtoPartModuleSnapshot __instance, PartModule module)
		{
			if (loadedHandlersByModuleInstanceId.TryGetValue(module.GetInstanceID(), out ModuleHandler handler))
			{
				protoHandlersByProtoModule[__instance] = handler;
				return;
			}

			if (!handlerTypesByModuleName.TryGetValue(__instance.moduleName, out ModuleHandlerType handlerType))
				return;

			if (!protoHandlersByProtoModule.TryGetValue(__instance, out handler))
			{
				handler = handlerType.Instantiate();
				protoHandlersByProtoModule[__instance] = handler;
			}

			if (handlerType.isPersistent)
			{
				IPersistentModuleHandler persistentHandler = (IPersistentModuleHandler)handler;

				if (!persistentHandler.ConfigLoaded)
				{
					ConfigNode moduleNode = __instance.moduleValues.GetNode(NODENAME_KSMMODULE);
					if (moduleNode != null)
					{
						handler.setupDone = true;
						handler.ParseEnabled(__instance, moduleNode);
						persistentHandler.Load(moduleNode);
						persistentHandler.ConfigLoaded = true;
					}
					else if (handlerType.isKsmModule)
					{
						((KsmModuleHandler)handler).Definition = KsmModuleDefinitionLibrary.GetDefinition((KsmPartModule)module, null);
					}
				}
			}

			if (handler.LoadedModuleBase == null)
			{
				handler.LoadedModuleBase = module;

				if (handlerType.isKsmModule)
				{
					((KsmPartModule)module).ModuleHandler = handler;
				}
			}
		}
	}

	[HarmonyPatch(typeof(ProtoPartModuleSnapshot))]
	[HarmonyPatch(MethodType.Constructor, typeof(ConfigNode))]
	class ProtoPartModuleSnapshot_ConfigNodeCtor
	{
		static void Postfix(ProtoPartModuleSnapshot __instance, ConfigNode node)
		{
			if (!handlerTypesByModuleName.TryGetValue(__instance.moduleName, out ModuleHandlerType handlerType))
				return;

			if (!protoHandlersByProtoModule.TryGetValue(__instance, out ModuleHandler handler))
			{
				handler = handlerType.Instantiate();
				protoHandlersByProtoModule[__instance] = handler;
			}

			if (handlerType.isPersistent)
			{
				IPersistentModuleHandler persistentHandler = (IPersistentModuleHandler)handler;

				if (!persistentHandler.ConfigLoaded)
				{
					ConfigNode moduleNode = node.GetNode(NODENAME_KSMMODULE);
					if (moduleNode != null)
					{
						handler.setupDone = true;
						handler.ParseEnabled(__instance, moduleNode);
						persistentHandler.Load(moduleNode);
						persistentHandler.ConfigLoaded = true;
					}
				}
			}
		}
	}

	[HarmonyPatch(typeof(ProtoPartModuleSnapshot))]
	[HarmonyPatch(nameof(ProtoPartModuleSnapshot.Save))]
	class ProtoPartModuleSnapshot_Save
	{
		static void Prefix(ProtoPartModuleSnapshot __instance)
		{
			if (!protoHandlersByProtoModule.TryGetValue(__instance, out ModuleHandler handler))
				return;

			if (!handler.handlerType.isPersistent)
				return;

			ConfigNode moduleNode = __instance.moduleValues.GetNode(NODENAME_KSMMODULE);
			if (moduleNode == null)
				moduleNode = __instance.moduleValues.AddNode(NODENAME_KSMMODULE);
			else
				moduleNode.ClearData();

			moduleNode.AddValue(nameof(ModuleHandler.handlerIsEnabled), handler.handlerIsEnabled);
			((IPersistentModuleHandler)handler).Save(moduleNode);
		}
	}

	public class PartModuleLifecycle
	{
	}
}
