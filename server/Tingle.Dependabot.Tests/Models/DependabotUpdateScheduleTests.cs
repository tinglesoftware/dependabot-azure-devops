using System.ComponentModel.DataAnnotations;
using Tingle.Dependabot.Models.Dependabot;
using Xunit;

namespace Tingle.Dependabot.Tests.Models;

public class DependabotUpdateScheduleTests
{
    [Theory]
    [InlineData(DependabotScheduleInterval.Daily, null, null, null, "0 2 * * 1,2,3,4,5")] // default to 02:00
    [InlineData(DependabotScheduleInterval.Daily, "23:30", DependabotScheduleDay.Saturday, null, "30 23 * * 1,2,3,4,5")] // ignores day
    [InlineData(DependabotScheduleInterval.Weekly, "10:00", DependabotScheduleDay.Saturday, null, "0 10 * * 6")]
    [InlineData(DependabotScheduleInterval.Weekly, "15:00", null, null, "0 15 * * 1")] // defaults to Mondays
    [InlineData(DependabotScheduleInterval.Monthly, "17:30", DependabotScheduleDay.Saturday, null, "30 17 1 * *")] // ignores day
    [InlineData(DependabotScheduleInterval.Quarterly, null, null, null, "0 2 1 1,4,7,10 *")]
    [InlineData(DependabotScheduleInterval.Semiannually, null, null, null, "0 2 1 1,7 *")]
    [InlineData(DependabotScheduleInterval.Yearly, null, null, null, "0 2 1 1 *")]
    [InlineData(DependabotScheduleInterval.Cron, null, null, "0 2 * * 1-5", "0 2 * * 1,2,3,4,5")]
    public void GenerateCronSchedule_Works(DependabotScheduleInterval interval,
                                           string? time,
                                           DependabotScheduleDay? day,
                                           string? cronjob,
                                           string expected)
    {
        var schedule = new DependabotUpdateSchedule { Interval = interval, };
        if (time != null) schedule.Time = TimeOnly.Parse(time);
        if (day != null) schedule.Day = day;
        if (cronjob != null) schedule.Cronjob = cronjob;
        var actual = schedule.GenerateCron();
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Validation_Works()
    {
        var schedule = new DependabotUpdateSchedule { Interval = DependabotScheduleInterval.Cron, Cronjob = "0 2 * * 1-5" };

        // works as expected
        var results = new List<ValidationResult>();
        var actual = RecursiveValidator.TryValidateObject(schedule, results);
        Assert.True(actual);
        Assert.Empty(results);

        // fails: Cronjob not provided
        schedule.Cronjob = null;
        results = [];
        actual = RecursiveValidator.TryValidateObject(schedule, results);
        Assert.False(actual);
        var val = Assert.Single(results);
        Assert.Equal([nameof(schedule.Interval), nameof(schedule.Cronjob)], val.MemberNames);
        Assert.NotNull(val.ErrorMessage);
        Assert.Equal("'cronjob' must be a valid CRON expression when 'interval' is set to 'cron'", val.ErrorMessage);

        // fails: Cronjob not valid
        schedule.Cronjob = "invalid cron";
        results = [];
        actual = RecursiveValidator.TryValidateObject(schedule, results);
        Assert.False(actual);
        val = Assert.Single(results);
        Assert.Equal([nameof(schedule.Interval), nameof(schedule.Cronjob)], val.MemberNames);
        Assert.NotNull(val.ErrorMessage);
        Assert.Equal("'cronjob' must be a valid CRON expression when 'interval' is set to 'cron'", val.ErrorMessage);
    }
}
