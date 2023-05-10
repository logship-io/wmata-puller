
using Logship.WmataPuller;
using Logship.WmataPuller.Bus;
using Logship.WmataPuller.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
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
        if (false == File.Exists("application.json"))
        {
            log.LogCritical("application.json not found");
            return;
        }

        var config = JsonSerializer.Deserialize<Configuration>(File.ReadAllText("application.json"), SourceGenerationContext.Default.Configuration);
        if (null == config)
        {
            log.LogCritical("application.json was found. But deserialization failed.");
            return;
        }

        using var client = new HttpClient();


        var fetcher = new BusPositionFetcher(client, "https://api.wmata.com/Bus.svc/json/jBusPositions", config.AuthToken!, log);

        while (false == token.IsCancellationRequested)
        {
            var values = await fetcher.PullBusPositionsAsync(token);
            log.LogInformation("uploading {count} bus position metrics", values.Count);
            await UploadMetrics(client, values, token);
            await Task.Delay(config.Interval, token);
        }
    }

    private static async Task UploadMetrics(HttpClient client, IReadOnlyList<JsonLogEntrySchema> entries, CancellationToken token)
    {
        var content = JsonSerializer.Serialize<IReadOnlyList<JsonLogEntrySchema>>(entries, SourceGenerationContext.Default.IReadOnlyListJsonLogEntrySchema);
        await client.PutAsync($"http://try.logship.ai:5000/inflow/{Guid.Empty}", new StringContent(content, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json")));
    }
}
