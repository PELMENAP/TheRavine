using System;
using System.Globalization;
using Cysharp.Threading.Tasks;
using Unity.Mathematics;

using TheRavine.EntityControl;

namespace TheRavine.Base
{
    public class CodexCommand : IValidatedCommand
    {
        private const int AxisCount = (int)VarianceAxis.Count;
        private const int MaxToken = 32;

        public string Name => "~codex";
        public string ShortName => "~cx";
        public string Description => "Законы мира: ~codex list | explain <ось> | propose <ось>=<сдвиг> ... | vote <id|-ось> <энергия>";

        public bool Validate(CommandContext context)
        {
            if (WorldAuthority.IsAuthority && !WorldAuthority.HasRemotePeers) return true;

            context.Display(CodexErrorText.Message(CodexError.NotAuthority));
            return false;
        }

        public UniTask ExecuteAsync(string[] args, CommandContext context)
        {
            if (!ServiceLocator.Services.TryGet(out WorldCodexService codex))
            {
                context.Display("Свод законов ещё не создан");
                return UniTask.CompletedTask;
            }

            string verb = args.Length > 1 ? args[1].ToLowerInvariant() : string.Empty;
            switch (verb)
            {
                case "list": List(codex, context); break;
                case "explain": Explain(codex, args, context); break;
                case "propose": Propose(codex, args, context); break;
                case "vote": Vote(codex, args, context); break;
                default: context.Display($"Использование: {Name} list | explain <ось> | propose <ось>=<сдвиг> ... | vote <id|-ось> <энергия>"); break;
            }
            return UniTask.CompletedTask;
        }

        private static void List(WorldCodexService codex, CommandContext context)
        {
            var laws = codex.Laws;
            context.Display($"Законов: {laws.Length}, версия {codex.Version}");
            for (int i = 0; i < laws.Length; i++)
            {
                ref readonly var law = ref laws[i];
                var delta = LawRecord.DeltaReadOnly(in law);
                string parts = string.Empty;
                for (int k = 0; k < AxisCount; k++)
                    if (delta[k] != 0f) parts += $" {(VarianceAxis)k}={delta[k]:+0.###;-0.###}";
                context.Display($"#{law.Id} поддержка {law.Support:F2} автор {AuthorLabel(law.Author)}:{parts}");
            }
        }

        private static void Explain(WorldCodexService codex, string[] args, CommandContext context)
        {
            if (args.Length != 3 || !TryParseAxis(args[2], out int axis))
            {
                context.Display("Использование: ~codex explain <ось>; оси: " + string.Join(", ", Enum.GetNames(typeof(VarianceAxis)), 0, AxisCount));
                return;
            }

            float theta = codex.Theta[axis];
            float opposition = codex.Opposition[axis];
            float inertia = SimulationRules.Active.CodexInertia;
            var laws = codex.Laws;

            float den = inertia + opposition;
            float absSum = 0f;
            for (int i = 0; i < laws.Length; i++)
            {
                float d = LawRecord.DeltaReadOnly(in laws[i])[axis];
                if (d == 0f) continue;
                den += laws[i].Support;
                absSum += laws[i].Support * math.abs(d);
            }

            string source = codex.PricesFromPrior ? "заданная" : "измеренная";
            context.Display($"{(VarianceAxis)axis}: θ={theta:F3} множитель ×{math.exp(theta):F3}");
            context.Display($"оппозиция {opposition:F2}, инерция мира {inertia:F2}, цена ({source}, окон {codex.PriceSamples}) {codex.Prices[axis]:+0.###;-0.###;0}");
            for (int i = 0; i < laws.Length; i++)
            {
                float d = LawRecord.DeltaReadOnly(in laws[i])[axis];
                if (d == 0f) continue;
                float contribution = laws[i].Support * d / den;
                float share = absSum > 0f ? laws[i].Support * math.abs(d) / absSum : 0f;
                context.Display($"#{laws[i].Id} сдвиг {d:+0.###;-0.###} вклад {contribution:+0.####;-0.####} доля {share * 100f:F0}%");
            }
        }

        private static void Propose(WorldCodexService codex, string[] args, CommandContext context)
        {
            if (args.Length < 3 || args.Length > 2 + AxisCount)
            {
                context.Display("Использование: ~codex propose <ось>=<сдвиг> [<ось>=<сдвиг> ...]");
                return;
            }

            Span<float> delta = stackalloc float[AxisCount];
            delta.Clear();
            for (int i = 2; i < args.Length; i++)
            {
                if (!TryParsePair(args[i], out int axis, out float value) || delta[axis] != 0f)
                {
                    context.Display($"Не разобрано: {Clip(args[i])}");
                    return;
                }
                delta[axis] = value;
            }

            if (!TryAuthor(context, out int author))
            {
                context.Display(CodexErrorText.Message(CodexError.NoPurse));
                return;
            }

            if (codex.TryPropose(author, delta, out int lawId, out var error))
                context.Display($"Закон #{lawId} предложен. Поддержи его: ~codex vote {lawId} <энергия>");
            else
                context.Display(CodexErrorText.Message(error));
        }

        private static void Vote(WorldCodexService codex, string[] args, CommandContext context)
        {
            if (args.Length != 4 || !TryParseEnergy(args[3], out float energy))
            {
                context.Display("Использование: ~codex vote <id|-ось> <энергия>");
                return;
            }

            if (!TryAuthor(context, out int author))
            {
                context.Display(CodexErrorText.Message(CodexError.NoPurse));
                return;
            }

            string target = args[2];
            CodexError error;
            bool ok;
            if (target.Length > 1 && target[0] == '-')
            {
                if (!TryParseAxis(target.Substring(1), out int axis))
                {
                    context.Display(CodexErrorText.Message(CodexError.BadAxis));
                    return;
                }
                ok = codex.TryOppose(author, axis, energy, out error);
            }
            else if (int.TryParse(target, NumberStyles.None, CultureInfo.InvariantCulture, out int lawId))
            {
                ok = codex.TryInvest(author, lawId, energy, out error);
            }
            else
            {
                context.Display($"Не разобрано: {Clip(target)}");
                return;
            }

            context.Display(ok ? "Голос учтён; закон вступит в силу на границе окна тика" : CodexErrorText.Message(error));
        }

        private static bool TryAuthor(CommandContext context, out int author)
        {
            author = CodexAuthor.None;
            var players = context.PlayersData;
            if (players == null || players.Count < 1) return false;

            var main = players[0].GetEntityComponent<MainComponent>();
            if (main == null) return false;

            author = CodexAuthor.ForPlayer(main.GetClientID());
            return true;
        }

        private static bool TryParsePair(string token, out int axis, out float value)
        {
            axis = 0;
            value = 0f;
            int eq = token.IndexOf('=');
            if (eq <= 0 || eq >= token.Length - 1 || token.Length > MaxToken) return false;
            return TryParseAxis(token.Substring(0, eq), out axis)
                && float.TryParse(token.AsSpan(eq + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                && math.isfinite(value);
        }

        private static bool TryParseEnergy(string token, out float energy)
        {
            energy = 0f;
            return token.Length <= MaxToken
                && float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out energy)
                && math.isfinite(energy);
        }

        private static bool TryParseAxis(string token, out int axis)
        {
            axis = -1;
            if (token.Length == 0 || token.Length > MaxToken) return false;

            for (int k = 0; k < AxisCount; k++)
            {
                if (!string.Equals(((VarianceAxis)k).ToString(), token, StringComparison.OrdinalIgnoreCase)) continue;
                axis = k;
                return true;
            }
            return false;
        }

        private static string AuthorLabel(int author)
        {
            if (CodexAuthor.IsColony(author)) return $"колония {CodexAuthor.ColonyOf(author)}";
            if (CodexAuthor.IsPlayer(author)) return $"игрок {CodexAuthor.ClientOf(author)}";
            return $"существо {author}";
        }

        private static string Clip(string text) => text.Length <= MaxToken ? text : text.Substring(0, MaxToken);
    }
}
