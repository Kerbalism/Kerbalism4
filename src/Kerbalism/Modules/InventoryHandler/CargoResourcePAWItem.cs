using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KERBALISM.ModuleUI;
using KSP.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KERBALISM
{
	public class UI_KSMCargoResourceFlight : UI_Control { }

	[UI_KSMCargoResourceFlight]
	public class UIPartActionKsmCargoResourceFlight : UIPartActionFieldItem
	{
		public static UIPartActionKsmCargoResourceFlight CreatePrefab(UIPartActionResource uiResourcePrefab)
		{
			GameObject resourcePrefab = Instantiate(uiResourcePrefab.gameObject);
			resourcePrefab.name = nameof(UIPartActionKsmCargoResourceFlight);

			UIPartActionResource stockComponent = resourcePrefab.GetComponent<UIPartActionResource>();
			UIPartActionKsmCargoResourceFlight ksmComponent = resourcePrefab.AddComponent<UIPartActionKsmCargoResourceFlight>();
			ksmComponent.resourceName = stockComponent.resourceName;
			ksmComponent.resourceAmnt = stockComponent.resourceAmnt;
			ksmComponent.resourceMax = stockComponent.resourceMax;
			ksmComponent.progBar = stockComponent.progBar;
			ksmComponent.flowBtn = stockComponent.flowBtn;
			DestroyImmediate(stockComponent);

			DontDestroyOnLoad(resourcePrefab);
			resourcePrefab.transform.SetParent(Loader.KerbalismPrefabs.transform);
			return ksmComponent;
		}

		public PartResourceWrapper resource;

		public TextMeshProUGUI resourceName;

		public TextMeshProUGUI resourceAmnt;

		public TextMeshProUGUI resourceMax;

		public Slider progBar;

		public UIButtonToggle flowBtn;

		public override void Setup(UIPartActionWindow window, Part part, PartModule partModule, UI_Scene scene, UI_Control control, BaseField field)
		{
			base.Setup(window, part, partModule, scene, control, field);
			resource = (PartResourceWrapper)field.host;
			if (resource.ResName.Length > 14)
				resourceName.text = resource.ResAbbr;
			else
				resourceName.text = resource.ResName;

			if (resource.Definition.resourceFlowMode != ResourceFlowMode.NO_FLOW)
			{
				flowBtn.onToggle.AddListener(FlowBtnToggled);
				flowBtn.SetState(resource.FlowState);
			}
			else
			{
				flowBtn.gameObject.SetActive(false);
			}
		}

		public override void UpdateItem()
		{
			if (flowBtn.state != resource.FlowState)
				flowBtn.SetState(resource.FlowState);

			resourceAmnt.text = KsmString.Get.ReadableAmountCompact(resource.Amount).GetStringAndRelease();
			resourceMax.text = KsmString.Get.ReadableAmountCompact(resource.Capacity).GetStringAndRelease();
			progBar.value = (float)resource.Level;
		}

		private void FlowBtnToggled()
		{
			if (InputLockManager.IsUnlocked((control == null || !control.requireFullControl) ? ControlTypes.TWEAKABLES_ANYCONTROL : ControlTypes.TWEAKABLES_FULLONLY))
			{
				resource.FlowState = !resource.FlowState;
				flowBtn.SetState(resource.FlowState);
			}
		}
	}

	public class UI_KSMCargoResourceEditor : UI_Control { }

	[UI_KSMCargoResourceEditor]
	public class UIPartActionKsmCargoResourceEditor : UIPartActionFieldItem, IPointerEnterHandler, IPointerExitHandler
	{
		public static UIPartActionKsmCargoResourceEditor CreatePrefab(UIPartActionResourceEditor uiResourcePrefab)
		{
			GameObject resourcePrefab = Instantiate(uiResourcePrefab.gameObject);
			resourcePrefab.name = nameof(UIPartActionKsmCargoResourceEditor);

			UIPartActionResourceEditor stockComponent = resourcePrefab.GetComponent<UIPartActionResourceEditor>();
			UIPartActionKsmCargoResourceEditor ksmComponent = resourcePrefab.AddComponent<UIPartActionKsmCargoResourceEditor>();
			ksmComponent.resourceName = stockComponent.resourceName;
			ksmComponent.resourceAmnt = stockComponent.resourceAmnt;
			ksmComponent.resourceMax = stockComponent.resourceMax;
			ksmComponent.slider = stockComponent.slider;
			ksmComponent.flowBtn = stockComponent.flowBtn;
			DestroyImmediate(stockComponent);

			DontDestroyOnLoad(resourcePrefab);
			resourcePrefab.transform.SetParent(Loader.KerbalismPrefabs.transform);
			return ksmComponent;
		}

		public CargoPartResourceWrapper resource;

		public TextMeshProUGUI resourceName;

		public TextMeshProUGUI resourceAmnt;

		public TextMeshProUGUI resourceMax;

		public Slider slider;

		public UIButtonToggle flowBtn;

		private Image cargoSlotBackground;
		public Image CargoSlotBackground
		{
			get
			{
				if (cargoSlotBackground == null)
				{
					ModuleInventoryPart inventoryModule = resource.storedPartData.inventory.loadedModule;
					UIPartActionInventory inventoryGrid = (UIPartActionInventory)inventoryModule?.Fields[nameof(ModuleInventoryPart.InventorySlots)]?.uiControlEditor?.partActionItem;
					UIPartActionInventorySlot slot = inventoryGrid?.slotButton[resource.storedPartData.stockStoredPart.slotIndex];
					cargoSlotBackground = slot?.transform.Find("ButtonImage")?.GetComponent<Image>();
				}

				return cargoSlotBackground;
			}
		}

		public override void Setup(UIPartActionWindow window, Part part, PartModule partModule, UI_Scene scene, UI_Control control, BaseField field)
		{
			base.Setup(window, part, partModule, scene, control, field);
			resource = (CargoPartResourceWrapper)field.host;
			if (resource.ResName.Length > 14)
				resourceName.text = resource.ResAbbr;
			else
				resourceName.text = resource.ResName;

			if (resource.Definition.resourceFlowMode != ResourceFlowMode.NO_FLOW)
			{
				flowBtn.onToggle.AddListener(FlowBtnToggled);
				flowBtn.SetState(resource.FlowState);
			}
			else
			{
				flowBtn.gameObject.SetActive(false);
			}

			slider.onValueChanged.AddListener(OnSliderChanged);
		}

		public override void UpdateItem()
		{
			if (flowBtn.state != resource.FlowState)
				flowBtn.SetState(resource.FlowState);

			resourceAmnt.text = KsmString.Get.ReadableAmountCompact(resource.Amount).GetStringAndRelease();
			resourceMax.text = KsmString.Get.ReadableAmountCompact(resource.Capacity).GetStringAndRelease();
			slider.value = (float)resource.Level;
		}

		private void FlowBtnToggled()
		{
			if (InputLockManager.IsUnlocked((control == null || !control.requireFullControl) ? ControlTypes.TWEAKABLES_ANYCONTROL : ControlTypes.TWEAKABLES_FULLONLY))
			{
				resource.FlowState = !resource.FlowState;
				flowBtn.SetState(resource.FlowState);
			}
		}

		private void OnSliderChanged(float value)
		{
			value = Mathf.Round(value * 10f) / 10f;
			resource.Amount = (double)value * resource.Capacity;
		}

		public void OnPointerEnter(PointerEventData eventData)
		{
			CargoSlotBackground.color = new Color(0.439f, 0.725f, 0f, 1f);
		}

		public void OnPointerExit(PointerEventData eventData)
		{
			CargoSlotBackground.color = Color.white;
		}
	}


	// TODO : better PAW UI for cargo resources
	// - Currently, all resources from all cargo parts are listed under a single "inventory resources" PAW group
	// - This make differentiating what resource is in what cargo part quite difficult
	// - A potential way to solve that issue would be to highlight the cargo part when hovering on the resource
	// Also, resource transfers :
	// - we can't really keep the stock paradigm of "transfer from/to all PAW visible resources", as a single PAW can now contain PAW entries for multiple cargo parts
	// - Functionally, the likely best way is to have in / out toggles to select where to transfer, then a start/stop transfer toggle.
	// - This mean we need a brand new UI component
	// - This also mean that we need to add that component to regular part resources

	public class UI_KSMCargoResourceTransfer : UI_Control { }

	[UI_KSMCargoResourceTransfer]
	public class UIPartActionKsmCargoResourceTransfer : UIPartActionFieldItem
	{
		public static UIPartActionKsmCargoResourceTransfer CreatePrefab(UIPartActionResourceTransfer uiResourcePrefab)
		{
			GameObject resourcePrefab = Instantiate(uiResourcePrefab.gameObject);
			resourcePrefab.name = nameof(UIPartActionKsmCargoResourceTransfer);

			UIPartActionResourceTransfer stockComponent = resourcePrefab.GetComponent<UIPartActionResourceTransfer>();
			UIPartActionKsmCargoResourceTransfer ksmComponent = resourcePrefab.AddComponent<UIPartActionKsmCargoResourceTransfer>();
			ksmComponent.flowInBtn = stockComponent.flowInBtn;
			ksmComponent.flowOutBtn = stockComponent.flowOutBtn;
			ksmComponent.flowStopBtn = stockComponent.flowStopBtn;
			DestroyImmediate(stockComponent);

			DontDestroyOnLoad(resourcePrefab);
			resourcePrefab.transform.SetParent(Loader.KerbalismPrefabs.transform);
			return ksmComponent;
		}

		public PartResourceWrapper resource;

		public Button flowInBtn;

		public Button flowOutBtn;

		public Button flowStopBtn;

		public UIPartActionResourceTransfer.FlowState state;

		public List<PartResourceWrapper> targets;
		public List<UIPartActionResourceTransfer> otherStockTransfers;
		public List<UIPartActionKsmCargoResourceTransfer> otherCargoTransfers;

		public double lastUT;

		public override void Setup(UIPartActionWindow window, Part part, PartModule partModule, UI_Scene scene, UI_Control control, BaseField field)
		{
			base.Setup(window, part, partModule, scene, control, field);
			resource = (PartResourceWrapper)field.host;
			flowInBtn.gameObject.SetActive(true);
			flowOutBtn.gameObject.SetActive(true);
			flowStopBtn.gameObject.SetActive(false);

			targets = new List<PartResourceWrapper>();
			otherStockTransfers = new List<UIPartActionResourceTransfer>();
			otherCargoTransfers = new List<UIPartActionKsmCargoResourceTransfer>();

			flowInBtn.onClick.AddListener(OnBtnIn);
			flowOutBtn.onClick.AddListener(OnBtnOut);
			flowStopBtn.onClick.AddListener(OnBtnStop);
		}

		public override void UpdateItem()
		{

		}

		private void OnBtnStop()
		{
			throw new NotImplementedException();
		}

		private void OnBtnOut()
		{
			throw new NotImplementedException();
		}

		private void OnBtnIn()
		{



		}

		private void FindTargets()
		{
			foreach (UIPartActionWindow paw in UIPartActionController.Instance.windows)
			{
				if (!PartData.TryGetLoadedPartData(paw.part, out PartData partData))
					continue;

				// find part resources
				foreach (PartResourceWrapper partResourceWrapper in partData.resources)
				{
					if (partResourceWrapper.resId == resource.resId)
					{
						targets.Add(partResourceWrapper);
					}
				}

				// find 
				foreach (UIPartActionItem uiPartActionItem in paw.ListItems)
				{
					//if (uiPartActionItem is UIPartActionResource partResource && partResource.Resource.info.id == resource.resId)
					//{
					//	partTargets.Add(partResource.Resource);
					//}
					//else if (uiPartActionItem is UIPartActionKsmCargoResourceFlight cargoResource && cargoResource.resource != resource && cargoResource.resource.resId == resource.resId)
					//{
					//	cargoTargets.Add(cargoResource.resource);
					//}
					//else if (uiPartActionItem is UIPartActionKsmCargoResourceTransfer transfer && transfer != this)
					//{
					//	otherTransfers.Add(transfer);
					//}
				}
			}
		}



	}
}
