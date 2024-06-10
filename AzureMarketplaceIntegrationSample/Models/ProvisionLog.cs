using Microsoft.Marketplace.SaaS.Models;

namespace AzureMarketplaceIntegrationSample.Models;

public class ProvisionLog
{
    public int Id { get; set; }
    public Action Action { get; set; }
    public required string Domain { get; set; }
    public OperationStatusEnum Status { get; set; }
    public required string Payload { get; set; }
    public DateTimeOffset TimeStamp { get; internal set; }
    public int Licenses { get; internal set; }
}
