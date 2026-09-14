namespace SmartX.Shared;

/// <summary>
/// Query for a single registered sensor. The API can also take this id on the route;
/// the client uses the same type when it asks for a device-details page.
/// </summary>
public sealed class GetSensorRequest
{
    public string Id { get; set; } = string.Empty;
}
