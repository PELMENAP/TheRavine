namespace TheRavine.Base
{
    namespace BootstrapStates
    {
        public class InitialState : IState<Bootstrap>, IEnterable, IExitable, ITickable
        {
            public Bootstrap Initializer { get; }
            private int timer = 0;
            private bool isChangeState = false;
            public InitialState(Bootstrap initializer, bool _isLoad)
            {
                Initializer = initializer;
            }
            public void OnEnter()
            {
                Initializer.StartNewService(null);
                Initializer.StartNewService(() => isChangeState = true);
                FaderOnTransit.instance.SetLogs("Загрузка ресурсов");
            }
            public void OnExit()
            {
                FaderOnTransit.instance.SetLogs("Загрузка ресурсов завершена");
            }
            public void OnTick()
            {
                timer += Initializer.tickPerUpdate;
                if (timer > 100 && isChangeState)
                {
                    isChangeState = false;
                    Initializer.StateMachine.SwitchState<LoadingState>();
                }
            }
        }
    }
}