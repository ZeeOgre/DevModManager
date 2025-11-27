using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using Octokit;
using System.Windows;
using System.Net;

namespace ZO.DMM.AppNF
{
    public class OauthWebBrowserAuthenticator
    {
        public DateTime TokenExpiration { get; private set; }

        // Step 1: Initiate OAuth Authorization
        public async Task<string> GetGitHubToken()
        {
            var client = new GitHubClient(new ProductHeaderValue("ZO.DevModManager"));

            // The authorization URL, where GitHub will ask the user to authorize the app
            var request = new OauthLoginRequest("YourClientId")
            {
                Scopes = { "user", "repo" }, // Request specific scopes
                RedirectUri = new Uri("http://localhost:12345/")  // Set to a local redirect URI or your app's registered callback URI
            };

            var authorizationUrl = client.Oauth.GetGitHubLoginUrl(request);

            // Step 2: Open the browser for the user to authorize
            Process.Start(new ProcessStartInfo
            {
                FileName = authorizationUrl.ToString(),
                UseShellExecute = true
            });

            // Step 3: Handle the redirect to your application
            // You need to start an HTTP listener or use a predefined redirect URL to capture the authorization code
            string authorizationCode = await WaitForAuthorizationCodeAsync();

            // Step 4: Exchange authorization code for access token
            var token = await GetAccessTokenAsync(authorizationCode);

            return token.AccessToken;
        }

        // This method waits for the authorization code (using a local HTTP server or a manual method)
        private async Task<string> WaitForAuthorizationCodeAsync()
        {
            // Use an HTTP listener to wait for the redirect from GitHub with the authorization code
            var httpListener = new HttpListener();
            httpListener.Prefixes.Add("http://localhost:12345/");  // The same redirect URI used above
            httpListener.Start();

            var context = await httpListener.GetContextAsync();
            var authorizationCode = context.Request.QueryString["code"];  // Capture the code from the URL
            httpListener.Stop();

            return authorizationCode;
        }

        // Step 5: Exchange authorization code for an access token
        private async Task<OauthToken> GetAccessTokenAsync(string authorizationCode)
        {
            var client = new GitHubClient(new ProductHeaderValue("ZO.DevModManager"));

            var tokenRequest = new OauthTokenRequest("YourClientId", "YourClientSecret", authorizationCode)
            {
                RedirectUri = new Uri("http://localhost:12345/")  // Must match the redirect URI used earlier
            };

            var token = await client.Oauth.CreateAccessToken(tokenRequest);
            TokenExpiration = DateTime.UtcNow.AddSeconds(token.ExpiresIn > 0 ? token.ExpiresIn : TimeSpan.FromDays(30).TotalSeconds);  // Default to 30 days if no expiration provided
            return token;
        }
    }
}
