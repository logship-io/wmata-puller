using System.Text.Json.Serialization;

namespace Logship.WmataPuller.Config
{
    public class Configuration
    {
        public string? LogshipEndpoint { get; set; }

        public Dictionary<string, GTFSRealtimeFeedConfiguration> GTFS { get; set; } = new Dictionary<string, GTFSRealtimeFeedConfiguration>();

        public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(5);

        public bool IsValid(out string whyNot)
        {
            if (string.IsNullOrEmpty(this.LogshipEndpoint))
            {
                whyNot = "LogshipEndpoint must be set. e.g. http://try.logship.ai:5000";
                return false;
            }

            if (this.GTFS != null)
            {
                foreach (var value in this.GTFS.Values)
                {
                    if (false == value.IsValid(out whyNot))
                    {
                        return false;
                    }
                }
            }

            whyNot = null!;
            return true;
        }
    }
}
