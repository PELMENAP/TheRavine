using System.Collections.Generic;
namespace TheRavine.Base
{
    public class BootstrapState : IState<GameStateMachine>, IEnterable, IExitable
    {
        public GameStateMachine Initializer { get; }
        private Queue<ISetAble> currentSetAbleScripts;
        public BootstrapState(GameStateMachine Initializer, Queue<ISetAble> currentSetAbleScripts)
        {
            this.Initializer = Initializer;
            this.currentSetAbleScripts = currentSetAbleScripts;
        }
        public void OnEnter()
        {
            Initializer.LogBootstrapInfo("Entry point state reached");
            FaderOnTransit.instance.SetLogs("Выполнен вход в игру");
            FaderOnTransit.instance.SetLogs("Создание точки входа");
            Initializer.StartNewServices(currentSetAbleScripts, () => Initializer.StateMachine.SwitchState<InitialState>());
        }
        public void OnExit()
        {
            FaderOnTransit.instance.SetLogs("Точка входа создана");
        }
    }
}