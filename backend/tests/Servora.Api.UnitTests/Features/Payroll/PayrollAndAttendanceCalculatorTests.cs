using FluentAssertions;
using Servora.Api.Features.Attendance;
using Servora.Api.Features.Devices;
using Servora.Api.Features.Payroll;
using Servora.Api.Features.Payroll.Bonuses;

namespace Servora.Api.UnitTests.Features.Payroll;

public class PayrollAndAttendanceCalculatorTests
{
    [Fact]
    public void AttendanceCalculator_CalculateHours_DeductsBreakMinutesDeterministically()
    {
        var checkIn = new DateTime(2026, 10, 16, 9, 0, 0);
        var checkOut = new DateTime(2026, 10, 16, 18, 0, 0);

        var hours = AttendanceCalculator.CalculateHours(checkIn, checkOut, breakMinutes: 60);

        hours.Should().Be(8.0m);
    }

    [Fact]
    public void AttendanceCalculator_IsOutsideOperatingWindow_DetectsOutOfBoundsShifts()
    {
        var checkInInside = new DateTime(2026, 10, 16, 9, 15, 0);
        var checkOutInside = new DateTime(2026, 10, 16, 17, 45, 0);
        var checkInEarly = new DateTime(2026, 10, 16, 8, 30, 0);
        var checkOutLate = new DateTime(2026, 10, 16, 19, 0, 0);

        AttendanceCalculator.IsOutsideOperatingWindow(checkInInside, checkOutInside, "09:00", "18:00")
            .Should().BeFalse();

        AttendanceCalculator.IsOutsideOperatingWindow(checkInEarly, checkOutInside, "09:00", "18:00")
            .Should().BeTrue();

        AttendanceCalculator.IsOutsideOperatingWindow(checkInInside, checkOutLate, "09:00", "18:00")
            .Should().BeTrue();
    }

    [Fact]
    public void AttendanceCalculator_IsOutsideOperatingWindow_UsesShopTimeZoneRegardlessOfServerRegion()
    {
        var jakartaTz = Servora.Api.Features.Settings.ShopTimeZoneHelper.ResolveTimeZone("Asia/Jakarta");
        var singaporeTz = Servora.Api.Features.Settings.ShopTimeZoneHelper.ResolveTimeZone("Asia/Singapore");

        // 09:00 Jakarta (WIB, UTC+7) is 02:00 UTC. Check-out at 13:00 Jakarta is 06:00 UTC.
        var checkInUtcForJakarta = new DateTimeOffset(2026, 11, 6, 2, 0, 0, TimeSpan.Zero);
        var checkOutUtcForJakarta = new DateTimeOffset(2026, 11, 6, 6, 0, 0, TimeSpan.Zero);

        // When evaluated against Jakarta shop time, 02:00 UTC converts to 09:00 WIB, which is inside 09:00 - 18:00
        AttendanceCalculator.IsOutsideOperatingWindow(checkInUtcForJakarta, checkOutUtcForJakarta, "09:00", "18:00", jakartaTz)
            .Should().BeFalse();

        // 09:00 Singapore (SGT, UTC+8) is 01:00 UTC. Check-out at 13:00 Singapore is 05:00 UTC.
        var checkInUtcForSingapore = new DateTimeOffset(2026, 11, 6, 1, 0, 0, TimeSpan.Zero);
        var checkOutUtcForSingapore = new DateTimeOffset(2026, 11, 6, 5, 0, 0, TimeSpan.Zero);

        AttendanceCalculator.IsOutsideOperatingWindow(checkInUtcForSingapore, checkOutUtcForSingapore, "09:00", "18:00", singaporeTz)
            .Should().BeFalse();

        // Check early shift in Singapore: 08:30 SGT is 00:30 UTC -> outside operating window
        var checkInEarlySingapore = new DateTimeOffset(2026, 11, 6, 0, 30, 0, TimeSpan.Zero);
        AttendanceCalculator.IsOutsideOperatingWindow(checkInEarlySingapore, checkOutUtcForSingapore, "09:00", "18:00", singaporeTz)
            .Should().BeTrue();
    }

    [Theory]
    [InlineData("Asia/Jakarta", 7)]
    [InlineData("Asia/Singapore", 8)]
    [InlineData("Asia/Makassar", 8)]
    [InlineData("UTC", 0)]
    public void ShopTimeZoneHelper_ResolvesStandardTimeZones(string timeZoneId, int expectedOffsetHours)
    {
        var tz = Servora.Api.Features.Settings.ShopTimeZoneHelper.ResolveTimeZone(timeZoneId);
        var offset = tz.GetUtcOffset(new DateTime(2026, 11, 6, 12, 0, 0));
        offset.TotalHours.Should().Be(expectedOffsetHours);
    }

    [Fact]
    public void TieredWageCalculator_TieredDaily_AppliesTier1Tier2AndProratedFallback()
    {
        const string tieredRatesJson = """
            [
              {"minHours": 5.0, "maxHours": 6.0, "flatRate": 35000, "extraHourlyRate": 0},
              {"minHours": 8.0, "maxHours": 24.0, "flatRate": 60000, "extraHourlyRate": 0}
            ]
            """;

        var approvedAttendances = new List<AttendanceRecord>
        {
            new()
            {
                WorkDate = new DateOnly(2026, 10, 12),
                CheckInAt = new DateTime(2026, 10, 12, 9, 0, 0),
                CheckOutAt = new DateTime(2026, 10, 12, 14, 30, 0),
                TotalHours = 5.5m,
                Status = AttendanceStatus.Approved
            },
            new()
            {
                WorkDate = new DateOnly(2026, 10, 13),
                CheckInAt = new DateTime(2026, 10, 13, 9, 0, 0),
                CheckOutAt = new DateTime(2026, 10, 13, 18, 30, 0),
                TotalHours = 8.5m,
                Status = AttendanceStatus.Approved
            },
            new()
            {
                WorkDate = new DateOnly(2026, 10, 14),
                CheckInAt = new DateTime(2026, 10, 14, 9, 0, 0),
                CheckOutAt = new DateTime(2026, 10, 14, 17, 0, 0),
                TotalHours = 7.0m,
                Status = AttendanceStatus.Approved
            }
        };

        var result = TieredWageCalculator.Calculate(
            BasePayMode.TieredDaily,
            baseHourlyRate: 7_000m,
            tieredRatesJson,
            approvedAttendances);

        // 5.5h -> 35,000 + 8.5h -> 60,000 + 7.0h (Tier 1 35,000 + 1h @ 7,000/h = 42,000) = 137,000
        result.TotalDaysWorked.Should().Be(3);
        result.TotalHoursWorked.Should().Be(21.0m);
        result.BaseCalculatedWage.Should().Be(137_000m);
        result.Breakdown.Should().HaveCount(3);
    }

    [Fact]
    public void TieredWageCalculator_Hourly_MultipliesTotalApprovedHoursByBaseRate()
    {
        var approvedAttendances = new List<AttendanceRecord>
        {
            new()
            {
                WorkDate = new DateOnly(2026, 10, 12),
                CheckInAt = new DateTime(2026, 10, 12, 9, 0, 0),
                CheckOutAt = new DateTime(2026, 10, 12, 15, 0, 0),
                TotalHours = 6.0m,
                Status = AttendanceStatus.Approved
            },
            new()
            {
                WorkDate = new DateOnly(2026, 10, 13),
                CheckInAt = new DateTime(2026, 10, 13, 9, 0, 0),
                CheckOutAt = new DateTime(2026, 10, 13, 18, 0, 0),
                TotalHours = 8.0m,
                Status = AttendanceStatus.Approved
            }
        };

        var result = TieredWageCalculator.Calculate(
            BasePayMode.Hourly,
            baseHourlyRate: 12_500m,
            tieredRatesJson: null,
            approvedAttendances);

        result.TotalDaysWorked.Should().Be(2);
        result.TotalHoursWorked.Should().Be(14.0m);
        result.BaseCalculatedWage.Should().Be(175_000m);
    }

    [Fact]
    public void TieredWageCalculator_ParseBrackets_SortsAndFiltersBrackets()
    {
        var brackets = TieredWageCalculator.ParseBrackets(
            """
            [
              {"minHours": 8.0, "maxHours": 24.0, "flatRate": 60000, "extraHourlyRate": 0},
              {"minHours": 5.0, "maxHours": 6.0, "flatRate": 35000, "extraHourlyRate": 0}
            ]
            """);

        brackets.Should().HaveCount(2);
        brackets[0].MinHours.Should().Be(5.0m);
        brackets[0].FlatRate.Should().Be(35_000m);
        brackets[1].MinHours.Should().Be(8.0m);
        brackets[1].FlatRate.Should().Be(60_000m);
    }

    [Fact]
    public void TieredWageCalculator_TieredDailyAverage_Averages5Point5Hours_AppliesTier1AcrossAllDays()
    {
        const string tieredRatesJson = """
            [
              {"minHours": 5.0, "maxHours": 6.0, "flatRate": 35000, "extraHourlyRate": 0},
              {"minHours": 8.0, "maxHours": 24.0, "flatRate": 60000, "extraHourlyRate": 0}
            ]
            """;

        // 22 days, each day 5.5 hours = 121 total hours
        var approvedAttendances = Enumerable.Range(1, 22).Select(day => new AttendanceRecord
        {
            WorkDate = new DateOnly(2026, 10, day),
            CheckInAt = new DateTime(2026, 10, day, 9, 0, 0),
            CheckOutAt = new DateTime(2026, 10, day, 14, 30, 0),
            TotalHours = 5.5m,
            Status = AttendanceStatus.Approved
        }).ToList();

        var result = TieredWageCalculator.Calculate(
            BasePayMode.TieredDailyAverage,
            baseHourlyRate: 7_000m,
            tieredRatesJson,
            approvedAttendances);

        result.TotalDaysWorked.Should().Be(22);
        result.TotalHoursWorked.Should().Be(121.0m);
        // Average = 121 / 22 = 5.5h -> Tier 1 (35,000) -> 22 * 35,000 = 770,000
        result.BaseCalculatedWage.Should().Be(770_000m);
        result.Breakdown.Should().ContainSingle();
        result.Breakdown[0].RateApplied.Should().Be(35_000m);
    }

    [Fact]
    public void TieredWageCalculator_TieredDailyAverage_Averages8Hours_AppliesTier2AcrossAllDays()
    {
        const string tieredRatesJson = """
            [
              {"minHours": 5.0, "maxHours": 6.0, "flatRate": 35000, "extraHourlyRate": 0},
              {"minHours": 8.0, "maxHours": 24.0, "flatRate": 60000, "extraHourlyRate": 0}
            ]
            """;

        // 20 days, each day 8.0 hours = 160 total hours
        var approvedAttendances = Enumerable.Range(1, 20).Select(day => new AttendanceRecord
        {
            WorkDate = new DateOnly(2026, 10, day),
            CheckInAt = new DateTime(2026, 10, day, 9, 0, 0),
            CheckOutAt = new DateTime(2026, 10, day, 18, 0, 0),
            TotalHours = 8.0m,
            Status = AttendanceStatus.Approved
        }).ToList();

        var result = TieredWageCalculator.Calculate(
            BasePayMode.TieredDailyAverage,
            baseHourlyRate: 7_000m,
            tieredRatesJson,
            approvedAttendances);

        result.TotalDaysWorked.Should().Be(20);
        result.TotalHoursWorked.Should().Be(160.0m);
        // Average = 160 / 20 = 8.0h -> Tier 2 (60,000) -> 20 * 60,000 = 1,200,000
        result.BaseCalculatedWage.Should().Be(1_200_000m);
        result.Breakdown.Should().ContainSingle();
        result.Breakdown[0].RateApplied.Should().Be(60_000m);
    }

    private const string DefaultTaskItemizedParamsJson = """
        {
          "rules": [
            {
              "taskKey": "OS_INSTALL",
              "taskLabel": "OS / Software Reinstallation",
              "matcherKeyword": "OS,Install OS,Clean Install,OS Reinstall,Reinstall OS,Windows",
              "deviceCategory": "Any",
              "rewardPerUnit": 25000
            },
            {
              "taskKey": "KEYBOARD_BATTERY",
              "taskLabel": "Keyboard / Battery Replacement",
              "matcherKeyword": "Keyboard,Battery Replacement,Replace Battery,New Battery,Swap Battery,Ganti Baterai,Tukar Baterai,Pasang Baterai",
              "deviceCategory": "Any",
              "rewardPerUnit": 25000
            },
            {
              "taskKey": "CLEANING_REPASTE_STD",
              "taskLabel": "Cleaning & Thermal Repasting (Standard)",
              "matcherKeyword": "Repaste,Repasting,Cleaning,Clean Dust,Thermal,Pasta",
              "deviceCategory": "Standard",
              "rewardPerUnit": 25000
            },
            {
              "taskKey": "CLEANING_REPASTE_GAMING",
              "taskLabel": "Cleaning & Thermal Repasting (Gaming)",
              "matcherKeyword": "Repaste,Repasting,Cleaning,Clean Dust,Thermal,Pasta",
              "deviceCategory": "Gaming",
              "rewardPerUnit": 50000
            }
          ]
        }
        """;

    [Theory]
    [InlineData("ASUS", "ROG Zephyrus G14", "", true)]
    [InlineData("Lenovo", "Legion Pro 5", "", true)]
    [InlineData("Acer", "Nitro 5 AN515", "", true)]
    [InlineData("HP", "Victus 15", "", true)]
    [InlineData("Lenovo", "ThinkPad T14s", "Office laptop", false)]
    [InlineData("Apple", "MacBook Pro 16", "Standard productivity", false)]
    public void TaskItemizedBonusEvaluator_IsGamingDevice_AccuratelyClassifiesHardware(
        string brand,
        string model,
        string notes,
        bool expectedGaming)
    {
        var device = new Device
        {
            Brand = brand,
            Model = model,
            Notes = notes
        };

        var isGaming = TaskItemizedBonusEvaluator.IsGamingDevice(device, null, null);
        isGaming.Should().Be(expectedGaming);
    }

    [Theory]
    [InlineData("Perform pre-repair diagnostics (touch, display, camera check)", "OS", false)]
    [InlineData("Clamp device and perform post-repair diagnostics", "OS", false)]
    [InlineData("Bios chip firmware update", "OS", false)]
    [InlineData("Wipe target storage drive and clean install OS", "OS", true)]
    [InlineData("OS/Drivers Setup", "OS", true)]
    [InlineData("Reinstall  OS and verify boot", "Reinstall OS", true)]
    public void TaskItemizedBonusEvaluator_ContainsKeyword_EnforcesWordBoundaries(
        string text,
        string keyword,
        bool expectedMatch)
    {
        TaskItemizedBonusEvaluator.ContainsKeyword(text, keyword).Should().Be(expectedMatch);
    }

    [Fact]
    public void TaskItemizedBonusEvaluator_EvaluateTickets_CapsSameTaskTypeToOncePerTicket()
    {
        var tickets = new List<TaskTicketCandidate>
        {
            new(
                "T-REPASTE-01",
                new Device { Brand = "Lenovo", Model = "ThinkPad T14s" },
                "Overheating",
                "Lenovo ThinkPad T14s",
                [
                    new TaskChecklistEntry("Verify initial system temperatures under stress test"),
                    new TaskChecklistEntry("Disassemble chassis and disconnect battery power source"),
                    new TaskChecklistEntry("Clean dust from cooling fans, heatsink fins, and vents"),
                    new TaskChecklistEntry("Remove old thermal paste using isopropyl alcohol"),
                    new TaskChecklistEntry("Apply premium thermal paste to CPU/GPU dies"),
                    new TaskChecklistEntry("Reassemble thermal unit and chassis screws"),
                    new TaskChecklistEntry("Boot system and verify load temperatures are within range")
                ])
        };

        var result = TaskItemizedBonusEvaluator.EvaluateTickets(tickets, DefaultTaskItemizedParamsJson);

        result.QualifiedCount.Should().Be(1);
        result.TotalBonusAmount.Should().Be(25_000m);
        result.ReferenceNumbers.Should().Equal(["T-REPASTE-01"]);
        result.FormulaSummary.Should().NotContain("Keyboard");
        result.FormulaSummary.Should().NotContain("OS");
    }

    [Fact]
    public void TaskItemizedBonusEvaluator_EvaluateTickets_CreditsMultipleDistinctTasksOnSingleTicketOnceEach()
    {
        var tickets = new List<TaskTicketCandidate>
        {
            new(
                "T-MULTI-01",
                new Device { Brand = "Dell", Model = "Latitude 5430" },
                "OS corrupt, keyboard worn, overheating",
                "Dell Latitude 5430",
                [
                    new TaskChecklistEntry("Wipe target storage drive and clean install OS"),
                    new TaskChecklistEntry("Final QA", "Verified Windows drivers and system OS updates"),
                    new TaskChecklistEntry("Apply premium thermal paste to CPU/GPU dies"),
                    new TaskChecklistEntry("Clean dust from cooling fans, heatsink fins, and vents"),
                    new TaskChecklistEntry("Replace Keyboard assembly"),
                    new TaskChecklistEntry("Battery replacement and power test")
                ])
        };

        var result = TaskItemizedBonusEvaluator.EvaluateTickets(tickets, DefaultTaskItemizedParamsJson);

        result.QualifiedCount.Should().Be(3);
        result.TotalBonusAmount.Should().Be(75_000m);
        result.ReferenceNumbers.Should().Equal(["T-MULTI-01"]);
    }

    [Fact]
    public void TaskItemizedBonusEvaluator_EvaluateTickets_AppliesGamingCategoryOncePerGamingTicket()
    {
        var tickets = new List<TaskTicketCandidate>
        {
            new(
                "T-GAMING-01",
                new Device { Brand = "ASUS", Model = "ROG Zephyrus G14" },
                "Thermal throttling",
                "ASUS ROG Zephyrus G14",
                [
                    new TaskChecklistEntry("Clean dust from cooling fans"),
                    new TaskChecklistEntry("Remove old thermal paste"),
                    new TaskChecklistEntry("Apply premium thermal paste to CPU/GPU dies")
                ])
        };

        var result = TaskItemizedBonusEvaluator.EvaluateTickets(tickets, DefaultTaskItemizedParamsJson);

        result.QualifiedCount.Should().Be(1);
        result.TotalBonusAmount.Should().Be(50_000m);
        result.ReferenceNumbers.Should().Equal(["T-GAMING-01"]);
    }

    [Fact]
    public void TaskItemizedBonusEvaluator_EvaluateTickets_AvoidsFalsePositivesFromScreenAndKeyboardChecklists()
    {
        var tickets = new List<TaskTicketCandidate>
        {
            new(
                "T-SCREEN-01",
                new Device { Brand = "Apple", Model = "iPhone 14 Pro" },
                "Cracked OLED panel",
                "Apple iPhone 14 Pro",
                [
                    new TaskChecklistEntry("Perform pre-repair diagnostics (touch, display, camera check)"),
                    new TaskChecklistEntry("Disconnect battery and old screen panel cables"),
                    new TaskChecklistEntry("Clamp device and perform post-repair diagnostics")
                ]),
            new(
                "T-KB-01",
                new Device { Brand = "Lenovo", Model = "ThinkPad X1 Carbon" },
                "Missing keycaps",
                "Lenovo ThinkPad X1 Carbon",
                [
                    new TaskChecklistEntry("Install new keyboard")
                ])
        };

        var result = TaskItemizedBonusEvaluator.EvaluateTickets(tickets, DefaultTaskItemizedParamsJson);

        result.QualifiedCount.Should().Be(1);
        result.TotalBonusAmount.Should().Be(25_000m);
        result.ReferenceNumbers.Should().Equal(["T-KB-01"]);
    }

    [Fact]
    public void TaskItemizedBonusEvaluator_EvaluateTickets_DeduplicatesDuplicateTaskKeysOnSameTicket()
    {
        const string duplicateKeyParamsJson = """
            {
              "rules": [
                {
                  "taskKey": "TASK_DUP",
                  "taskLabel": "Thermal Repaste Primary",
                  "matcherKeyword": "Repaste",
                  "deviceCategory": "Any",
                  "rewardPerUnit": 25000
                },
                {
                  "taskKey": "TASK_DUP",
                  "taskLabel": "Dust Cleaning Duplicate Key",
                  "matcherKeyword": "Cleaning",
                  "deviceCategory": "Any",
                  "rewardPerUnit": 40000
                }
              ]
            }
            """;

        var tickets = new List<TaskTicketCandidate>
        {
            new(
                "T-DUP-01",
                new Device { Brand = "HP", Model = "ProBook 450" },
                "Dust and heat",
                "HP ProBook 450",
                [
                    new TaskChecklistEntry("Thermal Repaste"),
                    new TaskChecklistEntry("Internal Cleaning")
                ])
        };

        var result = TaskItemizedBonusEvaluator.EvaluateTickets(tickets, duplicateKeyParamsJson);

        result.QualifiedCount.Should().Be(1);
        result.TotalBonusAmount.Should().Be(25_000m);
        result.ReferenceNumbers.Should().Equal(["T-DUP-01"]);
    }
}
