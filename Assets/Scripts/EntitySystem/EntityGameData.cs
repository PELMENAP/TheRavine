using System.Collections.Generic;
using UnityEngine;

using TheRavine.Events;
public class EntityGameData : IMainData, ISkillData, IEventBusData, IExistData
{
    // main
    public EntityStats stats { get; set; }
    public string name { get; set; }
    public int prefabID { get; set; }
    //skill
    public Dictionary<string, ISkill> skills { get; set; }
    //event bus
    public EventBusByName eventBus { get; set; }
    //exist
    public Vector2 position { get; set; }
    public EntityGameData(string _name, int _prefabID, EntityStats _stats)
    {
        stats = _stats;
        name = _name;
        prefabID = _prefabID;
        skills = new Dictionary<string, ISkill>();
        eventBus = new EventBusByName();
    }
}