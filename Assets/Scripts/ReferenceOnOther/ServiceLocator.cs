using UnityEngine;
using System.Collections.Generic;

namespace TheRavine.Services
{
    public class ServiceLocator
    {
        private Dictionary<System.Type, MonoBehaviour> services = new Dictionary<System.Type, MonoBehaviour>();
        private ILogger logger;
        private List<Transform> playersTransforms = new List<Transform>();
        public bool Register<T>(T service) where T : MonoBehaviour
        {
            System.Type type = typeof(T);
            if (!services.ContainsKey(type))
                services[type] = service;
            else
                return false;
            return true;
        }

        public void RegisterLogger(ILogger logger)
        {
            this.logger = logger;
        }
        public void RegisterPlayer<T>(T service) where T : MonoBehaviour
        {
            playersTransforms.Add(service.transform);
        }
        public T GetService<T>() where T : MonoBehaviour
        {
            System.Type type = typeof(T);
            if (services.ContainsKey(type))
                return services[type] as T;
            else
                return null;
        }

        public ILogger GetLogger() => logger;

        public Transform GetPlayerTransform() => playersTransforms[0];
        public List<Transform> GetPlayersTransforms() => playersTransforms;

        public void Dispose()
        {
            services.Clear();
        }
    }
}