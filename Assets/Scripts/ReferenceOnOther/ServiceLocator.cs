using UnityEngine;
using System.Collections.Generic;
using System;

namespace TheRavine.Services
{
    public class ServiceLocator
    {
        private Dictionary<Type, MonoBehaviour> services = new Dictionary<Type, MonoBehaviour>();
        private ILogger logger;
        private List<Transform> playersTransforms = new List<Transform>();
        public bool Register<T>(T service) where T : MonoBehaviour
        {
            Type type = typeof(T);
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
            Type type = typeof(T);
            if (services.ContainsKey(type))
                return services[type] as T;
            else
                return null;
        }

        public ILogger GetLogger() => logger;

        public Transform GetPlayerTransform()
        {
            if(playersTransforms.Count == 0)
            {
                logger.LogError("There is no players in the game");
                return null;
            }
            return playersTransforms[0];
        }
        public List<Transform> GetPlayersTransforms() => playersTransforms;

        public void Dispose()
        {
            services.Clear();
        }
    }
}