using System.ComponentModel.DataAnnotations;
using Tingle.Dependabot.Models.Dependabot;
using Xunit;

namespace Tingle.Dependabot.Tests.Models;

public class DependabotUpdateTests
{
    [Fact]
    public void Validation_Works()
    {
        var update = new DependabotUpdate
        {
            PackageEcosystem = "npm",
            Directory = "/",
            Directories = null,
            Schedule = new DependabotUpdateSchedule
            {
                Interval = DependabotScheduleInterval.Monthly,
                Time = new(2, 0),
            },
        };

        // works as expected
        var results = new List<ValidationResult>();
        var actual = RecursiveValidator.TryValidateObject(update, results);
        Assert.True(actual);
        Assert.Empty(results);

        // fails: directory and directories not provided
        update.Directory = null;
        update.Directories = null;
        results = [];
        actual = RecursiveValidator.TryValidateObject(update, results);
        Assert.False(actual);
        var val = Assert.Single(results);
        Assert.Equal([nameof(update.Directory), nameof(update.Directories)], val.MemberNames);
        Assert.NotNull(val.ErrorMessage);
        Assert.Equal("Either 'directory' or 'directories' must be provided", val.ErrorMessage);
    }
}
