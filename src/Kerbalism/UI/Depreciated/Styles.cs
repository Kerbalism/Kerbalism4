using UnityEngine;

namespace KERBALISM
{
	public static class Styles
	{
		static Styles()
		{
			blackBackground = Lib.GetTexture("black-background", 16, 16);

			// window container
			win = new GUIStyle(HighLogic.Skin.window)
			{
				padding =
				{
					left = 6,
					right = 6,
					top = 0,
					bottom = 0
				}
			};

			// window title container
			title_container = new GUIStyle
			{
				stretchWidth = true,
				fixedHeight = 16.0f,
				margin =
				{
					bottom = 2,
					top = 2
				}
			};

			// window title text
			title_text = new GUIStyle
			{
				fontStyle = FontStyle.Bold,
				fontSize = 10,
				fixedHeight = 16.0f,
				alignment = TextAnchor.MiddleCenter
			};

			// subsection title container
			section_container = new GUIStyle
			{
				stretchWidth = true,
				fixedHeight = 16.0f,
				normal = { background = blackBackground },
				margin =
				{
					bottom = 4,
					top = 4
				}
			};

			// subsection title text
			section_text = new GUIStyle(HighLogic.Skin.label)
			{
				stretchWidth = true,
				stretchHeight = true,
				fontSize = 12,
				alignment = TextAnchor.MiddleCenter,
				normal = { textColor = Color.white }
			};

			// entry row container
			entry_container = new GUIStyle
			{
				stretchWidth = true,
				fixedHeight = 16.0f
			};

			// entry label text
			entry_label = new GUIStyle(HighLogic.Skin.label)
			{
				richText = true,
				stretchWidth = true,
				stretchHeight = true,
				fontSize = 12,
				alignment = TextAnchor.MiddleLeft,
				normal = { textColor = Color.white }
			};

			// checkbox label
			entry_checkbox = new GUIStyle(HighLogic.Skin.label)
			{
				richText = true,
				wordWrap = false,
				stretchHeight = true,
				fixedWidth = 150,
				fontSize = 12,
				alignment = TextAnchor.MiddleLeft,
				normal = { textColor = Color.white }
			};

			entry_label_nowrap = new GUIStyle(HighLogic.Skin.label)
			{
				richText = true,
				wordWrap = false,
				stretchWidth = true,
				stretchHeight = true,
				fontSize = 12,
				alignment = TextAnchor.MiddleLeft,
				normal = { textColor = Color.white }
			};

			// entry value text
			entry_value = new GUIStyle(HighLogic.Skin.label)
			{
				richText = true,
				stretchWidth = true,
				stretchHeight = true,
				fontStyle = FontStyle.Bold,
				fontSize = 12,
				alignment = TextAnchor.MiddleRight,
				normal = { textColor = Color.white }
			};

			// desc row container
			desc_container = new GUIStyle
			{
				stretchWidth = true,
				stretchHeight = true
			};

			// entry multi-line description
			desc = new GUIStyle(entry_label)
			{
				fontStyle = FontStyle.Italic,
				alignment = TextAnchor.UpperLeft,
				margin =
				{
					top = 0,
					bottom = 0
				},
				padding =
				{
					top = 0,
					bottom = 10
				}
			};

			// left icon
			left_icon = new GUIStyle
			{
				stretchWidth = true,
				stretchHeight = true,
				fixedWidth = 16.0f,
				alignment = TextAnchor.MiddleLeft
			};

			// right icon
			right_icon = new GUIStyle
			{
				stretchWidth = true,
				stretchHeight = true,
				margin = { left = 8 },
				fixedWidth = 16.0f,
				alignment = TextAnchor.MiddleRight
			};

			// tooltip label style
			tooltip = new GUIStyle(HighLogic.Skin.label)
			{
				stretchWidth = true,
				stretchHeight = true,
				fontSize = 12,
				alignment = TextAnchor.MiddleCenter,
				border = new RectOffset(0, 0, 0, 0),
				normal =
				{
					textColor = Color.white,
					background = blackBackground
				},
				margin = new RectOffset(0, 0, 0, 0),
				padding = new RectOffset(6, 6, 3, 3)
			};

			tooltip.normal.background.wrapMode = TextureWrapMode.Repeat;

			// tooltip container style
			tooltip_container = new GUIStyle
			{
				stretchWidth = true,
				stretchHeight = true
			};

			smallStationHead = new GUIStyle(HighLogic.Skin.label)
			{
				fontSize = 12
			};

			smallStationText = new GUIStyle(HighLogic.Skin.label)
			{
				fontSize = 12,
				normal = { textColor = Color.white }
			};

			message = new GUIStyle()
			{
				normal =
				{
					background = blackBackground,
					textColor = new Color(0.66f, 0.66f, 0.66f, 1.0f)
				},
				richText = true,
				stretchWidth = true,
				stretchHeight = true,
				fixedWidth = 0,
				fixedHeight = 0,
				fontSize = 14,
				alignment = TextAnchor.MiddleCenter,
				border = new RectOffset(0, 0, 0, 0),
				padding = new RectOffset(2, 2, 2, 2)
			};
		}

		/// <summary>
		/// for some unkwnown reason since KSP 1.8 IMGUI background textures are dropped on scene changes
		/// so we reload then on every OnLoad()
		/// </summary>
		public static void ReloadBackgroundStyles()
		{
			section_container.normal.background = blackBackground;
			tooltip.normal.background = blackBackground;
			message.normal.background = blackBackground;
		}

		// styles
		private static Texture2D blackBackground;
		public static GUIStyle win;                       // window
		public static GUIStyle title_container;           // window title container
		public static GUIStyle title_text;                // window title text
		public static GUIStyle section_container;         // container for a section subtitle
		public static GUIStyle section_text;              // text for a section subtitle
		public static GUIStyle entry_container;           // container for a row
		public static GUIStyle entry_label;               // left content for a row
		public static GUIStyle entry_checkbox;            // left content for a row
		public static GUIStyle entry_label_nowrap;        // left content for a row that doesn't wrap
		public static GUIStyle entry_value;               // right content for a row
		public static GUIStyle desc_container;            // multi-line description container
		public static GUIStyle desc;                      // multi-line description content
		public static GUIStyle left_icon;                 // an icon on the left
		public static GUIStyle right_icon;                // an icon on the right
		public static GUIStyle tooltip;                   // tooltip label
		public static GUIStyle tooltip_container;         // tooltip label container
		public static GUIStyle smallStationHead;
		public static GUIStyle smallStationText;
		public static GUIStyle message;
	}
} // KERBALISM
