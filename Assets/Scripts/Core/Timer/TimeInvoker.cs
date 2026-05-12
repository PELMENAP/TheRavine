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
					var go = new GameObject("[TIME INVOKER]") { isStatic = true };
					_instance = go.AddComponent<TimeInvoker>();
					DontDestroyOnLoad(go);
				}
				return _instance;
			}
		}

		private static TimeInvoker _instance;
		private CancellationTokenSource _cts;
		private float _secondAccumulator;
		private float _secondUnscaledAccumulator;

		private void Start()
		{
			_cts = new CancellationTokenSource();
		}

		private void Update()
		{
			float dt = Time.deltaTime;
			float udt = Time.unscaledDeltaTime;

			OnUpdateTimeTickedEvent?.Invoke(dt);
			OnUpdateTimeUnscaledTickedEvent?.Invoke(udt);

			_secondAccumulator += dt;
			_secondUnscaledAccumulator += udt;

			if (_secondAccumulator >= 1f)
			{
				_secondAccumulator -= 1f;
				OnOneSyncedSecondTickedEvent?.Invoke();
			}

			if (_secondUnscaledAccumulator >= 1f)
			{
				_secondUnscaledAccumulator -= 1f;
				OnOneSyncedSecondUnscaledTickedEvent?.Invoke();
			}
		}

		private void OnDestroy()
		{
			_cts?.Cancel();
			_cts?.Dispose();
		}
	}
}