using AzureMarketplaceIntegrationSample.Models;
using Microsoft.EntityFrameworkCore;

namespace AzureMarketplaceIntegrationSample;

public class CompanyDbContext(DbContextOptions<CompanyDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers { get; set; }
    public DbSet<ProvisionLog> ProvisionLogs { get; set; }
}
