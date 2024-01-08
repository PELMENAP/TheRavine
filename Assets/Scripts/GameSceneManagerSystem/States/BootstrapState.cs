using UnityEngine;

namespace TheRavine.Base
{
    namespace BootstrapStates
    {
        public class BootstrapState : IState<Bootstrap>, IEnterable, IExitable
        {
            public Bootstrap Initializer { get; }
            public BootstrapState(Bootstrap initializer)
            {
                Initializer = initializer;
            }
            public void OnEnter()
            {
                Initializer.StartNewServise(null);
                FaderOnTransit.instance.SetLogs("Выполнен вход в игру");
                FaderOnTransit.instance.SetLogs("Создание точки входа");
                Initializer.StateMachine.SwitchState<InitialState>();
            }
            public void OnExit()
            {
                FaderOnTransit.instance.SetLogs("Точка входа создана");
                Initializer.AddCameraToStack(FaderOnTransit.instance.GetFaderCamera());
            }
        }
    }
}