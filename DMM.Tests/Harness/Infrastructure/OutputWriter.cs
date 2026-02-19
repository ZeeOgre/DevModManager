using System;
using System.IO;
using System.Text.Json;

using DMM.AssetManagers.GameStores.Common.Models;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DMM.Tests.Harness.Infrastructure;

public static class OutputWriter
{
    public static void Write(InstallSnapshot snapshot, CliArgs args)
    {
        var format = (args.Format ?? "yaml").Trim().ToLowerInvariant();

        string text = format switch
        {
            "yaml" => ToYaml(snapshot),
            "yml" => ToYaml(snapshot),
            "json" => ToJson(snapshot),
            _ => throw new ArgumentException($"Unknown format '{args.Format}'. Use yaml|json.")
        };

        if (string.IsNullOrWhiteSpace(args.OutPath))
        {
            Console.WriteLine(text);
            return;
        }

        var outPath = args.OutPath!;
        var dir = Path.GetDirectoryName(outPath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(outPath, text);
        Console.WriteLine($"Wrote: {outPath}");
    }

    private static string ToYaml(InstallSnapshot snapshot)
    {
        var serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults)
            .Build();

        return serializer.Serialize(snapshot);
    }

    private static string ToJson(InstallSnapshot snapshot)
    {
        var opts = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        return JsonSerializer.Serialize(snapshot, opts);
    }
}
