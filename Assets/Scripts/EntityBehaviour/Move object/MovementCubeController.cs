using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovementCubeController : MonoBehaviour
{
    private RoamMoveController moveController;

    private void Start()
    {
        moveController = (RoamMoveController)this.GetComponent("RoamMoveController");
        moveController.UpdateTargetWander();
    }

    private void Update()
    {
        moveController.UpdateBehaviour();
    }
}
