using System;
using UnityEngine;

namespace TheRavine.Generator
{
    public interface IMapEvent : IDisposable
    {
        void Step(double now, float dt);
        void Render(Camera camera);
    }

    public interface IMapSeason
    {
        float Value(double time);
        bool IsWinter(double time);
    }
}
