using System.Text.Json;
using System.Text.Json.Serialization;

namespace Logship.WmataPuller.Amtrak
{
    internal class AmtrakDataBlob
    {
        public string? Type { get; set; }

        public AmtrakFeature[]? Features { get; set; }
    }

    internal class AmtrakFeature
    {
        public string? Type { get; set; }

        public int Id { get; set; }

        public FeatureGeometry Geometry { get; set; }

        public FeatureProperties? Properties { get; set; }
    }

    public class FeatureGeometry
    {
        public string? Type { get; set; }

        public double[]? Coordinates { get; set; }
    }

    public class FeatureProperties
    {
        public int Id { get; set; }

        public string Route { get; set; } = string.Empty;

        public string TrainStatus { get; set; } = string.Empty;

        public string TrainNum { get; set; } = string.Empty;

        public string Aliases { get; set; } = string.Empty;

        [JsonPropertyName("EventTZ")]
        public string EventTZ { get; set; } = string.Empty;

        [JsonPropertyName("OriginTZ")]
        public string OriginTZ { get; set; } = string.Empty;

        [JsonPropertyName("LastValTS")]
        public string LastValTS { get; set; }

        [JsonPropertyName("created_at")]
        public string CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public string UpdatedAt { get; set; }

        public string Heading { get; set; } = string.Empty;

        public string Velocity { get; set; } = string.Empty;

        public string DestCode { get; set; } = string.Empty;

        [JsonExtensionData]
        public Dictionary<string, JsonElement> ExtraProperties { get; set; } = new Dictionary<string, JsonElement>();
    }
}
