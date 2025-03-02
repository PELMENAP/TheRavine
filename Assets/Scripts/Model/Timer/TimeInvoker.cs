using System;
using UnityEngine;

namespace TheRavine.Base
{
	public class TimeInvoker : MonoBehaviour
	{
		public event Action<float> OnUpdateTimeTickedEvent;
		public event Action<float> OnUpdateTimeUnscaledTickedEvent;
		public event Action OnOneSyncedSecondTickedEvent;
		public event Action OnOneSyncedSecondUnscaledTickedEvent;

		public static TimeInvoker Instance
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

		private void Start()
		{
			InvokeRepeating(nameof(UpdateTimeTick), 0f, 1f / 60f);
			InvokeRepeating(nameof(UpdateOneSecondTick), 1f, 1f);
			InvokeRepeating(nameof(UpdateOneSecondUnscaledTick), 1f, 1f);
		}

		private void UpdateTimeTick()
		{
			float deltaTime = Time.deltaTime;
			float unscaledDeltaTime = Time.unscaledDeltaTime;

			OnUpdateTimeTickedEvent?.Invoke(deltaTime);
			OnUpdateTimeUnscaledTickedEvent?.Invoke(unscaledDeltaTime);
		}

		private void UpdateOneSecondTick()
		{
			OnOneSyncedSecondTickedEvent?.Invoke();
		}

		private void UpdateOneSecondUnscaledTick()
		{
			OnOneSyncedSecondUnscaledTickedEvent?.Invoke();
		}

		private void OnDestroy()
		{
			CancelInvoke();
		}
	}
}