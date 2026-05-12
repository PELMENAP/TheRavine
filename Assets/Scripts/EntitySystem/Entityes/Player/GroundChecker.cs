using UnityEngine;

namespace TheRavine.EntityControl
{
    public class GroundChecker : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float minGroundNormalY = 0.7f;

        public bool IsGrounded { get; private set; }

        private int groundContactCount;

        private void OnCollisionEnter(Collision col) => EvaluateContacts(col, +1);
        private void OnCollisionExit(Collision col)  => EvaluateContacts(col, -1);
        private void OnCollisionStay(Collision col)  => EvaluateContacts(col,  0);

        private void EvaluateContacts(Collision col, int delta)
        {
            for (int i = 0; i < col.contactCount; i++)
            {
                if (col.GetContact(i).normal.y >= minGroundNormalY)
                {
                    groundContactCount = Mathf.Max(0, groundContactCount + delta);
                    IsGrounded = groundContactCount > 0;
                    return;
                }
            }
        }

        public void ExitGround() => groundContactCount--;
    }
}