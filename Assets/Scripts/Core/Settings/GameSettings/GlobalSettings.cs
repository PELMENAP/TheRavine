using MemoryPack;
using System;
namespace TheRavine.Base
{
    [MemoryPackable]
    public partial class GlobalSettings : IEquatable<GlobalSettings>
    {
        public int qualityLevel = 2;
        public bool enableShadows = true;
        public bool enableParticles = true;
        public bool enableProfiling = false;
        public ControlType controlType = ControlType.Personal;


        // GPU settings
        public bool enableGrass = false;
        public bool enableGrassShadows = false;
        public int grassDensityFactor = 5;

        public bool fullscreen = true;
        public int resolutionWidth;
        public int resolutionHeight;
        public bool vSync = false;
        public int targetFps = 60;

        public float masterVolume = 1f;
        public float musicVolume = 1f;
        public float sfxVolume = 1f;
        

        public void CopyFrom(GlobalSettings other)
        {
            qualityLevel = other.qualityLevel;
            enableShadows = other.enableShadows;
            enableParticles = other.enableParticles;
            enableProfiling = other.enableProfiling;
            enableGrass = other.enableGrass;
            enableGrassShadows = other.enableGrassShadows;
            grassDensityFactor = other.grassDensityFactor;
            controlType = other.controlType;
            fullscreen = other.fullscreen;
            resolutionWidth = other.resolutionWidth;
            resolutionHeight = other.resolutionHeight;
            vSync = other.vSync;
            targetFps = other.targetFps;
            masterVolume = other.masterVolume;
            musicVolume = other.musicVolume;
            sfxVolume = other.sfxVolume;
        }

        public GlobalSettings Clone()
        {
            var clone = new GlobalSettings();
            clone.CopyFrom(this);
            return clone;
        }

        public bool Equals(GlobalSettings other)
        {
            if (other == null) return false;
            
            return qualityLevel == other.qualityLevel &&
                   enableShadows == other.enableShadows &&
                   enableParticles == other.enableParticles &&
                   enableProfiling == other.enableProfiling &&
                   enableGrass == other.enableGrass &&
                   enableGrassShadows == other.enableGrassShadows &&
                   grassDensityFactor == other.grassDensityFactor &&
                   controlType == other.controlType &&
                   fullscreen == other.fullscreen &&
                   resolutionWidth == other.resolutionWidth &&
                   resolutionHeight == other.resolutionHeight &&
                   vSync == other.vSync &&
                   targetFps == other.targetFps &&
                   masterVolume == other.masterVolume &&
                   musicVolume == other.musicVolume &&
                   sfxVolume == other.sfxVolume;
        }
    }

    public enum ControlType
    {
        Personal,
        Mobile
    }
}