using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DMM.AssetManagers.MAT
{
    public sealed class MatReadResult
    {
        public string Path { get; init; } = "";
        public List<string> TextureTokens { get; } = new();
        public JsonDocument? RawJson { get; init; }
    }

    public sealed class MAT
    {
        // Reads MAT file and optionally parses JSON content (if present).
        public MatReadResult Read(string matPath)
        {
            if (matPath == null) throw new ArgumentNullException(nameof(matPath));
            // TODO: implement reading and JSON parsing
            throw new NotImplementedException();
        }

        // Extract texture tokens (dds/png/jpg) referenced in MAT
        public IEnumerable<string> ExtractTextures(string matPath)
        {
            throw new NotImplementedException();
        }
    }
}
