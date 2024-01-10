using Google.Protobuf.WellKnownTypes;
using Logship.WmataPuller.Config;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Logship.WmataPuller.Amtrak
{
    internal class AmtrakDataPuller : IDataUploaderSource
    {
        private static readonly SourceGenerationContext sourceGenerationContext = new SourceGenerationContext(new JsonSerializerOptions
        {
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            ReadCommentHandling = JsonCommentHandling.Skip,

        });
        private static readonly byte[] keyDerviationInitializationVector = new byte[] { 0xc6, 0xeb, 0x2f, 0x7f, 0x5c, 0x47, 0x40, 0xc1, 0xa2, 0xf7, 0x08, 0xfe, 0xfd, 0x94, 0x7d, 0x39 };

        private static readonly TimeZoneInfo EASTERN_TIME = TimeZoneInfo.GetSystemTimeZones().First(tz => tz.StandardName == "Eastern Standard Time");
        private static readonly TimeZoneInfo CENTRAL_TIME = TimeZoneInfo.GetSystemTimeZones().First(tz => tz.StandardName == "Central Standard Time");
        private static readonly TimeZoneInfo MOUNTAIN_TIME = TimeZoneInfo.GetSystemTimeZones().First(tz => tz.StandardName == "Mountain Standard Time");
        private static readonly TimeZoneInfo PACIFIC_TIME = TimeZoneInfo.GetSystemTimeZones().First(tz => tz.StandardName == "Pacific Standard Time");


        private readonly AmtrackConfiguration config;
        private readonly HttpClient client;
        private readonly ILogger logger;

        public string Name => "Amtrak";

        public AmtrakDataPuller(AmtrackConfiguration config, HttpClient client, ILogger logger)
        {
            this.config = config;
            this.client = client;
            this.logger = logger;
        }


        public async Task<IReadOnlyList<JsonLogEntrySchema>> FetchDataAsync(CancellationToken token)
        {
            // Fetch the full blob.
            var amtrakDatablob = await this.client.GetAsync(this.config.AmtrackEndpoint, token);
            amtrakDatablob.EnsureSuccessStatusCode();

            // Read the entire content blob.
            var content = await amtrakDatablob.Content.ReadAsStringAsync(token);

            // Main data is the first part of the blob.
            var mainData = content.Substring(0, content.Length - 88);

            // They encrypt the private key with a key derived from the string "69af143c-e8cf-47f8-bf09-fc1f61e5cc33"
            var encryptedPrivateKey = content.Substring(content.Length - 88);

            var privateKeyBytes = DecryptKeyString(Convert.FromBase64String(encryptedPrivateKey), "69af143c-e8cf-47f8-bf09-fc1f61e5cc33");

            // The private key is a string delimited by a pipe character, with a timestamp on the end.
            var privateKeyString = Encoding.UTF8.GetString(privateKeyBytes).Split('|')[0];

            var decryptedMainData = DecryptKeyString(Convert.FromBase64String(mainData), privateKeyString);
            var jsonString = Encoding.UTF8.GetString(decryptedMainData);

            var dataset = JsonSerializer.Deserialize<AmtrakDataBlob>(jsonString, sourceGenerationContext.AmtrakDataBlob);

            if (dataset == null || dataset.Features == null)
            {
                this.logger.LogError("Failed to deserialize Amtrak data blob.");
                return Array.Empty<JsonLogEntrySchema>();
            }

            var results = new List<JsonLogEntrySchema>();

            foreach (var item in dataset.Features)
            {
                if (item.Properties == null)
                {
                    continue;
                }

                var fields = new Dictionary<string, object>
                {
                    { "feedName", this.Name },
                    //{ "currentStopSequence", vehicle.CurrentStopSequence },
                    { "stopId", item.Properties.DestCode },
                    { "currentStatus", item.Properties.TrainStatus },
                    //{ "congestionLevel", vehicle.CongestionLevel.ToString() },
                    //{ "occupancyStatus", vehicle.OccupancyStatus.ToString()},
                };

                fields.Add("positionLatitude", item.Geometry!.Coordinates![1]);
                fields.Add("positionLongitude", item.Geometry.Coordinates![0]);
                //fields.Add("positionBearing", item.Properties.Heading);
                fields.Add("positionSpeed", string.IsNullOrWhiteSpace(item.Properties.Velocity) ? 0.0 : double.Parse(item.Properties.Velocity));

                fields.Add("vehicleId", item.Properties.TrainNum);
                fields.Add("vehicleLabel", item.Properties.TrainNum);
                fields.Add("vehicleLicensePlate", item.Properties.TrainNum);

                fields.Add("tripId", item.Properties.Route);
                fields.Add("routeId", item.Properties.Route);

                var timestamp = DateTime.ParseExact(false == string.IsNullOrWhiteSpace(item.Properties.LastValTS) ? item.Properties.LastValTS : item.Properties.CreatedAt, "M/d/yyyy h:mm:ss tt", CultureInfo.InvariantCulture);

                var lastUpdateTime = timestamp - TimeZoneConverter(item.Properties.EventTZ ?? item.Properties.OriginTZ).BaseUtcOffset;

                var log = new JsonLogEntrySchema("GTFS.VehiclePositions", lastUpdateTime, fields);
                results.Add(log);
            }

            return results;
        }

        private static byte[] DecryptKeyString(byte[] content, string key)
        {
            var pbkdf2 = KeyDerivation.Pbkdf2(key, new byte[] { 0x9a, 0x36, 0x86, 0xac }, KeyDerivationPrf.HMACSHA1, 1000, 16);

            using var aes = Aes.Create();
            aes.Padding = PaddingMode.PKCS7;
            var decrypter = aes.CreateDecryptor(pbkdf2, keyDerviationInitializationVector);

            var outputStream = new MemoryStream();
            using (var decryptorStream = new CryptoStream(new MemoryStream(content), decrypter, CryptoStreamMode.Read))
            {
                decryptorStream.CopyTo(outputStream);
            }

            outputStream.Position = 0;
            return outputStream.ToArray();
        }

        private static TimeZoneInfo TimeZoneConverter(string timeZoneCode)
        {
            switch (timeZoneCode)
            {
                case "E":
                    return EASTERN_TIME;
                case "C":
                    return CENTRAL_TIME;
                case "M":
                    return MOUNTAIN_TIME;
                case "P":
                    return PACIFIC_TIME;
                default:
                    throw new ArgumentException("Unknown timezone: " + timeZoneCode);
            }
        }
    }
}
