using UnityEngine;

namespace TheRavine.EntityControl
{
    public class DoubleTapDetector
    {
        public bool IsBoostActive { get; private set; }

        private readonly float doubleTapWindow;

        private bool lastPressed;
        private float lastTapTime;

        public DoubleTapDetector(float doubleTapWindow = 0.2f)
        {
            this.doubleTapWindow = doubleTapWindow;

            lastTapTime = -999f;
            lastPressed = false;
            IsBoostActive = false;
        }

        public void Update(bool pressed)
        {

            if (!lastPressed && pressed)
            {
                if (Time.time - lastTapTime <= doubleTapWindow)
                {
                    IsBoostActive = true;
                }

                lastTapTime = Time.time;
            }

            if (IsBoostActive && !pressed)
            {
                IsBoostActive = false;
            }

            lastPressed = pressed;
        }
    }

}