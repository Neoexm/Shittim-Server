namespace BlueArchiveAPI.Configuration
{
    public sealed class BlueArchiveVersionState
    {
        public string VersionId { get; init; } = "";
        public string CdnBaseUrl { get; init; } = "";

        public static BlueArchiveVersionState Current { get; private set; } = new();

        public static void Initialise(BlueArchiveVersionState state)
        {
            Current = state ?? throw new ArgumentNullException(nameof(state));
        }
    }
}
