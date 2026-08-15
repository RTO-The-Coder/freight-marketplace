namespace Freight.Domain.Tracking;

public sealed class DriverRulePreferenceRegistry
{
    private readonly Dictionary<Guid, DriverRulePreference> _preferences = [];

    public void Assign(DriverRulePreference preference)
    {
        ArgumentNullException.ThrowIfNull(preference);

        _preferences[preference.DriverId] = preference;
    }

    public bool TryGet(Guid driverId, out DriverRulePreference? preference) =>
        _preferences.TryGetValue(driverId, out preference);

    public DriverRulePreference Get(Guid driverId)
    {
        if (!_preferences.TryGetValue(driverId, out var preference))
        {
            throw new InvalidOperationException($"No rule preference has been assigned for driver '{driverId}'.");
        }

        return preference;
    }

    public bool IsAssigned(Guid driverId) => _preferences.ContainsKey(driverId);
}
