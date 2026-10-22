public struct TickWindowBudget
{
    private double _debt;

    public double Debt => _debt;

    public int Take(float dt, float interval, int count, int remaining)
    {
        if (count <= 0)
        {
            _debt = 0d;
            return 0;
        }
        if (!(dt > 0f) || remaining <= 0) return 0;

        double step = interval > SimulationRules.MinAgentTickInterval ? interval : SimulationRules.MinAgentTickInterval;
        _debt += dt * (double)count / step;
        if (_debt > count) _debt = count;

        int take = (int)_debt;
        if (take > remaining) take = remaining;
        _debt -= take;
        return take;
    }

    public void Reset() => _debt = 0d;
}
