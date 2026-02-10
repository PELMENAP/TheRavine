using UnityEngine;
using UnityEngine.InputSystem;
using System;

using TheRavine.Base;
using TheRavine.EntityControl;

namespace TheRavine.Inventory
{
    public class InventoryInputHandler : MonoBehaviour
    {
        [SerializeField] private GameObject grid, mobileInput;
        [SerializeField] private RectTransform activeCellIndicator;
        [SerializeField] private InputActionReference enterToggleAction, quitToggleAction, selectSlot;
        private PlayerEntity playerData;
        private GlobalSettings gameSettings;
        public int ActiveCellIndex {get; private set;}
        private bool isInventoryActive;

        public event Action<int> OnActiveCellChanged;

        private ActionMapController actionMapController;
        public void RegisterInput(PlayerEntity playerData, GlobalSettings gameSettings)
        {
            enterToggleAction.action.performed += ToggleInventory;
            quitToggleAction.action.performed += ToggleInventory;
            selectSlot.action.performed += HandleDigitInput;

            actionMapController = ServiceLocator.GetService<ActionMapController>();

            this.playerData = playerData;
            this.gameSettings = gameSettings;

            ActiveCellIndex = 1;

            grid.SetActive(false);
        }

        public void UnregisterInput()
        {
            enterToggleAction.action.performed -= ToggleInventory;
            quitToggleAction.action.performed -= ToggleInventory;
            selectSlot.action.performed -= HandleDigitInput;
        }

        private void ToggleInventory(InputAction.CallbackContext context)
        {
            isInventoryActive = !isInventoryActive;
            
            if (isInventoryActive && !actionMapController.IsGamePlayActive()) 
                return;

            if (isInventoryActive)
            {
                actionMapController.SwitchToInventory();
                playerData.SetBehaviourSit();
            }
            else
            {
                actionMapController.SwitchToGameplay();      
                playerData.SetBehaviourIdle();
            }
            
            if (gameSettings.controlType == ControlType.Mobile)
                mobileInput.SetActive(!isInventoryActive);
            
            grid.SetActive(isInventoryActive);
        }

        private void HandleDigitInput(InputAction.CallbackContext context)
        {
            byte newIndex = (byte)context.ReadValue<float>();
            if (newIndex == ActiveCellIndex || newIndex < 1 || newIndex > 9) 
                return;

            ActiveCellIndex = newIndex;
            UpdateActiveCellIndicator();
            OnActiveCellChanged?.Invoke(ActiveCellIndex);
        }

        private void UpdateActiveCellIndicator()
        {
            activeCellIndicator.anchoredPosition = new Vector2(55 + 120 * (ActiveCellIndex - 1), -50);
        }
    }
}