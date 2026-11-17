using NUnit.Framework;

public class FoodLookupAllocationTests
{
    [Test]
    public void SecondFoodLookupOnEmptyIndexAllocatesNothing()
    {
        var index = new ChunkFoodIndex(null, null);
        long cell = 0L;
        float distance = 0f;
        bool found = false;
        AllocationAssert.Zero(() => found = index.TryFindNearestFood(10f, 10f, 2f, out cell, out distance, 1), 256,
            "ChunkFoodIndex.TryFindNearestFood");
        Assert.IsFalse(found);
    }
}
