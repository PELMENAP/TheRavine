using UnityEngine;

namespace TheRavine.Services
{
    public class ServiceLocator
    {
        private System.Collections.Generic.Dictionary<System.Type, MonoBehaviour> services = new System.Collections.Generic.Dictionary<System.Type, MonoBehaviour>();
        private System.Type playerType;
        public bool Register<T>(T service) where T : MonoBehaviour
        {
            System.Type type = typeof(T);
            if (!services.ContainsKey(type))
                services[type] = service;
            else
                return false;
            return true;
        }
        public void RegisterPlayer<T>() where T : MonoBehaviour => playerType = typeof(T);
        public T GetService<T>() where T : MonoBehaviour
        {
            System.Type type = typeof(T);
            if (services.ContainsKey(type))
                return services[type] as T;
            else
                return null;
        }

        public Transform GetPlayerTransform() => services[playerType].transform;

        public void Dispose()
        {
            services.Clear();
        }
    }
}