using UnityEngine;

public class TransEffects : MonoBehaviour
{
    private Animator animator;
    private float time;
    private void Awake() {
        animator = this.GetComponent<Animator>();
        time = Random.Range(3f, 10f);
    }

    private void Update() {
        if(time <= 0){
            time = Random.Range(5f, 20f);
            animator.SetBool("act", !animator.GetBool("act"));
        }
        time -= Time.deltaTime;
    }
}
