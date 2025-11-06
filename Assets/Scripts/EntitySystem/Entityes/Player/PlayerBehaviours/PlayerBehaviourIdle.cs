using System;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
public class PlayerBehaviourIdle : AState
{
    private readonly IEntityController controller;
    private readonly Behaviour behaviourIdle;
    private readonly IRavineLogger logger;
    public PlayerBehaviourIdle(IEntityController _controller, Action _delegateIdle, IRavineLogger logger)
    {
        controller = _controller;
        behaviourIdle = _delegateIdle.Invoke;
        this.logger = logger;

        // AddCommand(new MoveAlongPathCommand(
        //     controller.GetModelTransform(),
        //     new List<Vector3>() {new Vector3(0, 0, 0), new Vector3(0, 20, 0), new Vector3(20, 20, 0), new Vector3(20, 0, 0)}, 
        //     1,
        //     logger));
    }
    public override void Enter()
    {
        ProcessCommandsAsync().Forget();
    }

    public override void Exit()
    {
        CancelCurrentCommand();
    }

    public override void Update()
    {
        behaviourIdle();
    }
}
