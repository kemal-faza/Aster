namespace Aster.Core.Focus;

public readonly record struct FocusDurations(
    TimeSpan Focus,
    TimeSpan ShortBreak,
    TimeSpan LongBreak)
{
    public static FocusDurations FromMinutes(int focus, int shortBreak, int longBreak) =>
        new(
            TimeSpan.FromMinutes(Math.Clamp(focus, 1, 180)),
            TimeSpan.FromMinutes(Math.Clamp(shortBreak, 1, 180)),
            TimeSpan.FromMinutes(Math.Clamp(longBreak, 1, 180)));

    public TimeSpan For(FocusPhase phase) => phase switch
    {
        FocusPhase.ShortBreak => ShortBreak,
        FocusPhase.LongBreak => LongBreak,
        _ => Focus
    };
}
