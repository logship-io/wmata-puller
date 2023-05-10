using System.Text.Json.Serialization;

namespace Logship.WmataPuller.Config
{
    public class Configuration
    {
        public string? AuthToken { get; set; }

        public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(5);
    }
}
