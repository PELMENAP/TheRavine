using UnityEngine;

using TheRavine.Generator;

namespace TheRavine.EntityControl
{
    public sealed class TerrainPhysicsResolver
    {
        private const float GroundedDistance = 0.15f;
        private const float RisingVelocity   = 0.1f;

        private readonly MapGenerator map;
        private readonly Rigidbody    rb;
        private readonly float        halfHeight;
        private readonly float        maxStepHeight;
        private bool                  active;
        private float                 lastValidX;
        private float                 lastValidZ;

        public bool IsGrounded { get; private set; }

        public TerrainPhysicsResolver(MapGenerator _map, Rigidbody _rb, float _halfHeight, float _maxStepHeight)
        {
            map           = _map;
            rb            = _rb;
            halfHeight    = _halfHeight;
            maxStepHeight = _maxStepHeight;
        }

        public void SetActive(bool value) => active = value;

        public void ConsumeGround() => IsGrounded = false;

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
                Vector3 velocity = rb.linearVelocity;

                if (IsGrounded && penetration > maxStepHeight)
                {
                    pos.x = lastValidX;
                    pos.z = lastValidZ;
                    rb.position = pos;
                    velocity.x = 0f;
                    velocity.z = 0f;
                    rb.linearVelocity = velocity;
                    return;
                }

                pos.y += penetration;
                rb.position = pos;
                if (velocity.y < 0f)
                    velocity.y = 0f;
                rb.linearVelocity = velocity;

                bool rising = velocity.y > RisingVelocity;
                rb.useGravity = rising;
                IsGrounded = !rising;
                if (IsGrounded)
                {
                    lastValidX = pos.x;
                    lastValidZ = pos.z;
                }
            }
            else
            {
                rb.useGravity = true;
                IsGrounded = penetration > -GroundedDistance && rb.linearVelocity.y <= RisingVelocity;
                if (IsGrounded)
                {
                    lastValidX = pos.x;
                    lastValidZ = pos.z;
                }
            }
        }
    }
}