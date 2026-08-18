using Freight.Domain.ValueObjects;

namespace Freight.Domain.Tracking;

public sealed class DrivingRuleRegistry
{
    private readonly Dictionary<Guid, DrivingRule> _rules = [];

    public void Assign(Guid driverId, DrivingRule rule)
    {
        if (driverId == Guid.Empty)
        {
            throw new ArgumentException("Driver id cannot be empty.", nameof(driverId));
        }

        ArgumentNullException.ThrowIfNull(rule);

        _rules[driverId] = rule;
    }

    public bool TryGet(Guid driverId, out DrivingRule? rule) =>
        _rules.TryGetValue(driverId, out rule);

    public DrivingRule Get(Guid driverId)
    {
        if (!_rules.TryGetValue(driverId, out var rule))
        {
            throw new InvalidOperationException($"No driving rule has been assigned for driver '{driverId}'.");
        }

        return rule;
    }

    public bool IsAssigned(Guid driverId) => _rules.ContainsKey(driverId);
}
