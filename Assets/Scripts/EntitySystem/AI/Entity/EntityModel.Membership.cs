public partial class EntityModel
{
    public int GuestOfColonyId { get; private set; }
    public bool IsGuest => GuestOfColonyId != 0;

    internal void SetGuestOf(int colonyId) => GuestOfColonyId = colonyId;

    internal void Rebind(ColonyState colony)
    {
        _plan?.Interrupt();
        InterruptCommand();
        Motor?.Stop();
        Colony = colony;
        Nest = colony.IsWild ? null : colony.Nest;
    }
}
