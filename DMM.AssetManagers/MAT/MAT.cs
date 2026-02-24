using System.Text.Json;
using System.Text.RegularExpressions;

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
        private static readonly Regex TextureTokenRegex =
            new(@"(?i)(?:^|[^A-Za-z0-9_])([A-Za-z0-9_./\\-]+\.(?:dds|png|jpe?g))(?:$|[^A-Za-z0-9_])",
                RegexOptions.Compiled);

        // Reads MAT file and parses JSON content when possible.
        public MatReadResult Read(string matPath)
        {
            if (matPath == null) throw new ArgumentNullException(nameof(matPath));
            if (!File.Exists(matPath)) throw new FileNotFoundException("MAT file not found", matPath);

            string fullPath = System.IO.Path.GetFullPath(matPath);
            string text = File.ReadAllText(fullPath);

            JsonDocument? json = null;
            var textures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                json = JsonDocument.Parse(text);
                ExtractFromJsonElement(json.RootElement, textures);
            }
            catch (JsonException)
            {
                // Some MATs are malformed or partially serialized; fall through to regex extraction.
            }

            ExtractFromText(text, textures);

            var result = new MatReadResult
            {
                Path = fullPath,
                RawJson = json
            };

            result.TextureTokens.AddRange(textures);
            return result;
        }

        // Extract texture tokens (dds/png/jpg) referenced in MAT
        public IEnumerable<string> ExtractTextures(string matPath)
        {
            return Read(matPath).TextureTokens;
        }

        private static void ExtractFromJsonElement(JsonElement element, HashSet<string> textures)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (JsonProperty property in element.EnumerateObject())
                    {
                        if (property.Value.ValueKind == JsonValueKind.String)
                            AddTextureToken(property.Value.GetString(), textures);
                        else
                            ExtractFromJsonElement(property.Value, textures);
                    }
                    break;
                case JsonValueKind.Array:
                    foreach (JsonElement item in element.EnumerateArray())
                        ExtractFromJsonElement(item, textures);
                    break;
                case JsonValueKind.String:
                    AddTextureToken(element.GetString(), textures);
                    break;
            }
        }

        private static void ExtractFromText(string text, HashSet<string> textures)
        {
            foreach (Match match in TextureTokenRegex.Matches(text))
            {
                if (match.Groups.Count < 2)
                    continue;

                AddTextureToken(match.Groups[1].Value, textures);
            }
        }

        private static void AddTextureToken(string? token, HashSet<string> textures)
        {
            if (string.IsNullOrWhiteSpace(token))
                return;

            string normalized = NormalizeToken(token);
            if (normalized.Length == 0)
                return;

            textures.Add(normalized);
        }

        private static string NormalizeToken(string token)
        {
            string normalized = token.Trim();
            normalized = normalized.Replace('/', '\\');

            while (normalized.StartsWith(".\\", StringComparison.Ordinal))
                normalized = normalized[2..];

            if (normalized.StartsWith("\\", StringComparison.Ordinal))
                normalized = normalized[1..];

            return normalized;
        }
    }
}
