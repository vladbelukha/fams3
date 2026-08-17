using IdentityModel.Client;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace SearchRequestAdaptor.Auth
{
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
            var token = await GetTokenAsync(cancellationToken);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
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
                throw new InvalidOperationException("Dynadapter token configuration is incomplete. Set auth:dynadapter:tokenUrl, auth:dynadapter:clientId, and auth:dynadapter:clientSecret.");
            }

            using var httpClient = _httpClientFactory.CreateClient("dynadapter_token");
            var response = await httpClient.RequestClientCredentialsTokenAsync(
                new ClientCredentialsTokenRequest
                {
                    Address = tokenUrl,
                    ClientId = clientId,
                    ClientSecret = clientSecret,
                },
                cancellationToken
            );

            if (response.IsError)
            {
                _logger.LogError(
                    "Dynadapter token acquisition failed: {Error} - {Description}",
                    response.Error,
                    response.ErrorDescription);

                throw new InvalidOperationException(
                    $"Dynadapter token acquisition failed: {response.Error} - {response.ErrorDescription}");
            }

            var expiresIn = response.ExpiresIn > 60 ? response.ExpiresIn - 60 : response.ExpiresIn;
            _cache.Set(CacheKey, response.AccessToken, TimeSpan.FromSeconds(expiresIn));

            return response.AccessToken;
        }
    }
}
