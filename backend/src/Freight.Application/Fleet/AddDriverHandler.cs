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

public sealed record AddDriverResponse(Guid DriverId);

public sealed class AddDriverHandler(IUnitOfWork unitOfWork)
{
    public async Task<AddDriverResponse> HandleAsync(AddDriverRequest request, CancellationToken cancellationToken = default)
    {
        var rules = DrivingRules.Create(
            request.BreakRule,
            request.DailyRestRule,
            request.WeeklyRestRule,
            request.ExtendDailyDrivingWhenEligible);

        var driver = Driver.Create(request.FirstName, request.LastName, rules);

        unitOfWork.Drivers.Add(driver);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AddDriverResponse(driver.Id);
    }
}
