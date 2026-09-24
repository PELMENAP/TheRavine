using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace TheRavine.EntityControl.Virology
{
    public struct EffectModifiers
    {
        public float HealthDelta;
        public float EnergyDelta;
        public float RegenMultiplier;
        public float MetabolismMultiplier;
        public float WanderBias;
        public float ForageBias;
        public float HuntBias;
        public float SocialBias;
        public float FleeBias;
        public float SharpnessDelta;
        public float MutationRateDelta;
        public float ReinforceAmount;
        public bool SpreadRequested;
        public bool ReplicateRequested;
        public bool ExciseRequested;
        public bool Dormant;
        public float SpreadAmp;
        public int RegenOwner;
        public int MetabolismOwner;
        public float Fever;
        public float Blind;
        public float Frenzy;
        public float Lethargy;
        public float CarryPoi;
        public float ImmunizeAmount;
        public bool ForceSpeechRequested;
        public bool SpreadViaSpeech;
        public bool CaptureRequested;

        public static EffectModifiers Neutral => new()
        {
            RegenMultiplier = 1f,
            MetabolismMultiplier = 1f,
            RegenOwner = -1,
            MetabolismOwner = -1
        };

        public void ResetTransient()
        {
            HealthDelta = 0f;
            EnergyDelta = 0f;
            ReinforceAmount = 0f;
            SpreadRequested = false;
            ReplicateRequested = false;
            ExciseRequested = false;
            SpreadAmp = 0f;
            SpreadViaSpeech = false;
            ForceSpeechRequested = false;
            CaptureRequested = false;
            ImmunizeAmount = 0f;
        }
    }

    public struct HostState
    {
        public float HpFraction;
        public bool  Night;
        public int   Crowd;
        public float WeakThreshold;
        public int   CrowdThreshold;
    }

    public struct TranslationResult
    {
        public int Action;
        public float Amp;
        public float SpentEnergy;
        public bool Executed;
    }

    public static class Ribosome
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TranslationResult Step(
            ref Tape tape,
            ref EffectModifiers modifiers,
            ref bool primed,
            int ownerSegment,
            int action,
            float d2,
            ProteinDescriptor[] descriptors,
            float sharpness,
            float availableEnergy,
            int maxTapeLength,
            in HostState host)
        {
            var result = default(TranslationResult);
            if (tape.Length <= 0) return result;

            float amp = math.exp(-d2 * sharpness);
            var descriptor = descriptors[action];

            result.Action = action;
            result.Amp = amp;

            bool gated = descriptor.RequiresPrime && !primed;
            float cost = ProteinTable.ResolveEnergyCost(action, descriptor.EnergyCost, amp);
            bool affordable = availableEnergy >= cost;

            if (!gated && affordable)
            {
                result.Executed = true;
                result.SpentEnergy = cost;
                Apply(ref modifiers, action, descriptor.BaseMagnitude * amp, amp, ownerSegment);
                primed = action == (int)ProteinAction.Prime;
            }
            else primed = false;

            tape.Advance(maxTapeLength, primed);
            if (result.Executed && ShouldSkip(action, in host)) tape.Advance(maxTapeLength, false);
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ShouldSkip(int action, in HostState host)
            => (ProteinAction)action switch
            {
                ProteinAction.SkipIfHostWeak => host.HpFraction < host.WeakThreshold,
                ProteinAction.SkipIfNight    => host.Night,
                ProteinAction.SkipIfCrowded  => host.Crowd >= host.CrowdThreshold,
                _ => false
            };

        private static void Apply(ref EffectModifiers m, int action, float magnitude, float amp, int owner)
        {
            switch ((ProteinAction)action)
            {
                case ProteinAction.AddHealth: m.HealthDelta += magnitude; break;
                case ProteinAction.DrainHealth: m.HealthDelta -= magnitude; break;
                case ProteinAction.AddEnergy:
                    m.RegenMultiplier = math.min(m.RegenMultiplier + magnitude, 4f);
                    m.RegenOwner = owner; break;
                case ProteinAction.DrainEnergy: m.EnergyDelta -= magnitude; break;
                case ProteinAction.RegenBoost:
                    m.RegenMultiplier = math.min(m.RegenMultiplier + magnitude, 4f);
                    m.RegenOwner = owner; break;
                case ProteinAction.MetabolismUp:
                    m.MetabolismMultiplier = math.min(m.MetabolismMultiplier + magnitude, 4f);
                    m.MetabolismOwner = owner; break;
                case ProteinAction.MetabolismDown:
                    m.MetabolismMultiplier = math.max(m.MetabolismMultiplier - magnitude, 0.1f);
                    m.MetabolismOwner = owner; break;
                case ProteinAction.ModifyWander: m.WanderBias = math.clamp(m.WanderBias + magnitude, -2f, 2f); break;
                case ProteinAction.ModifyForage: m.ForageBias = math.clamp(m.ForageBias + magnitude, -2f, 2f); break;
                case ProteinAction.ModifyHunt: m.HuntBias = math.clamp(m.HuntBias + magnitude, -2f, 2f); break;
                case ProteinAction.ModifySocial: m.SocialBias = math.clamp(m.SocialBias + magnitude, -2f, 2f); break;
                case ProteinAction.ModifyFlee: m.FleeBias = math.clamp(m.FleeBias + magnitude, -2f, 2f); break;
                case ProteinAction.SharpnessUp: m.SharpnessDelta = math.clamp(m.SharpnessDelta + magnitude, -0.5f, 0.5f); break;
                case ProteinAction.SharpnessDown: m.SharpnessDelta = math.clamp(m.SharpnessDelta - magnitude, -0.5f, 0.5f); break;
                case ProteinAction.Replicate: m.ReplicateRequested = true; break;
                case ProteinAction.SpreadToOther: m.SpreadRequested = true; m.SpreadAmp = amp; break;
                case ProteinAction.Reinforce: m.ReinforceAmount += magnitude; break;
                case ProteinAction.Excise: m.ExciseRequested = true; break;
                case ProteinAction.Dormant: m.Dormant = true; break;
                case ProteinAction.MutationRateUp:
                    m.MutationRateDelta = math.clamp(m.MutationRateDelta + magnitude, -0.5f, 0.5f); break;
                case ProteinAction.MutationRateDown:
                    m.MutationRateDelta = math.clamp(m.MutationRateDelta - magnitude, -0.5f, 0.5f); break;
                case ProteinAction.ForceSpeech:
                    m.ForceSpeechRequested = true;
                    m.SpreadRequested = true;
                    m.SpreadViaSpeech = true;
                    m.SpreadAmp = amp; break;
                case ProteinAction.Immunize: m.ImmunizeAmount += magnitude; break;
                case ProteinAction.Fever:    m.Fever    = math.min(m.Fever + magnitude, 1f); break;
                case ProteinAction.Capture:  m.CaptureRequested = true; break;
                case ProteinAction.CarryPoi: m.CarryPoi = math.min(m.CarryPoi + magnitude, 1f); break;
                case ProteinAction.Blind:    m.Blind    = math.min(m.Blind + magnitude, 1f); break;
                case ProteinAction.Frenzy:   m.Frenzy   = math.min(m.Frenzy + magnitude, 1f); break;
                case ProteinAction.Lethargy: m.Lethargy = math.min(m.Lethargy + magnitude, 1f); break;
            }
        }

        public static void Relax(ref EffectModifiers m, float k)
        {
            m.RegenMultiplier      = 1f + (m.RegenMultiplier - 1f) * k;
            m.MetabolismMultiplier = 1f + (m.MetabolismMultiplier - 1f) * k;
            m.WanderBias        *= k;
            m.ForageBias        *= k;
            m.HuntBias          *= k;
            m.SocialBias        *= k;
            m.FleeBias          *= k;
            m.MutationRateDelta *= k;
            m.Fever    *= k;
            m.Blind    *= k;
            m.Frenzy   *= k;
            m.Lethargy *= k;
            m.CarryPoi *= k;
            m.Dormant = false;
        }
    }
}