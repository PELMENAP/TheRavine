using UnityEngine;
using System.Collections.Generic;
using System.Reflection;

namespace TheRavine.Base
{
    public class ServiceRegisterMachine
    {
        private readonly Queue<ISetAble> disAble = new();
        public Queue<ISetAble> RegisterSomeServices(MonoBehaviour[] scripts)
        {
            Queue<ISetAble> someServices = new();
            
            for (byte i = 0; i < scripts.Length; i++)
            {
                if(scripts[i] == null) continue;
                System.Type serviceType = scripts[i].GetType();
                MethodInfo registerMethod = typeof(ServiceLocator).GetMethod("Register").MakeGenericMethod(new System.Type[] { serviceType });
                registerMethod.Invoke(null, new object[] { scripts[i] });
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
                setAble.SetUp(() => StartNewServices(services, callback));
            }
        }
        
        public void BreakUpServices()
        {
            if(disAble.Count > 0) disAble.Dequeue().BreakUp(() => BreakUpServices());
        }
    }
}