using UnityEngine;

namespace TheRavine.EntityControl
{
    public class JumpChargeDetector
    {
        public bool JumpRequested { get; private set; }
        public float ChargeNormalized { get; private set; }
        public bool IsCharging => isCharging;

        private readonly float maxChargeTime;
        private bool isCharging;
        private float chargeStartTime;

        public JumpChargeDetector(float maxChargeTime = 1f)
        {
            this.maxChargeTime = maxChargeTime;
        }

        public void StartCharge()
        {
            if (isCharging) return;
            isCharging = true;
            chargeStartTime = Time.time;
            JumpRequested = false;
        }

        public void Release()
        {
            if (!isCharging) return;
            isCharging = false;

            float held = Mathf.Clamp(Time.time - chargeStartTime, 0f, maxChargeTime);
            ChargeNormalized = held / maxChargeTime;
            JumpRequested = true;
        }

        public void ConsumeJump()
        {
            JumpRequested = false;
        }
    }
}