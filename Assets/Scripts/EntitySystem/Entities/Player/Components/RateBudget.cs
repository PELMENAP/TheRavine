using Unity.Mathematics;

namespace TheRavine.EntityControl
{
    public struct RateBudget
    {
        private double readyTime;

        public bool TryConsume(double now, float rate, float burst)
        {
            if (!(rate > 0f) || !(burst > 0f)) return false;

            double ready = math.max(readyTime, now);
            if (ready - now > burst / rate) return false;

            readyTime = ready + 1.0 / rate;
            return true;
        }
    }
}
