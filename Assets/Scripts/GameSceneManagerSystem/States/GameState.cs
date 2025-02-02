using System.Collections.Generic;
using Unity.Netcode;


namespace TheRavine.Base
{
        public class GameState : IState<GameStateMachine>, IEnterable, IExitable
        {
            public GameStateMachine Initializer { get; }
            bool aboba = false;
            public GameState(GameStateMachine initializer)
            {
                Initializer = initializer;
            }
            public void OnEnter()
            {
                FaderOnTransit.instance.SetLogs("Начало игры");
                FaderOnTransit.instance.FadeOut(() => aboba = true);
                Initializer.OnGameAlreadyStarted();
                NetworkManager.Singleton.StartHost();
            }
            public void OnExit()
            {
                DayCycle.closeThread = false;
            }
            public void OnTick()
            {
                if (aboba)
                {
                    // Initializer.DestroySceneTransitions();
                    aboba = false;
                }
            }
        }

}