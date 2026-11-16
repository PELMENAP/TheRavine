using UnityEngine;
using UnityEngine.UIElements;

using TheRavine.EntityControl;

public sealed class SkillGraphPanel : MonoBehaviour
{
    private const int RefreshMs = 250;
    private const int PanelSortingOrder = 100;
    private const string StyleSheetPath = "UI/SkillGraph";
    private const string BuiltinFont = "LegacyRuntime.ttf";

    [SerializeField] private PanelSettings panelSettings;

    private PanelSettings _fallbackSettings;
    private ThemeStyleSheet _fallbackTheme;
    private UIDocument _document;
    private VisualElement _shell;
    private Label _budget;
    private Label _stats;
    private SkillGraphMenu _menu;
    private Button _auto;
    private SkillGraphCanvas _canvas;
    private Label _cardTitle;
    private Label _cardBody;
    private VisualElement _confirm;
    private Label _confirmText;
    private IVisualElementScheduledItem _refresh;

    private PlayerSkillGraph _graph;
    private ISkillGraphCommands _commands;
    private ActionMapController _input;
    private readonly SkillGraphSnapshot _snapshot = new();
    private int _hoveredSkill = -1;
    private int _pendingU = -1;
    private int _pendingV = -1;
    private int _revision = int.MinValue;
    private int _version = int.MinValue;
    private int _checksum;

    public bool IsOpen { get; private set; }

    public static bool TryGetLocalGraph(out PlayerSkillGraph graph)
    {
        graph = null;
        var players = ServiceLocator.Players.GetAllPlayers();
        if (players.Count < 1 || players[0] is not PlayerEntity player) return false;

        graph = player.SkillGraph;
        return graph != null;
    }

    public static void ToggleLocal()
    {
        var existing = FindFirstObjectByType<SkillGraphPanel>();
        if (existing != null && existing.IsOpen)
        {
            existing.Close();
            return;
        }
        if (!TryGetLocalGraph(out var graph)) return;

        ServiceLocator.Services.TryGet(out ActionMapController input);
        if (input != null && !input.IsGameplayActive) return;
        Obtain().Open(graph, input);
    }

    public static SkillGraphPanel Obtain()
    {
        var found = FindFirstObjectByType<SkillGraphPanel>();
        if (found != null) return found;
        var created = new GameObject("SkillGraphPanel");
        created.SetActive(false);
        return created.AddComponent<SkillGraphPanel>();
    }

    public void Toggle(PlayerSkillGraph graph, ActionMapController input)
    {
        if (IsOpen) Close();
        else Open(graph, input);
    }

    public void Open(PlayerSkillGraph graph, ActionMapController input)
    {
        if (IsOpen || graph == null) return;
        _graph = graph;
        _commands = graph;
        _input = input;

        _document = gameObject.AddComponent<UIDocument>();
        if (panelSettings == null) panelSettings = _fallbackSettings = CreateFallbackSettings();
        _document.panelSettings = panelSettings;
        gameObject.SetActive(true);
        BuildTree(_document.rootVisualElement);

        IsOpen = true;
        if (_input != null)
        {
            _input.DisableAll();
            _input.EnableUI();
        }
        _revision = int.MinValue;
        Refresh();
        _refresh = _shell.schedule.Execute(Refresh).Every(RefreshMs);
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        _refresh?.Pause();
        _input?.SwitchToGameplay();
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (_fallbackSettings != null) Destroy(_fallbackSettings);
        if (_fallbackTheme != null) Destroy(_fallbackTheme);
    }

    private PanelSettings CreateFallbackSettings()
    {
        _fallbackTheme = ScriptableObject.CreateInstance<ThemeStyleSheet>();
        var settings = ScriptableObject.CreateInstance<PanelSettings>();
        settings.themeStyleSheet = _fallbackTheme;
        settings.sortingOrder = PanelSortingOrder;
        settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        settings.referenceResolution = new Vector2Int(1280, 720);
        settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
        settings.match = 0.5f;
        return settings;
    }

    private void BuildTree(VisualElement root)
    {
        var sheet = Resources.Load<StyleSheet>(StyleSheetPath);
        if (sheet != null) root.styleSheets.Add(sheet);
        var font = Resources.GetBuiltinResource<Font>(BuiltinFont);
        if (font != null) root.style.unityFontDefinition = FontDefinition.FromFont(font);

        root.style.position = Position.Absolute;
        root.style.left = root.style.top = root.style.right = root.style.bottom = 0f;

        _shell = new VisualElement();
        _shell.AddToClassList("skill-panel");
        root.Add(_shell);

        var header = new VisualElement();
        header.AddToClassList("skill-panel__header");
        var title = new Label("Skills");
        title.AddToClassList("skill-panel__title");
        _budget = new Label();
        _budget.AddToClassList("skill-panel__budget");
        _auto = new Button(OnAutoClicked);
        _auto.AddToClassList("skill-button");
        var close = new Button(Close) { text = "Close" };
        close.AddToClassList("skill-button");
        header.Add(title);
        header.Add(_budget);
        header.Add(_auto);
        header.Add(close);
        _shell.Add(header);

        var body = new VisualElement();
        body.AddToClassList("skill-panel__body");
        _canvas = new SkillGraphCanvas();
        _canvas.NodeHovered += OnNodeHovered;
        _canvas.NodeUnhovered += OnNodeUnhovered;
        _canvas.NodeClicked += OnNodeClicked;
        _canvas.EdgeClicked += OnEdgeClicked;
        _canvas.BackgroundPressed += OnBackgroundPressed;
        body.Add(_canvas);

        _stats = new Label();
        _stats.AddToClassList("skill-stats");
        _stats.pickingMode = PickingMode.Ignore;
        body.Add(_stats);

        var card = new VisualElement();
        card.AddToClassList("skill-card");
        _cardTitle = new Label();
        _cardTitle.AddToClassList("skill-card__title");
        _cardBody = new Label();
        _cardBody.AddToClassList("skill-card__body");
        card.Add(_cardTitle);
        card.Add(_cardBody);
        body.Add(card);
        _shell.Add(body);

        _menu = new SkillGraphMenu();
        _shell.Add(_menu);

        _confirm = new VisualElement();
        _confirm.AddToClassList("skill-confirm");
        _confirm.style.display = DisplayStyle.None;
        var box = new VisualElement();
        box.AddToClassList("skill-confirm__box");
        _confirmText = new Label("Prune this branch? Its skills freeze until it is restored.");
        _confirmText.AddToClassList("skill-card__body");
        var row = new VisualElement();
        row.AddToClassList("skill-confirm__row");
        var prune = new Button(ConfirmPrune) { text = "Prune" };
        prune.AddToClassList("skill-button");
        var cancel = new Button(CancelPrune) { text = "Cancel" };
        cancel.AddToClassList("skill-button");
        row.Add(prune);
        row.Add(cancel);
        box.Add(_confirmText);
        box.Add(row);
        _confirm.Add(box);
        _shell.Add(_confirm);
    }

    private void Refresh()
    {
        if (_graph == null || _graph.IsDisposed)
        {
            Close();
            return;
        }
        int revision = _graph.Revision;
        int version = _graph.StateVersion;
        int checksum = SkillGraphQuery.Checksum(_graph);
        if (revision == _revision && version == _version && checksum == _checksum) return;

        _revision = revision;
        _version = version;
        _checksum = checksum;
        SkillGraphQuery.Fill(_graph, _snapshot);
        _canvas.Bind(_snapshot, _graph.Atlas);
        _auto.text = _snapshot.AutoMode ? "Auto: on" : "Auto: off";
        _budget.text = "Edge points " + _snapshot.Available + " / " + _snapshot.Earned + "    Peak " + _snapshot.PhiPeak.ToString("0.0");
        _stats.text = StatsText();
        if (_hoveredSkill >= 0) ShowCard(_hoveredSkill);
    }

    private string StatsText()
        => "You can add: " + _snapshot.Available + (_snapshot.Available == 1 ? " link" : " links") + "\n" +
           "Earned " + _snapshot.Earned + "  /  spent " + _snapshot.Spent + "\n" +
           "Next point at peak sum " + _snapshot.NextPointPhi.ToString("0.0") + " (now " + _snapshot.PhiPeak.ToString("0.0") + ")\n" +
           "Build costs " + SkillGraphTopology.BuildCost + "  /  restore costs " + SkillGraphTopology.RestoreCost;

    private string Name(int skill) => _graph.Atlas.SkillAt(skill).Name;

    private string Blocker()
        => _snapshot.Available <= 0
            ? "No edge points left. The next one comes at peak sum " + _snapshot.NextPointPhi.ToString("0.0") + "."
            : "One end of the link must be a skill you have already learned.";

    private Vector2 MenuPoint(Vector2 canvasPoint) => _canvas.ChangeCoordinatesTo(_shell, canvasPoint);

    private void OnBackgroundPressed() => _menu.Hide();

    private void OnAutoClicked()
    {
        _commands.ToggleAutoMode();
        Refresh();
    }

    private void OnNodeHovered(int skill)
    {
        _hoveredSkill = skill;
        ShowCard(skill);
    }

    private void OnNodeUnhovered(int skill)
    {
        if (_hoveredSkill == skill) _hoveredSkill = -1;
    }

    private void ShowCard(int skill)
    {
        int slot = _snapshot.SlotOf(skill);
        if (slot < 0) return;

        var atlas = _graph.Atlas;
        _cardTitle.text = Name(skill);
        var state = _snapshot.State[slot];
        string effect = SkillGraphText.EffectLine(atlas, _snapshot, slot);
        string sources = SkillGraphText.SourcesLine(atlas, skill);
        if (state == SkillVertexState.Silhouette || state == SkillVertexState.Island)
        {
            _cardBody.text = "Not learned yet. Connect it to a skill you have learned to start training it.\n\n" + effect + "\n" + sources;
            return;
        }

        _cardBody.text =
            "Level " + _snapshot.Level[slot] + " (" + (_snapshot.Fraction[slot] * 100f).ToString("0") + "%)\n" +
            effect + "\n\n" +
            "Peak " + _snapshot.Peak[slot].ToString("0.0") + "   Floor " + _snapshot.Floor[slot].ToString("0.0") + "\n" +
            "Talent " + _snapshot.Talent[slot].ToString("0.00") + "\n" +
            "XP bonuses: clique x" + _snapshot.CliqueBoost[slot].ToString("0.00") +
            ", pull x" + _snapshot.LogicBoost[slot].ToString("0.00") +
            ", focus x" + _snapshot.FocusBoost.ToString("0.00") + "\n" +
            sources;
    }

    private void OnNodeClicked(int skill)
    {
        int slot = _snapshot.SlotOf(skill);
        if (slot < 0) return;

        var state = _snapshot.State[slot];
        _menu.Begin(Name(skill), SkillGraphText.EffectLine(_graph.Atlas, _snapshot, slot));
        if (state == SkillVertexState.Silhouette || state == SkillVertexState.Island || state == SkillVertexState.Pruned)
        {
            bool restore = state == SkillVertexState.Pruned;
            bool offered = false, usable = false;
            for (int e = 0; e < _snapshot.EdgeCount; e++)
            {
                if (_snapshot.EdgeKind[e] != SkillEdgeKind.Potential) continue;
                int from = _snapshot.EdgeFrom[e], to = _snapshot.EdgeTo[e];
                if (from != skill && to != skill) continue;

                int other = from == skill ? to : from;
                int otherSlot = _snapshot.SlotOf(other);
                if (otherSlot < 0 || _snapshot.State[otherSlot] != SkillVertexState.Open) continue;

                bool can = restore ? _commands.CanRestore(other, skill) : _commands.CanBuild(other, skill);
                int cost = restore ? SkillGraphTopology.RestoreCost : SkillGraphTopology.BuildCost;
                string label = (restore ? "Restore branch via " : "Learn via ") + Name(other) + " (cost " + cost + ")";
                int from0 = other, to0 = skill;
                _menu.AddOption(label, can, () =>
                {
                    if (restore) _commands.Restore(from0, to0);
                    else _commands.Build(from0, to0);
                    Refresh();
                });
                offered = true;
                usable |= can;
            }
            if (!offered) _menu.AddNote("No learned skill is linked to this one yet.");
            else if (!usable) _menu.AddNote(Blocker());
        }
        else
        {
            _menu.AddNote("Already part of your graph.");
            _menu.AddNote(SkillGraphText.SourcesLine(_graph.Atlas, skill));
        }
        _menu.Present(MenuPoint(_canvas.SlotPosition(slot)), _shell.contentRect.size);
    }

    private void OnEdgeClicked(int u, int v, SkillEdgeKind kind)
    {
        int slotU = _snapshot.SlotOf(u), slotV = _snapshot.SlotOf(v);
        if (slotU < 0 || slotV < 0) return;

        string title = Name(u) + " - " + Name(v);
        if (kind == SkillEdgeKind.Potential)
        {
            bool pruned = _snapshot.State[slotU] == SkillVertexState.Pruned || _snapshot.State[slotV] == SkillVertexState.Pruned;
            bool can = pruned ? _commands.CanRestore(u, v) : _commands.CanBuild(u, v);
            _menu.Begin(title, pruned ? "A pruned branch hangs on this link." : "A link you can build to learn the skill on the other end.");
            _menu.AddOption(pruned ? "Restore branch (cost " + SkillGraphTopology.RestoreCost + ")" : "Build link (cost " + SkillGraphTopology.BuildCost + ")", can, () =>
            {
                if (pruned) _commands.Restore(u, v);
                else _commands.Build(u, v);
                Refresh();
            });
            if (!can) _menu.AddNote(Blocker());
        }
        else if (kind == SkillEdgeKind.Bridge)
        {
            bool can = _commands.CanPrune(u, v);
            _menu.Begin(title, "A bridge: the only link to its branch. Pruning freezes that branch and focuses training on the rest.");
            _menu.AddOption("Prune branch (free)", can, () =>
            {
                _pendingU = u;
                _pendingV = v;
                _confirm.style.display = DisplayStyle.Flex;
            });
            if (!can) _menu.AddNote("This branch cannot be pruned: it holds your root or an island.");
        }
        else
        {
            _menu.Begin(title, "Part of a cycle: slows skill decay at both ends and can form a clique that speeds up training.");
        }
        _menu.Present(MenuPoint((_canvas.SlotPosition(slotU) + _canvas.SlotPosition(slotV)) * 0.5f), _shell.contentRect.size);
    }

    private void ConfirmPrune()
    {
        _confirm.style.display = DisplayStyle.None;
        if (_pendingU >= 0 && _commands.CanPrune(_pendingU, _pendingV)) _commands.Prune(_pendingU, _pendingV);
        _pendingU = _pendingV = -1;
        Refresh();
    }

    private void CancelPrune()
    {
        _confirm.style.display = DisplayStyle.None;
        _pendingU = _pendingV = -1;
    }
}
