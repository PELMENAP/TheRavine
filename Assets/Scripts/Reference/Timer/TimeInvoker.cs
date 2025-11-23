using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;


namespace TheRavine.Base
{
	public class TimeInvoker : MonoBehaviour
	{
		public event Action<float> OnUpdateTimeTickedEvent;
		public event Action<float> OnUpdateTimeUnscaledTickedEvent;
		public event Action OnOneSyncedSecondTickedEvent;
		public event Action OnOneSyncedSecondUnscaledTickedEvent;

		public static TimeInvoker instance
		{
			get
			{
				if (_instance == null)
				{
					var go = new GameObject("[TIME INVOKER]")
					{
						isStatic = true
					};
					_instance = go.AddComponent<TimeInvoker>();
					DontDestroyOnLoad(go);
				}
				return _instance;
			}
		}

		private static TimeInvoker _instance;
		private CancellationTokenSource cts;

		private void Start()
		{
			cts = new CancellationTokenSource();
			UpdateTimeTickAsync(cts.Token).Forget();
			UpdateOneSecondTickAsync(cts.Token).Forget();
			UpdateOneSecondUnscaledTickAsync(cts.Token).Forget();
		}

		private const double onesec = 1.0 / 60.0;
		private async UniTaskVoid UpdateTimeTickAsync(CancellationToken cancellationToken)
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				float deltaTime = Time.deltaTime;
				float unscaledDeltaTime = Time.unscaledDeltaTime;
				OnUpdateTimeTickedEvent?.Invoke(deltaTime);
				OnUpdateTimeUnscaledTickedEvent?.Invoke(unscaledDeltaTime);
				await UniTask.Delay(TimeSpan.FromSeconds(onesec), cancellationToken: cancellationToken);
			}
		}

		private async UniTaskVoid UpdateOneSecondTickAsync(CancellationToken cancellationToken)
		{
			await UniTask.Delay(TimeSpan.FromSeconds(1.0), cancellationToken: cancellationToken);
			while (!cancellationToken.IsCancellationRequested)
			{
				OnOneSyncedSecondTickedEvent?.Invoke();
				await UniTask.Delay(TimeSpan.FromSeconds(1.0), cancellationToken: cancellationToken);
			}
		}

		private async UniTaskVoid UpdateOneSecondUnscaledTickAsync(CancellationToken cancellationToken)
		{
			await UniTask.Delay(TimeSpan.FromSeconds(1.0), ignoreTimeScale: true, cancellationToken: cancellationToken);
			while (!cancellationToken.IsCancellationRequested)
			{
				OnOneSyncedSecondUnscaledTickedEvent?.Invoke();
				await UniTask.Delay(TimeSpan.FromSeconds(1.0), ignoreTimeScale: true, cancellationToken: cancellationToken);
			}
		}

		private void OnDestroy()
		{
			if (cts != null)
			{
				cts.Cancel();
				cts.Dispose();
				cts = null;
			}
		}
	}
}