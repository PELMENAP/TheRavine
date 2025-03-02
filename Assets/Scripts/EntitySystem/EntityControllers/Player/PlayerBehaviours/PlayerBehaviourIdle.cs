using System;
using System.Collections.Generic;
using UnityEngine;
public class PlayerBehaviourIdle : AState
{
    private IEntityController controller;
    private Behaviour behaviourIdle;
    private ILogger logger;
    public PlayerBehaviourIdle(IEntityController _controller, Action _delegateIdle, ILogger logger)
    {
        controller = _controller;
        behaviourIdle = _delegateIdle.Invoke;
        this.logger = logger;

        AddCommand(new MoveAlongPathCommand(
            controller.GetModelTransform(),
            new List<Vector3>() {new Vector3(0, 0, 0), new Vector3(0, 20, 0), new Vector3(20, 20, 0), new Vector3(20, 0, 0)}, 
            1,
            logger));
    }
    public override void Enter()
    {
        ProcessCommandsAsync();
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
