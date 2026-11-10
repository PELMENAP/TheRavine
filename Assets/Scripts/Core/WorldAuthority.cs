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

        public static bool HasRemotePeers
        {
            get
            {
                NetworkManager network = NetworkManager.Singleton;
                if (network == null || !network.IsListening)
                    return false;

                return !network.IsServer || network.ConnectedClientsIds.Count > 1;
            }
        }
    }
}
