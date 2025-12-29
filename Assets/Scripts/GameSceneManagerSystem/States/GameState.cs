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
                Initializer.LogBootstrapInfo("GAME IS STARTED");
                ServiceLocator.Services.LogRegisteredServices();
                
                FaderOnTransit.Instance.FadeOut(() => aboba = true);
                Initializer.OnGameAlreadyStarted();
            }
            public void OnExit()
            {
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