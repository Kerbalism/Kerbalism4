using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace KERBALISM.KsmGui
{
	public interface IKsmGuiText
	{
		string Text { get; set; }
	}

	public interface IKsmGuiInteractable
	{
		bool Interactable { get; set; }
	}

	public interface IKsmGuiButton
	{
		void SetButtonOnClick(UnityAction action);
	}

	public interface IKsmGuiToggle
	{
		void SetToggleOnChange(UnityAction<bool> action);
	}
}
