using AzureMarketplaceIntegrationSample.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Marketplace.SaaS;
using Microsoft.Marketplace.SaaS.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;

namespace AzureMarketplaceIntegrationSample
{
    public class OrderFunctions(IMarketplaceSaaSClient marketplaceSaaSClient, ILogger<OrderFunctions> logger, CompanyDbContext context, IConfiguration configuration)
    {
        private readonly ILogger<OrderFunctions> _logger = logger;
        private readonly IMarketplaceSaaSClient _marketplaceSaaSClient = marketplaceSaaSClient;
        private readonly CompanyDbContext _context = context;
        private readonly IConfiguration _config = configuration;

        [Function(nameof(Landing))]
        public async Task<IActionResult> Landing([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
        {
            var token = req.Query["token"];

            if (string.IsNullOrEmpty(token))
                return new StatusCodeResult(StatusCodes.Status400BadRequest);

            _logger.LogDebug("Landing with token {token}", token);

            var subscriptionResponse = await _marketplaceSaaSClient.Fulfillment.ResolveAsync(token);
            if (subscriptionResponse.GetRawResponse().IsError)
                return new StatusCodeResult(StatusCodes.Status404NotFound);
            var subscriptionResolution = subscriptionResponse.Value;
            var tenantId = subscriptionResolution.Subscription.Beneficiary.TenantId.Value;
            var email = subscriptionResolution.Subscription.Beneficiary.EmailId;
            var domain = subscriptionResolution.Subscription.Beneficiary.EmailId.Split('@')[1];
            var companyName = domain;
            var licenses = subscriptionResolution.Quantity.GetValueOrDefault().ToString();

            var customer = await _context.Customers.Where(o => o.TenantId == tenantId).FirstOrDefaultAsync();

            if (customer is not null)
            {
                return new RedirectResult(_config["Homepage"], false);
            }
            using var _ = _logger.BeginScope("TenantId: {tenantId}, Domain: {domain}", tenantId, domain);
            var row = _context.ProvisionLogs.Add(new ProvisionLog
            {
                Domain = domain,
                Action = Action.Create,
                Status = OperationStatusEnum.InProgress,
                Payload = JsonSerializer.Serialize(subscriptionResolution)
            }).Entity;
            await _context.SaveChangesAsync();
            try
            {
                _context.Customers.Add(new Customer
                {
                    Domain = domain,
                    TenantId = tenantId
                });
                await _context.SaveChangesAsync();

                var response = await _marketplaceSaaSClient.Fulfillment.ActivateSubscriptionAsync(subscriptionResolution.Id.Value, new Microsoft.Marketplace.SaaS.Models.SubscriberPlan
                {
                    PlanId = subscriptionResolution.PlanId,
                    Quantity = subscriptionResolution.Quantity
                });

                if (response.IsError)
                    throw new Exception(response.ToString());
                row.Status = OperationStatusEnum.Succeeded;
                await _context.SaveChangesAsync();

                return new ContentResult
                {
                    Content = $"<!DOCTYPE html><html><body>Your subscription has been activated! You can now use the <a href='{_config["Homepage"]}'>link</a> to access the backup portal using your Azure AD credentials.</body></html>",
                    ContentType = "text/html",
                    StatusCode = StatusCodes.Status200OK
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error activating subscription");
                row.Status = OperationStatusEnum.Failed;
                await _context.SaveChangesAsync();
                return new ContentResult
                {
                    Content = $"<!DOCTYPE html><html><body>Error activating subscription {subscriptionResolution.Id}! Please contact our support at <a href='mailto:{_config["SupportEmail"]}'>{_config["SupportEmail"]}</a></body></html>",
                    ContentType = "text/html",
                    StatusCode = StatusCodes.Status500InternalServerError
                };
            }
        }

        [Function(nameof(Webhook))]
        public async Task<IActionResult> Webhook(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req,
            ILogger log)
        {
            log.LogDebug("Modify order received: {payload}", await new StreamReader(req.Body).ReadToEndAsync());
            var authHeader = req.Headers.Authorization.ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return new StatusCodeResult(StatusCodes.Status401Unauthorized);
            }
            var token = authHeader["Bearer ".Length..];
            log.LogDebug("Authorization token: {token}", token);
            var tokenHandler = new JwtSecurityTokenHandler();
            var parsedToken = tokenHandler.ReadToken(token) as JwtSecurityToken;
            var aud = parsedToken.Audiences.FirstOrDefault();
            var tid = parsedToken.Claims.FirstOrDefault(c => c.Type == "tid")?.Value;
            if (aud != _config["ClientId"] || tid != _config["TenantId"])
            {
                return new StatusCodeResult(StatusCodes.Status401Unauthorized);
            }
            var payload = await req.ReadFromJsonAsync<MyOperation>();
            var row = _context.ProvisionLogs.Add(new ProvisionLog
            {
                Action = payload.Action == OperationActionEnum.Unsubscribe ? Action.Delete : Action.Modify,
                Payload = JsonSerializer.Serialize(payload),
                TimeStamp = DateTimeOffset.UtcNow,
                Domain = payload.Subscription.Beneficiary.EmailId,
                Licenses = payload.Quantity.GetValueOrDefault(),
                Status = OperationStatusEnum.InProgress
            }).Entity;
            await _context.SaveChangesAsync();
            var tenantId = payload.Subscription.Beneficiary.TenantId;
            var customer = await _context.Customers.Where(o => o.TenantId == tenantId).FirstOrDefaultAsync();
            if (customer is null)
            {
                log.LogError("Customer not found for tenant {tenantId}", tenantId);
                row.Status = OperationStatusEnum.Failed;
                await _context.SaveChangesAsync();
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
                await _context.SaveChangesAsync();
                row.Status = OperationStatusEnum.Succeeded;
                await _context.SaveChangesAsync();
                return new OkResult();
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error modifying subscription");
                row.Status = OperationStatusEnum.Failed;
                await _context.SaveChangesAsync();
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }
}