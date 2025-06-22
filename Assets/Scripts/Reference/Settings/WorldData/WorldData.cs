using UnityEngine;

namespace TheRavine.Base
{
    [System.Serializable]
    public struct WorldData
    {
        public int seed;
        public Vec3 playerPosition;
        public int cycleCount;
        public float startTime;
        public bool gameWon;
        public long lastSaveTime;

        public WorldData Clone()
        {
            return new WorldData
            {
                seed = this.seed,
                playerPosition = this.playerPosition,
                cycleCount = this.cycleCount,
                startTime = this.startTime,
                gameWon = this.gameWon,
                lastSaveTime = this.lastSaveTime
            };
        }

        [System.Serializable]
        public struct Vec3
        {
            public float x, y, z;
            
            public Vec3(Vector3 position)
            {
                x = position.x;
                y = position.y;
                z = position.z;
            }
            
            public Vector3 ToVector3() => new Vector3(x, y, z);
            
            public static implicit operator Vector3(Vec3 vec3) => vec3.ToVector3();
            public static implicit operator Vec3(Vector3 vector3) => new Vec3(vector3);
        }
    }
}