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
            if (!active)
                return;

            Vector3 pos = rb.position;
            float terrainY = map.SampleHeightBilinear(pos.x, pos.z) - 1;
            float feetY = pos.y - halfHeight;
            float penetration = terrainY - feetY;

            if (penetration >= 0f)
            {
                pos.y += penetration;
                rb.position = pos;
                Vector3 velocity = rb.linearVelocity;
                if (velocity.y < 0f)
                    velocity.y = 0f;
                rb.linearVelocity = velocity;
                rb.useGravity = false;
                IsGrounded = true;
            }
            else
            {
                rb.useGravity = true;
                IsGrounded = penetration > -GroundedDistance;
            }
        }
    }
}