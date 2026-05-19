extern alias AzureIdentity;

using AzureMarketplaceIntegrationSample;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Marketplace.SaaS;
using System.IdentityModel.Tokens.Jwt;

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
                return new MarketplaceSaaSClient(new AzureIdentity::Azure.Identity.ClientSecretCredential(
                    GetRequiredConfiguration(config, "TenantId"),
                    GetRequiredConfiguration(config, "ClientId"),
                    GetRequiredConfiguration(config, "ClientSecret")));
            });
        }
        services.AddHttpClient();
        services.AddSingleton<BaseConfigurationManager>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var tenantId = GetRequiredConfiguration(config, "TenantId");
            var authority = (config["OpenIdConnectAuthority"] ?? $"https://login.microsoftonline.com/{tenantId}").TrimEnd('/');
            return new ConfigurationManager<OpenIdConnectConfiguration>(
                $"{authority}/.well-known/openid-configuration",
                new OpenIdConnectConfigurationRetriever());
        });
        services.AddSingleton<SecurityTokenHandler, JwtSecurityTokenHandler>();
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

static string GetRequiredConfiguration(IConfiguration configuration, string key)
{
    return configuration[key] ?? throw new InvalidOperationException($"Missing required configuration value '{key}'.");
}
