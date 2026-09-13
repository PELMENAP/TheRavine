using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace TheRavine.EntityControl.Virology
{
    public class VirologyProbe : MonoBehaviour
    {
        [SerializeField] private float refreshInterval = 0.75f;
        [SerializeField] private List<InfectionView> infections = new(4);
        [SerializeField] private ProteinAction lastAction;
        [SerializeField] private float lastAmp;
        [SerializeField] private bool lastExecuted;
        [SerializeField] private int head;
        [SerializeField] private int tapeLength;
        [SerializeField] private int executedCodons;
        [SerializeField] private int failedCodons;
        [SerializeField] private float sharpness;

        private VirologyComponent _virology;
        private float _netFitnessDelta;
        private bool _hasSegments;

        public IReadOnlyList<InfectionView> Infections => infections;
        public float NetFitnessDelta => _netFitnessDelta;
        public bool HasSegments => _hasSegments;

        public void Bind(EntityModel model)
        {
            _virology = model.Virology;
            if (_virology == null) return;
            PollAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        private async UniTaskVoid PollAsync(CancellationToken ct)
        {
            int delay = Mathf.Max(100, (int)(refreshInterval * 1000f));

            while (!ct.IsCancellationRequested)
            {
                await UniTask.Delay(delay, cancellationToken: ct);
                if (_virology == null || _virology.IsDisposed) return;

                lastAction = _virology.LastAction;
                lastAmp = _virology.LastAmp;
                lastExecuted = _virology.LastExecuted;
                head = _virology.Head;
                tapeLength = _virology.TapeLength;
                executedCodons = _virology.ExecutedCodons;
                failedCodons = _virology.FailedCodons;
                sharpness = _virology.Sharpness;

                if (!_virology.SegmentsDirty) continue;
                _virology.ConsumeSegmentsDirty();

                _virology.BuildInfectionViews(infections);

                float sum = 0f;
                bool viral = false;
                for (int i = 0; i < infections.Count; i++)
                {
                    sum += infections[i].NetFitnessDelta;
                    if (infections[i].StrainLabel != "ENDOGEN") viral = true;
                }
                _netFitnessDelta = sum;
                _hasSegments = viral;
            }
        }
    }
}