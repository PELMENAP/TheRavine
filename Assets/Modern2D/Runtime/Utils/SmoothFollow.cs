using UnityEngine;

namespace Modern2D
{

    public class SmoothFollow : MonoBehaviour
    {
        [SerializeField] Transform followTarget;
        [SerializeField] Vector3 followOffset;
        [SerializeField] float followSpeed;

        //bad code
        void FixedUpdate()
        {
            if (followTarget != null)
            {
                var desiredPosition = followTarget.position + followOffset;

                var smoothedPosition = new Vector3(Mathf.SmoothStep(transform.position.x, desiredPosition.x, followSpeed * Time.fixedDeltaTime), Mathf.SmoothStep(transform.position.y, desiredPosition.y, followSpeed * Time.fixedDeltaTime), Mathf.SmoothStep(transform.position.z, desiredPosition.z, followSpeed * Time.fixedDeltaTime));
                transform.position = smoothedPosition;
            }
        }
    }

}