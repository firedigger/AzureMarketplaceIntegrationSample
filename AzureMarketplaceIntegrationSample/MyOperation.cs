using Microsoft.Marketplace.SaaS.Models;
using System.Text.Json.Serialization;

namespace AzureMarketplaceIntegrationSample;

public record MyOperation(int? Quantity, [property: JsonConverter(typeof(JsonStringEnumConverter))] OperationActionEnum Action, MySubscription Subscription);

public record MySubscription(Beneficiary Beneficiary);

public record Beneficiary(string EmailId, Guid TenantId);