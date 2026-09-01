using DynamicsAdapter.Web.Configuration;
using Microsoft.Extensions.Options;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DynamicsAdapter.Web.ApiGateway
{
    public class ApiGatewayHandler : DelegatingHandler
    {

        public readonly ApiGatewayOptions _apiGatewayOptions;
        private readonly DynamicsOptions _dynamicsOptions;

        public ApiGatewayHandler(
            IOptions<ApiGatewayOptions> apiGatewayOptions,
            IOptions<DynamicsOptions> dynamicsOptions = null)
        {
            _apiGatewayOptions = apiGatewayOptions.Value;
            _dynamicsOptions = dynamicsOptions?.Value;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // TODO: remove legacy gateway rewrite after full cloud migration.
            // This branch exists only to preserve the on-prem / API-gateway routing path.
            // Once all environments are cloud-only, the entire ApiGatewayHandler rewrite can be deleted.
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.RequestUri == null)
            {
                return await base.SendAsync(request, cancellationToken);
            }

            // TODO: remove this condition after full cloud migration when gateway proxying is no longer needed.
            if (IsCloudDynamicsRequest())
            {
                return await base.SendAsync(request, cancellationToken);
            }

            if (string.IsNullOrEmpty(_apiGatewayOptions.BasePath)) return await base.SendAsync(request, cancellationToken);

            if (Uri.TryCreate(CombineUrls(_apiGatewayOptions.BasePath, request.RequestUri.PathAndQuery), UriKind.Absolute, out var path))
            {
                request.Headers.Add("MSCRM.SuppressDuplicateDetection", "false");

                //this is to deal with Dynamics, when the method is POST and payload is empty, 
                //Dynamics still looking for content-type.
                //this senario now only happens with ssg_SearchRequestSubmittoQueueActions
                //this scenario now also happens with ssg_SearchRequestCreateCouldNotAutoCloseNote
                if (request.Content == null
                    && request.Method == HttpMethod.Post
                    && (request.RequestUri.AbsolutePath.EndsWith("ssg_SearchRequestSubmittoQueueActions", StringComparison.InvariantCultureIgnoreCase)
                    || request.RequestUri.AbsolutePath.EndsWith("ssg_SearchRequestCreateCouldNotAutoCloseNote", StringComparison.InvariantCultureIgnoreCase))
                    )
                    request.Content = new StringContent(string.Empty,
                                    Encoding.UTF8,
                                    "application/json");//CONTENT-TYPE header

                request.RequestUri = path;
            }

            return await base.SendAsync(request, cancellationToken);
        }

        private bool IsCloudDynamicsRequest()
        {
            // TODO: remove the cloud-guard once the legacy API gateway path is fully retired.
            if (_dynamicsOptions == null)
            {
                return false;
            }

            return _dynamicsOptions.IsCloud && requestUriIsDynamicsEndpoint(_dynamicsOptions.DynamicsApiEndpointUrl);
        }

        private static bool requestUriIsDynamicsEndpoint(string dynamicsEndpointUrl)
        {
            if (string.IsNullOrWhiteSpace(dynamicsEndpointUrl))
            {
                return false;
            }

            if (!Uri.TryCreate(dynamicsEndpointUrl, UriKind.Absolute, out var dynamicsUri))
            {
                return false;
            }

            return true;
        }


        public static string CombineUrls(string baseUrl, string relativeUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new ArgumentNullException(nameof(baseUrl));

            if (string.IsNullOrWhiteSpace(relativeUrl))
                return baseUrl;

            baseUrl = baseUrl.TrimEnd('/');
            relativeUrl = relativeUrl.TrimStart('/');

            return $"{baseUrl}/{relativeUrl}";
        }
    }
}