namespace DMM.AssetManagers.GameStores.Common
{
    public static class CatalogUtils
    {
        public static T? FindByIdentity<T>(List<T> catalog, string identityName, Func<T, string> identitySelector)
            where T : class
        {
            return catalog.FirstOrDefault(e => string.Equals(identitySelector(e), identityName, StringComparison.OrdinalIgnoreCase));
        }
    }
}