using System.Collections.Generic;
using UnityEngine;

namespace TheRavine.EntityControl
{
    public sealed class GroundChecker : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float minGroundNormalY = 0.7f;

        public bool IsGrounded { get; private set; }

        private readonly HashSet<int> _groundContacts = new();

        private void OnCollisionEnter(Collision col) => Evaluate(col);
        private void OnCollisionStay(Collision col)  => Evaluate(col);

        private void OnCollisionExit(Collision col)
        {
            _groundContacts.Remove(col.collider.GetInstanceID());
            Refresh();
        }

        private void Evaluate(Collision col)
        {
            bool valid = false;
            for (int i = 0; i < col.contactCount; i++)
            {
                if (col.GetContact(i).normal.y >= minGroundNormalY)
                {
                    valid = true;
                    break;
                }
            }

            int id = col.collider.GetInstanceID();
            if (valid) _groundContacts.Add(id);
            else       _groundContacts.Remove(id);

            Refresh();
        }

        private void Refresh() => IsGrounded = _groundContacts.Count > 0;

        public void Reset()
        {
            _groundContacts.Clear();
            IsGrounded = false;
        }
    }
}