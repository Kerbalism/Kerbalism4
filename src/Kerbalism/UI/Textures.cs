using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using KSP.UI.Screens;
using UnityEngine;

namespace KERBALISM
{

	///<summary> Kerbalism's Icons </summary>
	internal static class Textures
	{

		public static Dictionary<Texture2D, string> textureNames = new Dictionary<Texture2D, string>();
		public static Dictionary<string, Texture2D> texturesByName = new Dictionary<string, Texture2D>();

		///<summary> Path to Kerbalism's textures </summary>
		internal static string TexturePath;

		internal static Texture2D empty;

		internal static Texture2D applauncher_vessels;
		internal static Texture2D applauncher_database;

		internal static Texture2D category_normal;
		internal static Texture2D category_selected;

		// KsmGui

		internal static Sprite KsmGuiSpriteBackground;

		internal static Sprite KsmGuiSpriteBtnNormal;
		internal static Sprite KsmGuiSpriteBtnHighlight;
		internal static Sprite KsmGuiSpriteBtnDisabled;

		internal static Texture2D KsmGuiColorPickerBackground;
		internal static Texture2D KsmGuiColorPickerSelector;

		internal static Texture2D KsmGuiTexCheckmark;

		internal static Texture2D KsmGuiTexHeaderArrowsLeft;
		internal static Texture2D KsmGuiTexHeaderArrowsRight;
		internal static Texture2D KsmGuiTexHeaderArrowsUp;
		internal static Texture2D KsmGuiTexHeaderArrowsDown;

		internal static Texture2D KsmGuiTexHeaderClose;
		internal static Texture2D KsmGuiTexHeaderInfo;
		internal static Texture2D KsmGuiTexHeaderRnD;

		internal static Texture2D vesselTypeAircraft;
		internal static Texture2D vesselTypeBase;
		internal static Texture2D vesselTypeCommsRelay;
		internal static Texture2D vesselTypeDebris;
		internal static Texture2D vesselTypeDeployScience;
		internal static Texture2D vesselTypeLander;
		internal static Texture2D vesselTypeProbe;
		internal static Texture2D vesselTypeRover;
		internal static Texture2D vesselTypeShip;
		internal static Texture2D vesselTypeSpaceObj;
		internal static Texture2D vesselTypeStation;
		internal static Texture2D vesselTypeEVA;

		internal static Texture2D ttBattery;
		internal static Texture2D ttBox;
		internal static Texture2D ttEscape;
		internal static Texture2D ttHeart;
		internal static Texture2D ttLanded;
		internal static Texture2D ttOrbit;
		internal static Texture2D ttRadioactive;
		internal static Texture2D ttReliability;
		internal static Texture2D ttStorm;
		internal static Texture2D ttSun;
		internal static Texture2D ttSunStriked;
		internal static Texture2D ttSuborbit;
		internal static Texture2D ttFlying;
		internal static Texture2D ttPlasma;
		internal static Texture2D ttBelt;

		internal static Texture2D ttSignalFull;
		internal static Texture2D ttSignalMid;
		internal static Texture2D ttSignalLow;
		internal static Texture2D ttSignalData;
		internal static Texture2D ttSignalNoData;
		internal static Texture2D ttSignalDirect;
		internal static Texture2D ttSignalRelay;

		internal static Texture2D ttOverlayHidden;
		internal static Texture2D ttOverlayVisible;

		internal static Texture2D delete32;
		internal static Texture2D export32;
		internal static Texture2D file32;
		internal static Texture2D import32;
		internal static Texture2D lab32;
		internal static Texture2D locked32;
		internal static Texture2D sample32;
		internal static Texture2D transmit32;

		internal static Sprite cargoInstall32;
		internal static Sprite cargoInstalled32;


		// timer controller
		internal static float nextFlashing = Time.unscaledTime;
		internal static bool lastIcon = false;

		internal static Sprite GetSprite(string texturePath, int width, int height)
		{
			Texture2D tex = Lib.GetKerbalismTexture(texturePath);
			return Sprite.Create(tex, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f));
		}

		internal static Sprite Get9SlicesSprite(string textureName, int width, int height, int borderSize)
		{
			// 9 slice sprites are self extending, they don't need to get scaled manually (I think...)
			Texture2D tex = Lib.GetKerbalismTexture(textureName);
			return Sprite.Create(tex, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect, new Vector4(borderSize, borderSize, borderSize, borderSize));
		}

		///<summary> Initializes the icons </summary>
		internal static void Init()
		{
			TexturePath = KSPUtil.ApplicationRootPath + "GameData/Kerbalism/Textures/";

			empty = Lib.GetKerbalismTexture("empty");

			applauncher_vessels = Lib.GetKerbalismTexture("applauncher-vessels");
			applauncher_database = Lib.GetKerbalismTexture("applauncher-database");

			category_normal = Lib.GetKerbalismTexture("category_normal");
			category_selected = Lib.GetKerbalismTexture("category_selected");

			// ksmGui

			KsmGuiSpriteBackground = Get9SlicesSprite("ksm-gui/background-64-5", 64, 64, 5);
			KsmGuiSpriteBtnNormal = Get9SlicesSprite("ksm-gui/btn-black-64-5", 64, 64, 5);
			KsmGuiSpriteBtnHighlight = Get9SlicesSprite("ksm-gui/btn-black-highlight-64-5", 64, 64, 5);
			KsmGuiSpriteBtnDisabled = Get9SlicesSprite("ksm-gui/btn-black-disabled-64-5", 64, 64, 5);


			KsmGuiColorPickerBackground = Lib.GetKerbalismTexture("ksm-gui/ColorPicker");
			KsmGuiColorPickerSelector = Lib.GetKerbalismTexture("ksm-gui/Selector");

			KsmGuiTexCheckmark = Lib.GetKerbalismTexture("ksm-gui/checkmark-20");

			KsmGuiTexHeaderClose = Lib.GetKerbalismTexture("ksm-gui/i8-header-close-32");
			KsmGuiTexHeaderArrowsLeft = Lib.GetKerbalismTexture("ksm-gui/arrows-left-32");
			KsmGuiTexHeaderArrowsRight = Lib.GetKerbalismTexture("ksm-gui/arrows-right-32");
			KsmGuiTexHeaderArrowsUp = Lib.GetKerbalismTexture("ksm-gui/arrows-up-32");
			KsmGuiTexHeaderArrowsDown = Lib.GetKerbalismTexture("ksm-gui/arrows-down-32");

			KsmGuiTexHeaderClose = Lib.GetKerbalismTexture("ksm-gui/i8-header-close-32");
			KsmGuiTexHeaderInfo = Lib.GetKerbalismTexture("ksm-gui/info-32");
			KsmGuiTexHeaderRnD = Lib.GetKerbalismTexture("ksm-gui/i8-rnd-32");

			vesselTypeAircraft = Lib.GetKerbalismTexture("vesselTypes/vesselTypeAircraft-48");
			vesselTypeBase = Lib.GetKerbalismTexture("vesselTypes/vesselTypeBase-48");
			vesselTypeCommsRelay = Lib.GetKerbalismTexture("vesselTypes/vesselTypeCommsRelay-48");
			vesselTypeDebris = Lib.GetKerbalismTexture("vesselTypes/vesselTypeDebris-48");
			vesselTypeDeployScience = Lib.GetKerbalismTexture("vesselTypes/vesselTypeDeployScience-48");
			vesselTypeLander = Lib.GetKerbalismTexture("vesselTypes/vesselTypeLander-48");
			vesselTypeProbe = Lib.GetKerbalismTexture("vesselTypes/vesselTypeProbe-48");
			vesselTypeRover = Lib.GetKerbalismTexture("vesselTypes/vesselTypeRover-48");
			vesselTypeShip = Lib.GetKerbalismTexture("vesselTypes/vesselTypeShip-48");
			vesselTypeSpaceObj = Lib.GetKerbalismTexture("vesselTypes/vesselTypeSpaceObj-48");
			vesselTypeStation = Lib.GetKerbalismTexture("vesselTypes/vesselTypeStation-48");
			vesselTypeEVA = Lib.GetKerbalismTexture("vesselTypes/vesselTypeEVA-48");

			ttBattery = Lib.GetKerbalismTexture("Icons8MaterialTwoTones/battery-48");
			ttBox = Lib.GetKerbalismTexture("Icons8MaterialTwoTones/box-48");
			ttEscape = Lib.GetKerbalismTexture("Icons8MaterialTwoTones/escape-48");
			ttHeart = Lib.GetKerbalismTexture("Icons8MaterialTwoTones/heart-48");
			ttLanded = Lib.GetKerbalismTexture("Icons8MaterialTwoTones/landed-48");
			ttOrbit = Lib.GetKerbalismTexture("Icons8MaterialTwoTones/orbit-48");
			ttRadioactive = Lib.GetKerbalismTexture("Icons8MaterialTwoTones/radioactive-48");
			ttReliability = Lib.GetKerbalismTexture("Icons8MaterialTwoTones/reliability-48");
			ttStorm = Lib.GetKerbalismTexture("Icons8MaterialTwoTones/storm-48");
			ttSun = Lib.GetKerbalismTexture("Icons8MaterialTwoTones/sun-48");
			ttSunStriked = Lib.GetKerbalismTexture("Icons8MaterialTwoTones/sunStriked-48");
			ttSuborbit = Lib.GetKerbalismTexture("Icons8MaterialTwoTones/suborbit-48");
			ttFlying = Lib.GetKerbalismTexture("Icons8MaterialTwoTones/flying-48");
			ttPlasma = Lib.GetKerbalismTexture("Icons8MaterialTwoTones/plasma-48");
			ttBelt = Lib.GetKerbalismTexture("Icons8MaterialTwoTones/belt-48");

			ttSignalFull = Lib.GetKerbalismTexture("Icons8MaterialTwoTones/signalFull-48");
			ttSignalMid = Lib.GetKerbalismTexture("Icons8MaterialTwoTones/signalMid-48");
			ttSignalLow = Lib.GetKerbalismTexture("Icons8MaterialTwoTones/signalLow-48");
			ttSignalData = Lib.GetKerbalismTexture("Icons8MaterialTwoTones/signalData-48");
			ttSignalNoData = Lib.GetKerbalismTexture("Icons8MaterialTwoTones/signalNoData-48");
			ttSignalDirect = Lib.GetKerbalismTexture("Icons8MaterialTwoTones/signalDirect-48");
			ttSignalRelay = Lib.GetKerbalismTexture("Icons8MaterialTwoTones/signalRelay-48");

			ttOverlayHidden = Lib.GetKerbalismTexture("Icons8MaterialTwoTones/overlayHidden-48");
			ttOverlayVisible = Lib.GetKerbalismTexture("Icons8MaterialTwoTones/overlayVisible-48");

			delete32 = Lib.GetKerbalismTexture("UI/delete-32");
			export32 = Lib.GetKerbalismTexture("UI/export-32");
			file32 = Lib.GetKerbalismTexture("UI/file-32");
			import32 = Lib.GetKerbalismTexture("UI/import-32");
			lab32 = Lib.GetKerbalismTexture("UI/lab-32");
			locked32 = Lib.GetKerbalismTexture("UI/locked-32");
			sample32 = Lib.GetKerbalismTexture("UI/sample-32");
			transmit32 = Lib.GetKerbalismTexture("UI/transmit-32");

			cargoInstall32 = GetSprite("UI/cargoInstall-32", 32, 32);
			cargoInstalled32 = GetSprite("UI/cargoInstalled-32", 32, 32);

			//Texture2D winBg = Lib.GetTexture("ui-core/window-background", 64, 64);
			//// inspecting pixelPerUnit gives 92.75362, but 100f is the default value and seems to work fine
			//window_background = Sprite.Create(winBg, new Rect(0f, 0f, 64f, 64f), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.Tight, new Vector4(4.6f, 4.6f, 4.6f, 4.6f));

			//close_btn_tex = Lib.GetTexture("ui-core/icons8-cancel-24", 24, 24);
			//close_btn = Sprite.Create(close_btn_tex, new Rect(0f, 0f, 24f, 24f), new Vector2(0.5f, 0.5f), 100f);

			foreach (FieldInfo field in typeof(Textures).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
			{
				if (field.FieldType != typeof(Texture2D))
					continue;

				Texture2D value = (Texture2D) field.GetValue(null);
				if (value == null)
					continue;

				textureNames[value] = field.Name;
				texturesByName[field.Name] = value;
			}

		}

		/// <summary>Switch icons based on time </summary>
		/// <param name="icon1">First Texture2D</param>
		/// <param name="icon2">Second Texture2D</param>
		/// <param name="interval">interval in sec</param>
		/// <returns></returns>
		internal static Texture2D iconSwitch(Texture2D icon1, Texture2D icon2, float interval = 1f)
		{
			if (Time.unscaledTime > nextFlashing)
			{
				nextFlashing = Time.unscaledTime + interval;
				lastIcon ^= true;
			}
			if (lastIcon) return icon1;
			return icon2;
		}

		private static void GetVesselTypeTextures()
		{
			try
			{
				VesselRenameDialog prefab = AssetBase.GetPrefab("VesselRenameDialog").GetComponent<VesselRenameDialog>();
				Type rdType = typeof(VesselRenameDialog);
				BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;

				vesselTypeAircraft = ((TypeButton)rdType.GetField("toggleAircraft", flags).GetValue(prefab)).icon.sprite.texture;
				vesselTypeBase = ((TypeButton)rdType.GetField("toggleBase", flags).GetValue(prefab)).icon.sprite.texture;
				vesselTypeCommsRelay = ((TypeButton)rdType.GetField("toggleCommunicationsRelay", flags).GetValue(prefab)).icon.sprite.texture;
				vesselTypeDebris = ((TypeButton)rdType.GetField("toggleDebris", flags).GetValue(prefab)).icon.sprite.texture;
				vesselTypeDeployScience = ((TypeButton)rdType.GetField("toggleDeployedScience", flags).GetValue(prefab)).icon.sprite.texture;
				vesselTypeLander = ((TypeButton)rdType.GetField("toggleLander", flags).GetValue(prefab)).icon.sprite.texture;
				vesselTypeProbe = ((TypeButton)rdType.GetField("toggleProbe", flags).GetValue(prefab)).icon.sprite.texture;
				vesselTypeRover = ((TypeButton)rdType.GetField("toggleRover", flags).GetValue(prefab)).icon.sprite.texture;
				vesselTypeShip = ((TypeButton)rdType.GetField("toggleShip", flags).GetValue(prefab)).icon.sprite.texture;
				vesselTypeSpaceObj = ((TypeButton)rdType.GetField("toggleSpaceObj", flags).GetValue(prefab)).icon.sprite.texture;
				vesselTypeStation = ((TypeButton)rdType.GetField("toggleStation", flags).GetValue(prefab)).icon.sprite.texture;
			}
			catch (Exception e)
			{
				ErrorManager.AddError(true, "Error retrieving vessel types textures", e.ToString());
			}
		}

		private static Texture2D GetReadableTexture(Texture2D source)
		{
			RenderTexture renderTex = RenderTexture.GetTemporary(
				source.width,
				source.height,
				0,
				RenderTextureFormat.Default,
				RenderTextureReadWrite.Linear);

			Graphics.Blit(source, renderTex);
			RenderTexture previous = RenderTexture.active;
			RenderTexture.active = renderTex;
			Texture2D readableText = new Texture2D(source.width, source.height);
			readableText.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
			readableText.Apply();
			RenderTexture.active = previous;
			RenderTexture.ReleaseTemporary(renderTex);
			return readableText;
		}

		public static void ExportAsPNG(this Texture2D texture, string textureName)
		{
			File.WriteAllBytes(
				Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "KSP_Textures", textureName + ".png"),
				GetReadableTexture(texture).EncodeToPNG());
		}
	}
} // KERBALISM
