using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;

namespace Freight.Application.Fleet;

public sealed record AddDriverRequest(
    string FirstName,
    string LastName,
    DrivingBreakRule BreakRule,
    DailyRestRule DailyRestRule,
    WeeklyRestRule WeeklyRestRule,
    bool ExtendDailyDrivingWhenEligible);

public sealed record AddDriverResponse(
    Guid DriverId,
    string FirstName,
    string LastName,
    DrivingBreakRule BreakRule,
    DailyRestRule DailyRestRule,
    WeeklyRestRule WeeklyRestRule,
    bool ExtendDailyDrivingWhenEligible);

public sealed class AddDriverHandler(IUnitOfWork unitOfWork)
{
    public async Task<AddDriverResponse> AddDriverAsync(AddDriverRequest request, CancellationToken cancellationToken = default)
    {
        var rules = DrivingRules.Create(
            request.BreakRule,
            request.DailyRestRule,
            request.WeeklyRestRule,
            request.ExtendDailyDrivingWhenEligible);

        var driver = Driver.Create(request.FirstName, request.LastName, rules);

        unitOfWork.Drivers.Add(driver);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AddDriverResponse(
            driver.Id,
            driver.FirstName,
            driver.LastName,
            driver.Rules.BreakRule,
            driver.Rules.DailyRestRule,
            driver.Rules.WeeklyRestRule,
            driver.Rules.ExtendDailyDrivingWhenEligible);
    }
}
