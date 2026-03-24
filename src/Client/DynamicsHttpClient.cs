using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EfCore.Dynamics365.Client
{
    /// <summary>
    /// Low-level HTTP client for the Dataverse Web API.
    /// Handles OAuth token acquisition, retries, and JSON serialization.
    /// </summary>
    public sealed class DynamicsHttpClient : IDisposable
    {
        private readonly DynamicsClientOptions _options;
        private readonly HttpClient _http;
        private readonly IConfidentialClientApplication? _ccApp;
        private readonly IPublicClientApplication? _pcApp;
        private bool _disposed;

        public DynamicsHttpClient(DynamicsClientOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            options.Validate();

            _http = new HttpClient { Timeout = options.HttpTimeout };
            _http.DefaultRequestHeaders.Add("OData-MaxVersion", "4.0");
            _http.DefaultRequestHeaders.Add("OData-Version", "4.0");
            _http.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            if (!string.IsNullOrEmpty(options.ClientSecret))
            {
                _ccApp = ConfidentialClientApplicationBuilder
                    .Create(options.ClientId)
                    .WithClientSecret(options.ClientSecret)
                    .WithAuthority(options.Authority)
                    .Build();
            }
            else
            {
                _pcApp = PublicClientApplicationBuilder
                    .Create(options.ClientId)
                    .WithAuthority(options.Authority)
                    .Build();
            }
        }

        // ── Token acquisition ────────────────────────────────────────────────

        private string[] Scopes => new[] { _options.ServiceUrl.TrimEnd('/') + "/.default" };

        private async Task<string> GetAccessTokenAsync(CancellationToken ct)
        {
            AuthenticationResult result;

            if (_ccApp != null)
            {
                result = await _ccApp
                    .AcquireTokenForClient(Scopes)
                    .ExecuteAsync(ct)
                    .ConfigureAwait(false);
            }
            else
            {
                var accounts = await _pcApp!.GetAccountsAsync().ConfigureAwait(false);
                try
                {
                    result = await _pcApp
                        .AcquireTokenSilent(Scopes, accounts.FirstOrDefault())
                        .ExecuteAsync(ct)
                        .ConfigureAwait(false);
                }
                catch (MsalUiRequiredException)
                {
#pragma warning disable CS0618
                    result = await _pcApp
                        .AcquireTokenByUsernamePassword(Scopes, _options.Username, _options.Password)
                        .ExecuteAsync(ct)
                        .ConfigureAwait(false);
#pragma warning restore CS0618
                }
            }

            return result.AccessToken;
        }

        private async Task AuthorizeAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var token = await GetAccessTokenAsync(ct).ConfigureAwait(false);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        // ── Query (GET) ──────────────────────────────────────────────────────

        /// <summary>
        /// Executes an OData GET query and returns all result objects (handles @odata.nextLink paging).
        /// </summary>
        public async Task<IList<JObject>> QueryAsync(
            string entitySetName,
            string? odataQuery,
            CancellationToken ct = default)
        {
            var results = new List<JObject>();
            var url = _options.ApiBaseUrl + entitySetName;
            if (!string.IsNullOrEmpty(odataQuery))
                url += "?" + odataQuery;

            while (url != null)
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Add("Prefer", "odata.include-annotations=\"*\"");
                await AuthorizeAsync(req, ct).ConfigureAwait(false);

                using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
                await EnsureSuccessAsync(resp).ConfigureAwait(false);

                var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                var doc = JObject.Parse(json);

                if (doc["value"] is JArray arr)
                    foreach (var item in arr)
                        results.Add((JObject)item);

                url = doc["@odata.nextLink"]?.Value<string>();
            }

            return results;
        }

        // ── Create (POST) ────────────────────────────────────────────────────

        /// <summary>
        /// Creates a record and returns its new GUID primary key.
        /// </summary>
        public async Task<Guid> CreateAsync(
            string entitySetName,
            object payload,
            CancellationToken ct = default)
        {
            var body = JsonConvert.SerializeObject(payload);
            using var req = new HttpRequestMessage(HttpMethod.Post, _options.ApiBaseUrl + entitySetName);
            req.Content = new StringContent(body, Encoding.UTF8, "application/json");
            req.Headers.Add("Prefer", "return=representation");
            await AuthorizeAsync(req, ct).ConfigureAwait(false);

            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            await EnsureSuccessAsync(resp).ConfigureAwait(false);

            // The OData-EntityId header contains the URL with the new GUID
            if (resp.Headers.TryGetValues("OData-EntityId", out var vals))
            {
                var entityIdUrl = vals.FirstOrDefault();
                if (entityIdUrl != null)
                {
                    var start = entityIdUrl.LastIndexOf('(');
                    var end   = entityIdUrl.LastIndexOf(')');
                    if (start >= 0 && end > start)
                    {
                        var guidStr = entityIdUrl.Substring(start + 1, end - start - 1);
                        if (Guid.TryParse(guidStr, out var guid))
                            return guid;
                    }
                }
            }

            // Fallback: parse body
            var responseBody = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!string.IsNullOrEmpty(responseBody))
            {
                var doc = JObject.Parse(responseBody);
                // Dynamics primary key convention: {logicalname}id
                foreach (var prop in doc.Properties())
                {
                    if (prop.Name.EndsWith("id", StringComparison.OrdinalIgnoreCase)
                        && prop.Value.Type == JTokenType.String
                        && Guid.TryParse(prop.Value.Value<string>(), out var g))
                        return g;
                }
            }

            return Guid.Empty;
        }

        // ── Update (PATCH) ───────────────────────────────────────────────────

        /// <summary>
        /// Updates a record using PATCH (partial update).
        /// </summary>
        public async Task UpdateAsync(
            string entitySetName,
            Guid id,
            object payload,
            CancellationToken ct = default)
        {
            var body = JsonConvert.SerializeObject(payload);
            var url  = _options.ApiBaseUrl + entitySetName + "(" + id + ")";

            using var req = new HttpRequestMessage(new HttpMethod("PATCH"), url);
            req.Content = new StringContent(body, Encoding.UTF8, "application/json");
            req.Headers.Add("If-Match", "*"); // prevent accidental creates
            await AuthorizeAsync(req, ct).ConfigureAwait(false);

            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            await EnsureSuccessAsync(resp).ConfigureAwait(false);
        }

        // ── Delete (DELETE) ──────────────────────────────────────────────────

        /// <summary>
        /// Deletes a record by primary key.
        /// </summary>
        public async Task DeleteAsync(
            string entitySetName,
            Guid id,
            CancellationToken ct = default)
        {
            var url = _options.ApiBaseUrl + entitySetName + "(" + id + ")";
            using var req = new HttpRequestMessage(HttpMethod.Delete, url);
            await AuthorizeAsync(req, ct).ConfigureAwait(false);

            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            // 204 No Content is success for DELETE
            if (resp.StatusCode != HttpStatusCode.NoContent)
                await EnsureSuccessAsync(resp).ConfigureAwait(false);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static async Task EnsureSuccessAsync(HttpResponseMessage resp)
        {
            if (resp.IsSuccessStatusCode) return;

            var body = string.Empty;
            try { body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false); }
            catch { /* ignore */ }

            string? message = null;
            try
            {
                var err = JObject.Parse(body);
                message = err["error"]?["message"]?.Value<string>();
            }
            catch { /* ignore */ }

            throw new DynamicsRequestException(
                (int)resp.StatusCode,
                message ?? $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}",
                body);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _http.Dispose();
            _disposed = true;
        }
    }

    public sealed class DynamicsRequestException : Exception
    {
        public int StatusCode { get; }
        public string ResponseBody { get; }

        public DynamicsRequestException(int statusCode, string message, string responseBody)
            : base($"Dynamics 365 API error {statusCode}: {message}")
        {
            StatusCode   = statusCode;
            ResponseBody = responseBody;
        }
    }
}
