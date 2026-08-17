using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using IdentityModel.Client;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DynamicsAdapter.Web.Auth
{
    public class RequestApiTokenHandler : DelegatingHandler
    {
        private const string CacheKey = "request_api_access_token";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _configuration;
        private readonly ILogger<RequestApiTokenHandler> _logger;

        public RequestApiTokenHandler(
            IHttpClientFactory httpClientFactory,
            IMemoryCache cache,
            IConfiguration configuration,
            ILogger<RequestApiTokenHandler> logger)
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

            var tokenUrl = _configuration["auth:requestApi:tokenUrl"];
            var clientId = _configuration["auth:requestApi:clientId"];
            var clientSecret = _configuration["auth:requestApi:clientSecret"];

            if (string.IsNullOrWhiteSpace(tokenUrl) || string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            {
                throw new InvalidOperationException("Request API token configuration is incomplete. Set auth:requestApi:tokenUrl, auth:requestApi:clientId, and auth:requestApi:clientSecret.");
            }

            using var httpClient = _httpClientFactory.CreateClient("request_api_token");
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
                    "Request API token acquisition failed: {Error} - {Description}",
                    response.Error,
                    response.ErrorDescription);

                throw new InvalidOperationException(
                    $"Request API token acquisition failed: {response.Error} - {response.ErrorDescription}");
            }

            var expiresIn = response.ExpiresIn > 60 ? response.ExpiresIn - 60 : response.ExpiresIn;
            _cache.Set(CacheKey, response.AccessToken, TimeSpan.FromSeconds(expiresIn));

            return response.AccessToken;
        }
    }
}
