using Servora.Api.Features.Attendance;

namespace Servora.Api.Features.Payroll.Bonuses;

public interface IBonusEvaluator
{
    string BonusTypeCode { get; }
    Task<BonusEvaluationResult> EvaluateAsync(
        BonusEvaluationContext context,
        string parametersJson,
        CancellationToken cancellationToken);
}

public sealed record BonusEvaluationContext(
    int ShopId,
    int ShopUserId,
    int UserId,
    DateOnly StartDate,
    DateOnly EndDate,
    DateTimeOffset StartDateTimeUtc,
    DateTimeOffset EndDateTimeExclusiveUtc,
    IReadOnlyCollection<AttendanceRecord> ApprovedAttendances);

public sealed record BonusEvaluationResult(
    string BonusTypeCode,
    string BonusName,
    int MetricCount,
    int QualifiedCount,
    decimal UnitRate,
    decimal FlatReward,
    decimal TotalBonusAmount,
    string FormulaSummary,
    IReadOnlyList<string> ReferenceNumbers);

