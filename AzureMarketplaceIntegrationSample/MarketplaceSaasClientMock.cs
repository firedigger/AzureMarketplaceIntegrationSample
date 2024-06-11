using System.Reflection;
using Microsoft.Marketplace.SaaS;
using Microsoft.Marketplace.SaaS.Models;
using Moq;

namespace AzureMarketplaceIntegrationSample;

public static class MarketplaceSaasClientMockFactory
{
    public static IMarketplaceSaaSClient Create(Guid tenantId)
    {
        var client = new Mock<IMarketplaceSaaSClient>(MockBehavior.Strict);
        var azureResponseMock = new Mock<Azure.Response<ResolvedSubscription>>(MockBehavior.Strict);
        azureResponseMock.Setup(x => x.GetRawResponse().IsError).Returns(false);
        var beneficiary = (AadIdentifier)Activator.CreateInstance(typeof(AadIdentifier), BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { "alex@email.com", Guid.NewGuid(), tenantId, "" }, null);
        var subscription = (Subscription)Activator.CreateInstance(typeof(Subscription), BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { Guid.NewGuid(), "", "", "", SubscriptionStatusEnum.NotStarted, beneficiary, null, "", 10, null, true, false, false, null, Guid.NewGuid(), Guid.NewGuid(), "", SandboxTypeEnum.None, DateTimeOffset.UtcNow, SessionModeEnum.None }, null);
        var resolvedSubscription = (ResolvedSubscription)Activator.CreateInstance(typeof(ResolvedSubscription), BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { Guid.NewGuid(), "sub", "offer", "plan", (long?)10, subscription }, null);
        azureResponseMock.Setup(x => x.Value).Returns(resolvedSubscription);
        client.Setup(x => x.Fulfillment.ResolveAsync(It.IsAny<string>(), default, default, default)).Returns(Task.FromResult(azureResponseMock.Object));
        var emptyAzureResponseMock = new Mock<Azure.Response>(MockBehavior.Strict);
        emptyAzureResponseMock.Setup(x => x.IsError).Returns(false);
        client.Setup(x => x.Fulfillment.ActivateSubscriptionAsync(It.IsAny<Guid>(), It.IsAny<SubscriberPlan>(), default, default, default)).ReturnsAsync(emptyAzureResponseMock.Object);
        return client.Object;
    }
}