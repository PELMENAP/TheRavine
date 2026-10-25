using NUnit.Framework;
using Unity.Mathematics;

public class ColonyRegistryTests
{
    [Test]
    public void ColoniesHaveIndependentNestsAndIds()
    {
        var registry = new ColonyRegistry();
        try
        {
            var a = registry.Add(new float2(0f, 0f), null, 8);
            var b = registry.Add(new float2(500f, 0f), null, 8);
            var w = registry.Add(new float2(0f, 500f), null, 8, true);

            Assert.AreNotSame(a.Nest, b.Nest);
            Assert.AreNotEqual(a.ColonyId, b.ColonyId);
            Assert.AreEqual(2, registry.CivilCount);
            Assert.AreEqual(0, a.Index);
            Assert.AreEqual(2, w.Index);

            Assert.IsTrue(registry.TryGetById(b.ColonyId, out var found));
            Assert.AreSame(b, found);
            Assert.IsFalse(registry.TryGetById(0, out _));
            Assert.AreSame(a, registry.Nearest(new float2(10f, 0f)));
            Assert.AreSame(b, registry.Nearest(new float2(400f, 0f)));
            Assert.GreaterOrEqual(registry.Views.Length, 3);
        }
        finally
        {
            registry.Dispose();
        }
    }

    [Test]
    public void ColonyModelFileNamesCarryColonyId()
    {
        string name = NeuralModelStorage.ColonyFileName("shared_brain", 3);
        Assert.AreEqual("shared_brain_c3", name);
        Assert.IsTrue(NeuralModelStorage.TryParseColonyId(name, out int id));
        Assert.AreEqual(3, id);
        Assert.IsFalse(NeuralModelStorage.TryParseColonyId("shared_brain", out id));
        Assert.AreEqual(-1, id);
        Assert.IsFalse(NeuralModelStorage.TryParseColonyId("brain_cx", out _));
    }
}
