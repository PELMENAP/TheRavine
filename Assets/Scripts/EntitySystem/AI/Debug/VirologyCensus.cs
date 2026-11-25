using System.Collections.Generic;
using TheRavine.EntityControl.Virology;

public sealed class VirologyCensus
{
    private struct StrainInfo
    {
        public int   Hosts;
        public int   LastHost;
        public ulong Signature;
    }

    private readonly Dictionary<ulong, StrainInfo> _strains = new();
    private readonly HashSet<ulong> _lineages = new();
    private readonly HashSet<ulong> _previous = new();

    public int   Hosts { get; private set; }
    public int   Strains { get; private set; }
    public int   Lineages { get; private set; }
    public int   StrainsNew { get; private set; }
    public int   StrainsLost { get; private set; }
    public ulong Dominant { get; private set; }
    public float DominantPrevalence { get; private set; }
    public float ImmuneDominantShare { get; private set; }
    public float MeanIntegrity { get; private set; }

    public void Sample(List<EntityModel> entities)
    {
        _strains.Clear();
        _lineages.Clear();
        Hosts = 0;
        double integrity = 0d;
        int segments = 0;

        for (int i = 0; i < entities.Count; i++)
        {
            var host = entities[i]?.Virology;
            if (host == null || host.IsDisposed || !host.IsCreated) continue;
            Hosts++;

            var span = host.Segments;
            for (int s = 0; s < span.Length; s++)
            {
                var seg = span[s];
                if (seg.IsEndogenous) continue;
                integrity += seg.Integrity;
                segments++;
                _lineages.Add(seg.LineageId);

                _strains.TryGetValue(seg.StrainId, out var info);
                if (info.Hosts == 0) info.Signature = seg.Signature;
                if (info.LastHost != Hosts) { info.Hosts++; info.LastHost = Hosts; }
                _strains[seg.StrainId] = info;
            }
        }

        Strains  = _strains.Count;
        Lineages = _lineages.Count;
        MeanIntegrity = segments > 0 ? (float)(integrity / segments) : 0f;

        int added = 0;
        foreach (var pair in _strains) if (!_previous.Contains(pair.Key)) added++;
        int kept = 0;
        foreach (var id in _previous) if (_strains.ContainsKey(id)) kept++;
        StrainsNew  = added;
        StrainsLost = _previous.Count - kept;
        _previous.Clear();
        foreach (var pair in _strains) _previous.Add(pair.Key);

        Dominant = 0UL;
        int best = 0;
        ulong signature = 0UL;
        foreach (var pair in _strains)
        {
            if (pair.Value.Hosts < best || (pair.Value.Hosts == best && pair.Key > Dominant)) continue;
            best = pair.Value.Hosts;
            Dominant = pair.Key;
            signature = pair.Value.Signature;
        }

        DominantPrevalence = Hosts > 0 ? (float)best / Hosts : 0f;
        ImmuneDominantShare = 0f;
        if (best == 0 || Hosts == 0) return;

        int immune = 0;
        for (int i = 0; i < entities.Count; i++)
        {
            var host = entities[i]?.Virology;
            if (host == null || host.IsDisposed || !host.IsCreated) continue;
            if (host.IsImmune(signature)) immune++;
        }
        ImmuneDominantShare = (float)immune / Hosts;
    }
}
