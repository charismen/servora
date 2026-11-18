using Microsoft.EntityFrameworkCore;
using Servora.Api.Features.Attendance;
using Servora.Api.Features.Payroll.Bonuses;
using Servora.Api.Features.Shops;
using Servora.Api.Infrastructure.Data;

namespace Servora.Api.Features.Payroll;

public sealed record PayrollAdjustmentDto(
    int Id,
    int ShopUserId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal Amount,
    string Reason,
    DateTimeOffset CreatedAt);

public sealed record WorkerPayrollRowDto(
    int ShopUserId,
    int UserId,
    string FullName,
    string Email,
    string EmploymentType,
    IReadOnlyList<string> Roles,
    string BasePayMode,
    decimal BaseHourlyRate,
    int TotalDaysWorked,
    decimal TotalHoursWorked,
    int PendingShiftCount,
    decimal BaseCalculatedWage,
    IReadOnlyList<TierBreakdownItemDto> TierBreakdown,
    decimal TotalBonusAmount,
    IReadOnlyList<BonusEvaluationResult> BonusBreakdown,
    decimal TotalAdjustments,
    IReadOnlyList<PayrollAdjustmentDto> Adjustments,
    decimal FinalPayableAmount);

public sealed record PayrollSummaryTotalsDto(
    int TotalFreelanceWorkers,
    int TotalDaysWorked,
    decimal TotalApprovedHours,
    int TotalPendingShifts,
    decimal TotalBaseWages,
    decimal TotalBonuses,
    decimal TotalAdjustments,
    decimal TotalPayableAmount);

public sealed record PayrollSummaryDto(
    DateOnly From,
    DateOnly To,
    string Cycle,
    PayrollSummaryTotalsDto Totals,
    IReadOnlyList<WorkerPayrollRowDto> Workers);

public sealed class PayrollCalculationService(
    AppDbContext dbContext,
    IEnumerable<IBonusEvaluator> bonusEvaluators)
{
    private readonly IReadOnlyDictionary<string, IBonusEvaluator> _evaluatorsByCode =
        bonusEvaluators.ToDictionary(evaluator => evaluator.BonusTypeCode, StringComparer.OrdinalIgnoreCase);

    public async Task<PayrollSummaryDto> CalculateSummaryAsync(
        int shopId,
        DateOnly startDate,
        DateOnly endDate,
        string cycle = "monthly",
        int? roleId = null,
        int? userId = null,
        bool includeFullTime = false,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.ShopUsers
            .AsNoTracking()
            .Include(shopUser => shopUser.User)
            .Include(shopUser => shopUser.Roles)
                .ThenInclude(role => role.Role)
            .Include(shopUser => shopUser.FreelanceProfile)
                .ThenInclude(profile => profile!.BonusConfigs)
                    .ThenInclude(config => config.BonusType)
            .Where(shopUser => shopUser.ShopId == shopId && shopUser.Status == ShopUserStatus.Active);

        if (!includeFullTime)
        {
            query = query.Where(shopUser => shopUser.EmploymentType == EmploymentType.Freelance);
        }

        if (roleId.HasValue)
        {
            query = query.Where(shopUser => shopUser.Roles.Any(role => role.RoleId == roleId.Value));
        }

        if (userId.HasValue)
        {
            query = query.Where(shopUser => shopUser.UserId == userId.Value);
        }

        var workers = await query
            .OrderBy(shopUser => shopUser.User.FirstName)
            .ThenBy(shopUser => shopUser.User.LastName)
            .ToListAsync(cancellationToken);

        var shopUserIds = workers.Select(worker => worker.Id).ToList();

        var allAttendances = shopUserIds.Count == 0
            ? []
            : await dbContext.Attendances
                .AsNoTracking()
                .Where(attendance =>
                    attendance.ShopId == shopId &&
                    shopUserIds.Contains(attendance.ShopUserId) &&
                    attendance.WorkDate >= startDate &&
                    attendance.WorkDate <= endDate)
                .OrderBy(attendance => attendance.WorkDate)
                .ThenBy(attendance => attendance.CheckInAt)
                .ToListAsync(cancellationToken);

        var allAdjustments = shopUserIds.Count == 0
            ? []
            : await dbContext.PayrollAdjustments
                .AsNoTracking()
                .Where(adjustment =>
                    adjustment.ShopId == shopId &&
                    shopUserIds.Contains(adjustment.ShopUserId) &&
                    adjustment.PeriodStart <= endDate &&
                    adjustment.PeriodEnd >= startDate)
                .OrderByDescending(adjustment => adjustment.CreatedAt)
                .ToListAsync(cancellationToken);

        var startDateTimeUtc = new DateTimeOffset(startDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var endDateTimeExclusiveUtc = new DateTimeOffset(endDate.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        var workerRows = new List<WorkerPayrollRowDto>(workers.Count);
        foreach (var worker in workers)
        {
            var workerAttendances = allAttendances
                .Where(item => item.ShopUserId == worker.Id)
                .ToList();
            var approvedAttendances = workerAttendances
                .Where(item => item.Status == AttendanceStatus.Approved)
                .ToList();
            var pendingCount = workerAttendances
                .Count(item => item.Status == AttendanceStatus.Pending);

            var profile = worker.FreelanceProfile;
            var basePayMode = profile?.BasePayMode ?? BasePayMode.TieredDaily;
            var baseHourlyRate = profile?.BaseHourlyRate ?? 0m;
            var tieredRatesJson = profile?.TieredRatesJson ?? FreelanceProfile.DefaultTieredRatesJson;

            var wageCalc = TieredWageCalculator.Calculate(
                basePayMode,
                baseHourlyRate,
                tieredRatesJson,
                approvedAttendances);

            var bonusResults = new List<BonusEvaluationResult>();
            if (profile?.BonusConfigs is { Count: > 0 })
            {
                var evalContext = new BonusEvaluationContext(
                    shopId,
                    worker.Id,
                    worker.UserId,
                    startDate,
                    endDate,
                    startDateTimeUtc,
                    endDateTimeExclusiveUtc,
                    approvedAttendances);

                foreach (var bonusConfig in profile.BonusConfigs.Where(cfg => cfg.IsActive && cfg.BonusType.IsActive))
                {
                    if (_evaluatorsByCode.TryGetValue(bonusConfig.BonusType.Code, out var evaluator))
                    {
                        var result = await evaluator.EvaluateAsync(evalContext, bonusConfig.ParametersJson, cancellationToken);
                        bonusResults.Add(result);
                    }
                }
            }

            var totalBonus = Math.Round(bonusResults.Sum(item => item.TotalBonusAmount), 2, MidpointRounding.AwayFromZero);

            var workerAdjustments = allAdjustments
                .Where(item => item.ShopUserId == worker.Id)
                .Select(item => new PayrollAdjustmentDto(
                    item.Id,
                    item.ShopUserId,
                    item.PeriodStart,
                    item.PeriodEnd,
                    item.Amount,
                    item.Reason,
                    item.CreatedAt))
                .ToList();
            var totalAdjustments = Math.Round(workerAdjustments.Sum(item => item.Amount), 2, MidpointRounding.AwayFromZero);

            var finalPayable = Math.Round(wageCalc.BaseCalculatedWage + totalBonus + totalAdjustments, 2, MidpointRounding.AwayFromZero);
            var fullName = $"{worker.User.FirstName} {worker.User.LastName}".Trim();
            var roles = worker.Roles
                .OrderBy(role => role.Role.Name)
                .Select(role => role.Role.Name ?? string.Empty)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();

            workerRows.Add(new WorkerPayrollRowDto(
                worker.Id,
                worker.UserId,
                string.IsNullOrWhiteSpace(fullName) ? (worker.User.Email ?? string.Empty) : fullName,
                worker.User.Email ?? string.Empty,
                worker.EmploymentType.ToString(),
                roles,
                basePayMode.ToString(),
                baseHourlyRate,
                wageCalc.TotalDaysWorked,
                wageCalc.TotalHoursWorked,
                pendingCount,
                wageCalc.BaseCalculatedWage,
                wageCalc.Breakdown,
                totalBonus,
                bonusResults,
                totalAdjustments,
                workerAdjustments,
                finalPayable));
        }

        var totals = new PayrollSummaryTotalsDto(
            workerRows.Count,
            workerRows.Sum(row => row.TotalDaysWorked),
            Math.Round(workerRows.Sum(row => row.TotalHoursWorked), 2, MidpointRounding.AwayFromZero),
            workerRows.Sum(row => row.PendingShiftCount),
            Math.Round(workerRows.Sum(row => row.BaseCalculatedWage), 2, MidpointRounding.AwayFromZero),
            Math.Round(workerRows.Sum(row => row.TotalBonusAmount), 2, MidpointRounding.AwayFromZero),
            Math.Round(workerRows.Sum(row => row.TotalAdjustments), 2, MidpointRounding.AwayFromZero),
            Math.Round(workerRows.Sum(row => row.FinalPayableAmount), 2, MidpointRounding.AwayFromZero));

        return new PayrollSummaryDto(
            startDate,
            endDate,
            string.IsNullOrWhiteSpace(cycle) ? "monthly" : cycle.Trim().ToLowerInvariant(),
            totals,
            workerRows);
    }
}

