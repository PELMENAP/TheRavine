using UnityEngine;

using TheRavine.Security;

namespace TheRavine.Base
{
    // using System.Threading;
    // private CancellationTokenSource _cts;
    // _cts    = new CancellationTokenSource();
    // !_cts.Token.IsCancellationRequested
    // _cts?.Cancel();
    // _cts?.Dispose();
    
    
    public class DataStorage
    {
        public static int cycleCount;
        public static float startTime;
        public static bool winTheGame;
        public static bool loadkey = false;
        public static bool normkey = false;

        [SerializeField] public Data data;
        public string worldName = "new game"; 
        private void Awake()
        {
            if (loadkey)
            {
                // data = SaveLoad.LoadEncryptedData<Data>(worldName); // saveload class принимает Serializable
            }
        }

        public void Save()
        {
            SaveLoad.SaveEncryptedData<Data>(worldName, data);
        }

        [System.Serializable]
        public struct Data
        {
            public int seed;
            public Vec3 PlPos;

            [System.Serializable]
            public struct Vec3
            {
                public float x, y, z;
                public Vec3(Vector3 position)
                {
                    this.x = position.x;
                    this.y = position.y;
                    this.z = position.z;
                }
            }
        }
    }
}