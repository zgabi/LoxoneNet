
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Certes.Json
{
    /// <summary>
    /// Helper methods for JSON serialization.
    /// </summary>
    public static class JsonUtil
    {
        /// <summary>
        /// Creates the <see cref="JsonSerializerOptions"/> used for ACME entity serialization.
        /// </summary>
        /// <returns>The JSON serializer options.</returns>
        public static JsonSerializerOptions CreateSettings()
        {
            var jsonSettings = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            };

            return jsonSettings;
        }
    }
}
