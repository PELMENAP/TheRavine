using UnityEngine;
using System.Collections.Generic;
using System.Reflection;

using TheRavine.Services;

namespace TheRavine.Base
{
    public class ServiceRegisterMachine
    {
        private ServiceLocator serviceLocator;
        private Queue<ISetAble> disAble;
        public ServiceRegisterMachine(ServiceLocatorAccess serviceLocatorAccess)
        {
            disAble = new Queue<ISetAble>();
            serviceLocator = new ServiceLocator();

            if(serviceLocatorAccess != null) serviceLocatorAccess.serviceLocator = serviceLocator;
            else Debug.Log("There's not service locator accesses on the scene!");
        }

        public Queue<ISetAble> RegisterSomeServices(MonoBehaviour[] scripts)
        {
            Queue<ISetAble> someServices = new Queue<ISetAble>();
            
            for (byte i = 0; i < scripts.Length; i++)
            {
                if(scripts[i] == null) continue;
                System.Type serviceType = scripts[i].GetType();
                MethodInfo registerMethod = typeof(ServiceLocator).GetMethod("Register").MakeGenericMethod(new System.Type[] { serviceType });
                registerMethod.Invoke(serviceLocator, new object[] { scripts[i] });
                someServices.Enqueue((ISetAble)scripts[i]);
            }

            return someServices;
        }

        public void StartNewServices(Queue<ISetAble> services, ISetAble.Callback callback)
        {
            if (services.Count <= 0) callback?.Invoke();
            else
            {
                ISetAble setAble = services.Dequeue();
                disAble.Enqueue(setAble);
                setAble.SetUp(() => StartNewServices(services, callback), serviceLocator);
            }
        }
        
        public void BreakUpServices()
        {
            if(disAble.Count > 0) disAble.Dequeue().BreakUp(() => BreakUpServices());
            else serviceLocator.Dispose();
        }
    }
}