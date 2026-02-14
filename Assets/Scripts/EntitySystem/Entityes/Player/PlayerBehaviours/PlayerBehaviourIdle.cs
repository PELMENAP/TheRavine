using System;
using Cysharp.Threading.Tasks;
using TheRavine.EntityControl;
public class PlayerBehaviourIdle : AState
{
    private readonly PlayerController controller;
    private readonly Behaviour behaviourIdle;
    private readonly RavineLogger logger;
    public PlayerBehaviourIdle(PlayerController _controller, Action _delegateIdle, RavineLogger logger)
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
