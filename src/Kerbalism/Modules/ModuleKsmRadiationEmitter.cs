using Flee.PublicTypes;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace KERBALISM
{
	public class ModuleKsmRadiationEmitter : KsmPartModule<ModuleKsmRadiationEmitter, RadiationEmitterData>
	{
		private static StringBuilder sb = new StringBuilder();

		[KSPField] public string title = string.Empty;        // GUI name of the status action in the PAW. Default to the target module name if a module is defined.
		[KSPField] public double passiveRadiation = 0.0;      // (rad/s) always emitted radiation
		[KSPField] public bool highEnergy = false;            // if true, emitted radiation is high energy (higher shielding penetration)
		[KSPField] public double targetModuleRadiation = 0.0; // (rad/s) radiation level that will be multiplied by the result of the targetModuleModifier expression evaluation
		[KSPField] public string targetModuleName;            // name of the partmodule to apply the targetModuleModifier expression to
		[KSPField] public int targetModulePosition = 0;       // in case there is multiple times the same module on the part, position of that module (ex : the second module has position 1)

		// C# expression. Result must evaluate to a double and is multipled to targetModuleRadiation.
		// If targetModuleRadiation isn't defined, the result is the radiation in rad/s
		// Can access all public and private fields, properties and methods of the target module, as well as static methods in the System.Math class.
		[KSPField] public string targetModuleModifier; 

		[KSPField(guiActive = true, guiActiveEditor = true, guiName = "_", groupName = "Radiation", groupDisplayName = "#KERBALISM_Group_Radiation")]//Radiation
		public string status;  // rate of radiation emitted

		private ExpressionContext modifierContext;
		private IGenericExpression<double> radiationExpression;
		private PartModule targetModule;

		public override void OnLoad(ConfigNode node)
		{
			if (HighLogic.LoadedScene == GameScenes.LOADING)
			{
				if (title.Length == 0 && string.IsNullOrEmpty(targetModuleName))
				{
					title = Local.Emitter_Name;
				}

				if (!string.IsNullOrEmpty(targetModuleName))
				{
					targetModule = part.FindModule(targetModuleName, targetModulePosition);
				}
			}
		}

		public override void OnStart(StartState state)
		{
			if (string.IsNullOrEmpty(targetModuleName))
			{
				Fields["status"].guiName = title;
			}
			else
			{
				targetModule = part.FindModule(targetModuleName, targetModulePosition);
				if (targetModule == null)
				{
					Disable();
				}
				else
				{
					Fields["status"].guiName = targetModule.GUIName;

					modifierContext = new ExpressionContext(targetModule);
					modifierContext.Options.CaseSensitive = true;
					modifierContext.Options.ParseCulture = System.Globalization.CultureInfo.InvariantCulture;
					modifierContext.Options.OwnerMemberAccess = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
					modifierContext.Imports.AddType(typeof(Math));

					if (!string.IsNullOrEmpty(targetModuleModifier))
					{
						try
						{
							radiationExpression = modifierContext.CompileGeneric<double>(targetModuleModifier);
							radiationExpression.Owner = targetModule;
						}
						catch (Exception e)
						{
							Lib.Log($"Can't parse radiationModifier on part '{part.partInfo.title}'\nmodifier: '{targetModuleModifier}'\n{e.Message}", Lib.LogLevel.Error);
							Disable();
						}
					}
				}
			}
		}

		public void Update()
		{
			status = Lib.HumanReadableRadiation(moduleData.RadiationRate);
		}

		public void FixedUpdate()
		{
			if (radiationExpression != null)
			{
				moduleData.RadiationRate = targetModuleRadiation == 0.0 ? radiationExpression.Evaluate() : targetModuleRadiation * radiationExpression.Evaluate();
				moduleData.RadiationRate += passiveRadiation;
			}
		}

		public override string GetInfo()
		{
			sb.Clear();
			
			if (highEnergy)
				sb.AppendKSPLine(Local.Emitter_EmitHigh);
			else
				sb.AppendKSPLine(Local.Emitter_EmitLow);

			sb.AppendKSPNewLine();

			if (passiveRadiation > 0.0)
			{
				sb.AppendInfo(Local.Emitter_Passive, Lib.HumanReadableRadiation(passiveRadiation));
			}

			if (!string.IsNullOrEmpty(targetModuleName))
			{
				if (targetModuleRadiation > 0.0)
				{
					sb.AppendInfo(Local.Emitter_Active, Lib.HumanReadableRadiation(passiveRadiation + targetModuleRadiation));
				}

				sb.AppendInfo(Local.Emitter_Activation, targetModule == null ? targetModuleName : targetModule.GetModuleDisplayName());
			}

			return sb.ToString();
		}

		public override string GetModuleDisplayName()
		{
			return Local.Emitter_Name;
		}

		private void Disable()
		{
			enabled = moduleIsEnabled = isEnabled = false;
			moduleData.moduleIsEnabled = false;
		}
	}
}
