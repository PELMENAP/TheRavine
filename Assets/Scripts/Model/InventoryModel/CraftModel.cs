using System.Collections.Generic;

public struct CraftStack
{
    public IInventoryItemInfo Info;
    public int Amount;
}

public struct CraftMatch
{
    public CraftRecipe Recipe;
    public int Matched;
    public int Missing;
    public int Foreign;
}

public sealed class CraftRecipe
{
    public readonly InventoryItemInfo Result;
    public readonly int ResultCount;
    public readonly int TimeToComplete;
    public readonly CraftIngredient[] Ingredients;

    public CraftRecipe(InventoryItemInfo result, int resultCount, int timeToComplete, CraftIngredient[] ingredients)
    {
        Result         = result;
        ResultCount    = resultCount;
        TimeToComplete = timeToComplete;
        Ingredients    = ingredients;
    }

    public static CraftIngredient[] Merge(InventoryItemInfo[] infos, int[] amounts)
    {
        var merged = new List<CraftIngredient>(infos.Length);
        int count  = System.Math.Min(infos.Length, amounts.Length);

        for (int i = 0; i < count; i++)
        {
            if (infos[i] == null || amounts[i] <= 0) continue;

            int index = -1;
            for (int k = 0; k < merged.Count; k++)
                if (ReferenceEquals(merged[k].info, infos[i])) { index = k; break; }

            if (index < 0)
            {
                merged.Add(new CraftIngredient { info = infos[i], amount = amounts[i] });
                continue;
            }

            var ingredient = merged[index];
            ingredient.amount += amounts[i];
            merged[index] = ingredient;
        }

        return merged.ToArray();
    }
}

public sealed class CraftModel
{
    private readonly CraftRecipe[] _recipes;

    public CraftModel(CraftRecipe[] recipes) => _recipes = recipes;

    public int Count => _recipes.Length;

    public CraftRecipe this[int index] => _recipes[index];

    public static CraftModel FromInfos(InventoryCraftInfo[] infos)
    {
        var recipes = new List<CraftRecipe>(infos.Length);
        for (int i = 0; i < infos.Length; i++)
        {
            var info = infos[i];
            if (info == null || !info.IsAvailable || info.Res == null || info.AmountRes <= 0) continue;
            if (info.Ingr == null || info.AmountIngr == null) continue;

            var ingredients = CraftRecipe.Merge(info.Ingr, info.AmountIngr);
            if (ingredients.Length == 0) continue;

            recipes.Add(new CraftRecipe(info.Res, info.AmountRes, info.TimeToComplete, ingredients));
        }
        return new CraftModel(recipes.ToArray());
    }

    public bool TryPlan(CraftStack[] grid, int cellCount, int[] deductions, out CraftRecipe recipe)
    {
        for (int r = 0; r < _recipes.Length; r++)
        {
            recipe = _recipes[r];
            if (!Covers(recipe, grid, cellCount)) continue;

            Deduct(recipe, grid, cellCount, deductions);
            return true;
        }

        recipe = null;
        return false;
    }

    public void Score(CraftStack[] grid, int cellCount, CraftMatch[] results)
    {
        for (int r = 0; r < _recipes.Length; r++)
        {
            CraftRecipe recipe = _recipes[r];
            int matched = 0;
            int missing = 0;

            for (int i = 0; i < recipe.Ingredients.Length; i++)
            {
                int have = 0;
                for (int c = 0; c < cellCount; c++)
                    if (ReferenceEquals(grid[c].Info, recipe.Ingredients[i].info)) have += grid[c].Amount;

                int need = recipe.Ingredients[i].amount;
                matched += System.Math.Min(have, need);
                missing += System.Math.Max(0, need - have);
            }

            int foreign = 0;
            for (int c = 0; c < cellCount; c++)
            {
                if (grid[c].Info == null) continue;
                if (!Contains(recipe, grid[c].Info)) foreign++;
            }

            results[r] = new CraftMatch { Recipe = recipe, Matched = matched, Missing = missing, Foreign = foreign };
        }
    }

    private static bool Covers(CraftRecipe recipe, CraftStack[] grid, int cellCount)
    {
        for (int c = 0; c < cellCount; c++)
        {
            if (grid[c].Info == null) continue;
            if (!Contains(recipe, grid[c].Info)) return false;
        }

        for (int i = 0; i < recipe.Ingredients.Length; i++)
        {
            int have = 0;
            for (int c = 0; c < cellCount; c++)
                if (ReferenceEquals(grid[c].Info, recipe.Ingredients[i].info)) have += grid[c].Amount;
            if (have < recipe.Ingredients[i].amount) return false;
        }

        return true;
    }

    private static bool Contains(CraftRecipe recipe, IInventoryItemInfo info)
    {
        for (int i = 0; i < recipe.Ingredients.Length; i++)
            if (ReferenceEquals(recipe.Ingredients[i].info, info)) return true;
        return false;
    }

    private static void Deduct(CraftRecipe recipe, CraftStack[] grid, int cellCount, int[] deductions)
    {
        for (int c = 0; c < cellCount; c++) deductions[c] = 0;

        for (int i = 0; i < recipe.Ingredients.Length; i++)
        {
            int need = recipe.Ingredients[i].amount;
            for (int c = 0; c < cellCount && need > 0; c++)
            {
                if (!ReferenceEquals(grid[c].Info, recipe.Ingredients[i].info)) continue;

                int take = System.Math.Min(need, grid[c].Amount);
                deductions[c] += take;
                need -= take;
            }
        }
    }
}
