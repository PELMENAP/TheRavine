using UnityEngine;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;

using TheRavine.InventoryElements;
using TheRavine.EntityControl;

namespace TheRavine.Inventory
{
    public class CraftService : MonoBehaviour
    {
        const int CellsCount = 8;
        [SerializeField] private UIInventory UIInventory;
        [SerializeField] private CraftPresenter craftPresenter;
        [SerializeField] private UIInventorySlot[] craftCells;
        [SerializeField] private UIInventorySlot resultCell;
        private readonly CraftStack[] grid = new CraftStack[CellsCount];
        private readonly int[] deductions = new int[CellsCount];
        private CraftMatch[] matches = Array.Empty<CraftMatch>();
        private CraftModel craftModel;
        private InventoryItemInfo resultItemInfo;
        private int resultCount, craftDelay, resultRecipe = -1;
        private PlayerEntity player;
        private bool cancel, inProcess;
        private CancellationTokenSource _cts;
        private EventDrivenInventoryProxy inventory;
        private InfoManager infoManager;
        public IReadOnlyList<CraftStack> Grid => grid;
        public IReadOnlyList<UIInventorySlot> CraftCells => craftCells;
        public UIInventorySlot ResultCell => resultCell;
        public void SetUp(InfoManager infoManager, EventDrivenInventoryProxy inventoryModel, PlayerEntity player)
        {
            cancel = false;
            _cts = new();

            this.infoManager = infoManager;
            this.player = player;
            inventory = inventoryModel;

            craftModel = CraftModel.Shared;
            if (craftModel.Count == 0)
                Debug.LogWarning("[CraftService] No craft recipes loaded: CraftInfo assets must live under Resources/CraftInfo");
            matches = new CraftMatch[craftModel.Count];
        }
        public CraftMatch[] Evaluate()
        {
            craftModel?.Score(grid, CellsCount, matches);
            return matches;
        }
        public bool OnInventoryCraftCheck(object sender)
        {
            if(inProcess || !resultCell.slot.isEmpty) return false;

            for (int i = 0; i < CellsCount; i++)
            {
                if(craftCells[i].slot.isEmpty)
                {
                    grid[i] = default;
                    continue;
                }

                var item = craftCells[i]._uiInventoryItem.item;
                grid[i] = new CraftStack { Info = item.info, Amount = item.state.amount };
            }

            if(!craftModel.TryPlan(grid, CellsCount, deductions, out CraftRecipe recipe))
            {
                craftPresenter.CraftPossible(false);
                return false;
            }

            resultItemInfo = recipe.Result;
            resultCount    = recipe.ResultCount;
            craftDelay     = recipe.TimeToComplete;
            resultRecipe   = craftModel.IndexOf(recipe);
            craftPresenter.CraftPossible(true);
            return true;
        }
        public void OnCraftAction()
        {
            if(cancel) return;
            if(OnInventoryCraftCheck(this)) CraftProcess().Forget();
            else craftPresenter.CraftPossible(false);
        }

        public async UniTaskVoid CraftProcess()
        {
            if(inProcess) return;
            inProcess = true;
            craftPresenter.CraftPossible(false);

            if(craftDelay > 0)
            {
                while(true)
                {
                    bool canceled = await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate, _cts.Token, true).SuppressCancellationThrow();
                    if(canceled)
                    {
                        inProcess = false;
                        return;
                    }
                    if(!craftPresenter.FillProgressBar(Time.deltaTime / craftDelay)) break;
                }
            }

            inProcess = false;
            CraftThing();
        }

        public void CraftThing()
        {
            if(cancel || !OnInventoryCraftCheck(this) || !HasRoomForResult()) return;

            var result = infoManager.GetInventoryItemByInfo(resultItemInfo, resultCount);
            if (result == null) return;

            if (!inventory.TryProduceToSlot(this, resultCell.slot, result)) return;
            ConsumeIngredients();
            player?.RequestCraft(resultRecipe);
        }

        private bool HasRoomForResult() =>
            resultCount <= resultItemInfo.maxItemsInInventorySlot ||
            inventory.CanFit(resultItemInfo, resultCount - resultItemInfo.maxItemsInInventorySlot);

        private void ConsumeIngredients()
        {
            for(int i = 0; i < CellsCount; i++){
                var slot = craftCells[i].slot;
                if(slot.isEmpty || deductions[i] <= 0) continue;
                inventory.ConsumeFromSlot(this, slot, Math.Min(deductions[i], slot.amount), false);
            }
            inventory.NotifyStateChanged(this);
        }


        public void BreakUp()
        {
            cancel = true;
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}