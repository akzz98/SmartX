namespace SmartX.Shared;

/// <summary>
/// Outcome of gateway validation. The dashboard displays these messages; it does not invent them.
/// </summary>
public sealed class ValidationResult
{
    public List<string> Errors { get; } = [];

    public bool IsValid => Errors.Count == 0;

    public void AddError(string message) => Errors.Add(message);
}
