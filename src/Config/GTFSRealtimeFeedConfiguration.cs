namespace Logship.WmataPuller.Config
{
    public class GTFSRealtimeFeedConfiguration
    {
        /// <summary>
        /// Gets or sets the vehicle positions information.
        /// </summary>
        public string VehiclePositionsProtoEndpoint { get; set; }

        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();

        public bool IsValid(out string error)
        {
            if (string.IsNullOrEmpty(this.VehiclePositionsProtoEndpoint))
            {
                error = "The vehicle positions endpoint is not set.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
