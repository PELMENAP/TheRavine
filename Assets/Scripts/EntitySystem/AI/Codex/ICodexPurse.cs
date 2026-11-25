public interface ICodexPurse
{
    float Balance(int authorId);
    bool TryDebit(int authorId, float amount);
}
