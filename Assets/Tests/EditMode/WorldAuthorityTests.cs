using NUnit.Framework;
using TheRavine.Base;

public class WorldAuthorityTests
{
    [Test]
    public void NoNetworkSessionHasNoRemotePeers()
    {
        Assert.IsFalse(WorldAuthority.HasRemotePeers);
        Assert.IsTrue(WorldAuthority.IsAuthority);
    }
}
