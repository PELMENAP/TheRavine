public interface ISetAble
{
    delegate void Callback();
    void SetUp(Callback callback);
    void BreakUp(Callback callback);
}
