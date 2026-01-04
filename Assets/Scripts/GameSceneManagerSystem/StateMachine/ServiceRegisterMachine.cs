using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using System;

using TheRavine.Extensions;

namespace TheRavine.Base
{
    public class ServiceRegisterMachine
    {
        private readonly IRavineLogger ravineLogger;
        private readonly Queue<Pair<ISetAble, string>> disAble = new();
        public ServiceRegisterMachine(IRavineLogger ravineLogger)
        {
            this.ravineLogger = ravineLogger;
        }
        public Queue<Pair<ISetAble, string>> RegisterSomeServices(MonoBehaviour[] scripts)
        {
            Queue<Pair<ISetAble, string>> someServices = new();
            // var servicesType = ServiceLocator.Services.GetType();

            for (byte i = 0; i < scripts.Length; i++)
            {
                // if (scripts[i] == null) continue;
                // Type serviceType = scripts[i].GetType();

                // MethodInfo registerMethod = servicesType
                //     .GetMethod("Register")
                //     .MakeGenericMethod(serviceType);
                // registerMethod.Invoke(ServiceLocator.Services, new object[] { scripts[i] });
                someServices.Enqueue(new Pair<ISetAble, string>((ISetAble)scripts[i], scripts[i].name));
            }

            return someServices;
        }

        public void StartNewServices(Queue<Pair<ISetAble, string>> services, ISetAble.Callback callback)
        {
            if (services.Count <= 0) callback?.Invoke();
            else
            {
                Pair<ISetAble, string> setAble = services.Dequeue();
                disAble.Enqueue(setAble);
                try
                {
                    setAble.First.SetUp(() => StartNewServices(services, callback));
                }
                catch (Exception ex)
                {
                    ravineLogger.LogError($"Service {setAble.Second} cannot be started: {ex.Message}");
                    StartNewServices(services, callback);
                }
            }
        }
        
        public void BreakUpServices()
        {
            if(disAble.Count > 0)
            {
                Pair<ISetAble, string> setAble = disAble.Dequeue();
                try
                {
                    setAble.First.BreakUp(() => BreakUpServices());
                }
                catch (Exception ex)
                {
                    ravineLogger.LogError($"Service {setAble.Second} cannot be broken up: {ex.Message}");
                    BreakUpServices();
                }
            }
        }
    }
}