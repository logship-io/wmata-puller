namespace Logship.WmataPuller
{
    internal interface IDataUploaderSource
    {
        /// <summary>
        /// Gets the name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Fetch json log entries.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns>The list of log entries to upload.</returns>
        Task<IReadOnlyList<JsonLogEntrySchema>> FetchDataAsync(
            CancellationToken token);
    }
}
