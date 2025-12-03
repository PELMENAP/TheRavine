using UnityEngine;

namespace TheRavine.Base
{
    [System.Serializable]
    public struct WorldState
    {
        public int seed;
        public Vec3 playerPosition;
        public int cycleCount;
        public float startTime;
        public bool gameWon;
        public long lastSaveTime;

        public WorldState Clone()
        {
            return new WorldState
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

            public Vector3 ToVector3() => new(x, y, z);

            public static implicit operator Vector3(Vec3 vec3) => vec3.ToVector3();
            public static implicit operator Vec3(Vector3 vector3) => new(vector3);
        }
        
        public readonly bool IsDefault() => 
            seed == 0 && 
            cycleCount == 0 && 
            startTime == 0f && 
            !gameWon && 
            lastSaveTime == 0L &&
            playerPosition.x == 0f && 
            playerPosition.y == 0f && 
            playerPosition.z == 0f;
    }
}