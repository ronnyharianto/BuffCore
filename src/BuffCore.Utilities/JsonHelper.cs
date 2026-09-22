using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BuffCore.Utilities
{
    /// <summary>
    /// JSON serialization and deserialization using System.Text.Json with centralized configuration
    /// for consistent behavior across services.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Property names are emitted exactly as declared (PascalCase), preserving stored and transmitted
    /// payload shapes. HTTP hosts that apply their own naming policy (e.g. MVC camelCase) are unaffected:
    /// they serialize view models themselves and do not route responses through this helper.
    /// </para>
    /// <para>
    /// Register via <see cref="ServiceCollectionExtensions.AddBuffCoreUtilities"/> and inject,
    /// or construct directly with an optional logger. Deserialization failures are logged (when a logger
    /// is available) and return <c>default</c> instead of throwing, matching a lenient contract for
    /// untrusted external payloads.
    /// </para>
    /// </remarks>
    public class JsonHelper(ILogger? logger = null)
    {
        private readonly ILogger? _logger = logger;

        private static readonly JsonSerializerOptions _options = new()
        {
            ReferenceHandler = ReferenceHandler.IgnoreCycles, // Prevents infinite loops when serializing objects with circular references.
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, // Omits properties with null values from the JSON output to reduce size.
            WriteIndented = false, // Produces a compact JSON format; use the indented overload for human readability.
            PropertyNameCaseInsensitive = true, // Accepts payloads regardless of property-name casing (e.g. camelCase responses feeding PascalCase DTOs).
        };

        private static readonly JsonSerializerOptions _indentedOptions = new(_options)
        {
            WriteIndented = true,
        };

        /// <summary>
        /// Serializes an object to a JSON string using the configured options.
        /// </summary>
        /// <param name="data">The object to serialize.</param>
        /// <param name="indented">If true, formats the JSON with indentation for readability.</param>
        /// <returns>A JSON string representation of the object.</returns>
        public string SerializeObject(object? data, bool indented = false)
            => JsonSerializer.Serialize(data, indented ? _indentedOptions : _options);

        /// <summary>
        /// Deserializes a JSON string into an object of the specified type using the configured options.
        /// </summary>
        /// <typeparam name="T">The target type to deserialize to.</typeparam>
        /// <param name="jsonString">The JSON string to deserialize.</param>
        /// <returns>The deserialized object, or null if deserialization fails or input is invalid.</returns>
        public T? DeserializeObject<T>(string jsonString)
        {
            try
            {
                return JsonSerializer.Deserialize<T>(jsonString, _options);
            }
            catch (JsonException ex)
            {
                _logger?.LogWarning(ex, "Failed to deserialize JSON to {Type}. Returning default value.", typeof(T).Name);
                return default;
            }
        }
    }
}
