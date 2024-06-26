# Azure Marketplace Integration Sample

This repository provides a sample implementation of an Azure function that handles the integration between the Azure Marketplace and your customers' database. In my company we had an application which used Azure SSO with manual license provisioning. Then we decided to publish our application to Azure Marketplace so I was tasked with creating the integration. Implementing the integration is needed not only if you would like to get customers through Azure Marketplace but also if you want to get certain Microsoft Partner benefits. I spent some time creating the minimal project to make it work because I didn't find a simple sample, only a whole ASP.NET app when I didn't need any new components. So I decided to publish this sample with the permission from my employer in case someone would need to do a similar task.

## Key Resources
- Learn more about creating an Azure Marketplace offer [here](https://learn.microsoft.com/en-us/partner-center/marketplace-offers/create-new-saas-offer).
- Information about SaaS Fulfillment API can be found [here](https://learn.microsoft.com/en-us/partner-center/marketplace-offers/partner-center-portal/pc-saas-fulfillment-apis).

## Dependency
- .NET 8
- Azure functions v4 isolated
- `Marketplace.SaaS.Client`. It is only used in the landing code flow. Due to changes in the latest version affecting the ability to mock certain classes, this sample uses a previous version of the library.

## Integration Overview
The integration consists of two methods:
1. **Landing**: A create subscription endpoint that allows for asynchronous activation. This can include a form for collecting relevant info such as tenant and number of licenses. For more detailed data collection, a dedicated UI page should be added to your existing API. Otherwise, a durable azure function technique could also be utilized if there is a need for manual approval or long time wait. Only the landing function calls `Marketplace.SaaS.Client`.
2. **Webhook**: An endpoint that should maintain near 100% uptime. While Azure Functions facilitates this, Azure will retry requests in case of error responses, up to 500 times.

## Deployment
This code can be deployed as a standalone Azure function (isolated) on a consumption plan or added to an existing service plan. The easiest way to deploy is through Visual Studio:
- Right-click publish on the project. You will go through the process of creating a new azure function app.
- Initially, test the function with a privately-published plan to verify integration.
- Monitor the logs using Application Insights.

### Local Testing
Test the Azure function locally using [Azure Core Tools](https://learn.microsoft.com/en-us/azure/azure-functions/functions-run-local?tabs=windows%2Cisolated-process%2Cnode-v4%2Cpython-v2%2Chttp-trigger%2Ccontainer-apps&pivots=programming-language-csharp). 

### Environment Variables
Add the following environment variables to your Azure Function:
- `TenantId`
- `ClientId`
- `ClientSecret`
- `HomePage`
- `SupportEmail`
- Additional settings as needed, such as your database connection string.  
For local run you can reuse existing `local.settings.json`. Don't forget to start azurite (it has a convinient VS code extension) before running `func start`.

### Integration with Azure SaaS
Generate URLs to the functions and input them into the technical specification of your Marketplace offer. Ensure the `code` argument is included in the URL for authentication, which is crucial for protecting the webhook endpoint.

## Database
The sample application uses a inmemory database when run locally (no data persists after stopping the application). This can be easily switched to use SQL Server or another database system.

## Testing
- Run unit tests with the command: `dotnet test`.
- The project includes basic tests for the landing and webhook license modification scenarios. Mocking is used for the classes relied upon by the library, with an example payload provided for reference.
- You can easily add more scenarios to the unit tests by copying relevant setup code.