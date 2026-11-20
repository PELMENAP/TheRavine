using NUnit.Framework;
using Unity.Mathematics;

public class PredispositionTests
{
    [SetUp]
    public void SetUp() => SimulationRules.Bind(null);

    private static GeneticParameters Parent(float a, float b, float c)
    {
        var p = GeneticParameters.Default;
        p.MutationChance = 0f;
        p.Predisposition0 = a;
        p.Predisposition1 = b;
        p.Predisposition2 = c;
        return p;
    }

    [Test]
    public void FounderDrawsLieInRangeAndDiffer()
    {
        float first = GeneticParameters.Default.Predisposition0;
        bool differs = false;
        for (int i = 0; i < 50; i++)
        {
            var p = GeneticParameters.Default;
            Assert.That(p.Predisposition0, Is.InRange(-1f, 1f));
            Assert.That(p.Predisposition1, Is.InRange(-1f, 1f));
            Assert.That(p.Predisposition2, Is.InRange(-1f, 1f));
            differs |= p.Predisposition0 != first;
        }
        Assert.IsTrue(differs);
    }

    [Test]
    public void ChildBlendsParentsWithoutMutation()
    {
        var a = Parent(1f, -0.5f, 0.25f);
        var b = Parent(-1f, 0.5f, 0.75f);
        GeneticParameters.Crossover(in a, in b, out var child);
        Assert.AreEqual(0f, child.Predisposition0, 1e-6f);
        Assert.AreEqual(0f, child.Predisposition1, 1e-6f);
        Assert.AreEqual(0.5f, child.Predisposition2, 1e-6f);
    }

    [Test]
    public void MutationMovesTheVectorInsideItsRange()
    {
        var a = Parent(0f, 0f, 0f);
        a.MutationChance = 1f;
        bool moved = false;
        for (int i = 0; i < 20; i++)
        {
            var m = a.GetMutatedGeneticParameters();
            Assert.That(m.Predisposition0, Is.InRange(-1f, 1f));
            Assert.That(m.Predisposition2, Is.InRange(-1f, 1f));
            moved |= m.Predisposition0 != 0f || m.Predisposition1 != 0f || m.Predisposition2 != 0f;
        }
        Assert.IsTrue(moved);
    }

    [Test]
    public void PredispositionIsPhenotypicAndReadsIntoTheInputArray()
    {
        Assert.IsTrue(GeneticParameters.IsPhenotypic(GeneticParameters.IdxPredisposition0));

        var own = Parent(0.1f, 0.2f, 0.3f);
        var other = Parent(-0.9f, -0.8f, -0.7f);
        GeneticParameters.CopyLearningGenes(in other, ref own);
        Assert.AreEqual(0.1f, own.Predisposition0);

        var dst = new float[SimulationRules.PredispositionSize];
        GeneticParameters.ReadPredisposition(in own, dst);
        Assert.AreEqual(0.1f, dst[0]);
        Assert.AreEqual(0.2f, dst[1]);
        Assert.AreEqual(0.3f, dst[2]);
    }

    [Test]
    public void VectorizerAppendsThePredispositionForEveryFrame()
    {
        var vectorizer = new InputVectorizer(100f, 100f);
        var times = new double[ActionCatalog.Count];
        var frame = new VectorizerFrame
        {
            Health = 50f, Energy = 50f,
            SpeechBag = new float[SimulationRules.SpeechVocab],
            PlanRewardEma = new float[PlanCatalog.Count],
            Predisposition = new[] { 0.25f, -0.5f, 0.75f },
        };

        var v = vectorizer.Vectorize(in frame, times, in TerrainSample.Invalid);
        Assert.AreEqual(InputVectorizer.PredispositionOffset + SimulationRules.PredispositionSize, v.Length);
        Assert.AreEqual(0.25f, v[InputVectorizer.PredispositionOffset]);
        Assert.AreEqual(-0.5f, v[InputVectorizer.PredispositionOffset + 1]);
        Assert.AreEqual(0.75f, v[InputVectorizer.PredispositionOffset + 2]);

        frame.Predisposition = null;
        v = vectorizer.Vectorize(in frame, times, in TerrainSample.Invalid);
        Assert.AreEqual(0f, v[InputVectorizer.PredispositionOffset]);
    }
}
