namespace UnoTP.Backend.Mock;

/// <summary>
/// PIN codes, as the backend's PIN directory answers them. The mock knows a head
/// post office in each of a dozen cities by its exact code; any other PIN is placed
/// in its state by its first two digits, with the district the backend would give
/// left as the state's postal circle. A PIN whose digits name no circle is not found.
/// </summary>
public sealed class MockPlaces : IPlaceApi
{
    private static readonly PinPlace[] Known =
    [
        new("400001", "Mumbai", "Maharashtra"),
        new("411001", "Pune", "Maharashtra"),
        new("110001", "New Delhi", "Delhi"),
        new("560001", "Bengaluru Urban", "Karnataka"),
        new("600001", "Chennai", "Tamil Nadu"),
        new("700001", "Kolkata", "West Bengal"),
        new("500001", "Hyderabad", "Telangana"),
        new("380001", "Ahmedabad", "Gujarat"),
        new("302001", "Jaipur", "Rajasthan"),
        new("226001", "Lucknow", "Uttar Pradesh"),
        new("682001", "Ernakulam", "Kerala"),
        new("751001", "Khordha", "Odisha"),
    ];

    // The postal circles by a PIN's first two digits.
    private static readonly (int From, int To, string District, string State)[] Circles =
    [
        (11, 11, "Delhi", "Delhi"), (12, 13, "Ambala", "Haryana"), (14, 16, "Ludhiana", "Punjab"),
        (17, 17, "Shimla", "Himachal Pradesh"), (18, 19, "Jammu", "Jammu and Kashmir"),
        (20, 28, "Lucknow", "Uttar Pradesh"), (30, 34, "Jaipur", "Rajasthan"), (36, 39, "Ahmedabad", "Gujarat"),
        (40, 44, "Mumbai", "Maharashtra"), (45, 48, "Bhopal", "Madhya Pradesh"), (49, 49, "Raipur", "Chhattisgarh"),
        (50, 50, "Hyderabad", "Telangana"), (51, 53, "Vijayawada", "Andhra Pradesh"), (56, 59, "Bengaluru Urban", "Karnataka"),
        (60, 66, "Chennai", "Tamil Nadu"), (67, 69, "Thiruvananthapuram", "Kerala"), (70, 74, "Kolkata", "West Bengal"),
        (75, 77, "Khordha", "Odisha"), (78, 78, "Kamrup Metropolitan", "Assam"), (80, 85, "Patna", "Bihar"),
    ];

    public Task<PinPlace?> PinCodeAsync(string pin, CancellationToken ct = default)
    {
        pin = pin.Trim();
        if (pin.Length != 6 || !pin.All(char.IsAsciiDigit) || pin[0] == '0') return Task.FromResult<PinPlace?>(null);
        if (Known.FirstOrDefault(p => p.PinCode == pin) is { } known) return Task.FromResult<PinPlace?>(known);
        var two = int.Parse(pin[..2]);
        return Task.FromResult(Circles.FirstOrDefault(c => two >= c.From && two <= c.To) is { State.Length: > 0 } c
            ? new PinPlace(pin, c.District, c.State) : null);
    }
}
