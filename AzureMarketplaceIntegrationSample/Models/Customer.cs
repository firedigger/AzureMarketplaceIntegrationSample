namespace AzureMarketplaceIntegrationSample.Models;

public class Customer
{
    public int Id { get; set; }
    public Guid TenantId { get; set; }
    public required string Domain { get; set; }
    public int Licenses { get; set; }
    public bool Active { get; set; }
}
