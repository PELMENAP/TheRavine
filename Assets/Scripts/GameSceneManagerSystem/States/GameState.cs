namespace TheRavine.Base
{
    namespace BootstrapStates
    {
        public class GameState : IState<Bootstrap>, IEnterable, IExitable
        {
            public Bootstrap Initializer { get; }
            bool aboba = false;
            public GameState(Bootstrap initializer)
            {
                Initializer = initializer;
            }
            public void OnEnter()
            {
                Initializer.StartNewService(null);
                Initializer.StartNewService(Initializer.Finally);
                FaderOnTransit.instance.SetLogs("Начало игры");
                FaderOnTransit.instance.FadeOut(() => aboba = true);
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
}