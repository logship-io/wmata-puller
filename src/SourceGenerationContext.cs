using Logship.WmataPuller.Config;
using System.Text.Json.Serialization;

namespace Logship.WmataPuller
{
    [JsonSerializable(typeof(BusPositionsWrapper))]
    [JsonSerializable(typeof(IReadOnlyList<JsonLogEntrySchema>))]
    [JsonSerializable(typeof(string))]
    [JsonSerializable(typeof(bool))]
    [JsonSerializable(typeof(long))]
    [JsonSerializable(typeof(double))]
    [JsonSerializable(typeof(Configuration))]
    internal partial class SourceGenerationContext : JsonSerializerContext
    {
    }
}
