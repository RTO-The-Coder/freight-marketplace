using Freight.Domain.ValueObjects;

namespace Freight.Domain.Tracking;

public sealed class DrivingRuleRegistry
{
    private readonly Dictionary<Guid, DrivingRules> _rules = [];

    public void Assign(Guid driverId, DrivingRules rule)
    {
        if (driverId == Guid.Empty)
        {
            throw new ArgumentException("Driver id cannot be empty.", nameof(driverId));
        }

        ArgumentNullException.ThrowIfNull(rule);

        _rules[driverId] = rule;
    }

    public bool TryGet(Guid driverId, out DrivingRules? rule) =>
        _rules.TryGetValue(driverId, out rule);

    public DrivingRules Get(Guid driverId)
    {
        if (!_rules.TryGetValue(driverId, out var rule))
        {
            throw new InvalidOperationException($"No driving rule has been assigned for driver '{driverId}'.");
        }

        return rule;
    }

    public bool IsAssigned(Guid driverId) => _rules.ContainsKey(driverId);
}
