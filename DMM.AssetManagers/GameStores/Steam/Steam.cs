using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SteamKit2;

namespace DMM.AssetManagers.GameStores.Steam
{
    public sealed class SteamGame
    {
        public int AppId { get; init; }
        public string? Name { get; init; }
        public string? InstallDir { get; init; }
        public IReadOnlyDictionary<int, InstalledDepotInfo> InstalledDepots { get; init; } =
            new Dictionary<int, InstalledDepotInfo>(0);
        public string SourceFilePath { get; init; } = string.Empty;
    }

    public sealed class InstalledDepotInfo
    {
        public long ManifestId { get; init; }
        public long Size { get; init; }
    }

    public sealed class DepotManifest
    {
        public int DepotId { get; init; }
        public long ManifestId { get; init; }
        public DateTime? Date { get; init; }
        public long TotalBytesOnDisk { get; init; }
        public long TotalBytesCompressed { get; init; }
        public List<DepotFile> Files { get; } = new();
    }

    public sealed class DepotFile
    {
        public long Size { get; init; }
        public int Chunks { get; init; }
        public string? Sha { get; init; }
        public int Flags { get; init; }
        public string? Path { get; init; }
    }

    public static partial class Steam
    {
        /// <summary>
        /// Simple DTO returned from parsing an appmanifest .acf
        /// </summary>
        public sealed class AcfDepotInfo
        {
            public uint DepotId { get; init; }
            public ulong ManifestId { get; init; }
            public long Size { get; init; }
        }

        /// <summary>
        /// Parse an appmanifest_*.acf file and return the installed depots (depot id + manifest id + size).
        /// This is lightweight and tolerant of variations in spacing.
        /// </summary>
        public static IReadOnlyList<AcfDepotInfo> ResolveDepotsFromAcf(string acfPath)
        {
            if (string.IsNullOrWhiteSpace(acfPath))
                throw new ArgumentNullException(nameof(acfPath));
            if (!File.Exists(acfPath))
                throw new FileNotFoundException(nameof(acfPath), acfPath);

            var text = File.ReadAllText(acfPath);

            var results = new List<AcfDepotInfo>();

            // Find the InstalledDepots block
            var instIndex = text.IndexOf("\"InstalledDepots\"", StringComparison.OrdinalIgnoreCase);
            if (instIndex < 0) return results;

            var braceIndex = text.IndexOf('{', instIndex);
            if (braceIndex < 0) return results;

            // Find matching closing brace for the InstalledDepots block (simple nesting)
            int idx = braceIndex + 1;
            int depth = 1;
            while (idx < text.Length && depth > 0)
            {
                if (text[idx] == '{') depth++;
                else if (text[idx] == '}') depth--;
                idx++;
            }

            if (depth != 0) return results;

            var block = text.Substring(braceIndex + 1, idx - braceIndex - 2);

            // Match entries like: "1716741" { "manifest" "1844812871651601030" "size" "269370312" }
            var depotEntryRegex = new Regex(
                @"""(?<depot>\d+)""\s*\{\s*(?<body>[^}]*)\}",
                RegexOptions.Compiled | RegexOptions.Singleline);

            var kvRegex = new Regex(
                @"""(?<k>[^""]+)""\s*""(?<v>[^""]*)""",
                RegexOptions.Compiled);

            foreach (Match m in depotEntryRegex.Matches(block))
            {
                if (!uint.TryParse(m.Groups["depot"].Value, out var depotId))
                    continue;

                var body = m.Groups["body"].Value;
                ulong manifest = 0;
                long size = 0;

                foreach (Match kv in kvRegex.Matches(body))
                {
                    var k = kv.Groups["k"].Value;
                    var v = kv.Groups["v"].Value;

                    if (string.Equals(k, "manifest", StringComparison.OrdinalIgnoreCase))
                    {
                        ulong.TryParse(v, out manifest);
                    }
                    else if (string.Equals(k, "size", StringComparison.OrdinalIgnoreCase))
                    {
                        long.TryParse(v, out size);
                    }
                }

                results.Add(new AcfDepotInfo
                {
                    DepotId = depotId,
                    ManifestId = manifest,
                    Size = size
                });
            }

            return results;
        }

        /// <summary>
        /// Very lightweight SteamKit2 wrapper: connect + logon.
        /// This intentionally avoids APIs that have been removed in SteamKit2 3.x (RequestAppInfo, GetAppInfo, etc.).
        /// </summary>
        public sealed class SteamKitClient : IDisposable
        {
            private readonly SteamClient _client;
            private readonly CallbackManager _callbacks;
            private readonly SteamUser _steamUser;

            private Task? _callbackLoopTask;
            private CancellationTokenSource? _cts;

            // Login state handshake
            private TaskCompletionSource<EResult> _loginTcs =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            /// <summary>Returns last connection exception if any.</summary>
            public Exception? LastError { get; private set; }

            public SteamKitClient()
            {
                _client = new SteamClient();
                _callbacks = new CallbackManager(_client);
                _steamUser = _client.GetHandler<SteamUser>()!;
            }

            /// <summary>
            /// Connect and log on. If username is null or empty, an anonymous logon is attempted.
            /// </summary>
            public async Task<EResult> ConnectAndLoginAsync(
                string? username,
                string? password,
                CancellationToken cancellationToken = default)
            {
                _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                StartCallbackLoop(_cts.Token);

                // reset login TCS in case this instance is reused
                _loginTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

                try
                {
                    _client.Connect();

                    // register callbacks
                    _callbacks.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
                    _callbacks.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);
                    _callbacks.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
                    _callbacks.Subscribe<SteamUser.LoggedOffCallback>(OnLoggedOff);

                    if (string.IsNullOrWhiteSpace(username))
                    {
                        _steamUser.LogOnAnonymous();
                    }
                    else
                    {
                        var details = new SteamUser.LogOnDetails
                        {
                            Username = username,
                            Password = password,
                            ShouldRememberPassword = false
                        };

                        _steamUser.LogOn(details);
                    }

                    using var reg = cancellationToken.Register(
                        () => _loginTcs.TrySetCanceled(cancellationToken));

                    return await _loginTcs.Task.ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LastError = ex;
                    _loginTcs.TrySetException(ex);
                    throw;
                }
            }

            private void StartCallbackLoop(CancellationToken token)
            {
                _callbackLoopTask ??= Task.Run(() =>
                {
                    try
                    {
                        while (!token.IsCancellationRequested)
                        {
                            _callbacks.RunWaitCallbacks(TimeSpan.FromMilliseconds(500));
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // normal shutdown
                    }
                }, token);
            }

            private void OnConnected(SteamClient.ConnectedCallback cb)
            {
                Debug.WriteLine("Steam: Connected.");
            }

            private void OnDisconnected(SteamClient.DisconnectedCallback cb)
            {
                Debug.WriteLine("Steam: Disconnected.");
                // If we got disconnected before a login result, surface it
                _loginTcs.TrySetResult(EResult.Fail);
            }

            private void OnLoggedOn(SteamUser.LoggedOnCallback cb)
            {
                Debug.WriteLine($"Steam: LoggedOn result={cb.Result}");
                _loginTcs.TrySetResult(cb.Result);
            }

            private void OnLoggedOff(SteamUser.LoggedOffCallback cb)
            {
                Debug.WriteLine($"Steam: Logged off ({cb.Result}).");
            }

            /// <summary>
            /// Shutdown and dispose client.
            /// </summary>
            public void Dispose()
            {
                try
                {
                    _cts?.Cancel();
                    _client.Disconnect();
                }
                catch
                {
                    // ignore cleanup errors
                }
            }
        }
    }
}
