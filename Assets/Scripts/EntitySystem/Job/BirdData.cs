using Unity.Mathematics;

namespace TheRavine.EntityControl
{
    public static class BirdState
    {
        public const byte Flying = 0, Approach = 1, Landing = 2, Perched = 3, TakeOff = 4;
        public const int PerchGround = -1, PerchNone = -2;
        public const int SlotShift = 6, SlotMask = 63;
        public const float PanicNone = -1f, PanicSpread = -2f;

        public static bool IsAirborne(byte s) => s == Flying || s == Approach || s == TakeOff;
    }

    public struct BirdParams
    {
        public float MaxSpeed, MinAirSpeed, InvSlowRadius, TargetWeight;
        public float Avoidance, Along, VerticalFlockScale;
        public float3 Weights;
        public float CruiseAGL, Lookahead, Kp, Kd, MinClearance;
        public float TreeMargin, TreeAvoidWeight;
        public float GroundPerchChance, MaxPerchSlope, WaterThreshold, PerchSearchRadius;
        public float GroundPerchSpread, GroundPerchOffset;
        public int GroundPerchTries;
        public float PerchMinHeight, PerchTopMargin;
        public float PerchRadiusBase, PerchRadiusTop, PerchRadiusInner;
        public int SlotsTheta, SlotsHeight;
        public float InvApproachSlow, InvApproachFade, ApproachTimeout;
        public float LandingStart, LandingApproachHeight, LandingSpeed, LandingFinalSpeed, LandingDescent, LandingWindBias;
        public float2 LandingDuration, PerchDuration, HopInterval;
        public float HopDuration, HopHeight, HopDistance, SwayAmplitude;
        public float ScareRadiusSq, PanicRadiusSq, PanicDelay;
        public float TakeOffUp, TakeOffOut, TakeOffDuration;
        public float WindDrift, FlapMinScale, FlipDeadzone;
        public int MaxNeighbors;

        public float TargetJitterRadius, TargetJitterRate;
        public float WanderStrength, WanderRate;
        public float TargetHeadingGain;

        public static BirdParams From(BoidsInfo b, SimulationRules r) => new()
        {
            MaxSpeed = b.velocityLimit,
            MinAirSpeed = b.minAirSpeed,
            InvSlowRadius = 1f / b.slowRadius,
            TargetWeight = b.targetWeight,
            Avoidance = b.avoidanceThreshold,
            Along = b.alongThreshold,
            VerticalFlockScale = b.verticalFlockScale,
            Weights = b.accelerationWeights,
            CruiseAGL = b.cruiseAGL,
            Lookahead = b.heightLookahead,
            Kp = b.heightKp,
            Kd = b.heightKd,
            MinClearance = b.minClearance,
            TreeMargin = b.treeMargin,
            TreeAvoidWeight = b.treeAvoidWeight,
            GroundPerchChance = b.groundPerchChance,
            MaxPerchSlope = b.maxPerchSlope,
            WaterThreshold = b.waterThreshold,
            PerchSearchRadius = b.perchSearchRadius,
            GroundPerchSpread = b.groundPerchSpread,
            GroundPerchOffset = b.groundPerchOffset,
            GroundPerchTries = b.groundPerchTries,
            PerchMinHeight   = b.perchMinHeight,
            PerchTopMargin   = b.perchTopMargin,
            PerchRadiusBase  = b.perchRadiusBase,
            PerchRadiusTop   = b.perchRadiusTop,
            PerchRadiusInner = b.perchRadiusInner,
            SlotsTheta = b.perchSlotsTheta,
            SlotsHeight = b.perchSlotsHeight,
            InvApproachSlow = 1f / b.approachSlowRadius,
            InvApproachFade = 1f / b.approachFlockFade,
            ApproachTimeout = b.approachTimeout,
            LandingStart = b.landingStartDistance,
            LandingApproachHeight = b.landingApproachHeight,
            LandingSpeed = b.landingSpeed,
            LandingFinalSpeed = b.landingFinalSpeed,
            LandingDescent = b.landingDescent,
            LandingWindBias = b.landingWindBias,
            LandingDuration = b.landingDuration,
            PerchDuration = b.perchDuration,
            HopInterval = b.hopInterval,
            HopDuration = b.hopDuration,
            HopHeight = b.hopHeight,
            HopDistance = b.hopDistance,
            SwayAmplitude = b.swayAmplitude,
            ScareRadiusSq = b.scareRadius * b.scareRadius,
            PanicRadiusSq = b.panicRadius * b.panicRadius,
            PanicDelay = b.panicDelay,
            TakeOffUp = b.takeOffUp,
            TakeOffOut = b.takeOffOut,
            TakeOffDuration = b.takeOffDuration,
            WindDrift = b.windDrift,
            FlapMinScale = b.flapMinScale,
            FlipDeadzone = b.flipDeadzone,
            MaxNeighbors = math.max(r.BirdMaxNeighbors, 1),

            TargetJitterRadius = b.targetJitterRadius,
            TargetJitterRate = b.targetJitterRate,
            WanderStrength = b.wanderStrength,
            WanderRate = b.wanderRate,
            TargetHeadingGain = b.targetHeadingGain,
        };
    }
}
