using System.Collections.Generic;
using System.Linq;
using TheRavine.Extensions;

namespace TheRavine.Base
{
    public class BootstrapState : IState<GameStateMachine>, IEnterable, IExitable
    {
        public GameStateMachine Initializer { get; }
        private readonly Queue<Pair<ISetAble, string>> currentSetAbleScripts;
        public BootstrapState(GameStateMachine Initializer, Queue<Pair<ISetAble, string>> currentSetAbleScripts)
        {
            this.Initializer = Initializer;
            this.currentSetAbleScripts = currentSetAbleScripts;
        }
        public void OnEnter()
        {
            Initializer.LogBootstrapInfo("Entry point state reached");
            FaderOnTransit.Instance.SetLogs("Выполнен вход в игру");
            FaderOnTransit.Instance.SetLogs("Создание точки входа");
            Initializer.StartNewServices(currentSetAbleScripts, () => Initializer.StateMachine.SwitchState<InitialState>());
        }
        public void OnExit()
        {
            FaderOnTransit.Instance.SetLogs("Точка входа создана");
        }
    }
}