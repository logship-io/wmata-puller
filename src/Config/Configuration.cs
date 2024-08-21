using System.Text.Json.Serialization;

namespace Logship.WmataPuller.Config
{
    public class Configuration
    {
        public string? LogshipEndpoint { get; set; }

        public Guid? InflowSubscription { get; set; }

        public string? BearerToken { get; set; }

        public int MaxDegreeOfParallelism { get; set; } = 16;

        public AmtrackConfiguration Amtrak { get; set; } = new AmtrackConfiguration();

        public Dictionary<string, GTFSRealtimeFeedConfiguration> GTFS { get; set; } = new Dictionary<string, GTFSRealtimeFeedConfiguration>();

        public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(5);

        public bool IsValid(out string whyNot)
        {
            if (string.IsNullOrEmpty(this.LogshipEndpoint))
            {
                whyNot = "LogshipEndpoint must be set. e.g. http://try.logship.ai:5000";
                return false;
            }

            if (null == this.InflowSubscription)
            {

                whyNot = "InflowSubscription must be set.";
                return false;
            }

            if (string.IsNullOrEmpty(this.BearerToken))
            {
                whyNot = "BearerToken must be set.";
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

            if (this.MaxDegreeOfParallelism < -1)
            {
                whyNot = "MaxDegreeOfParallelism must be -1 or greater.";
                return false;
            }

            if (false == Amtrak.IsValid(out whyNot))
            {
                return false;
            }

            whyNot = null!;
            return true;
        }
    }
}
