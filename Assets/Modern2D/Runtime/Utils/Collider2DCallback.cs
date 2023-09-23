using UnityEngine;
using UnityEngine.Events;

namespace Modern2D
{

    //  used for Lighting system collider callbacks
    //  shadow detection is not really optimized, but I didn't want to impose limitations on your project,
    //  in form of taking a Layer for limited collider detection
    //  if you have a free layer for that, and you need speed, then go for it


    public class Collider2DCallback : MonoBehaviour
    {
        [SerializeField][HideInInspector] public UnityEvent<Collision2D> OnCollider2DEnter;
        [SerializeField][HideInInspector] public UnityEvent<Collider2D> OnTrigger2DEnter;

        [SerializeField][HideInInspector] public UnityEvent<Collision2D> OnCollider2DExit;
        [SerializeField][HideInInspector] public UnityEvent<Collider2D> OnTrigger2DExit;

        private void OnTriggerEnter2D(Collider2D collision) { OnTrigger2DEnter.Invoke(collision); }

        private void OnTriggerExit2D(Collider2D collision) => OnTrigger2DExit.Invoke(collision);

        private void OnCollisionEnter2D(Collision2D collision) => OnCollider2DEnter.Invoke(collision);

        private void OnCollisionExit2D(Collision2D collision) => OnCollider2DExit.Invoke(collision);
    }

}