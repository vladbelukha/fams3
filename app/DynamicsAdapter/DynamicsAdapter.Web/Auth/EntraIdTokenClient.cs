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
    /// <summary>
    /// The EntraIdTokenClient interacts with Microsoft Entra ID (Azure AD) using the OAuth2
    /// client-credentials grant to obtain and refresh access tokens for Dynamics Cloud.
    /// </summary>
    public class EntraIdTokenClient : IOAuthApiClient
    {
        private readonly HttpClient _httpClient;

        private readonly EntraIdOptions _entraIdOptions;

        public EntraIdTokenClient(HttpClient httpClient, IOptionsMonitor<DynamicsOptions> dynamicsOptions)
        {
            this._httpClient = httpClient;
            this._entraIdOptions = dynamicsOptions.CurrentValue.EntraId;
        }

        public async Task<Token> GetRefreshToken(CancellationToken cancellationToken)
        {
            if (_httpClient.DefaultRequestHeaders.Contains("Accept"))
            {
                _httpClient.DefaultRequestHeaders.Remove("Accept");
            }
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            var tokenEndpoint = $"https://login.microsoftonline.com/{_entraIdOptions.TenantId}/oauth2/v2.0/token";

            var data = new Dictionary<string, string>
            {
                {"client_id", _entraIdOptions.ClientId},
                {"client_secret", _entraIdOptions.ClientSecret},
                {"scope", $"{_entraIdOptions.ResourceName}/.default"},
                {"grant_type", "client_credentials"}
            };

            var content = new FormUrlEncodedContent(data);

            using (var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint) { Content = content })
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
