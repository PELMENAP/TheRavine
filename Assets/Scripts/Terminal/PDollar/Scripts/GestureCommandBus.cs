using System;

 
namespace TheRavine.Extensions
{
    public static class GestureCommandBus
    {
        public static event Action<string> OnGestureCommand;
 
        public static void Dispatch(string command) => OnGestureCommand?.Invoke(command);
    }
}
 