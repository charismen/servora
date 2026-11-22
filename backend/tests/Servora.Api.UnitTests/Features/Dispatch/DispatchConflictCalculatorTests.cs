using FluentAssertions;
using Servora.Api.Features.Dispatch;
using Xunit;

namespace Servora.Api.UnitTests.Features.Dispatch;

public class DispatchConflictCalculatorTests
{
    private static DateTimeOffset Utc(int hour, int minute = 0)
        => new(2026, 11, 23, hour, minute, 0, TimeSpan.Zero);

    [Fact]
    public void AdjacentIntervals_DoNotConflict()
    {
        var existing = new[]
        {
            new ScheduledTicketSlot(1, "SRV-001", "Alice", "AC Split", AssignedTechnicianUserId: 5, ServiceResourceId: 2, Start: Utc(9), End: Utc(10))
        };

        var conflicts = DispatchConflictCalculator.EvaluateConflicts(
            new ScheduleCandidate(ExcludeTicketId: 2, AssignedTechnicianUserId: 5, ServiceResourceId: 2, Start: Utc(10), End: Utc(11)),
            existing);

        conflicts.Should().BeEmpty();
    }

    [Fact]
    public void OverlappingSlot_ClassifiesTechnicianResourceAndBoth()
    {
        var existing = new[]
        {
            new ScheduledTicketSlot(1, "SRV-001", "Alice", "Car A", AssignedTechnicianUserId: 10, ServiceResourceId: 1, Start: Utc(9), End: Utc(11)),
            new ScheduledTicketSlot(2, "SRV-002", "Bob", "Car B", AssignedTechnicianUserId: 11, ServiceResourceId: 2, Start: Utc(9, 30), End: Utc(10, 30)),
            new ScheduledTicketSlot(3, "SRV-003", "Cara", "Car C", AssignedTechnicianUserId: 10, ServiceResourceId: 2, Start: Utc(10), End: Utc(12))
        };

        // Candidate overlaps tech 10 + resource 2 from 10:00 to 11:00
        var conflicts = DispatchConflictCalculator.EvaluateConflicts(
            new ScheduleCandidate(ExcludeTicketId: 99, AssignedTechnicianUserId: 10, ServiceResourceId: 2, Start: Utc(10), End: Utc(11)),
            existing);

        conflicts.Should().HaveCount(3);
        conflicts.Single(c => c.TicketId == 1).ConflictType.Should().Be("Technician");
        conflicts.Single(c => c.TicketId == 2).ConflictType.Should().Be("Resource");
        conflicts.Single(c => c.TicketId == 3).ConflictType.Should().Be("Both");
    }

    [Fact]
    public void NullEndTime_DefaultsToSixtyMinutes_AndExcludesSelf()
    {
        var existing = new[]
        {
            new ScheduledTicketSlot(10, "SRV-010", "Self", "Laptop", AssignedTechnicianUserId: 3, ServiceResourceId: null, Start: Utc(14), End: null),
            new ScheduledTicketSlot(11, "SRV-011", "Other", "Laptop", AssignedTechnicianUserId: 3, ServiceResourceId: null, Start: Utc(14), End: null)
        };

        var conflicts = DispatchConflictCalculator.EvaluateConflicts(
            new ScheduleCandidate(ExcludeTicketId: 10, AssignedTechnicianUserId: 3, ServiceResourceId: null, Start: Utc(14, 30), End: null),
            existing);

        conflicts.Should().ContainSingle(c => c.TicketId == 11 && c.ConflictType == "Technician");
        conflicts[0].ScheduledVisitEnd.Should().Be(Utc(15));
    }
}
