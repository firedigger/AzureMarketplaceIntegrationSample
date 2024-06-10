using AzureMarketplaceIntegrationSample;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Marketplace.SaaS;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        services.AddScoped<IMarketplaceSaaSClient, MarketplaceSaaSClient>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            return new MarketplaceSaaSClient(new Azure.Identity.ClientSecretCredential(config["TenantId"], config["ClientId"], config["ClientSecret"]));
        });
        services.AddDbContext<CompanyDbContext>(options => options.UseSqlite("Data Source=companydb.sqlite"));
    })
    .Build();

host.Run();
