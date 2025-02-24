using Unity.Netcode;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace TheRavine.EntityControl
{
    public class BotView : NetworkBehaviour
    {
        AEntity botEntity;
        [SerializeField] private GameObject view;
        [SerializeField] private bool isActive;

        public override void OnNetworkSpawn() 
        {
            // botEntity = new BotEntity();
            botEntity.AddComponentToEntity(new TransformComponent(this.transform, this.transform));
        }

        public void SetSpeed()
        {
            // animator.SetFloat("Speed", 0);
        }
        public RoamMoveController moveController;
        public Animator animator;
        public Transform botTransform;
        public Rigidbody2D botRigidbody;
        public void EnableView()
        {
            isActive = true;
            view.SetActive(isActive);
        }
        public void DisableView()
        {
            isActive = false;
            view.SetActive(isActive);
        }
    }
}