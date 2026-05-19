using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Marketplace.SaaS;
using Microsoft.Marketplace.SaaS.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace AzureMarketplaceIntegrationSample.Tests;

public class AzureMarketplaceIntegrationTests
{
    [Fact]
    public async Task Landing()
    {
        var context = new CompanyDbContext(new DbContextOptionsBuilder<CompanyDbContext>().UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options);
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var tenantId = Guid.NewGuid();
        var orderFunctions = new OrderFunctions(MarketplaceSaasClientMockFactory.Create(tenantId), LoggerFactory.Create(_ => { }).CreateLogger<OrderFunctions>(), context, config, null!, null!);
        var result = Assert.IsType<ContentResult>(await orderFunctions.Landing(CreateRequest(query: new Dictionary<string, StringValues>
        {
            ["token"] = "token"
        })));
        Assert.Equal(200, result.StatusCode);
        var customer = await context.Customers.FirstOrDefaultAsync();
        Assert.NotNull(customer);
        Assert.Equal(tenantId, customer.TenantId);
        Assert.Equal("email.com", customer.Domain);
    }

    [Fact]
    public async Task Webhook()
    {
        const string tenantId = "9b92d605-9919-4f8e-86cb-dd9071dd0a2e";
        const string clientId = "appId";
        const string marketplaceIsv = "resourceId";
        const string keyId = "marketplace-test-key";

        using var rsa = RSA.Create(2048);
        var signingKey = new RsaSecurityKey(rsa) { KeyId = keyId };
        var publicKey = rsa.ExportParameters(false);
        using var openIdServer = CreateOpenIdServer(tenantId, keyId, publicKey);

        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "ClientId", clientId },
            { "TenantId", tenantId },
            { "MarketplaceISV", marketplaceIsv },
            { "OpenIdConnectAuthority", $"{openIdServer.Url}/{tenantId}" }
        });
        builder.Services.AddLogging();
        builder.Services.AddSingleton<IConfiguration>(builder.Configuration);
        builder.Services.AddDbContext<CompanyDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        builder.Services.AddSingleton<BaseConfigurationManager>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var authority = config["OpenIdConnectAuthority"]!.TrimEnd('/');
            return new ConfigurationManager<OpenIdConnectConfiguration>(
                $"{authority}/.well-known/openid-configuration",
                new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever { RequireHttps = false });
        });
        builder.Services.AddSingleton<SecurityTokenHandler, JwtSecurityTokenHandler>();
        builder.Services.AddSingleton(MarketplaceSaasClientMockFactory.Create(Guid.Parse(tenantId)));
        builder.Services.AddScoped<OrderFunctions>();

        await using var app = builder.Build();
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CompanyDbContext>();
        context.Customers.Add(new Models.Customer { Id = 1, TenantId = Guid.Parse(tenantId), Domain = "email.com", Licenses = 1 });
        await context.SaveChangesAsync();

        var orderFunctions = scope.ServiceProvider.GetRequiredService<OrderFunctions>();
        var token = CreateMarketplaceToken(tenantId, clientId, marketplaceIsv, signingKey);
        var result = await orderFunctions.Webhook(CreateRequest(@"{
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
}", headers: new Dictionary<string, StringValues> { ["Authorization"] = $"Bearer {token}" }), new Microsoft.Extensions.Logging.Debug.DebugLoggerProvider().CreateLogger("test"));
        Assert.IsType<OkResult>(result);
        var licenses = await context.Customers.Select(c => c.Licenses).FirstOrDefaultAsync();
        Assert.Equal(2, licenses);
    }

    private static WireMockServer CreateOpenIdServer(string tenantId, string keyId, RSAParameters publicKey)
    {
        var server = WireMockServer.Start();
        var authority = $"{server.Url}/{tenantId}";

        server
            .Given(Request.Create().WithPath($"/{tenantId}/.well-known/openid-configuration").UsingGet())
            .RespondWith(Response.Create()
                .WithHeader("Content-Type", "application/json")
                .WithBody($$"""
                {
                    "issuer": "https://sts.windows.net/{{tenantId}}/",
                    "jwks_uri": "{{authority}}/discovery/v2.0/keys"
                }
                """));

        server
            .Given(Request.Create().WithPath($"/{tenantId}/discovery/v2.0/keys").UsingGet())
            .RespondWith(Response.Create()
                .WithHeader("Content-Type", "application/json")
                .WithBody($$"""
                {
                    "keys": [
                        {
                            "kty": "RSA",
                            "use": "sig",
                            "kid": "{{keyId}}",
                            "alg": "RS256",
                            "n": "{{Base64UrlEncoder.Encode(publicKey.Modulus!)}}",
                            "e": "{{Base64UrlEncoder.Encode(publicKey.Exponent!)}}"
                        }
                    ]
                }
                """));

        return server;
    }

    private static string CreateMarketplaceToken(string tenantId, string clientId, string marketplaceIsv, SecurityKey signingKey)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateJwtSecurityToken(new SecurityTokenDescriptor
        {
            Issuer = $"https://sts.windows.net/{tenantId}/",
            Audience = clientId,
            Claims = new Dictionary<string, object>
            {
                { "tid", tenantId },
                { "appid", marketplaceIsv }
            },
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256)
        });

        return tokenHandler.WriteToken(token);
    }

    private static HttpRequest CreateRequest(
        string? body = null,
        IReadOnlyDictionary<string, StringValues>? query = null,
        IReadOnlyDictionary<string, StringValues>? headers = null)
    {
        var context = new DefaultHttpContext();

        if (body is not null)
        {
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
            context.Request.ContentType = "application/json";
        }

        if (query is not null)
        {
            context.Request.Query = new QueryCollection(query.ToDictionary());
        }

        if (headers is not null)
        {
            foreach (var header in headers)
            {
                context.Request.Headers[header.Key] = header.Value;
            }
        }

        return context.Request;
    }
}
