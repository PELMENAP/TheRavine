using Unity.Netcode;

namespace TheRavine.Base
{
    public static class WorldAuthority
    {
        public static bool IsAuthority
        {
            get
            {
                NetworkManager network = NetworkManager.Singleton;
                return network == null || !network.IsListening || network.IsServer;
            }
        }
    }
}
