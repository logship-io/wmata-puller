using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Logship.WmataPuller.Bus
{
    internal class BusPositionFetcher
    {
        private readonly HttpClient client;
        private readonly ILogger logger;

        private readonly string apiKey;
        private readonly string rootEndpoint;
        private readonly TimeZoneInfo estTimeZone;

        public BusPositionFetcher(
            HttpClient client,
            string rootEndpoint,
            string apiKey,
            ILogger logger)
        {
            this.client = client;
            this.logger = logger;

            this.rootEndpoint = rootEndpoint;
            this.apiKey = apiKey;
            this.estTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        }

        public async Task<IReadOnlyList<JsonLogEntrySchema>> PullBusPositionsAsync(CancellationToken token)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.wmata.com/Bus.svc/json/jBusPositions");
            request.Headers.TryAddWithoutValidation("api_key", this.apiKey);
            var result = await this.client.SendAsync(request);

            var results = await JsonSerializer.DeserializeAsync<BusPositionsWrapper>(await result.Content.ReadAsStreamAsync(), SourceGenerationContext.Default.BusPositionsWrapper, token);
            var metrics = new List<JsonLogEntrySchema>(results.BusPositions.Count);

            for (var i = 0; i < results.BusPositions.Count; i++)
            {
                var busInfo = results.BusPositions[i];
                metrics.Add(new JsonLogEntrySchema("wmata.buspositions", DateTimeOffset.UtcNow.UtcDateTime, new Dictionary<string, object>
                {
                    { "last_update", new DateTimeOffset(busInfo.DateTime, this.estTimeZone.BaseUtcOffset).UtcDateTime },
                    { "delay_minutes", busInfo.Deviation },
                    { "direction", busInfo.DirectionText },
                    { "latitude", busInfo.Lat },
                    { "longitude", busInfo.Lon },
                    { "route_id", busInfo.RouteID },
                    { "trip_end", new DateTimeOffset(busInfo.TripEndTime, this.estTimeZone.BaseUtcOffset).UtcDateTime },
                    { "trip_begin", new DateTimeOffset(busInfo.TripStartTime, this.estTimeZone.BaseUtcOffset).UtcDateTime },
                    { "trip_id", busInfo.TripID },
                    { "vehicle_id", busInfo.VehicleID },
                }));
            }
            return metrics;
        }
    }
}
