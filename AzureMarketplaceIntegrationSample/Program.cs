using AzureMarketplaceIntegrationSample;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Marketplace.SaaS;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
        if (isDevelopment)
        {
            services.AddScoped((_) => MarketplaceSaasClientMockFactory.Create(Guid.NewGuid()));
        }
        else
        {
            services.AddScoped<IMarketplaceSaaSClient, MarketplaceSaaSClient>(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                return new MarketplaceSaaSClient(new Azure.Identity.ClientSecretCredential(config["TenantId"], config["ClientId"], config["ClientSecret"]));
            });
        }
        services.AddDbContext<CompanyDbContext>(options => options.UseInMemoryDatabase("CompanyDb"));
    })
    .ConfigureLogging((HostBuilderContext hostingContext, ILoggingBuilder logging) =>
    {
        logging.Services.Configure<LoggerFilterOptions>(options =>
        {
            var defaultRule = options.Rules.FirstOrDefault(rule => rule.ProviderName == "Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider");
            if (defaultRule is not null)
            {
                options.Rules.Remove(defaultRule);
            }
        });
    })
.Build();

host.Run();