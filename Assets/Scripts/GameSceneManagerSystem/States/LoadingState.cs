using System.Collections.Generic;

using TheRavine.Extensions;

namespace TheRavine.Base
{
    public class LoadingState : IState<GameStateMachine>, IEnterable, IExitable, ITickable
    {
        public GameStateMachine Initializer { get; }
        private float timer = 0;
        private int timerStep = 1;
        private bool isChangeState = false;
        private readonly Queue<Pair<ISetAble, string>> currentSetAbleScripts;
        public LoadingState(GameStateMachine initializer, Queue<Pair<ISetAble, string>> currentSetAbleScripts)
        {
            this.currentSetAbleScripts = currentSetAbleScripts;
            Initializer = initializer;
        }
        public void OnEnter()
        {
            Initializer.LogBootstrapInfo("Loading state reached");
            Initializer.StartNewServices(currentSetAbleScripts, () => isChangeState = true);
            FaderOnTransit.Instance.SetLogs("Создание сцены");
        }
        public void OnExit()
        {
            FaderOnTransit.Instance.SetLogs("Сцена создана");
        }
        public void OnTick()
        {
            timer += Initializer.GetTickPerUpdate();
            if (timer > 100 && isChangeState)
            {
                isChangeState = false;
                Initializer.StateMachine.SwitchState<GameState>();
            }
            if (timer >= timerStep * 10)
            {
                FaderOnTransit.Instance.SetLogs($"Загрузка сцены {timer}%");
                timerStep += 1;
            }
        }
    }
}