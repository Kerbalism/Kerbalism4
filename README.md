<p align="center">
<img src="https://raw.githubusercontent.com/Kerbalism/Kerbalism4/master/misc/kerbalism_logo_colorV2.png" width="260" height="300">

### Welcome to Kerbalism

***Hundreds of Kerbals were killed in the making of this mod.***

Kerbalism is a mod for Kerbal Space Program that alters the game to add life support, radiation, failures and an entirely new way of doing science.

# Version 4 - In development

- **KSP version** : 1.12.x  
- **Requires : [ModuleManager] - [CommunityResourcePack] - [B9PartSwitch] - [HarmonyKSP]**
- **[Mod compatibility]**
- **License** : [Unlicense] (public domain)  

## Download and installation

Two folders must be copied to your `GameData` folder :
- **Kerbalism** is the core plugin, always required.
- **KerbalismConfig** is the default configuration pack.

**Installation checklist** for the "GameData" folder required content : 

- `000_Harmony` (folder)
- `CommunityResourcePack` (folder)
- `Kerbalism` (folder)
- `KerbalismConfig` (folder)
- `ModuleManager.X.X.X.dll` (file)

## The Kerbalism 4 project

Kerbalism 4 is an almost complete rewrite of Kerbalism. The main motivation is that the current version (3.x) has seen a lot of contributions on top of the original codebase, from a lot of people and without a clear plan. A lot of the current features are built on top of a codebase that was never meant to become that large and complex. The result has become very hard to work with and maintain, and there are long standing issues/limitations that can't really be fixed.

Progress tracking is done mainly through the [projects](https://github.com/Kerbalism/Kerbalism4/projects) page.

In terms of major changes, the goal for the initial release are to provide feature parity with the 3.x branch with the notable exception that we are likely scrapping the whole Reliability/Failures features (see https://github.com/Kerbalism/Kerbalism4/issues/3)

**Internals**
- Editor/Flight/Unloaded states agnostic [Vessel/Part/Module data/persistence framework](https://github.com/Kerbalism/Kerbalism4/tree/master/src/Kerbalism/Database)
- [PartModule framework](https://github.com/Kerbalism/Kerbalism4/tree/master/src/Kerbalism/Modules/Base)
- Virtual resources and [editor/flight agnostic resource sim](https://github.com/Kerbalism/Kerbalism4/tree/master/src/Kerbalism/ResourceSim)
- [Discrete high warp vessel environment evaluation](https://github.com/Kerbalism/Kerbalism4/tree/master/src/Kerbalism/Sim)

**Main feature - whishlist**
- Rules (life support, stress, radiation...) rewrite : https://github.com/Kerbalism/Kerbalism4/issues/4
- Habitat and pressure rewrite : https://github.com/Kerbalism/Kerbalism4/issues/9
- Thermal system (radiators & stock core heat replacement)
- Processes and resource management rewrite
- B9PS integration
- Radiation refactor and new active radiation shield system : https://github.com/Kerbalism/Kerbalism4/issues/5
- Simplification of the default profile

**UI**
- Processes and resources management dedicated UI
- Planner and flight UI rewrite : https://github.com/Kerbalism/Kerbalism4/issues/2


[Github releases]: https://github.com/Kerbalism/Kerbalism4/releases
[Github wiki]: https://github.com/Kerbalism/Kerbalism4/wiki
[GitHub issues]: https://github.com/Kerbalism/Kerbalism4/issues
[Dev Builds]: https://github.com/Kerbalism/DevBuilds/releases
[Mod Compatibility]: https://github.com/Kerbalism/Kerbalism4/projects/2
[Changelog]: https://github.com/Kerbalism/Kerbalism4/blob/master/CHANGELOG.md
[Contributing]: https://github.com/Kerbalism/Kerbalism4/blob/master/CONTRIBUTING.md
[BuildSystem]: https://github.com/Kerbalism/Kerbalism4/blob/master/BuildSystem/README.MD
[KSP forums thread]: https://forum.kerbalspaceprogram.com/index.php?/topic/201171-kerbalism
[Discord]: https://discord.gg/3JAE2JE

[KSPBugReport]: https://github.com/KSPModdingLibs/KSPBugReport
[ModuleManager]: https://ksp.sarbian.com/jenkins/job/ModuleManager/lastStableBuild/
[CommunityResourcePack]: https://github.com/BobPalmer/CommunityResourcePack/releases
[HarmonyKSP]: https://github.com/KSPModdingLibs/HarmonyKSP/releases
[B9PartSwitch]: https://github.com/blowfishpro/B9PartSwitch/releases
[MiniAVC]: https://ksp.cybutek.net/miniavc/Documents/README.htm
[KSP-AVC Plugin]: https://forum.kerbalspaceprogram.com/index.php?/topic/72169-13-12-ksp-avc-add-on-version-checker-plugin-1162-miniavc-ksp-avc-online-2016-10-13/
[CKAN]: https://forum.kerbalspaceprogram.com/index.php?/topic/197082-ckan
[Unlicense]: https://github.com/Kerbalism/Kerbalism/blob/master/LICENSE

[FAQ]: https://github.com/Kerbalism/Kerbalism4/wiki/FAQ
