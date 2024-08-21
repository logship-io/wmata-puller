
using Logship.WmataPuller;
using Logship.WmataPuller.Amtrak;
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

        var fetchers = new List<IDataUploaderSource>();

        using var client = new HttpClient();

        if (config.Amtrak?.Enabled == true)
        {
            fetchers.Add(new AmtrakDataPuller(config.Amtrak, client, log));
        }
        foreach (var feed in config.GTFS)
        {
            fetchers.Add(new GtfsProtobufDataPuller(feed.Key, client, feed.Value));
        }

        while (false == token.IsCancellationRequested)
        {
            await Parallel.ForEachAsync(fetchers, new ParallelOptions() { CancellationToken = token, MaxDegreeOfParallelism = config.MaxDegreeOfParallelism }, async (feed, token) =>
            {
                using var childCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                childCts.CancelAfter(TimeSpan.FromSeconds(45));
                var requestToken = childCts.Token;

                var timer = Stopwatch.StartNew();
                log.LogInformation("Fetching feed {name}", feed.Name);
                try
                {
                    var results = await feed.FetchDataAsync(requestToken);
                    await UploadMetrics(client, config.LogshipEndpoint!, config.InflowSubscription!.Value, config.BearerToken, results, requestToken);
                    log.LogInformation("Finished fetching feed {name} in {elapsed}", feed.Name, timer.Elapsed);
                }
                catch (Exception ex)
                {
                    log.LogError(ex, "Failed to pull gtfs feed data for feed {feed}", feed.Name);
                }
            });

            await Task.Delay(config.Interval, token);
        }
    }

    private static async Task UploadMetrics(HttpClient client, string endpoint, Guid subscription, string bearerToken, IReadOnlyList<JsonLogEntrySchema> entries, CancellationToken token)
    {
        var content = JsonSerializer.Serialize<IReadOnlyList<JsonLogEntrySchema>>(entries, SourceGenerationContext.Default.IReadOnlyListJsonLogEntrySchema);

        var stringContent = new StringContent(content, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"));
        stringContent.Headers.TryAddWithoutValidation("Authorization", "Bearer " + bearerToken);

        await client.PutAsync(
            $"{endpoint}/inflow/{subscription}",
            stringContent);
    }
}
