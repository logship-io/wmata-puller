using System.Text.Json.Serialization;

namespace Logship.WmataPuller.Config
{
    public class Configuration
    {
        public string? LogshipEndpoint { get; set; }

        public string? AuthToken { get; set; }

        public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(5);

        public bool IsValid(out string whyNot)
        {
            if (string.IsNullOrEmpty(this.LogshipEndpoint))
            {
                whyNot = "LogshipEndpoint must be set. e.g. http://try.logship.ai:5000";
                return false;
            }

            if (string.IsNullOrEmpty(this.AuthToken))
            {
                whyNot = "AuthToken must be set to your WMATA auth token";
                return false;
            }

            whyNot = null!;
            return true;
        }
    }
}
