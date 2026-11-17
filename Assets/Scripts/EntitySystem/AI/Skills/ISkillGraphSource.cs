public interface ISkillGraphSource
{
    SkillAtlas Atlas { get; }
    SkillGraphTopology Topology { get; }
    SkillGraphStore Store { get; }
    int StoreIndex { get; }
    int StateVersion { get; }
    bool AutoMode { get; }
}
