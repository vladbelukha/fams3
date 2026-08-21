using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DynamicsAdapter.Web.Auth;
using DynamicsAdapter.Web.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using NUnit.Framework;

namespace DynamicsAdapter.Web.Test.Auth
{
    public class EntraIdTokenClientErrorTest
    {
        private EntraIdTokenClient _sut;
        private HttpClient _httpClient;
        private Mock<HttpMessageHandler> httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        private Mock<IOptionsMonitor<DynamicsOptions>> _optionsMock = new Mock<IOptionsMonitor<DynamicsOptions>>();

        [SetUp]
        public void SetUp()
        {

            httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                // prepare the expected response of the mocked http call
                .ReturnsAsync(new HttpResponseMessage()
                {
                    StatusCode = HttpStatusCode.BadRequest,
                    Content = new StringContent(
                        "{\"error\": \"invalid_client\"}"),
                })
                .Verifiable();

            _optionsMock.Setup(x => x.CurrentValue).Returns(new DynamicsOptions()
            {
                AuthenticationType = DynamicsOptions.Cloud,
                EntraId = new EntraIdOptions()
                {
                    ClientSecret = "secret",
                    DynamicsApiEndpointUrl = "resourceCloudUrl",
                    ResourceName = "resourceCloudUrl",
                    ClientId = "clientId",
                    TenantId = "tenantId"
                }
            });

            // use real http client with mocked handler here
            _httpClient = new HttpClient(httpMessageHandlerMock.Object)
            {
                BaseAddress = new Uri("http://test.com/"),
            };

            _sut = new EntraIdTokenClient(_httpClient, _optionsMock.Object);
        }

        [Test]
        public void When_error_response_it_should_throw_an_exception()
        {
            Assert.ThrowsAsync<OAuthApiException>(async () =>
            {
                var token = await _sut.GetRefreshToken(CancellationToken.None);
            });

        }
    }
}
