namespace DMM.AssetManagers.Stores
{
	public interface IStoreProvider
	{
		// Resolve manifest id / depot key and any CDN auth metadata needed to download a manifest.
		System.Threading.Tasks.Task<StoreManifestResolution> ResolveManifestAsync(int appId, int depotId, long? manifestId = null, System.Threading.CancellationToken ct = default);
	}
}