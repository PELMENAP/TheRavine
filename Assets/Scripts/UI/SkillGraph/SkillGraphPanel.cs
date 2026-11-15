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
        body.Add(_canvas);

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
        _canvas.Bind(_snapshot);
        _auto.text = _snapshot.AutoMode ? "Auto: on" : "Auto: off";
        _budget.text = "Edge points " + _snapshot.Available + " / " + _snapshot.Earned + "    Peak " + _snapshot.PhiPeak.ToString("0.0");
        if (_hoveredSkill >= 0) ShowCard(_hoveredSkill);
    }

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

        var info = _graph.Atlas.SkillAt(skill);
        _cardTitle.text = info.Name;
        var state = _snapshot.State[slot];
        if (state == SkillVertexState.Silhouette || state == SkillVertexState.Island)
        {
            _cardBody.text = "Not explored yet.";
            return;
        }

        _cardBody.text =
            "Level " + _snapshot.Level[slot] + " (" + (_snapshot.Fraction[slot] * 100f).ToString("0") + "%)\n" +
            "Peak " + _snapshot.Peak[slot].ToString("0.0") + "\n" +
            "Floor " + _snapshot.Floor[slot].ToString("0.0") + "\n" +
            "Talent " + _snapshot.Talent[slot].ToString("0.00") + "\n" +
            "XP bonuses: clique x" + _snapshot.CliqueBoost[slot].ToString("0.00") +
            ", pull x" + _snapshot.LogicBoost[slot].ToString("0.00") +
            ", focus x" + _snapshot.FocusBoost.ToString("0.00");
    }

    private void OnNodeClicked(int skill)
    {
        int slot = _snapshot.SlotOf(skill);
        if (slot < 0) return;

        var state = _snapshot.State[slot];
        if (state == SkillVertexState.Silhouette || state == SkillVertexState.Island)
        {
            if (TryOnEdgeOfNode(skill, build: true)) Refresh();
        }
        else if (state == SkillVertexState.Pruned)
        {
            if (TryOnEdgeOfNode(skill, build: false)) Refresh();
        }
    }

    private bool TryOnEdgeOfNode(int skill, bool build)
    {
        for (int e = 0; e < _snapshot.EdgeCount; e++)
        {
            if (_snapshot.EdgeKind[e] != SkillEdgeKind.Potential) continue;
            int from = _snapshot.EdgeFrom[e], to = _snapshot.EdgeTo[e];
            if (from != skill && to != skill) continue;

            int other = from == skill ? to : from;
            if (build ? _commands.CanBuild(other, skill) && _commands.Build(other, skill)
                      : _commands.CanRestore(other, skill) && _commands.Restore(other, skill))
                return true;
        }
        return false;
    }

    private void OnEdgeClicked(int u, int v, SkillEdgeKind kind)
    {
        if (kind == SkillEdgeKind.Potential)
        {
            if (_commands.CanBuild(u, v)) _commands.Build(u, v);
            else if (_commands.CanBuild(v, u)) _commands.Build(v, u);
            else if (_commands.CanRestore(u, v)) _commands.Restore(u, v);
            Refresh();
        }
        else if (kind == SkillEdgeKind.Bridge && _commands.CanPrune(u, v))
        {
            _pendingU = u;
            _pendingV = v;
            _confirm.style.display = DisplayStyle.Flex;
        }
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
