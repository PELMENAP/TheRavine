using System.Text;
using TMPro;
using Unity.Mathematics;
using UnityEngine;

public sealed class NestDebugView : MonoBehaviour
{
    [Header("View")]
    [SerializeField] private TextMeshPro  label;
    [SerializeField] private LineRenderer ring;
    [SerializeField] private bool  showLabel       = true;
    [SerializeField] private bool  faceCamera      = true;
    [SerializeField] private float refreshInterval = 0.5f;
    [SerializeField] private float labelHeight     = 14f;
    [SerializeField] private float groundOffset    = 0.5f;
    [SerializeField] private int   ringSegments    = 48;
    [SerializeField] private float ringWidth       = 0.4f;
    [SerializeField] private float fontSize        = 6f;

    [Header("Colony")]
    [SerializeField] private int   colonyId;
    [SerializeField] private bool  isWild;
    [SerializeField] private Vector2 nestPosition;
    [SerializeField] private float nestRadius;
    [SerializeField] private int   alive;
    [SerializeField] private int   born;
    [SerializeField] private int   died;
    [SerializeField] private int   juveniles;
    [SerializeField] private int   leaderId;
    [SerializeField] private int   colonyPois;
    [SerializeField] private int   relocations;
    [SerializeField] private int   reseeds;
    [SerializeField] private int   quorumVotes;

    [Header("Economy")]
    [SerializeField] private float storage;
    [SerializeField] private float storageShare;
    [SerializeField] private float storageEaten;
    [SerializeField] private float storageStored;
    [SerializeField] private float meanFill;
    [SerializeField] private float hunger;
    [SerializeField] private float alarm;
    [SerializeField] private float migrationPressure;

    [Header("Behaviour")]
    [SerializeField] private float restAtNestShare;
    [SerializeField] private float restPlanShare;
    [SerializeField] private float farShare;
    [SerializeField] private int[] activePlans   = new int[PlanCatalog.Count];
    [SerializeField] private int[] casteCounts   = new int[(int)Caste.Count];
    [SerializeField] private PlanKind[] topPlanByCaste = new PlanKind[(int)Caste.Count];

    [Header("Mortality & combat")]
    [SerializeField] private float deathRatePerMinute;
    [SerializeField] private float lifespanMean;
    [SerializeField] private int[] deathsByCause = new int[(int)DeathCause.Age + 1];
    [SerializeField] private int[] attacks       = new int[(int)ColonyStats.AttackTarget.Count];

    [Header("Selection")]
    [SerializeField] private float score;
    [SerializeField] private float storageEma;
    [SerializeField] private float populationEma;
    [SerializeField] private float hungerEma;

    private static readonly Color[] Palette =
    {
        new(0.30f, 0.65f, 1f), new(1f, 0.42f, 0.42f), new(0.42f, 1f, 0.54f), new(1f, 0.82f, 0.30f),
        new(0.78f, 0.49f, 1f), new(0.30f, 1f, 0.94f), new(1f, 0.62f, 0.30f), new(1f, 0.42f, 0.84f),
    };
    private static readonly Color WildColor = new(0.75f, 0.5f, 0.25f);
    private static readonly string[] CasteShort = { "W", "So", "Sc", "N" };
    private static readonly string[] CauseShort = { "K", "S", "V", "T", "A" };

    private readonly StringBuilder _sb = new(512);
    private ColonyState   _colony;
    private EntityManager _manager;
    private Camera _camera;
    private float  _nextRefresh;
    private float  _ringRadius = -1f;
    private float  _groundY;
    private bool   _hasGround;

    public ColonyState Colony => _colony;

    public static NestDebugView Create(Transform parent)
    {
        var go = new GameObject("Nest");
        go.transform.SetParent(parent, false);
        var view = go.AddComponent<NestDebugView>();

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(go.transform, false);
        view.label = labelGo.AddComponent<TextMeshPro>();

        view.ring = go.AddComponent<LineRenderer>();
        view.ring.material = new Material(Shader.Find("Sprites/Default"));
        return view;
    }

    public void Bind(ColonyState colony, EntityManager manager)
    {
        _colony  = colony;
        _manager = manager;
        colonyId = colony.ColonyId;
        isWild   = colony.IsWild;
        gameObject.name = isWild ? "Nest (wild)" : $"Nest C{colony.ColonyId}";

        Color color = isWild ? WildColor : Palette[(colony.ColonyId - 1 & 0x7FFFFFFF) % Palette.Length];

        if (label != null)
        {
            label.richText           = true;
            label.fontSize           = fontSize;
            label.alignment          = TextAlignmentOptions.Center;
            label.textWrappingMode   = TextWrappingModes.NoWrap;
            label.color              = Color.white;
            label.transform.localPosition = new Vector3(0f, labelHeight, 0f);
        }

        if (ring != null)
        {
            ring.useWorldSpace = false;
            ring.loop          = true;
            ring.startWidth    = ringWidth;
            ring.endWidth      = ringWidth;
            ring.startColor    = color;
            ring.endColor      = color;
            ring.positionCount = math.max(ringSegments, 8);
        }

        Refresh();
    }

    private void Update()
    {
        if (_colony == null) return;
        float now = SimulationClock.Time;
        if (now < _nextRefresh) return;
        _nextRefresh = now + refreshInterval;
        Refresh();
    }

    private void LateUpdate()
    {
        if (!faceCamera || label == null || !showLabel) return;
        if (_camera == null) _camera = Camera.main;
        if (_camera == null) return;
        label.transform.rotation = Quaternion.LookRotation(label.transform.position - _camera.transform.position);
    }

    private void Refresh()
    {
        var colony = _colony;
        var nest   = colony.Nest;
        var st     = colony.Stats;
        var r      = SimulationRules.Active;

        nestPosition = new Vector2(nest.Position.x, nest.Position.y);
        nestRadius   = nest.Radius;
        alive        = colony.MemberCount;
        born         = st.Born;
        died         = st.Died;
        juveniles    = colony.JuvenileCount;
        leaderId     = colony.Leader != null ? colony.Leader.EntityId : -1;
        colonyPois   = colony.Pois.Count;
        relocations  = nest.Relocations;
        reseeds      = st.Reseeds;
        quorumVotes  = colony.SiteVotesMax;

        storage           = nest.Storage;
        storageShare      = st.StorageShare;
        storageEaten      = st.StorageEaten;
        storageStored     = st.StorageStored;
        meanFill          = st.MeanFill;
        hunger            = nest.Hunger;
        alarm             = nest.Alarm;
        migrationPressure = nest.MigrationPressure;

        restAtNestShare = st.RestAtNestShare;
        restPlanShare   = st.PlanShare(PlanKind.Rest);
        farShare        = st.FarShare;

        deathRatePerMinute = st.DeathRatePerMinute;
        lifespanMean       = st.MeanLifespan;
        System.Array.Copy(st.DeathsByCause, deathsByCause, deathsByCause.Length);
        System.Array.Copy(st.Attacks, attacks, attacks.Length);
        System.Array.Copy(colony.CasteCounts, casteCounts, casteCounts.Length);
        for (int c = 0; c < topPlanByCaste.Length; c++) topPlanByCaste[c] = st.TopPlan((Caste)c);

        score         = colony.Score;
        storageEma    = colony.StorageEma;
        populationEma = colony.PopulationEma;
        hungerEma     = colony.HungerEma;

        SampleMembers(colony);
        UpdateTransform(nest);
        if (showLabel) WriteLabel(r);
        else if (label != null && label.text.Length > 0) label.SetText(string.Empty);
    }

    private void SampleMembers(ColonyState colony)
    {
        System.Array.Clear(activePlans, 0, activePlans.Length);
        var members  = colony.Members;
        var entities = _manager.Entities;
        var nest     = colony.Nest;
        float ySum = 0f;
        int   yCount = 0;

        for (int i = 0; i < members.Length; i++)
        {
            int idx = members[i];
            if ((uint)idx >= (uint)entities.Count) continue;
            var e = entities[idx];
            if (e == null || e.IsDisposed || e.IsDeathPending) continue;

            var kind = e.Plan.Kind;
            if (kind < PlanKind.Count) activePlans[(int)kind]++;

            Vector3 p = e.Motor.Position();
            if (!nest.Contains(new float2(p.x, p.z))) continue;
            ySum += p.y;
            yCount++;
        }

        if (yCount > 0)
        {
            _groundY   = ySum / yCount;
            _hasGround = true;
        }
        else if (!_hasGround && ySum == 0f)
        {
            for (int i = 0; i < members.Length; i++)
            {
                int idx = members[i];
                if ((uint)idx >= (uint)entities.Count) continue;
                var e = entities[idx];
                if (e == null || e.IsDisposed || e.IsDeathPending) continue;
                _groundY   = e.Motor.Position().y;
                _hasGround = true;
                break;
            }
        }
    }

    private void UpdateTransform(NestState nest)
    {
        float y = _hasGround ? _groundY : transform.position.y;
        transform.position = new Vector3(nest.Position.x, y + groundOffset, nest.Position.y);

        if (ring == null || math.abs(_ringRadius - nest.Radius) < 1e-3f) return;
        _ringRadius = nest.Radius;
        int n = ring.positionCount;
        float step = 2f * math.PI / n;
        for (int i = 0; i < n; i++)
        {
            math.sincos(i * step, out float s, out float c);
            ring.SetPosition(i, new Vector3(c * _ringRadius, 0f, s * _ringRadius));
        }
    }

    private void WriteLabel(SimulationRules r)
    {
        if (label == null) return;
        var sb = _sb;
        sb.Clear();

        sb.Append("<b>").Append(isWild ? "WILD" : "C");
        if (!isWild) sb.AppendInt(colonyId);
        sb.Append("</b>  ")
          .AppendInt(alive).Append(" alive  +").AppendInt(born).Append(" -").AppendInt(died).Append('\n');

        if (!isWild)
        {
            sb.Append("store ").AppendFixed(storage, 0).Append(" (").AppendPercent(storageShare)
              .Append("%)  fill ").AppendFixed(meanFill, 2).Append('\n');
            sb.Append("eaten/stored ").AppendFixed(storageEaten, 0).Append('/').AppendFixed(storageStored, 0).Append('\n');
            sb.Append("hunger ").AppendFixed(hunger, 2).Append("  alarm ").AppendFixed(alarm, 2)
              .Append("  migr ").AppendFixed(migrationPressure, 2).Append('\n');
            sb.Append("rest@nest ").AppendPercent(restAtNestShare).Append("%  rest plans ")
              .AppendPercent(restPlanShare).Append("%  far ").AppendPercent(farShare).Append("%\n");

            sb.Append("castes");
            for (int c = 0; c < casteCounts.Length; c++)
                sb.Append(' ').Append(CasteShort[c]).AppendInt(casteCounts[c]).Append(':').Append(PlanShort(topPlanByCaste[c]));
            sb.Append("  juv ").AppendInt(juveniles).Append('\n');

            sb.Append("leader ").AppendInt(leaderId).Append("  poi ").AppendInt(colonyPois)
              .Append("  quorum ").AppendInt(quorumVotes).Append('/').AppendInt(r.QuorumSize)
              .Append("  moves ").AppendInt(relocations).Append("  reseeds ").AppendInt(reseeds).Append('\n');
        }

        sb.Append("plans");
        for (int p = 0; p < activePlans.Length; p++)
            if (activePlans[p] > 0) sb.Append(' ').Append(PlanCatalog.Names[p]).Append(' ').AppendInt(activePlans[p]);
        sb.Append('\n');

        sb.Append("deaths");
        for (int c = 0; c < deathsByCause.Length; c++) sb.Append(' ').Append(CauseShort[c]).AppendInt(deathsByCause[c]);
        sb.Append("  /min ").AppendFixed(deathRatePerMinute, 2).Append('\n');

        sb.Append("attacks own/foreign/player ")
          .AppendInt(attacks[(int)ColonyStats.AttackTarget.Own]).Append('/')
          .AppendInt(attacks[(int)ColonyStats.AttackTarget.Foreign]).Append('/')
          .AppendInt(attacks[(int)ColonyStats.AttackTarget.Player]);

        label.SetText(sb);
    }

    private static string PlanShort(PlanKind plan) => plan < PlanKind.Count ? PlanCatalog.Names[(int)plan] : "-";
}

public static class StringBuilderNumeric
{
    public static StringBuilder AppendInt(this StringBuilder sb, long value)
    {
        if (value < 0) { sb.Append('-'); value = -value; }
        if (value == 0) return sb.Append('0');
        int start = sb.Length;
        while (value > 0)
        {
            sb.Append((char)('0' + (int)(value % 10)));
            value /= 10;
        }
        for (int i = start, j = sb.Length - 1; i < j; i++, j--)
            (sb[i], sb[j]) = (sb[j], sb[i]);
        return sb;
    }

    public static StringBuilder AppendFixed(this StringBuilder sb, float value, int decimals)
    {
        if (!math.isfinite(value)) return sb.Append("nan");
        if (value < 0f) { sb.Append('-'); value = -value; }
        long scale = 1;
        for (int i = 0; i < decimals; i++) scale *= 10;
        long scaled = (long)math.round(value * scale);
        sb.AppendInt(scaled / scale);
        if (decimals <= 0) return sb;
        sb.Append('.');
        long frac = scaled % scale;
        for (long d = scale / 10; d > 0; d /= 10)
        {
            sb.Append((char)('0' + (int)(frac / d)));
            frac %= d;
        }
        return sb;
    }

    public static StringBuilder AppendPercent(this StringBuilder sb, float fraction) => sb.AppendFixed(fraction * 100f, 0);
}
