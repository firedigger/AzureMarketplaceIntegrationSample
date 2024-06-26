using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Marketplace.SaaS;
using Microsoft.Marketplace.SaaS.Models;
using Moq;
using System.Reflection;

namespace AzureMarketplaceIntegrationSample.Tests;

public class AzureMarketplaceIntegrationTests
{
    [Fact]
    public async Task Landing()
    {
        var context = new CompanyDbContext(new DbContextOptionsBuilder<CompanyDbContext>().UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options);
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var tenantId = Guid.NewGuid();
        var orderFunctions = new OrderFunctions(MarketplaceSaasClientMockFactory.Create(tenantId), LoggerFactory.Create(_ => { }).CreateLogger<OrderFunctions>(), context, config);
        var result = await orderFunctions.Landing(new HttpRequestMock(null, new QueryCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["token"] = new Microsoft.Extensions.Primitives.StringValues("token")
        }))) as ContentResult;
        Assert.Equal(200, result.StatusCode);
        var customer = await context.Customers.FirstOrDefaultAsync();
        Assert.NotNull(customer);
        Assert.Equal(tenantId, customer.TenantId);
        Assert.Equal("email.com", customer.Domain);
    }

    [Fact]
    public async Task Webhook()
    {
        var context = new CompanyDbContext(new DbContextOptionsBuilder<CompanyDbContext>().UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options);
        context.Customers.Add(new Models.Customer { Id = 1, TenantId = Guid.Parse("9b92d605-9919-4f8e-86cb-dd9071dd0a2e"), Domain = "email.com", Licenses = 1 });
        await context.SaveChangesAsync();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                { "ClientId", "appId" },
                { "TenantId", "tenantId" }
            }).Build();
        var orderFunctions = new OrderFunctions(null, LoggerFactory.Create(_ => { }).CreateLogger<OrderFunctions>(), context, config);
        var result = await orderFunctions.Webhook(new HttpRequestMock(@"{
    ""id"": ""65f7dffe-047d-439e-8608-65eb658a1b94"",
    ""activityId"": ""65f7dffe-047d-439e-8608-65eb658a1b94"",
    ""publisherId"": ""company1276040168517"",
    ""offerId"": ""offer"",
    ""planId"": ""basic"",
    ""quantity"": 2,
    ""subscriptionId"": ""ad3a21fa-42a8-443e-d99c-af170ec84859"",
    ""timeStamp"": ""2024-02-12T12:37:18.4014575Z"",
    ""action"": ""ChangeQuantity"",
    ""status"": ""InProgress"",
    ""operationRequestSource"": ""Azure"",
    ""subscription"": {
        ""id"": ""ad3a21fa-42a8-443e-d99c-af170ec84859"",
        ""name"": ""Subscription"",
        ""publisherId"": ""nexeticoy1676040168516"",
        ""offerId"": ""offer"",
        ""planId"": ""basic"",
        ""quantity"": 1,
        ""beneficiary"": {
            ""emailId"": ""person@email.com"",
            ""objectId"": ""3e8a0a39-7db6-44f5-9b7c-357bd02ccf88"",
            ""tenantId"": ""9b92d605-9919-4f8e-86cb-dd9071dd0a2e"",
            ""puid"": ""100320028C4B6E87""
        },
        ""purchaser"": {
            ""emailId"": ""person@email.com"",
            ""objectId"": ""3e8a0a39-7db6-44f5-9b7c-357bd02ccf88"",
            ""tenantId"": ""9b92d605-9919-4f8e-86cb-dd9071dd0a2e"",
            ""puid"": ""100320028C4B6E87""
        },
        ""allowedCustomerOperations"": [
            ""Delete"",
            ""Read"",
            ""Update""
        ],
        ""sessionMode"": ""None"",
        ""isFreeTrial"": true,
        ""isTest"": false,
        ""sandboxType"": ""None"",
        ""saasSubscriptionStatus"": ""Subscribed"",
        ""term"": {
            ""startDate"": ""2024-02-12T00:00:00Z"",
            ""endDate"": ""2024-03-11T00:00:00Z"",
            ""termUnit"": ""P1M"",
            ""chargeDuration"": null
        },
        ""autoRenew"": false,
        ""created"": ""2024-02-09T12:31:35.2482251Z"",
        ""lastModified"": ""2024-02-12T12:24:01.9483678Z""
    },
    ""purchaseToken"": null
}", null, new HeaderDictionary(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues> { ["Authorization"] = "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJhdWQiOiJhcHBJZCIsInRpZCI6InRlbmFudElkIn0.jXT5wP3bENfW19B4ed0a_WQpcdl880e1WO88H8rynyU" })), new Microsoft.Extensions.Logging.Debug.DebugLoggerProvider().CreateLogger("test"));
        Assert.IsType<OkResult>(result);
        var licenses = await context.Customers.Select(c => c.Licenses).FirstOrDefaultAsync();
        Assert.Equal(2, licenses);
    }
}