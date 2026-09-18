using System.Collections.Generic;
using UnityEngine;

namespace TheRavine.EntityControl.Virology
{
    public class VirologyProbe : MonoBehaviour
    {
        [SerializeField] private string status;
        [SerializeField] private List<InfectionView> infections = new(4);
        [SerializeField] private float viralNetFitnessDelta;
        [SerializeField] private int viralCount;
        [SerializeField] private ProteinAction lastAction;
        [SerializeField] private float lastAmp;
        [SerializeField] private bool lastExecuted;
        [SerializeField] private int head;
        [SerializeField] private int tapeLength;
        [SerializeField] private int executedCodons;
        [SerializeField] private int failedCodons;
        [SerializeField] private float sharpness;
        [SerializeField] private uint tick;

        public IReadOnlyList<InfectionView> Infections => infections;
        public float NetFitnessDelta => viralNetFitnessDelta;
        public bool HasSegments => viralCount > 0;

        public void Capture(VirologyComponent virology, bool forceRebuild)
        {
            if (virology == null) { Clear("no VirologyComponent"); return; }
            if (virology.IsDisposed) { Clear("disposed"); return; }
            if (!virology.IsCreated) { Clear("not created (VirologyRuntime not ready)"); return; }

            status = "live";
            lastAction = virology.LastAction;
            lastAmp = virology.LastAmp;
            lastExecuted = virology.LastExecuted;
            head = virology.Head;
            tapeLength = virology.TapeLength;
            executedCodons = virology.ExecutedCodons;
            failedCodons = virology.FailedCodons;
            sharpness = virology.Sharpness;

            bool rebuild = forceRebuild || virology.SegmentsDirty;
            if (!rebuild && (virology.ValuesDirty || virology.TickCount != tick))
                rebuild = !virology.RefreshInfectionValues(infections);
            if (rebuild) virology.BuildInfectionViews(infections);

            virology.ConsumeSegmentsDirty();
            virology.ConsumeValuesDirty();
            tick = virology.TickCount;

            float sum = 0f;
            int count = 0;
            
            for (int i = 0; i < infections.Count; i++)
            {
                var v = infections[i];
                if (v.IsEndogenous) continue;
                sum += v.NetFitnessDelta;
                count++;
            }
            viralNetFitnessDelta = sum;
            viralCount = count;
        }

        public void Clear(string reason = "unbound")
        {
            status = reason;
            infections.Clear();
            viralNetFitnessDelta = 0f;
            viralCount = 0;
            tick = 0u;
        }
    }
}