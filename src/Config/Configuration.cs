using System.Text.Json.Serialization;

namespace Logship.WmataPuller.Config
{
    internal class Configuration
    {
        [JsonInclude]
        public string AuthToken { get; set; }

        [JsonInclude]
        public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(5);
    }
}
