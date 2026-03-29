using Newtonsoft.Json;

namespace KiwiRichPresence.Models
{
    internal class LocalizationEntry
    {
        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;
        [JsonProperty("description")]
        public string Description { get; set; } = string.Empty;
    }
}
