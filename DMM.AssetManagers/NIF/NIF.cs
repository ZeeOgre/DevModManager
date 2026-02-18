namespace DMM.AssetManagers.NIF
{
    public sealed class NifReadResult
    {
        public string Path { get; init; } = "";
        public List<string> Mats { get; } = new();
        public List<string> Meshes { get; } = new();
        public List<string> OtherAssets { get; } = new();
    }

    public sealed class NIF
    {
        // Parse/validate the NIF file and populate metadata
        public NifReadResult Read(string nifPath)
        {
            if (nifPath == null) throw new ArgumentNullException(nameof(nifPath));
            // TODO: implement parsing logic
            throw new NotImplementedException();
        }

        // High level: extract every referenced asset token from the NIF
        public IEnumerable<string> ExtractAll(string nifPath)
        {
            // returns relative asset tokens (e.g. "Data\\Materials\\foo.mat", "geometries\\bar")
            throw new NotImplementedException();
        }

        // Return material tokens referenced by this NIF
        public IEnumerable<string> ExtractMat(string nifPath)
        {
            throw new NotImplementedException();
        }

        // Return mesh tokens/stems referenced by this NIF
        public IEnumerable<string> ExtractMesh(string nifPath)
        {
            throw new NotImplementedException();
        }
    }
}
