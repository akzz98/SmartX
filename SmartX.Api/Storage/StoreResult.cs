namespace SmartX.Api.Storage;

/// <summary>
/// Result of a store write. Replaces out-parameters so the API can await the call.
/// </summary>
public readonly record struct StoreResult(bool Succeeded, string? Error);
