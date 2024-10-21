using AzureMarketplaceIntegrationSample.Models;
using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Marketplace.SaaS;
using Microsoft.Marketplace.SaaS.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text.Json;

namespace AzureMarketplaceIntegrationSample;

public class OrderFunctions(IMarketplaceSaaSClient marketplaceSaaSClient, ILogger<OrderFunctions> logger, CompanyDbContext context, IConfiguration configuration, IHttpClientFactory httpClientFactory, SecurityTokenHandler tokenHandler)
{
    [Function(nameof(Landing))]
    public async Task<IActionResult> Landing([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req)
    {
        var token = req.Query["token"];

        if (string.IsNullOrEmpty(token))
            return new StatusCodeResult(StatusCodes.Status400BadRequest);

        logger.LogInformation("Landing with token {token}", token);

        var subscriptionResponse = await marketplaceSaaSClient.Fulfillment.ResolveAsync(token);
        if (subscriptionResponse.GetRawResponse().IsError)
            return new StatusCodeResult(StatusCodes.Status404NotFound);
        var subscriptionResolution = subscriptionResponse.Value;
        var tenantId = subscriptionResolution.Subscription.Beneficiary.TenantId.Value;
        var domain = subscriptionResolution.Subscription.Beneficiary.EmailId.Split('@')[1];

        var customer = await context.Customers.Where(o => o.TenantId == tenantId).FirstOrDefaultAsync();

        if (customer is not null)
        {
            return new RedirectResult(configuration["Homepage"], false);
        }
        using var _ = logger.BeginScope("TenantId: {tenantId}, Domain: {domain}", tenantId, domain);
        var row = context.ProvisionLogs.Add(new ProvisionLog
        {
            Domain = domain,
            Action = Action.Create,
            Status = OperationStatusEnum.InProgress,
            Payload = JsonSerializer.Serialize(subscriptionResolution),
            TimeStamp = DateTimeOffset.UtcNow
        }).Entity;
        await context.SaveChangesAsync();
        try
        {
            context.Customers.Add(new Customer
            {
                Domain = domain,
                TenantId = tenantId
            });
            await context.SaveChangesAsync();

            var response = await marketplaceSaaSClient.Fulfillment.ActivateSubscriptionAsync(subscriptionResolution.Id.Value, new SubscriberPlan
            {
                PlanId = subscriptionResolution.PlanId,
                Quantity = subscriptionResolution.Quantity
            });

            if (response.IsError)
                throw new Exception(response.ToString());
            row.Status = OperationStatusEnum.Succeeded;
            await context.SaveChangesAsync();

            return new ContentResult
            {
                Content = $"<!DOCTYPE html><html><body>Your subscription has been activated! You can now use the <a href='{configuration["Homepage"]}'>link</a> to access the backup portal using your Azure AD credentials.</body></html>",
                ContentType = "text/html",
                StatusCode = StatusCodes.Status200OK
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error activating subscription");
            row.Status = OperationStatusEnum.Failed;
            await context.SaveChangesAsync();
            return new ContentResult
            {
                Content = $"<!DOCTYPE html><html><body>Error activating subscription {subscriptionResolution.Id}! Please contact our support at <a href='mailto:{configuration["SupportEmail"]}'>{configuration["SupportEmail"]}</a></body></html>",
                ContentType = "text/html",
                StatusCode = StatusCodes.Status500InternalServerError
            };
        }
    }

    private async Task<RSAParameters> GetRSAParameters(string kid)
    {
        using var jsonDocument = JsonDocument.Parse(await httpClientFactory.CreateClient().GetStringAsync("https://login.microsoftonline.com/common/discovery/v2.0/keys"));
        var key = jsonDocument.RootElement.GetProperty("keys").EnumerateArray().First(k => k.GetProperty("kid").GetString() == kid);
        return new RSAParameters
        {
            Modulus = Base64UrlEncoder.DecodeBytes(key.GetProperty("n").GetString()),
            Exponent = Base64UrlEncoder.DecodeBytes(key.GetProperty("e").GetString())
        };
    }

    [Function(nameof(Webhook))]
    public async Task<IActionResult> Webhook(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req,
        ILogger log)
    {
        var authHeader = req.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            return new StatusCodeResult(StatusCodes.Status401Unauthorized);
        }
        var token = authHeader["Bearer ".Length..];
        var parsedToken = tokenHandler.ReadToken(token) as JwtSecurityToken;
        var aud = parsedToken.Audiences.FirstOrDefault();
        var tid = parsedToken.Claims.FirstOrDefault(c => c.Type == "tid")?.Value;
        var appId = parsedToken.Claims.FirstOrDefault(c => c.Type == "appid" || c.Type == "azp")?.Value;
        var validationResult = await tokenHandler.ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new RsaSecurityKey(await GetRSAParameters(parsedToken.Header.Kid)),
            ValidateIssuer = true,
            ValidIssuer = $"https://sts.windows.net/{tid}/",
            ValidateAudience = true,
            ValidAudience = configuration["ClientId"],
            ValidateLifetime = true
        });
        if (!validationResult.IsValid || appId != configuration["MarketplaceISV"] || aud != configuration["ClientId"] || tid != configuration["TenantId"])
        {
            return new StatusCodeResult(StatusCodes.Status401Unauthorized);
        }
        var payload = await req.ReadFromJsonAsync<MyOperation>();
        var domain = payload.Subscription.Beneficiary.EmailId.Split('@')[1];
        var tenantId = payload.Subscription.Beneficiary.TenantId;
        using var _ = logger.BeginScope("TenantId: {tenantId}, Domain: {domain}", tenantId, domain);
        var row = context.ProvisionLogs.Add(new ProvisionLog
        {
            Action = payload.Action == OperationActionEnum.Unsubscribe ? Action.Delete : Action.Modify,
            Payload = JsonSerializer.Serialize(payload),
            TimeStamp = DateTimeOffset.UtcNow,
            Domain = domain,
            Licenses = payload.Quantity.GetValueOrDefault(),
            Status = OperationStatusEnum.InProgress
        }).Entity;
        await context.SaveChangesAsync();
        var customer = await context.Customers.Where(o => o.TenantId == tenantId).FirstOrDefaultAsync();
        if (customer is null)
        {
            log.LogError("Customer not found for tenant {tenantId}", tenantId);
            row.Status = OperationStatusEnum.Failed;
            await context.SaveChangesAsync();
            return new StatusCodeResult(StatusCodes.Status404NotFound);
        }
        try
        {
            switch (payload.Action)
            {
                case OperationActionEnum.ChangeQuantity:
                case OperationActionEnum.ChangePlan:
                    customer.Licenses = payload.Quantity.GetValueOrDefault();
                    break;
                case OperationActionEnum.Suspend:
                case OperationActionEnum.Unsubscribe:
                    customer.Active = false;
                    customer.Licenses = 0;
                    break;
                case OperationActionEnum.Reinstate:
                case OperationActionEnum.Renew:
                    customer.Active = true;
                    customer.Licenses = payload.Quantity.GetValueOrDefault();
                    break;
            }
            await context.SaveChangesAsync();
            row.Status = OperationStatusEnum.Succeeded;
            await context.SaveChangesAsync();
            return new OkResult();
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Error modifying subscription");
            row.Status = OperationStatusEnum.Failed;
            await context.SaveChangesAsync();
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
    }
}