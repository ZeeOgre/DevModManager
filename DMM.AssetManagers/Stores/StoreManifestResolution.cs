namespace DMM.AssetManagers.Stores
{
    public sealed class StoreManifestResolution
    {
        public ulong ManifestId { get; init; }
        public byte[]? DepotKey { get; init; } // encryption key (if available)
        public string? CdnHost { get; init; }  // optional CDN host/token info
        public string? DiagnosticText { get; init; }
    }
}