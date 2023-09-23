using UnityEngine;
using UnityEngine.Events;

namespace Modern2D
{

	//used for variables that don't need to be updated every frame
	[System.Serializable]
	public class Cryo<T> 
	{
        public static implicit operator T(Cryo<T> val) => (T)val.value;
        public static implicit operator Cryo<T>(T val) => new Cryo<T>(val);

        [SerializeField]
		private T _value;

		[SerializeField]
		public T value
		{
			get { return _value; }
			set
			{
				T oldVal = _value;
                _value = value;
                if (!value.Equals(oldVal) && onValueChanged != null)
					onValueChanged.Invoke();
			}
		}
		public UnityAction onValueChanged;

        public Cryo(T value, UnityAction onValueChanged)
        {
            this.value = value;
            this.onValueChanged = onValueChanged;
        }

		public Cryo(T value)
		{
			this.value = value;
		}
	}

}