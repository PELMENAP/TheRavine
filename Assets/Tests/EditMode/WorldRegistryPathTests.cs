using System.IO;
using NUnit.Framework;
using TheRavine.Base;
using UnityEngine;

public class WorldRegistryPathTests
{
    private static string Root => Path.GetFullPath(Path.Combine(Application.persistentDataPath, "worlds"));

    [Test]
    public void ValidNameResolvesDirectlyUnderTheWorldsRoot()
    {
        Assert.IsTrue(WorldRegistry.TryGetSafeWorldDirectory("world one", out var path));
        Assert.AreEqual(Path.Combine(Root, "world one"), path);
    }

    [TestCase("..")]
    [TestCase(".")]
    [TestCase("../outside")]
    [TestCase("..\\outside")]
    [TestCase("a/b")]
    [TestCase("a\\b")]
    [TestCase("/etc")]
    [TestCase("\\\\server\\share")]
    [TestCase("C:\\Windows")]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase(" world")]
    [TestCase("world ")]
    [TestCase(null)]
    public void NamesThatCouldEscapeTheRootAreRefused(string name)
    {
        Assert.IsFalse(WorldRegistry.TryGetSafeWorldDirectory(name, out var path), name);
        Assert.IsNull(path);
    }

    [Test]
    public void OverlongAndControlCharacterNamesAreRefused()
    {
        Assert.IsFalse(WorldRegistry.TryGetSafeWorldDirectory(new string('a', 5000), out _));
        Assert.IsFalse(WorldRegistry.TryGetSafeWorldDirectory("bad\0name", out _));
    }
}
