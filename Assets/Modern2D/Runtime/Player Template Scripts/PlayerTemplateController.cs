using System.Collections;
using UnityEngine;

namespace Modern2D
{

    //  my template character controller based on a state machine system
    //  used in a reference scene
    //  you can build on top of it, if you're suicidal lol

    public class PlayerTemplateController : MonoBehaviour
    {
        [SerializeField] Animator _playerAnimator;
        [SerializeField] SpriteRenderer _sr;
        [SerializeField] PlayerStats _playerStats;

        PlayerInput _playerInput = new PlayerInput();
        PlayerState _playerState = PlayerState.moving;

        bool _dashing;
        float _spriteDir;


        void Update()
        {
            GatherInput();
            switch (_playerState)
            {
                case PlayerState.moving:
                    MovementChandler();
                    DashChandler();
                    AttackChandler();
                    break;
                case PlayerState.attacking:
                    DashChandler();
                    break;
            }
        }

        void ChangePlayerState(PlayerState from, PlayerState to)
        {
            // you can make it so there are special functions for every state start and end. 
            // I won't make it tho because it's just a small state controller template you can build on

            _playerState = to;
        }

        void MovementChandler()
        {
            if (_playerInput.horizontal < 0)
                _sr.flipX = true;
            else if (_playerInput.horizontal > 0)
                _sr.flipX = false;

            Vector3 movementVec = _playerInput.dir * _playerStats.speed * Time.deltaTime;

            transform.position += movementVec;
            _playerAnimator.SetFloat("speed", movementVec.magnitude);
        }

        void DashChandler()
        {
            if (_playerInput.dash)
            {
                if (_dashing)
                    return;
                if (_playerState == PlayerState.attacking)
                    ChangePlayerState(PlayerState.attacking, PlayerState.moving);
                StartCoroutine(Dash(_playerInput.dir, _playerStats.dashDistance, _playerStats.dashDuration));
            }
        }

        void AttackChandler()
        {
            if (_playerInput.attack)
            {
                if (_playerState == PlayerState.attacking)
                    return;
                else
                {
                    ChangePlayerState(PlayerState.moving, PlayerState.attacking);
                    StartCoroutine(Attack(_playerStats.attackDuration));
                }
            }
        }

        IEnumerator Attack(float time)
        {
            _playerAnimator.SetTrigger("attack");

            float timer = 0;
            while (timer < time)
            {
                timer += Time.deltaTime;
                yield return new WaitForEndOfFrame();
            }
            ChangePlayerState(PlayerState.attacking, PlayerState.moving);
        }

        IEnumerator Dash(Vector2 dir, float distance, float time)
        {
            _playerAnimator.SetTrigger("dash");
            _dashing = true;
            float timer = 0;
            float speed = distance / time;
            while (timer < time)
            {
                timer += Time.deltaTime;
                transform.position += (Vector3)(dir * speed * Time.deltaTime);
                yield return new WaitForEndOfFrame();
            }
            _dashing = false;
        }

        void GatherInput()
        {
            // call this function every frame before others
            _playerInput = PlayerInput.GetInputThisFrame();
        }


    }



    //                              //
    //       [ OTHER CLASSES ]      //
    //                              //

    public enum PlayerState
    {
        moving = 0,
        attacking = 1
    }

    [System.Serializable]
    public class PlayerStats
    {
        public float speed;
        public float dashDuration;
        public float attackDuration;
        public float dashDistance;
    }

    public struct PlayerInput
    {
        public static PlayerInput GetInputThisFrame()
        {
            return new PlayerInput
            {
                horizontal = Input.GetAxisRaw("Horizontal"),
                vertical = Input.GetAxisRaw("Vertical"),
                dash = Input.GetKey(KeyCode.LeftShift),
                attack = Input.GetKey(KeyCode.J),
                dir = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized
            };
        }

        public Vector2 dir;
        public float horizontal;
        public float vertical;
        public bool dash;
        public bool attack;

    }

}