using FluentAssertions;
using Servora.Api.Features.Devices;
using Servora.Api.Features.Maintenance;
using Xunit;

namespace Servora.Api.UnitTests.Features.Maintenance;

public class PreventiveMaintenanceScheduleTests
{
    [Theory]
    [InlineData(MaintenanceIntervalUnit.Days, 15, 15)]
    [InlineData(MaintenanceIntervalUnit.Weeks, 2, 14)]
    public void AdvanceNextDue_CalendarDaysAndWeeks_ComputesExpectedDate(
        MaintenanceIntervalUnit unit,
        int value,
        int expectedDaysDelta)
    {
        var completedAt = new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);
        var schedule = new MaintenanceSchedule
        {
            TriggerType = MaintenanceTriggerType.Calendar,
            IntervalUnit = unit,
            IntervalValue = value
        };

        PreventiveMaintenanceJob.AdvanceNextDue(schedule, completedAt);

        schedule.LastCompletedAt.Should().Be(completedAt);
        schedule.NextDueAt.Should().Be(completedAt.AddDays(expectedDaysDelta));
    }

    [Fact]
    public void AdvanceNextDue_BothCalendarAndMeter_AdvancesBothThresholds()
    {
        var completedAt = new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);
        var schedule = new MaintenanceSchedule
        {
            TriggerType = MaintenanceTriggerType.Both,
            IntervalUnit = MaintenanceIntervalUnit.Months,
            IntervalValue = 3,
            MeterInterval = 5000m,
            ServiceAsset = new Device { MeterReading = 42500m, MeterUnit = "km" }
        };

        PreventiveMaintenanceJob.AdvanceNextDue(schedule, completedAt);

        schedule.NextDueAt.Should().Be(completedAt.AddMonths(3));
        schedule.LastCompletedMeter.Should().Be(42500m);
        schedule.NextDueMeter.Should().Be(47500m);
    }
}
