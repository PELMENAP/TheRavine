using System.Collections.Generic;

using TheRavine.Extensions;

namespace TheRavine.Base
{
    public class InitialState : IState<GameStateMachine>, IEnterable, IExitable, ITickable
    {
        public GameStateMachine Initializer { get; }
        private int timer = 0;
        private bool isChangeState = false;
        private readonly Queue<Pair<ISetAble, string>> currentSetAbleScripts;
        public InitialState(GameStateMachine initializer, Queue<Pair<ISetAble, string>> currentSetAbleScripts)
        {
            this.currentSetAbleScripts = currentSetAbleScripts;
            Initializer = initializer;
        }
        public void OnEnter()
        {
            Initializer.LogBootstrapInfo("Initial state reached");
            Initializer.StartNewServices(currentSetAbleScripts, () => isChangeState = true);
            FaderOnTransit.instance.SetLogs("Загрузка ресурсов");
        }
        public void OnExit()
        {
            FaderOnTransit.instance.SetLogs("Загрузка ресурсов завершена");
        }
        public void OnTick()
        {
            timer += Initializer.GetTickPerUpdate();
            if (timer > 100 && isChangeState)
            {
                isChangeState = false;
                Initializer.StateMachine.SwitchState<LoadingState>();
            }
        }
    }
}