public interface ISkillGraphCommands
{
    bool AutoMode { get; }
    bool CanBuild(int u, int v);
    bool Build(int u, int v);
    bool CanPrune(int u, int v);
    bool Prune(int u, int v);
    bool CanRestore(int u, int v);
    bool Restore(int u, int v);
    bool ToggleAutoMode();
}
