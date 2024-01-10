
using Logship.WmataPuller.Config;
using TransitRealtime;

namespace Logship.WmataPuller.GTFS
{
    internal class GtfsProtobufDataPuller : IDataUploaderSource
    {
        private readonly string feedName;
        private readonly HttpClient client;
        private readonly GTFSRealtimeFeedConfiguration config;

        private DateTime lastFetch = DateTime.MinValue;

        public GtfsProtobufDataPuller(string feedName, HttpClient client, GTFSRealtimeFeedConfiguration config)
        {
            this.feedName = feedName;
            this.client = client;
            this.config = config;
        }

        public string Name => this.feedName;

        public async Task<IReadOnlyList<JsonLogEntrySchema>> FetchDataAsync(CancellationToken token)
        {
            var nextFetch = DateTime.UtcNow;

            using var request = new HttpRequestMessage(HttpMethod.Get, config.VehiclePositionsProtoEndpoint);
            foreach (var header in config.Headers)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            var result = await client.SendAsync(request, token);
            if (false == result.IsSuccessStatusCode)
            {
                return Array.Empty<JsonLogEntrySchema>();
            }

            var message = FeedMessage.Parser.ParseFrom(await result.Content.ReadAsStreamAsync(token));

            var now = DateTimeOffset.UtcNow;
            var results = new List<JsonLogEntrySchema>();
            foreach (var item in message.Entity)
            {
                var vehicle = item.Vehicle;

                if (null == vehicle)
                {
                    // We only process vehicle updates for now.
                    continue;
                }

                var fields = new Dictionary<string, object>
                {
                    { "feedName", feedName },
                    { "currentStopSequence", vehicle.CurrentStopSequence },
                    { "stopId", vehicle.StopId },
                    { "currentStatus", vehicle.CurrentStatus.ToString() },
                    { "congestionLevel", vehicle.CongestionLevel.ToString() },
                    { "occupancyStatus", vehicle.OccupancyStatus.ToString()},
                };

                if (null != vehicle.Position)
                {
                    fields.Add("positionLatitude", vehicle.Position.Latitude);
                    fields.Add("positionLongitude", vehicle.Position.Longitude);
                    fields.Add("positionBearing", vehicle.Position.Bearing);
                    fields.Add("positionSpeed", vehicle.Position.Speed);
                }

                if (null != vehicle.Vehicle)
                {
                    fields.Add("vehicleId", vehicle.Vehicle.Id);
                    fields.Add("vehicleLabel", vehicle.Vehicle.Label);
                    fields.Add("vehicleLicensePlate", vehicle.Vehicle.LicensePlate);
                }

                if (null != vehicle.Trip)
                {
                    fields.Add("tripId", vehicle.Trip.TripId);
                    fields.Add("routeId", vehicle.Trip.RouteId);
                }

                // Sometimes the send us a timestamp of 0, which is not valid, so just default to now;
                var timestamp = (long)vehicle.Timestamp != default && vehicle.Timestamp <= (ulong)now.ToUnixTimeSeconds()
                    ? DateTimeOffset.FromUnixTimeSeconds((long)vehicle.Timestamp).UtcDateTime
                    : now;

                if (timestamp < lastFetch)
                {
                    // Ignore old data.
                    continue;
                }

                var log = new JsonLogEntrySchema("GTFS.VehiclePositions", timestamp.UtcDateTime, fields);

                results.Add(log);
            }

            this.lastFetch = nextFetch;
            return results;
        }

        
    }
}
