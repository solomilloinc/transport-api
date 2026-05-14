using System.Text.Json;

namespace Transport_Api.Infrastructure;

/// <summary>
/// Shared JsonSerializerOptions for the API: camelCase output + case-insensitive input.
/// Use these everywhere we serialize/deserialize JSON for HTTP wire format.
/// </summary>
public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
}
