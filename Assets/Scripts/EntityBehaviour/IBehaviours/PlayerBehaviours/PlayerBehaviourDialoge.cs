public class PlayerBehaviourDialoge : IPlayerBehaviour
{
    public Behaviour behaviourDialoge;

    public void Enter()
    {
        // PlayerController.instance.reloadSpeed = 0.5f;
        // PlayerController.instance.rb.mass = 10000;
    }

    public void Exit()
    {
        // PlayerController.instance.rb.mass = 1;
    }

    public void Update()
    {
        behaviourDialoge();
    }
}
