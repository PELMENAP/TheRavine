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

        private static readonly float MaxWalkableGradientSq = math.pow(math.tan(math.radians(MapGenerator.MaxWalkableSlope)), 2f);

        private readonly MapGenerator map;
        private readonly Rigidbody    rb;
        private readonly float        halfHeight;
        private readonly float        maxStepHeight;
        private readonly float        bodyRadius;
        private readonly float        sweepReachSq;
        private float2                obstacleNormal;
        private float2                terrainNormal;
        private Vector3               previous;
        private bool                  hasPrevious;
        private bool                  obstructed;
        private bool                  active;
        private bool                  terrainUnavailable;
        private double                lastSupportedTime = double.NegativeInfinity;

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
            if (terrainUnavailable)
            {
                x = 0f;
                z = 0f;
                return;
            }

            if (obstructed) ClipAgainst(obstacleNormal, ref x, ref z);
            ClipAgainst(terrainNormal, ref x, ref z);
        }

        private static void ClipAgainst(float2 normal, ref float x, ref float z)
        {
            float into = math.dot(new float2(x, z), normal);
            if (into >= 0f) return;
            x -= into * normal.x;
            z -= into * normal.y;
        }

        private void ResolveObstacles()
        {
            Vector3 pos = rb.position;
            float2 xz = new float2(pos.x, pos.z);
            float feet = pos.y - halfHeight + GroundOffset;
            float2 origin = hasPrevious ? new float2(previous.x, previous.z) : xz;
            obstructed = map.ResolveObstacles(origin, ref xz, bodyRadius, feet, feet + 2f * halfHeight, maxStepHeight, out obstacleNormal);
            if (!obstructed) return;

            pos.x = xz.x;
            pos.z = xz.y;
            rb.position = pos;

            Vector3 velocity = rb.linearVelocity;
            float vx = velocity.x;
            float vz = velocity.z;
            ClipAgainst(obstacleNormal, ref vx, ref vz);
            velocity.x = vx;
            velocity.z = vz;
            rb.linearVelocity = velocity;
        }

        public void SetActive(bool value)
        {
            active = value;
            hasPrevious = false;
            IsGrounded = false;
            lastSupportedTime = double.NegativeInfinity;
        }

        public bool TryConsumeJumpSupport(double now, float coyoteTime)
        {
            if (!active || (!IsGrounded && now - lastSupportedTime > coyoteTime)) return false;

            IsGrounded = false;
            lastSupportedTime = double.NegativeInfinity;
            rb.useGravity = true;
            return true;
        }

        public void Resolve(double now)
        {
            obstructed = false;
            terrainUnavailable = false;
            terrainNormal = float2.zero;
            if (!active) return;

            Vector3 position = rb.position;
            float2 displacement = new float2(position.x - previous.x, position.z - previous.z);
            if (hasPrevious && math.lengthsq(displacement) > sweepReachSq)
            {
                hasPrevious = false;
                IsGrounded = false;
                lastSupportedTime = double.NegativeInfinity;
            }

            ResolveObstacles();
            if (!ResolveGround(now)) return;

            previous = rb.position;
            hasPrevious = true;
        }

        private bool TryResolveTerrain(ref Vector3 position, out float height, out float2 gradient)
        {
            float2 destination = new float2(position.x, position.z);
            float2 origin = new float2(previous.x, previous.z);
            if (!hasPrevious || math.all(destination == origin))
                return map.TrySampleTerrainSurface(destination, out height, out gradient);

            if (!map.TrySampleTerrainSurface(origin, out height, out gradient)) return false;

            float2 travel = destination - origin;
            float distance = math.length(travel);
            int steps = (int)math.ceil(distance / (MapGenerator.scale * 0.5f));
            float2 accepted = origin;
            float feetOffset = GroundOffset - halfHeight;
            float previousFeet = previous.y + feetOffset;
            float feetDelta = position.y - previous.y;

            for (int step = 1; step <= steps; step++)
            {
                float fraction = (float)step / steps;
                float2 point = origin + travel * fraction;
                if (!map.TrySampleTerrainSurface(point, out float nextHeight, out float2 nextGradient))
                {
                    terrainNormal = -travel / distance;
                    break;
                }

                float feet = previousFeet + feetDelta * fraction;
                float priorFeet = previousFeet + feetDelta * ((float)(step - 1) / steps);
                bool steep = math.lengthsq(nextGradient) > MaxWalkableGradientSq;
                bool risingTerrain = nextHeight > height || math.dot(nextGradient, point - accepted) > 0f;
                bool tooHigh = IsGrounded ? nextHeight - previousFeet > maxStepHeight : nextHeight > priorFeet;
                if (nextHeight > feet && risingTerrain && (steep || tooHigh))
                {
                    if (steep && !IsGrounded) lastSupportedTime = double.NegativeInfinity;
                    float2 downhill = -math.normalizesafe(nextGradient);
                    terrainNormal = math.dot(downhill, travel) < 0f ? downhill : -travel / distance;
                    break;
                }

                accepted = point;
                height = nextHeight;
                gradient = nextGradient;
            }

            position.x = accepted.x;
            position.z = accepted.y;
            return true;
        }

        private bool ResolveGround(double now)
        {
            Vector3 originalPosition = rb.position;
            Vector3 position = originalPosition;
            Vector3 originalVelocity = rb.linearVelocity;
            Vector3 velocity = originalVelocity;
            if (!TryResolveTerrain(ref position, out float ground, out float2 gradient))
            {
                terrainUnavailable = true;
                if (hasPrevious) rb.position = previous;
                rb.linearVelocity = Vector3.zero;
                rb.useGravity = false;
                IsGrounded = false;
                lastSupportedTime = double.NegativeInfinity;
                return false;
            }

            float feet = position.y - halfHeight + GroundOffset;
            bool walkable = math.lengthsq(gradient) <= MaxWalkableGradientSq;
            if (map.SupportObstacles(new float2(position.x, position.z), bodyRadius, feet, maxStepHeight, out float top) && top > ground)
            {
                ground = top;
                walkable = true;
            }

            float penetration = ground - feet;
            bool rising = velocity.y > RisingVelocity;
            bool contact = penetration >= 0f;
            bool followGround = IsGrounded && penetration >= -GroundedDistance;
            IsGrounded = walkable && !rising && (contact || followGround);

            if (contact || IsGrounded)
            {
                position.y += penetration;
                velocity.y = math.max(0f, velocity.y);
            }

            if (IsGrounded)
                lastSupportedTime = now;
            else if (contact && !walkable)
                lastSupportedTime = double.NegativeInfinity;

            ClipAgainst(terrainNormal, ref velocity.x, ref velocity.z);
            if (!position.Equals(originalPosition)) rb.position = position;
            if (!velocity.Equals(originalVelocity)) rb.linearVelocity = velocity;
            rb.useGravity = !IsGrounded;
            return true;
        }
    }
}
