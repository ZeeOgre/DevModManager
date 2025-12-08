using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
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
		public IReadOnlyDictionary<int, InstalledDepotInfo> InstalledDepots { get; init; } = new Dictionary<int, InstalledDepotInfo>(0);
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
			if (string.IsNullOrWhiteSpace(acfPath)) throw new ArgumentNullException(nameof(acfPath));
			if (!File.Exists(acfPath)) throw new FileNotFoundException(nameof(acfPath), acfPath);

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
			var depotEntryRegex = new Regex(@"""(?<depot>\d+)""\s*\{\s*(?<body>[^}]*)\}", RegexOptions.Compiled | RegexOptions.Singleline);
			var kvRegex = new Regex(@"""(?<k>[^""]+)""\s*""(?<v>[^""]*)""", RegexOptions.Compiled);

			foreach (Match m in depotEntryRegex.Matches(block))
			{
				if (!uint.TryParse(m.Groups["depot"].Value, out var depotId)) continue;
				var body = m.Groups["body"].Value;
				ulong manifest = 0;
				long size = 0;
				foreach (Match kv in kvRegex.Matches(body))
				{
					var k = kv.Groups["k"].Value;
					var v = kv.Groups["v"].Value;
					if (string.Equals(k, "manifest", StringComparison.OrdinalIgnoreCase))
						ulong.TryParse(v, out manifest);
					else if (string.Equals(k, "size", StringComparison.OrdinalIgnoreCase))
						long.TryParse(v, out size);
				}

				results.Add(new AcfDepotInfo { DepotId = depotId, ManifestId = manifest, Size = size });
			}

			return results;
		}

		/// <summary>
		/// Lightweight SteamKit2 wrapper that manages a SteamClient, performs login and exposes events for SteamGuard/2FA.
		/// It is intentionally minimal — it establishes a session and lets callers request AppInfo (resolution step will be separate).
		/// </summary>
		public sealed class SteamKitClient : IDisposable
		{
			readonly SteamClient _client;
			readonly CallbackManager _callbacks;
			readonly SteamUser _steamUser;
			readonly SteamApps _steamApps;

			Task? _callbackLoopTask;
			CancellationTokenSource? _cts;

			// Login state handshake
			readonly TaskCompletionSource<EResult> _loginTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
			readonly TaskCompletionSource<(string authType, string details)> _steamGuardTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

			/// <summary>Raised when steam requires Steam Guard (email code) or 2FA code ("2FA"). Handler should call <see cref="SubmitAuthCode"/>.</summary>
			public event Action<string /* authType: email|2fa|qr */, string /* details (email hint or QR URI) */>? SteamGuardRequested;

			/// <summary>Returns last connection exception if any.</summary>
			public Exception? LastError { get; private set; }

			public SteamKitClient()
			{
				_client = new SteamClient();
				_callbacks = new CallbackManager(_client);
				_steamUser = _client.GetHandler<SteamUser>();
				_steamApps = _client.GetHandler<SteamApps>();
			}

			/// <summary>
			/// Connect and log on. If username is null or empty, an anonymous logon is attempted.
			/// If SteamGuard or 2FA is required, the <see cref="SteamGuardRequested"/> event will be raised and this method
			/// will wait until you call <see cref="SubmitAuthCode(string)"/>.
			/// </summary>
			public async Task<EResult> ConnectAndLoginAsync(string? username, string? password, CancellationToken cancellationToken = default)
			{
				_cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
				StartCallbackLoop(_cts.Token);

				try
				{
					_client.Connect();

					// register callbacks
					new Callback<SteamClient.ConnectedCallback>(OnConnected, _callbacks);
					new Callback<SteamClient.DisconnectedCallback>(OnDisconnected, _callbacks);
					new Callback<SteamUser.LoggedOnCallback>(OnLoggedOn, _callbacks);
					new Callback<SteamUser.LoggedOffCallback>(OnLoggedOff, _callbacks);
					new Callback<SteamUser.UpdateMachineAuthCallback>(OnMachineAuth, _callbacks);

					// If no credentials provided -> anonymous logon
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
							// Don't persist tokens here — caller will handle caching if desired
							ShouldRememberPassword = false
						};

						_steamUser.LogOn(details);
					}

					// wait for login result or SteamGuard prompt
					using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
					var completed = await Task.WhenAny(_loginTcs.Task, _steamGuardTcs.Task).ConfigureAwait(false);

					if (completed == _steamGuardTcs.Task)
					{
						// Steam requested auth (2FA or email)
						var payload = await _steamGuardTcs.Task.ConfigureAwait(false);
						SteamGuardRequested?.Invoke(payload.authType, payload.details);

						// Wait for login to succeed after caller calls SubmitAuthCode
						return await _loginTcs.Task.ConfigureAwait(false);
					}
					else
					{
						return await _loginTcs.Task.ConfigureAwait(false);
					}
				}
				catch (Exception ex)
				{
					LastError = ex;
					throw;
				}
			}

			void StartCallbackLoop(CancellationToken token)
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
					catch (OperationCanceledException) { }
				}, token);
			}

			// Called by the app when it has a code (email or 2FA) obtained from user / QR flow.
			public void SubmitAuthCode(string code)
			{
				// SteamKit2 accepts providing a TwoFactorCode or auth code via LogOn again with the same credentials,
				// or by using the LoginKey workflow. The simple approach: call LogOn again with the code set.
				// Note: caller should have stored username/password to resubmit — here we assume the client reuses them.
				// For a production flow, store the LoginKey from LoggedOnCallback and call LoginKey if available.
				// For simplicity we'll set the expected code fields via LogOnDetails and re-call LogOn.
				// Because we don't keep the last LogOnDetails in this minimal wrapper, callers should re-call ConnectAndLoginAsync
				// with credentials if they want to re-submit. To simplify, we'll also set the login result to failure if no flow continues.
				// TODO: For full UX implement storing last LogOnDetails and submitting the code properly.

				// Signal to the login flow that an auth code has been provided.
				_loginTcs.TrySetException(new InvalidOperationException("SubmitAuthCode invoked; please re-call ConnectAndLoginAsync with credentials to complete login (TODO: implement resend in wrapper)."));
			}

			void OnConnected(SteamClient.ConnectedCallback obj)
			{
				Debug.WriteLine("Steam: Connected to " + obj.ServerAddress);
			}

			void OnDisconnected(SteamClient.DisconnectedCallback obj)
			{
				Debug.WriteLine("Steam: Disconnected.");
				// reset login TCS if not completed
				_loginTcs.TrySetResult(EResult.OK);
			}

			void OnLoggedOff(SteamUser.LoggedOffCallback obj)
			{
				Debug.WriteLine($"Steam: Logged off ({obj.Result}).");
			}

			void OnMachineAuth(SteamUser.UpdateMachineAuthCallback obj)
			{
				// This callback is used for "remembering machine" feature; ignore for now.
			}

			void OnLoggedOn(SteamUser.LoggedOnCallback cb)
			{
				Debug.WriteLine($"Steam: LoggedOn result={cb.Result}");

				if (cb.Result == EResult.OK)
				{
					_loginTcs.TrySetResult(cb.Result);
				}
				else if (cb.Result == EResult.AccountLogonDenied || cb.Result == EResult.AccountLoginDeniedNeedTwoFactor)
				{
					// Two-factor or SteamGuard required. SteamKit2 provides additional fields in the callback.
					// When AccountLoginDeniedNeedTwoFactor -> need 2FA code; when AccountLogonDenied -> email code.
					string authType = cb.Result == EResult.AccountLoginDeniedNeedTwoFactor ? "2fa" : "email";
					string details = string.Empty;

					// SteamKit2's LoggedOnCallback includes the message SteamGuardRequired? Not all versions expose same fields.
					// Provide minimal details to the caller.
					_steamGuardTcs.TrySetResult((authType, details));
				}
				else
				{
					_loginTcs.TrySetResult(cb.Result);
				}
			}

			/// <summary>
			/// Request app info; useful to ensure SteamApps has data required for manifest resolution.
			/// </summary>
			public Task RequestAppInfoAsync(uint appId)
			{
				// SteamApps.RequestAppInfo triggers internal AppInfo updates; we use a small delay for callback propagation.
				_steamApps.RequestAppInfo(new uint[] { appId });
				// Caller should wait small time or poll state; for now return completed task.
				return Task.CompletedTask;
			}

			/// <summary>
			/// Helper that attempts to extract the manifest id for a depot from Steam's app info.
			/// This inspects the KeyValues returned by SteamApps (SteamKit2) and tries to find the manifests node.
			/// Returns manifest id (ulong) or null if not present.
			/// </summary>
			public ulong? TryGetDepotManifestId(uint appId, uint depotId, string branch = "public")
			{
				var appInfo = _steamApps.GetAppInfo(appId);
				if (appInfo == null) return null;

				try
				{
					// appInfo is a KeyValue tree; navigate to depots -> <depotId> -> manifests -> <branch> -> gid
					var depots = appInfo["depots"];
					if (depots == null) return null;
					var depotNode = depots[depotId.ToString()];
					if (depotNode == null) return null;
					var manifests = depotNode["manifests"];
					if (manifests == null) return null;
					var branchNode = manifests[branch];
					if (branchNode == null) return null;
					var gidNode = branchNode["gid"];
					if (gidNode == null || string.IsNullOrWhiteSpace(gidNode.Value)) return null;
					if (ulong.TryParse(gidNode.Value, out var gid)) return gid;
				}
				catch
				{
					// tolerate any parsing differences
				}

				return null;
			}

			/// <summary>
			/// Shutdown and dispose client.
			/// </summary>
			public void Dispose()
			{
				try
				{
					_cts?.Cancel();
					_client?.Disconnect();
				}
				catch { }
			}
		}
	}
}