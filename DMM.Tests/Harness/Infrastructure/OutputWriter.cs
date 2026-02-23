using System;
using System.IO;
using System.Linq;
using System.Text.Json;

using DMM.AssetManagers.GameStores.Common.Models;
using DMM.Core.IO.Converters;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DMM.Tests.Harness.Infrastructure;

public static class OutputWriter
{
    public static void Write(InstallSnapshot snapshot, CliArgs args)
    {
        var format = (args.Format ?? "yaml").Trim().ToLowerInvariant();

        object payload = snapshot;
        if (snapshot.Identity.Scope == ScanScope.StoresAll)
            payload = BuildMergedOutput(snapshot);

        string text = format switch
        {
            "yaml" => ToYaml(payload),
            "yml" => ToYaml(payload),
            "json" => ToJson(payload),
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

    private static string ToYaml(object snapshot)
    {
        var serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .DisableAliases()
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.Preserve) // keep []
        .Build();


        return serializer.Serialize(snapshot);
    }

    private static string ToJson(object snapshot)
    {
        var opts = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        return JsonSerializer.Serialize(snapshot, opts);
    }

    private static MergedInstallSnapshot BuildMergedOutput(InstallSnapshot snapshot)
    {
        var appsByStore = snapshot.Apps
            .GroupBy(a => a.Id.StoreKey ?? "unknown", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<AppInstallSnapshot>)g.ToList(), StringComparer.OrdinalIgnoreCase);

        var issuesByStore = snapshot.Issues
            .GroupBy(i => i.StoreKey ?? "unknown", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ScanIssue>)g.ToList(), StringComparer.OrdinalIgnoreCase);

        var storeKeys = new HashSet<string>(appsByStore.Keys, StringComparer.OrdinalIgnoreCase);
        storeKeys.UnionWith(issuesByStore.Keys);

        var stores = new Dictionary<string, StorePartition>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in storeKeys)
        {
            stores[key] = new StorePartition
            {
                Apps = appsByStore.TryGetValue(key, out var apps) ? apps : Array.Empty<AppInstallSnapshot>(),
                Issues = issuesByStore.TryGetValue(key, out var issues) ? issues : Array.Empty<ScanIssue>()
            };
        }

        return new MergedInstallSnapshot
        {
            Identity = snapshot.Identity,
            Apps = snapshot.Apps,
            Issues = snapshot.Issues,
            Stores = stores
        };
    }

    private sealed record StorePartition
    {
        public required IReadOnlyList<AppInstallSnapshot> Apps { get; init; }
        public required IReadOnlyList<ScanIssue> Issues { get; init; }
    }

    private sealed record MergedInstallSnapshot
    {
        public required SnapshotIdentity Identity { get; init; }
        public required IReadOnlyList<AppInstallSnapshot> Apps { get; init; }
        public required IReadOnlyList<ScanIssue> Issues { get; init; }
        public required IReadOnlyDictionary<string, StorePartition> Stores { get; init; }
    }
}
