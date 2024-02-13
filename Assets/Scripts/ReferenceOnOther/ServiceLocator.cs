using UnityEngine;
using System;
using System.Collections.Generic;

namespace TheRavine.Services
{
    public class ServiceLocator
    {
        private Dictionary<Type, MonoBehaviour> services = new Dictionary<Type, MonoBehaviour>();
        private Type playerType;
        public bool Register<T>(T service) where T : MonoBehaviour
        {
            Type type = typeof(T);
            if (!services.ContainsKey(type))
                services[type] = service;
            else
                return false;
            return true;
        }
        public void RegisterPlayer<T>() where T : MonoBehaviour => playerType = typeof(T);
        public T GetService<T>() where T : MonoBehaviour
        {
            Type type = typeof(T);
            if (services.ContainsKey(type))
                return services[type] as T;
            else
                return null;
        }

        public Transform GetPlayerTransform() => services[playerType].transform;
    }
}