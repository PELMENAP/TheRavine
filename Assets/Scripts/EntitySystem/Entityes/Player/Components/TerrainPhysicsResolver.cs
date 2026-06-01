using UnityEngine;

using TheRavine.Generator;

namespace TheRavine.EntityControl
{
    public sealed class TerrainPhysicsResolver
    {
        private const float GroundedDistance = 0.15f;

        private readonly MapGenerator map;
        private readonly Rigidbody    rb;
        private readonly float        halfHeight;
        private bool                  active;

        public bool IsGrounded { get; private set; }

        public TerrainPhysicsResolver(MapGenerator _map, Rigidbody _rb, float _halfHeight)
        {
            map        = _map;
            rb         = _rb;
            halfHeight = _halfHeight;
        }

        public void SetActive(bool value) => active = value;

        public void Resolve()
        {
            if (!active) return;

            Vector3 pos      = rb.position;
            float   terrainY = map.SampleHeightBilinear(pos.x, pos.z) - 1;
            float   feetY    = pos.y - halfHeight;
            float   delta    = terrainY - feetY;

            if (delta > 0f)
            {
                pos.y += delta;

                rb.position = pos;

                Vector3 vel = rb.linearVelocity;

                if (vel.y < 0f)
                    vel.y = 0f;

                rb.linearVelocity = vel;

                IsGrounded = true;
            }
            else
            {
                IsGrounded = delta > -GroundedDistance;
            }
        }
    }
}