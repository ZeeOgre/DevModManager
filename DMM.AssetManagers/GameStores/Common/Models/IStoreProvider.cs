namespace DMM.AssetManagers.GameStores.Common.Models
{
    public interface IStoreProvider
    {
        // Resolve manifest id / depot key and any CDN auth metadata needed to download a manifest.
        Task<StoreManifestResolution> ResolveManifestAsync(int appId, int depotId, long? manifestId = null, CancellationToken ct = default);
    }
}