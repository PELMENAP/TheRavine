using System.Collections.Generic;
namespace TheRavine.Base
{
        public class LoadingState : IState<GameStateMachine>, IEnterable, IExitable, ITickable
        {
            public GameStateMachine Initializer { get; }
            private float timer = 0;
            private int timerStep = 1;
            private bool isChangeState = false;
            private Queue<ISetAble> currentSetAbleScripts;
            public LoadingState(GameStateMachine initializer, Queue<ISetAble> currentSetAbleScripts)
            {
                this.currentSetAbleScripts = currentSetAbleScripts;
                Initializer = initializer;
            }
            public void OnEnter()
            {
                UnityEngine.Debug.Log("load");
                Initializer.StartNewServices(currentSetAbleScripts, () => isChangeState = true);
                FaderOnTransit.instance.SetLogs("Создание сцены");
            }
            public void OnExit()
            {
                FaderOnTransit.instance.SetLogs("Сцена создана");
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
                    FaderOnTransit.instance.SetLogs($"Загрузка сцены {timer}%");
                    timerStep += 1;
                }
            }
        }
}