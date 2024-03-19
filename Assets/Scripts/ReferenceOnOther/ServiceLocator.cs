using UnityEngine;

namespace TheRavine.Services
{
    public class ServiceLocator
    {
        private System.Collections.Generic.Dictionary<System.Type, MonoBehaviour> services = new System.Collections.Generic.Dictionary<System.Type, MonoBehaviour>();
        private Transform playerTransform;
        public bool Register<T>(T service) where T : MonoBehaviour
        {
            System.Type type = typeof(T);
            if (!services.ContainsKey(type))
                services[type] = service;
            else
                return false;
            return true;
        }
        public void RegisterPlayer<T>() where T : MonoBehaviour => playerTransform = services[typeof(T)].transform;
        public T GetService<T>() where T : MonoBehaviour
        {
            System.Type type = typeof(T);
            if (services.ContainsKey(type))
                return services[type] as T;
            else
                return null;
        }

        public Transform GetPlayerTransform() => playerTransform;

        public void Dispose()
        {
            services.Clear();
        }
    }
}