using NUnit.Framework;
using UnityEngine;

public class CraftModelTests
{
    private InventoryItemInfo _a, _b, _c, _x, _y;
    private int[] _deductions;

    [SetUp]
    public void SetUp()
    {
        _a = ScriptableObject.CreateInstance<InventoryItemInfo>();
        _b = ScriptableObject.CreateInstance<InventoryItemInfo>();
        _c = ScriptableObject.CreateInstance<InventoryItemInfo>();
        _x = ScriptableObject.CreateInstance<InventoryItemInfo>();
        _y = ScriptableObject.CreateInstance<InventoryItemInfo>();
        _deductions = new int[8];
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_a);
        Object.DestroyImmediate(_b);
        Object.DestroyImmediate(_c);
        Object.DestroyImmediate(_x);
        Object.DestroyImmediate(_y);
    }

    private CraftRecipe Recipe(InventoryItemInfo result, params (InventoryItemInfo info, int amount)[] ingredients)
    {
        var list = new CraftIngredient[ingredients.Length];
        for (int i = 0; i < ingredients.Length; i++)
            list[i] = new CraftIngredient { info = ingredients[i].info, amount = ingredients[i].amount };
        return new CraftRecipe(result, 1, 0, list);
    }

    private static CraftStack Stack(IInventoryItemInfo info, int amount) => new CraftStack { Info = info, Amount = amount };

    [Test]
    public void MatchesIgnoringCellPositions()
    {
        var model = new CraftModel(new[] { Recipe(_y, (_a, 2), (_b, 1)) });
        var grid = new CraftStack[8];
        grid[0] = Stack(_b, 1);
        grid[3] = Stack(_a, 1);
        grid[5] = Stack(_a, 1);

        bool matched = model.TryPlan(grid, 8, _deductions, out CraftRecipe recipe);

        Assert.IsTrue(matched);
        Assert.AreSame(_y, recipe.Result);
        Assert.AreEqual(1, _deductions[0]);
        Assert.AreEqual(1, _deductions[3]);
        Assert.AreEqual(1, _deductions[5]);
        Assert.AreEqual(0, _deductions[1]);
    }

    [Test]
    public void LargerStackIsPartiallyConsumed()
    {
        var model = new CraftModel(new[] { Recipe(_y, (_a, 2), (_b, 1)) });
        var grid = new CraftStack[8];
        grid[0] = Stack(_a, 5);
        grid[1] = Stack(_b, 1);

        Assert.IsTrue(model.TryPlan(grid, 8, _deductions, out _));
        Assert.AreEqual(2, _deductions[0]);
        Assert.AreEqual(1, _deductions[1]);
    }

    [Test]
    public void ExtraIngredientTypeBlocksMatch()
    {
        var model = new CraftModel(new[] { Recipe(_y, (_a, 1), (_b, 1)) });
        var grid = new CraftStack[8];
        grid[0] = Stack(_a, 1);
        grid[1] = Stack(_b, 1);
        grid[2] = Stack(_c, 1);

        Assert.IsFalse(model.TryPlan(grid, 8, _deductions, out _));
    }

    [Test]
    public void InsufficientAmountBlocksMatch()
    {
        var model = new CraftModel(new[] { Recipe(_y, (_a, 2)) });
        var grid = new CraftStack[8];
        grid[0] = Stack(_a, 1);
        grid[4] = Stack(_a, 0);

        Assert.IsFalse(model.TryPlan(grid, 8, _deductions, out _));
    }

    [Test]
    public void FirstMatchingRecipeWins()
    {
        var model = new CraftModel(new[] { Recipe(_x, (_a, 1)), Recipe(_y, (_a, 1)) });
        var grid = new CraftStack[8];
        grid[0] = Stack(_a, 1);

        Assert.IsTrue(model.TryPlan(grid, 8, _deductions, out CraftRecipe recipe));
        Assert.AreSame(_x, recipe.Result);
    }

    [Test]
    public void EmptyGridMatchesNothing()
    {
        var model = new CraftModel(new[] { Recipe(_y, (_a, 1)) });
        var grid = new CraftStack[8];

        Assert.IsFalse(model.TryPlan(grid, 8, _deductions, out _));
    }

    [Test]
    public void MergeCombinesDuplicateIngredientsByReference()
    {
        var merged = CraftRecipe.Merge(new[] { _a, _b, _a, null }, new[] { 1, 4, 2, 9 });

        Assert.AreEqual(2, merged.Length);
        Assert.AreSame(_a, merged[0].info);
        Assert.AreEqual(3, merged[0].amount);
        Assert.AreSame(_b, merged[1].info);
        Assert.AreEqual(4, merged[1].amount);
    }

    [Test]
    public void ProjectCraftRecipesLoadFromResources()
    {
        var infos = Resources.LoadAll<InventoryCraftInfo>("CraftInfo");

        Assert.AreEqual(5, CraftModel.FromInfos(infos).RecipeCount);
    }
}
