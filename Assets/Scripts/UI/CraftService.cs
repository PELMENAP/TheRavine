using UnityEngine;
using Cysharp.Threading.Tasks;
using System;

using TheRavine.Services;
using TheRavine.InventoryElements;

namespace TheRavine.Inventory
{
    public class CraftService : MonoBehaviour, ISetAble
    {
        const byte CellsCount = 8;
        [SerializeField] private UIInventory UIInventory;
        [SerializeField] private CraftPresenter craftPresenter;
        [SerializeField] private InventoryCraftInfo[] CraftInfo;
        [SerializeField] private string[] names = new string[CellsCount], craftIngredients = new string[CellsCount];
        [SerializeField] private int[] currentCount = new int[CellsCount], ingredientsCount = new int[CellsCount];
        [SerializeField] private UIInventorySlot[] craftCells;
        [SerializeField] private UIInventorySlot result;
        private InventoryItemInfo resultItemInfo;
        private int resultCount, craftDelay;
        public void SetUp(ISetAble.Callback callback, ServiceLocator locator)
        {
            CraftInfo = InfoManager.GetAllCraftRecepts();
            callback?.Invoke();
        }
        public bool OnInventoryCraftCheck(object sender)
        {
            if(!result.slot.isEmpty) return false;
            for (byte i = 0; i < CellsCount; i++)
            {
                names[i] = craftCells[i].slot.isEmpty ? "#" : craftCells[i]._uiInventoryItem.item.info.id;
                currentCount[i] = craftCells[i].slot.isEmpty ? 0 : craftCells[i]._uiInventoryItem.item.state.amount;
            }

            bool isPossible = false;

            for (int i = 0; i < CraftInfo.Length; i++)
            {
                var craftInfo = CraftInfo[i];
                if(!craftInfo.isAvailable) continue;
                
                for (byte j = 0; j < CellsCount; j++)
                {
                    if(j >= craftInfo.ingr.Length)
                    {
                        craftIngredients[j] = "#";
                        ingredientsCount[j] = 0;
                    }
                    else
                    {
                        craftIngredients[j] = craftInfo.ingr[j] != null ? craftInfo.ingr[j].id : "#";
                        ingredientsCount[j] = craftInfo.ingr[j] != null ? craftInfo.amountIngr[j] : 0;
                    }
                }

                for (byte j = 0; j < CellsCount; j++)
                {
                    if (craftIngredients[j].Equals(names[j], StringComparison.OrdinalIgnoreCase) && currentCount[j] >= ingredientsCount[j])
                    {
                        isPossible = true;
                    }
                    else
                    {
                        isPossible = false;
                        break;
                    }
                }

                if(isPossible)
                {
                    resultItemInfo = craftInfo.res;
                    resultCount = craftInfo.amountRes;
                    craftDelay = craftInfo.timeToComplete;
                }

                craftPresenter.CraftPossible(isPossible);
                if (isPossible) break;
            }

            return isPossible;
        }
        public void OnCraftAction(){
            if(OnInventoryCraftCheck(this)) CraftProcess().Forget();
            else craftPresenter.CraftPossible(false);
        }

        public async UniTaskVoid CraftProcess()
        {
            for(byte i = 0; i < CellsCount; i++){
                if(craftCells[i].slot.isEmpty) continue;
                craftCells[i]._uiInventoryItem.item.state.amount -= ingredientsCount[i];
                if (craftCells[i].slot.amount <= 0)
                    craftCells[i].slot.Clear();
            }
            craftPresenter.CraftPossible(false);
            while(craftPresenter.FillProgressBar(Time.deltaTime / craftDelay)) await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
            CraftThing();
        }

        public void CraftThing()
        {
            
            for(byte i = 0; i < CellsCount; i++){
                craftCells[i].Refresh();
            }
            UIInventory.inventory.TryToAdd(this, InfoManager.GetInventoryItemByInfo(resultItemInfo.id, resultItemInfo, resultCount));
            OnInventoryCraftCheck(this);
        }


        public void BreakUp()
        {
        }
    }
}