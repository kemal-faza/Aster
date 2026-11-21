using Aster.Core.Water;

namespace Aster.Core.Tests;

public class WaterScheduleTests
{
    private static DateTimeOffset At(int hour, int minute = 0) =>
        new(2026, 11, 20, hour, minute, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(8, 22, 8, true)]
    [InlineData(8, 22, 21, true)]
    [InlineData(8, 22, 22, false)]
    [InlineData(8, 22, 7, false)]
    public void SameDayActiveHours(int start, int end, int hour, bool expected)
    {
        Assert.Equal(expected, WaterSchedule.IsWithinActiveHours(At(hour), start, end));
    }

    [Theory]
    [InlineData(22, 8, 23, true)]
    [InlineData(22, 8, 2, true)]
    [InlineData(22, 8, 8, false)]
    [InlineData(22, 8, 12, false)]
    public void OvernightActiveHours(int start, int end, int hour, bool expected)
    {
        Assert.Equal(expected, WaterSchedule.IsWithinActiveHours(At(hour), start, end));
    }

    [Fact]
    public void StartEqualsEndMeansTwentyFourHours()
    {
        Assert.True(WaterSchedule.IsWithinActiveHours(At(3), 8, 8));
        Assert.True(WaterSchedule.IsWithinActiveHours(At(23), 8, 8));
    }

    [Fact]
    public void NextActiveStartHandlesBeforeDuringAndAfter()
    {
        Assert.Equal(At(8), WaterSchedule.NextActiveStart(At(6), 8, 22));
        Assert.Equal(At(8), WaterSchedule.NextActiveStart(At(12), 8, 22));
        Assert.Equal(At(8).AddDays(1), WaterSchedule.NextActiveStart(At(23), 8, 22));
        Assert.Equal(At(22), WaterSchedule.NextActiveStart(At(23), 22, 8));
        Assert.Equal(At(22), WaterSchedule.NextActiveStart(At(2), 22, 8));
        Assert.Equal(At(22), WaterSchedule.NextActiveStart(At(12), 22, 8));
    }

    [Fact]
    public void NextReminderIsNullWhenDisabledOrAtGoal()
    {
        var config = new WaterConfig(true, 60, 8, 22, 8);

        Assert.Null(WaterSchedule.ComputeNextReminder(At(9), config with { Enabled = false }, 0));
        Assert.Null(WaterSchedule.ComputeNextReminder(At(9), config, 8));
    }

    [Fact]
    public void NextReminderUsesNowInsideHoursAndStartOutside()
    {
        var config = new WaterConfig(true, 60, 8, 22, 8);

        Assert.Equal(At(10), WaterSchedule.ComputeNextReminder(At(9), config, 0));
        Assert.Equal(At(9), WaterSchedule.ComputeNextReminder(At(7), config, 0));
    }

    [Theory]
    [InlineData(0, 8, 0)]
    [InlineData(4, 8, 4)]
    [InlineData(8, 8, 8)]
    [InlineData(99, 8, 8)]
    [InlineData(1, 16, 0)]
    [InlineData(3, 16, 2)]
    [InlineData(5, 16, 2)]
    public void BarsRoundLikeWpf(int glasses, int goal, int expected)
    {
        Assert.Equal(expected, WaterSchedule.FilledBars(glasses, goal));
    }

    [Fact]
    public void BarsHandleInvalidGoalAndBarCount()
    {
        Assert.Equal(0, WaterSchedule.FilledBars(1, 0));
        Assert.Equal(0, WaterSchedule.FilledBars(1, 8, 0));
    }

    [Fact]
    public void TodayKeyIsInvariant()
    {
        Assert.Equal("2026-11-20", WaterSchedule.TodayKey(At(23, 59)));
    }
}
