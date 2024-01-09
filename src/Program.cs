
using Logship.WmataPuller;
using Logship.WmataPuller.Config;
using Logship.WmataPuller.GTFS;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;


public class Program
{
    public static async Task Main(string[] args)
    {
        var tokenSource = new CancellationTokenSource();
        Console.CancelKeyPress += Console_CancelKeyPress;
        var token = tokenSource.Token;
        void Console_CancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            tokenSource.Cancel();
            e.Cancel = true;
        }

#pragma warning disable CS0618 // Type or member is obsolete
        var consoleOptions = new SimpleOptionsMonitor<ConsoleLoggerOptions>(new ConsoleLoggerOptions
        {
            //FormatterName = ConsoleFormatterNames.Systemd,
            Format = ConsoleLoggerFormat.Systemd,
            IncludeScopes = true,
            UseUtcTimestamp = true,
        });
#pragma warning restore CS0618 // Type or member is obsolete

        var consoleProvider = new ConsoleLoggerProvider(options: consoleOptions, formatters: null);
        using var loggerFactory = new LoggerFactory(new ILoggerProvider[] { consoleProvider },
            new SimpleOptionsMonitor<LoggerFilterOptions>(new LoggerFilterOptions
            {
                MinLevel = LogLevel.Debug,
            }),
            Options.Create(new LoggerFactoryOptions
            {
                ActivityTrackingOptions = ActivityTrackingOptions.SpanId | ActivityTrackingOptions.TraceId | ActivityTrackingOptions.ParentId | ActivityTrackingOptions.Tags
            }));

        var log = loggerFactory.CreateLogger("Logship.WmataPuller");

        var configBuilder = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddJsonFile($"appsettings.{System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json", optional: true);

        var config = new Configuration();
        configBuilder.Build().Bind(config);

        if (false == config.IsValid(out var reason))
        {
            log.LogCritical("appsettings.json was found. But is not valid. {reason}", reason);
            return;
        }

        var startupThresholds = new ConcurrentDictionary<string, DateTime>();
        using var client = new HttpClient();

        while (false == token.IsCancellationRequested)
        {
            await Parallel.ForEachAsync(config.GTFS, new ParallelOptions() { CancellationToken = token, MaxDegreeOfParallelism = config.MaxDegreeOfParallelism }, async (feed, token) =>
            {
                using var childCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                childCts.CancelAfter(TimeSpan.FromSeconds(60));
                var requestToken = childCts.Token;

                var preRequestTime = DateTime.UtcNow;
                var oldRange = startupThresholds.GetValueOrDefault(feed.Key, DateTime.UtcNow - config.Interval);

                var timer = Stopwatch.StartNew();
                log.LogInformation("Fetching feed {name}", feed.Key);
                try
                {
                    var results = (await GTFSDataPuller.FetchVehiclePositions(feed.Key, client, feed.Value, requestToken))
                        .Where(r => r.Timestamp >= oldRange).ToList();

                    if (results.Count == 0)
                    {
                        log.LogInformation("Fetched {count} entries for feed {name}", results.Count, feed.Key);
                        return;
                    }
                    await UploadMetrics(client, config.LogshipEndpoint!, results, requestToken);
                    log.LogInformation("Finished fetching feed {name} in {elapsed}", feed.Key, timer.Elapsed);

                    startupThresholds.AddOrUpdate(feed.Key, preRequestTime, (k, v) => preRequestTime);
                }
                catch (Exception ex)
                {
                    log.LogError(ex, "Failed to pull gtfs feed data for feed {feed}", feed.Key);
                }
            });

            await Task.Delay(config.Interval, token);
        }
    }

    private static async Task UploadMetrics(HttpClient client, string endpoint, IReadOnlyList<JsonLogEntrySchema> entries, CancellationToken token)
    {
        var content = JsonSerializer.Serialize<IReadOnlyList<JsonLogEntrySchema>>(entries, SourceGenerationContext.Default.IReadOnlyListJsonLogEntrySchema);
        await client.PutAsync($"{endpoint}/inflow/{Guid.Empty}", new StringContent(content, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json")));
    }
}
