# Azure Marketplace Integration Sample

This repository provides a sample implementation of an Azure function that handles the integration between the Azure Marketplace and your customers' database. In my company we had an application which used Azure SSO with manual license provisioning. Then we decided to publish our application to Azure Marketplace so I was tasked with creating the integration. Implementing the integration is needed not only if you would like to get customers through Azure Marketplace but also if you want to get certain Microsoft Partner benefits. I spent some time creating the minimal project to make it work because the official sample is overly complicated featuring a full on ASP.NET application for the Landing method, but fails to demonstrate how to properly secure the webhook in the Azure function implementation. So I decided to publish this sample with the permission from my employer in case someone would need to do a similar task. You can still learn quite a a bit at the [official sample repo](https://github.com/Ercenk/ContosoAMPBasic) although it links to a *new location* which doesn't exist or [official client repo](https://github.com/microsoft/commercial-marketplace-client-dotnet). Otherwise most of the material on the Azure marketplace offer management can be found at learn.microsoft.com docs. You can also have a look at the official [course](https://microsoft.github.io/Mastering-the-Marketplace/saas/) with videos, slides and code samples.

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
2. **Webhook**: An endpoint that should maintain near 100% uptime. While Azure Functions facilitates this, Azure will retry requests in case of error responses, up to 500 times. The payload contains the info on the subscription action while Authorization header contains a JWT token which contains claims with your app ids. 

### Securing your saas offer webhook with JWT validation
The JWT token is the main way to secure the webhook by ensuring that the caller is Microsoft-authorized. This can be done with the JWT token signature which uses assymetric RS256 signature, meaning it was signed by Microsoft with a secret key, and they provide a public key that can be used to validate it. The public key can be found at the [discovery endpoint](https://login.microsoftonline.com/common/discovery/v2.0/keys). The key id (kid) can be found in the JWT headers. A naive option is to use a secret in the URL to protect the endpoint from being accessed by unauthorized users, however this is considered anti-pattern by Microsoft and has recently become enforced, meaning Microsoft will disable the marketplace offer it is contains a secret in the Webhook URL. However, Microsoft documentation itself does not describe the token validation step, which means that developers can miss the critical step and expose the endpoint only more. This is one of the most important I decided to upload this sample. The [sample provided by Microsoft](https://microsoft.github.io/Mastering-the-Marketplace/saas/dev/#implementing-a-simple-saas-webhook-in-net) allows an attacker to construct an arbitrary JWT token which will be able to bypass any trivial attribute checks after learning your endpoint, tenant and application IDs (which are not secrets).
The logic apps tutorial works correctly because it is specifically integrated with "Azure AD" policy and knows where to get the keys.

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
- `MarketplaceISV` - this seems to be constant `20e940b3-4c77-4b0b-9a53-9e16a1b010a7`
- `HomePage`
- `SupportEmail`
- Additional settings as needed, such as your database connection string.  
For local run you can reuse existing `local.settings.json`. The sample from Microsoft has those variable placeholders in `host.json`, but they would not actually work unless moved into a `local.settings.json`. You can test locally using Visual Studio or VS code.

### Integration with Azure SaaS
Generate URLs to the functions and input them into the technical specification of your Marketplace offer. Ensure the `code` argument is included in the URL for authentication, which is crucial for protecting the webhook endpoint. This is considered an anti-pattern by the course tutorial which does not however go through how the webhook can be properly secured otherwise.

## Database
The sample application uses a inmemory database when run locally (no data persists after stopping the application). This can be easily switched to use SQL Server or another database system.

## Testing
- Run unit tests with the command: `dotnet test`.
- The project includes basic tests for the landing and webhook license modification scenarios. Mocking is used for the classes relied upon by the library, with an example payload provided for reference.
- You can easily add more scenarios to the unit tests by copying relevant setup code.