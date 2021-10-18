using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KERBALISM.KsmGui
{
	public class KsmGuiInputLock : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
	{
		private string inputLockId;
		public RectTransform rectTransform;
		public Action onPointerEnterAction;
		public Action onPointerExitAction;

		public bool IsHovering { get; private set; } = false;

		private ControlTypes inputLocks =
				ControlTypes.MANNODE_ADDEDIT |
				ControlTypes.MANNODE_DELETE |
				ControlTypes.MAP_UI | // not sure this is necessary, and might cause infinite loop of adding/removing the lock
				ControlTypes.TARGETING |
				ControlTypes.VESSEL_SWITCHING |
				ControlTypes.TWEAKABLES |
				//ControlTypes.EDITOR_UI |
				ControlTypes.EDITOR_SOFT_LOCK //|
			//ControlTypes.UI |
			//ControlTypes.CAMERACONTROLS
			;

		void Awake()
		{
			inputLockId = "KsmGuiInputLock:" + gameObject.GetInstanceID();
		}

		public void OnPointerEnter(PointerEventData pointerEventData)
		{
			if (!IsHovering)
			{
				global::InputLockManager.SetControlLock(inputLocks, inputLockId);
				IsHovering = true;
				onPointerEnterAction?.Invoke();
			}
		}

		public void OnPointerExit(PointerEventData pointerEventData)
		{
			global::InputLockManager.RemoveControlLock(inputLockId);
			IsHovering = false;
			onPointerExitAction?.Invoke();
		}

		// this handle disabling and destruction
		void OnDisable()
		{
			global::InputLockManager.RemoveControlLock(inputLockId);
		}
	}
}
