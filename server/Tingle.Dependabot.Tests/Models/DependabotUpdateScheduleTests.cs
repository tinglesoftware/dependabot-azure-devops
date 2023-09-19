using Tingle.Dependabot.Models.Dependabot;
using Xunit;

namespace Tingle.Dependabot.Tests.Models;

public class DependabotUpdateScheduleTests
{
    [Theory]
    [InlineData(DependabotScheduleInterval.Daily, null, null, "00 02 * * 1-5")] // default to 02:00
    [InlineData(DependabotScheduleInterval.Daily, "23:30", DependabotScheduleDay.Saturday, "30 23 * * 1-5")] // ignores day
    [InlineData(DependabotScheduleInterval.Weekly, "10:00", DependabotScheduleDay.Saturday, "00 10 * * 6")]
    [InlineData(DependabotScheduleInterval.Weekly, "15:00", null, "00 15 * * 1")] // defaults to Mondays
    [InlineData(DependabotScheduleInterval.Monthly, "17:30", DependabotScheduleDay.Saturday, "30 17 1 * *")] // ignores day
    public void GenerateCronSchedule_Works(DependabotScheduleInterval interval, string time, DependabotScheduleDay? day, string expected)
    {
        var schedule = new DependabotUpdateSchedule { Interval = interval, };
        if (time != null) schedule.Time = TimeOnly.Parse(time);
        if (day != null) schedule.Day = day;
        var actual = schedule.GenerateCron();
        Assert.Equal(expected, actual);
    }
}
