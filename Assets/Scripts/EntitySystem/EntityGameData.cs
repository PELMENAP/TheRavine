using System.Collections.Generic;
using UnityEngine;

using TheRavine.Events;

namespace TheRavine.EntityControl
{
    public struct EntityGameData : IMainData, ISkillData, IEventBusData, IBelongData
    {
        // main
        public EntityStats stats { get; set; }
        public string name { get; set; }
        public int prefabID { get; set; }
        //skill
        public Dictionary<string, ISkill> skills { get; set; }
        //event bus
        public EventBusByName eventBus { get; set; }
        //belong
        public EntityGameData(string _name, int _prefabID, EntityStats _stats)
        {
            stats = _stats;
            name = _name;
            prefabID = _prefabID;
            skills = new Dictionary<string, ISkill>();
            eventBus = new EventBusByName();
        }

        public EntityGameData(EntityInfo info)
        {
            stats = new EntityStats(info.statsInfo);
            name = info.Name;
            prefabID = info.prefab.GetInstanceID();
            skills = new Dictionary<string, ISkill>();
            eventBus = new EventBusByName();
        }
    }


    public struct MobGameData : IMainData, IBelongData
    {
        // main
        public EntityStats stats { get; set; }
        public string name { get; set; }
        public int prefabID { get; set; }
        //belong
        public MobGameData(string _name, int _prefabID, EntityStats _stats)
        {
            stats = _stats;
            name = _name;
            prefabID = _prefabID;
        }

        public MobGameData(EntityInfo info)
        {
            stats = new EntityStats(info.statsInfo);
            name = info.Name;
            prefabID = info.prefab.GetInstanceID();
        }
    }
}