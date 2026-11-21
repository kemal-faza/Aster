namespace Aster.Core.Water;

public readonly record struct WaterConfig(
    bool Enabled,
    int IntervalMinutes,
    int StartHour,
    int EndHour,
    int Goal);
