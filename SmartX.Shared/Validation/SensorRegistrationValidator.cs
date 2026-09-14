using System.Text.RegularExpressions;

namespace SmartX.Shared;

/// <summary>
/// Checks that a registration has a usable identity, MAC, category, and a location
/// that actually exists as a node in the nested deployment tree.
/// The API and Blazor client can both call this so operators see the same rules.
/// </summary>
public static class SensorRegistrationValidator
{
    // ESP32 boards report a 48-bit MAC. Accept colon, dash, or packed hex so seeded
    // and typed input do not fail over separator style.
    private static readonly Regex MacAddressPattern = new(
        @"^([0-9A-Fa-f]{2}[:-]){5}([0-9A-Fa-f]{2})$|^[0-9A-Fa-f]{12}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Optional unique id when the operator supplies one instead of letting the gateway assign it.
    private static readonly Regex OptionalIdPattern = new(
        @"^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Validates a registration against required fields and the current deployment forest.
    /// Collects every problem so the UI can show them together.
    /// </summary>
    public static ValidationResult Validate(
        SensorRegistration registration,
        IEnumerable<DeploymentNode> deploymentRoots)
    {
        var result = new ValidationResult();

        // Id is optional. Empty means the gateway will generate one later.
        if (registration.Id is { Length: > 0 } && !OptionalIdPattern.IsMatch(registration.Id))
        {
            result.AddError("Device id must be 1–64 characters: letters, digits, dot, underscore, or hyphen.");
        }

        if (string.IsNullOrWhiteSpace(registration.MacAddress))
        {
            result.AddError("MAC address is required.");
        }
        else if (!MacAddressPattern.IsMatch(registration.MacAddress.Trim()))
        {
            result.AddError("MAC address must be 6 octets (AA:BB:CC:DD:EE:FF, dashes, or 12 hex digits).");
        }

        // Reject numeric values that are not Environmental, PowerConsumption, or Actuator.
        if (!Enum.IsDefined(registration.Category))
        {
            result.AddError("Sensor category is not a recognised Smart-X category.");
        }

        if (string.IsNullOrWhiteSpace(registration.LocationNodeId))
        {
            result.AddError("Deployment location is required.");
        }
        else
        {
            var location = DeploymentTree.Find(deploymentRoots, registration.LocationNodeId.Trim());
            if (location is null)
            {
                result.AddError("Deployment location is not in the site → zone → node tree.");
            }
            else if (location.Level != DeploymentLevel.Node)
            {
                // A site/zone is a grouping, not a mount point for a single ESP32.
                result.AddError("A sensor must be attached to a node, not a site, zone, or sub-zone.");
            }
            else
            {
                var path = DeploymentTreeValidator.ValidateSensorPlacement(
                    deploymentRoots,
                    registration.LocationNodeId.Trim());
                foreach (var error in path.Errors)
                {
                    result.AddError(error);
                }
            }
        }

        return result;
    }
}
