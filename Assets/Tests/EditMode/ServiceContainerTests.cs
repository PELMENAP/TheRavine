using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class ServiceContainerTests
{
    private sealed class Probe { }

    [Test]
    public void DuplicateRegistrationIsRejected()
    {
        var container = new ServiceContainer();
        Assert.IsTrue(container.Register(new Probe()));
        Assert.IsFalse(container.Register(new Probe()));
        Assert.IsFalse(container.Register<Probe>(null));
    }

    [Test]
    public void UnregisterRemovesOnlyOwnInstance()
    {
        var container = new ServiceContainer();
        var owner     = new Probe();
        Assert.IsTrue(container.Register(owner));

        Assert.IsFalse(container.Unregister(new Probe()));
        Assert.IsTrue(container.TryGet(out Probe current));
        Assert.AreSame(owner, current);

        Assert.IsTrue(container.Unregister(owner));
        Assert.IsFalse(container.TryGet(out Probe _));
    }

    [Test]
    public void SceneReentryRegistersWithoutRefusal()
    {
        var container = new ServiceContainer();
        for (int scene = 0; scene < 3; scene++)
        {
            var service = new Probe();
            Assert.IsTrue(container.Register(service), $"вход {scene}");
            Assert.IsTrue(container.Unregister(service), $"выход {scene}");
        }
    }

    [Test]
    public void LocatorLogsRefusal()
    {
        var first = new Probe();
        Assert.IsTrue(ServiceLocator.Register(first));
        try
        {
            LogAssert.Expect(LogType.Error, new Regex("отказ регистрации Probe"));
            Assert.IsFalse(ServiceLocator.Register(new Probe()));
        }
        finally
        {
            ServiceLocator.Services.Unregister(first);
        }
    }
}
