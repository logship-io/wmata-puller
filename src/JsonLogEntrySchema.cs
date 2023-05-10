namespace Logship.WmataPuller
{
    internal record JsonLogEntrySchema(string Schema, DateTime Timestamp, IDictionary<string, object> Data)
    {
    }
}
