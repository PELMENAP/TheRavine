using System.Collections.Generic;

namespace TheRavine.EntityControl
{
    public sealed class PlayerItemLedger
    {
        private readonly Dictionary<IInventoryItemInfo, int> counts = new();

        public int Count(IInventoryItemInfo info) => info != null && counts.TryGetValue(info, out int n) ? n : 0;

        public void Credit(IInventoryItemInfo info, int amount)
        {
            if (info == null || amount <= 0) return;
            long next = (long)Count(info) + amount;
            counts[info] = next > int.MaxValue ? int.MaxValue : (int)next;
        }

        public bool TryDebit(IInventoryItemInfo info, int amount)
        {
            if (info == null || amount <= 0) return false;
            int have = Count(info);
            if (have < amount) return false;
            if (have == amount) counts.Remove(info);
            else counts[info] = have - amount;
            return true;
        }

        public bool TryCraft(CraftRecipe recipe)
        {
            if (recipe == null) return false;
            CraftIngredient[] ingredients = recipe.Ingredients;

            for (int i = 0; i < ingredients.Length; i++)
                if (Count(ingredients[i].info) < ingredients[i].amount) return false;

            for (int i = 0; i < ingredients.Length; i++)
                if (!ingredients[i].isCatalyst) TryDebit(ingredients[i].info, ingredients[i].amount);

            Credit(recipe.Result, recipe.ResultCount);
            return true;
        }

        public void Clear() => counts.Clear();
    }
}
