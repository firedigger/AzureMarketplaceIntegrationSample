using System.Net;

namespace AzureMarketplaceIntegrationSample.Tests;

public class HttpMessageHandlerMock(string content = "") : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content)
        });
}