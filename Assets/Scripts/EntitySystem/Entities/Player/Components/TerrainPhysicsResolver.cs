using Unity.Mathematics;
using UnityEngine;

using TheRavine.Generator;

namespace TheRavine.EntityControl
{
    public sealed class TerrainPhysicsResolver
    {
        private const float GroundedDistance = 0.15f;
        private const float RisingVelocity   = 0.1f;
        private const float GroundOffset     = 1f;

        private readonly MapGenerator map;
        private readonly Rigidbody    rb;
        private readonly float        halfHeight;
        private readonly float        maxStepHeight;
        private readonly float        bodyRadius;
        private readonly float        sweepReachSq;
        private float2                obstacleNormal;
        private float2                previous;
        private bool                  hasPrevious;
        private bool                  obstructed;
        private bool                  active;
        private float                 lastValidX;
        private float                 lastValidZ;

        public bool IsGrounded { get; private set; }

        public TerrainPhysicsResolver(MapGenerator _map, Rigidbody _rb, float _halfHeight, float _maxStepHeight, float _bodyRadius)
        {
            map           = _map;
            rb            = _rb;
            halfHeight    = _halfHeight;
            maxStepHeight = _maxStepHeight;
            bodyRadius    = _bodyRadius;
            sweepReachSq  = ObstacleQuery.SweepReach(_bodyRadius) * ObstacleQuery.SweepReach(_bodyRadius);
        }

        public void ClipVelocity(ref float x, ref float z)
        {
            if (!obstructed) return;
            float into = x * obstacleNormal.x + z * obstacleNormal.y;
            if (into >= 0f) return;
            x -= into * obstacleNormal.x;
            z -= into * obstacleNormal.y;
        }

        private void ResolveObstacles()
        {
            Vector3 pos = rb.position;
            float2 xz = new float2(pos.x, pos.z);
            float feet = pos.y - halfHeight + GroundOffset;
            float2 origin = hasPrevious && math.distancesq(previous, xz) <= sweepReachSq ? previous : xz;
            obstructed = map.ResolveObstacles(origin, ref xz, bodyRadius, feet, feet + 2f * halfHeight, maxStepHeight, out obstacleNormal);
            if (!obstructed) return;

            pos.x = xz.x;
            pos.z = xz.y;
            rb.position = pos;

            Vector3 velocity = rb.linearVelocity;
            float vx = velocity.x;
            float vz = velocity.z;
            ClipVelocity(ref vx, ref vz);
            velocity.x = vx;
            velocity.z = vz;
            rb.linearVelocity = velocity;
        }

        public void SetActive(bool value)
        {
            active = value;
            hasPrevious = false;
        }

        public void ConsumeGround() => IsGrounded = false;

        public void Resolve()
        {
            if (!active)
            {
                obstructed = false;
                hasPrevious = false;
                return;
            }

            ResolveObstacles();
            ResolveGround();

            Vector3 resolved = rb.position;
            previous = new float2(resolved.x, resolved.z);
            hasPrevious = true;
        }

        private void ResolveGround()
        {
            Vector3 pos = rb.position;
            float surfaceFeet = pos.y - halfHeight + GroundOffset;
            float ground = map.SampleHeightBilinear(pos.x, pos.z);
            if (map.SupportObstacles(new float2(pos.x, pos.z), bodyRadius, surfaceFeet, maxStepHeight, out float top) && top > ground)
                ground = top;

            float groundY = ground - GroundOffset;
            float feetY = pos.y - halfHeight;
            float penetration = groundY - feetY;

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