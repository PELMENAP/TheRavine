using Cysharp.Threading.Tasks;

using TheRavine.EntityControl;

namespace TheRavine.Base
{
    public class SkillGraphCommand : IValidatedCommand
    {
        private readonly ActionMapController _input;

        public string Name => "~skills";
        public string ShortName => "~sk";
        public string Description => "Открывает или закрывает граф навыков: ~skills";

        public SkillGraphCommand(ActionMapController input) => _input = input;

        public bool Validate(CommandContext context)
        {
            if (TryGetLocalGraph(out _)) return true;

            context.Display("Игрок ещё не инициализирован");
            return false;
        }

        public UniTask ExecuteAsync(string[] args, CommandContext context)
        {
            if (TryGetLocalGraph(out var graph))
                SkillGraphPanel.Obtain().Toggle(graph, _input);
            return UniTask.CompletedTask;
        }

        private static bool TryGetLocalGraph(out PlayerSkillGraph graph)
        {
            graph = null;
            var players = ServiceLocator.Players.GetAllPlayers();
            if (players.Count < 1 || players[0] is not PlayerEntity player) return false;

            graph = player.SkillGraph;
            return graph != null;
        }
    }
}
