using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM
{


	public static class UI
	{
		public static void Init()
		{
			// create subsystems
			new MainUILauncher();

			message = new Message();
			// initialize tooltip utility
			tooltip = new Tooltip();
		}

		public static void On_gui(bool show_window)
		{
			// render subsystems
			message.On_gui();

			tooltip.Draw();
		}

		static Message message;

		// tooltip utility
		static Tooltip tooltip;
	}


} // KERBALISM

