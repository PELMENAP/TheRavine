using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;

namespace TheRavine.EntityControl.Virology
{
    public class VirologyInspector : MonoBehaviour
    {
        [SerializeField] private float refreshInterval = 0.25f;
        [SerializeField] private bool followSelection = true;
        [SerializeField] private GameObject target;


        private GameObject _bound;
        private GameObject _lastSelection;
        private VirologyProbe _probe;
        private VirologyComponent _virology;
        private bool _forceRebuild;

        private void Start() => PollAsync(this.GetCancellationTokenOnDestroy()).Forget();

        private async UniTaskVoid PollAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                await UniTask.Delay(math.max(50, (int)(refreshInterval * 1000f)), cancellationToken: ct);

                TrackSelection();
                if (target != _bound) Rebind(target);
                if (_probe == null) continue;

                _probe.Capture(_virology, _forceRebuild);
                _forceRebuild = false;
            }
        }

        private void TrackSelection()
        {
#if UNITY_EDITOR
            if (!followSelection) return;
            var selected = UnityEditor.Selection.activeGameObject;
            if (selected == _lastSelection) return;
            _lastSelection = selected;
            if (selected == null) return;

            var vm = selected.GetComponentInParent<EntityViewModel>();
            if (vm != null) target = vm.gameObject;
#endif
        }

        private void Rebind(GameObject go)
        {
            if (_probe != null) _probe.Clear();

            _bound = go;
            _probe = null;
            _virology = null;
            _forceRebuild = true;
            if (go == null) return;

            _probe = go.GetComponentInChildren<VirologyProbe>(true);
            if (go.TryGetComponent(out EntityViewModel vm) && vm.Entity is EntityModel model)
                _virology = model.Virology;
        }
    }
}