using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DynamicsAdapter.Web.Configuration;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace DynamicsAdapter.Web.Auth
{
    public interface IOAuthApiClient
    {
        Task<Token> GetRefreshToken(CancellationToken cancellationToken);
    }

    /// <summary>
    /// The AdfsTokenClient interacts with the on-premise ADFS OAuth2 endpoint (resource owner
    /// password credentials grant) to obtain and refresh access tokens for Dynamics On-Premise.
    /// </summary>
    public class AdfsTokenClient : IOAuthApiClient
    {
        private readonly HttpClient _httpClient;

        private readonly AdfsOptions _adfsOptions;

        public AdfsTokenClient(HttpClient httpClient, IOptionsMonitor<DynamicsOptions> dynamicsOptions)
        {
            this._httpClient = httpClient;
            this._adfsOptions = dynamicsOptions.CurrentValue.ADFS;
        }

        public async Task<Token> GetRefreshToken(CancellationToken cancellationToken)
        {
            if (_httpClient.DefaultRequestHeaders.Contains("client-request-id"))
            {
                _httpClient.DefaultRequestHeaders.Remove("client-request-id");
            }
            _httpClient.DefaultRequestHeaders.Add("client-request-id", Guid.NewGuid().ToString());
            if (_httpClient.DefaultRequestHeaders.Contains("return-client-request-id"))
            {
                _httpClient.DefaultRequestHeaders.Remove("return-client-request-id");
            }
            _httpClient.DefaultRequestHeaders.Add("return-client-request-id", "true");
            if (_httpClient.DefaultRequestHeaders.Contains("Accept"))
            {
                _httpClient.DefaultRequestHeaders.Remove("Accept");
            }
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            var data = new Dictionary<string, string>
            {
                {"resource", _adfsOptions.ResourceName},
                {"client_id", _adfsOptions.ClientId},
                {"client_secret", _adfsOptions.ClientSecret},
                {"username", _adfsOptions.ServiceAccountName},
                {"password", _adfsOptions.ServiceAccountPassword},
                {"scope", "openid"},
                {"response_mode", "form_post"},
                {"grant_type", "password"}
            };

            var content = new FormUrlEncodedContent(data);

            using (var request = new HttpRequestMessage(HttpMethod.Post, _adfsOptions.OAuth2TokenEndpoint) { Content = content })
            {
                var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var responseData = response.Content == null
                        ? null
                        : await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    throw new OAuthApiException(
                        "The HTTP status code of the response was not expected (" + (int)response.StatusCode + ").",
                        (int)response.StatusCode, responseData,
                        response.Headers.ToDictionary(x => x.Key, x => x.Value), null);
                }


                var stream = await response.Content.ReadAsStreamAsync();

                using (StreamReader sr = new StreamReader(stream))
                using (JsonReader reader = new JsonTextReader(sr))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    return serializer.Deserialize<Token>(reader);
                }
            }
        }

    }
}
