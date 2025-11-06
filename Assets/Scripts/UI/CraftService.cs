using UnityEngine;
using Cysharp.Threading.Tasks;
using System;

using TheRavine.InventoryElements;

namespace TheRavine.Inventory
{
    public class CraftService : MonoBehaviour
    {
        const int CellsCount = 8;
        [SerializeField] private UIInventory UIInventory;
        [SerializeField] private CraftPresenter craftPresenter;
        [SerializeField] private InventoryCraftInfo[] CraftInfo;
        [SerializeField] private string[] names = new string[CellsCount], craftIngredients = new string[CellsCount];
        [SerializeField] private int[] currentCount = new int[CellsCount], ingredientsCount = new int[CellsCount];
        [SerializeField] private UIInventorySlot[] craftCells;
        [SerializeField] private UIInventorySlot result;
        private InventoryItemInfo resultItemInfo;
        private int resultCount, craftDelay;
        private bool cancel, inProcess;
        private System.Threading.CancellationTokenSource _cts;
        private InventoryModel inventory;
        private InfoManager infoManager;
        public void SetUp(InfoManager infoManager, InventoryModel inventoryModel)
        {
            cancel = false;
            _cts = new();

            this.infoManager = infoManager;
            inventory = inventoryModel;

            CraftInfo = infoManager.GetAllCraftRecepts();
        }
        public bool OnInventoryCraftCheck(object sender)
        {
            if(!result.slot.isEmpty) return false;
            for (int i = 0; i < CellsCount; i++)
            {
                names[i] = craftCells[i].slot.isEmpty ? "#" : craftCells[i]._uiInventoryItem.item.info.id;
                currentCount[i] = craftCells[i].slot.isEmpty ? 0 : craftCells[i]._uiInventoryItem.item.state.amount;
            }

            bool isPossible = false;

            for (int i = 0; i < CraftInfo.Length; i++)
            {
                var craftInfo = CraftInfo[i];
                if(!craftInfo.IsAvailable) continue;
                
                for (int j = 0; j < CellsCount; j++)
                {
                    if(j >= craftInfo.Ingr.Length)
                    {
                        craftIngredients[j] = "#";
                        ingredientsCount[j] = 0;
                    }
                    else
                    {
                        craftIngredients[j] = craftInfo.Ingr[j] != null ? craftInfo.Ingr[j].id : "#";
                        ingredientsCount[j] = craftInfo.Ingr[j] != null ? craftInfo.AmountIngr[j] : 0;
                    }
                }

                for (int j = 0; j < CellsCount; j++)
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
                    resultItemInfo = craftInfo.Res;
                    resultCount = craftInfo.AmountRes;
                    craftDelay = craftInfo.TimeToComplete;
                }

                craftPresenter.CraftPossible(isPossible);
                if (isPossible) break;
            }

            return isPossible;
        }
        public void OnCraftAction(){
            if(cancel) return;
            if(OnInventoryCraftCheck(this)) CraftProcess().Forget();
            else craftPresenter.CraftPossible(false);
        }

        public async UniTaskVoid CraftProcess()
        {
            if(inProcess) return;
            inProcess = true;
            for(int i = 0; i < CellsCount; i++){
                if(craftCells[i].slot.isEmpty) continue;
                craftCells[i]._uiInventoryItem.item.state.amount -= ingredientsCount[i];
                if (craftCells[i].slot.amount <= 0)
                    craftCells[i].slot.Clear();
                craftCells[i].Refresh();
            }
            craftPresenter.CraftPossible(false);

            while(true)
            { 
                await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate, cancellationToken: _cts.Token, true);
                if(!craftPresenter.FillProgressBar(Time.deltaTime / craftDelay)) break;
            }

            CraftThing();
        }

        public void CraftThing()
        {
            inventory.TryToAdd(this, infoManager.GetInventoryItemByInfo(resultItemInfo.id, resultItemInfo, resultCount));
            
            if(cancel) return;
            OnInventoryCraftCheck(this);
            inProcess = false;
        }


        public void BreakUp()
        {
            cancel = true;
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}