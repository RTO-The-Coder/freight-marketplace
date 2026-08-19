using Freight.Domain.Common;
using Freight.Domain.ValueObjects.RuleVariants;

namespace Freight.Application.Fleet;

public sealed record GetDriverDetailRequest(Guid DriverId);

public sealed record DriverDetailDto(
    Guid DriverId,
    string FirstName,
    string LastName,
    DrivingBreakRule BreakRule,
    DailyRestRule DailyRestRule,
    WeeklyRestRule WeeklyRestRule,
    bool ExtendDailyDrivingWhenEligible);

public sealed class GetDriverDetailHandler(IUnitOfWork unitOfWork)
{
    public async Task<DriverDetailDto> HandleAsync(GetDriverDetailRequest request, CancellationToken cancellationToken = default)
    {
        var driver = await unitOfWork.Drivers.GetByIdAsync(request.DriverId, cancellationToken)
            ?? throw new InvalidOperationException($"Driver '{request.DriverId}' was not found.");

        return new DriverDetailDto(
            driver.Id,
            driver.FirstName,
            driver.LastName,
            driver.Rules.BreakRule,
            driver.Rules.DailyRestRule,
            driver.Rules.WeeklyRestRule,
            driver.Rules.ExtendDailyDrivingWhenEligible);
    }
}
