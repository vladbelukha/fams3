using IdentityModel.Client;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace SearchApi.Web.Auth
{
    /// <summary>
    /// HTTP message handler for outbound requests to dynadapter.
    /// Toggles between JWT bearer token (when enabled) and X-ApiKey header (when disabled).
    /// </summary>
    public class DynadapterTokenHandler : DelegatingHandler
    {
        private const string CacheKey = "dynadapter_access_token";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DynadapterTokenHandler> _logger;

        public DynadapterTokenHandler(
            IHttpClientFactory httpClientFactory,
            IMemoryCache cache,
            IConfiguration configuration,
            ILogger<DynadapterTokenHandler> logger)
        {
            _httpClientFactory = httpClientFactory;
            _cache = cache;
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var jwtEnabled = _configuration.GetValue<bool>("auth:dynadapter:enabled", defaultValue: false);

            if (jwtEnabled)
            {
                var token = await GetTokenAsync(cancellationToken);
                if (!string.IsNullOrEmpty(token))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    _logger.LogDebug("Request sent with JWT bearer token");
                }
            }
            else
            {
                var apiKey = _configuration["SearchApi:ApiKeyForDynadaptor"];
                if (!string.IsNullOrEmpty(apiKey))
                {
                    request.Headers.Add("X-ApiKey", apiKey);
                    _logger.LogDebug("Request sent with X-ApiKey header (JWT disabled)");
                }
            }

            return await base.SendAsync(request, cancellationToken);
        }

        private async Task<string> GetTokenAsync(CancellationToken cancellationToken)
        {
            if (_cache.TryGetValue(CacheKey, out string cachedToken) && !string.IsNullOrWhiteSpace(cachedToken))
            {
                return cachedToken;
            }

            var tokenUrl = _configuration["auth:dynadapter:tokenUrl"];
            var clientId = _configuration["auth:dynadapter:clientId"];
            var clientSecret = _configuration["auth:dynadapter:clientSecret"];

            if (string.IsNullOrWhiteSpace(tokenUrl) || string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            {
                _logger.LogWarning("Dynadapter token configuration incomplete. Proceeding without JWT token.");
                return string.Empty;
            }

            try
            {
                using var httpClient = _httpClientFactory.CreateClient("dynadapter_token");
                var response = await httpClient.RequestClientCredentialsTokenAsync(
                    new ClientCredentialsTokenRequest
                    {
                        Address = tokenUrl,
                        ClientId = clientId,
                        ClientSecret = clientSecret,
                    },
                    cancellationToken);

                if (response.IsError)
                {
                    _logger.LogError(
                        "Dynadapter token acquisition failed: {Error} - {Description}",
                        response.Error,
                        response.ErrorDescription);
                    return string.Empty;
                }

                var expiresIn = response.ExpiresIn > 60 ? response.ExpiresIn - 60 : response.ExpiresIn;
                _cache.Set(CacheKey, response.AccessToken, TimeSpan.FromSeconds(expiresIn));

                return response.AccessToken;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during token acquisition for dynadapter");
                return string.Empty;
            }
        }
    }
}
