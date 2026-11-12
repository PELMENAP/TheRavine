using NUnit.Framework;
using UnityEngine;
using TheRavine.ObjectControl;

public class PoolScaleTests
{
    private GameObject host;
    private GameObject prefab;
    private PoolManager manager;

    [SetUp]
    public void SetUp()
    {
        host = new GameObject("host");
        prefab = new GameObject("prefab");
        prefab.transform.localScale = new Vector3(2f, 3f, 4f);
        manager = new PoolManager(host.transform);
    }

    [TearDown]
    public void TearDown()
    {
        manager.Dispose();
        Object.DestroyImmediate(host);
        Object.DestroyImmediate(prefab);
    }

    [Test]
    public void ReusedObjectComesBackAtItsBaseScale()
    {
        PoolManager.Pool pool = manager.Register(1, prefab);
        PooledObject first = pool.Get(Vector3.zero);
        first.Transform.localScale = new Vector3(0.1f, 9f, 0.1f);
        pool.Release(first);

        PooledObject second = pool.Get(Vector3.one);

        Assert.AreSame(first, second);
        Assert.AreEqual(new Vector3(2f, 3f, 4f), second.Transform.localScale);
        Assert.AreEqual(new Vector3(2f, 3f, 4f), second.BaseScale);
    }

    [Test]
    public void FirstActiveFollowsGetAndRelease()
    {
        PoolManager.Pool pool = manager.Register(1, prefab);
        Assert.IsFalse(pool.TryGetFirstActive(out _));

        PooledObject a = pool.Get(Vector3.zero);
        PooledObject b = pool.Get(Vector3.one);
        Assert.IsTrue(pool.TryGetFirstActive(out PooledObject first));
        Assert.AreSame(a, first);

        pool.Release(a);
        Assert.IsTrue(pool.TryGetFirstActive(out first));
        Assert.AreSame(b, first);

        pool.Release(b);
        Assert.IsFalse(pool.TryGetFirstActive(out _));
        Assert.AreEqual(0, pool.ActiveCount);
    }

    [Test]
    public void DoubleReleaseIsIgnored()
    {
        PoolManager.Pool pool = manager.Register(1, prefab);
        PooledObject a = pool.Get(Vector3.zero);
        PooledObject b = pool.Get(Vector3.one);

        pool.Release(a);
        pool.Release(a);

        Assert.AreEqual(1, pool.ActiveCount);
        Assert.IsTrue(pool.TryGetFirstActive(out PooledObject first));
        Assert.AreSame(b, first);
    }
}
