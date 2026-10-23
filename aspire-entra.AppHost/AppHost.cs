var builder = DistributedApplication.CreateBuilder(args);
builder.AddAzureContainerAppEnvironment("env");

var apiEntra = builder.AddEntraIdApplication("apiservice-entra")
    .WithScope("access_as_user", "Access the weather API on behalf of the signed-in user");

var apiService = builder.AddDotnetProject("apiservice", "../aspire-entra.ApiService/aspire-entra.ApiService.csproj")
    .WithHttpHealthCheck("/health")
    .WithReference(apiEntra);

var webEntra = builder.AddEntraIdApplication("webfrontend-entra")
    .WithApiPermissions(apiEntra, "access_as_user")
    .PublishWithFederatedManagedIdentityCredential();

if (builder.ExecutionContext.IsRunMode)
{
    var secret = builder.AddParameter("webfrontend-secret", secret: true);
    webEntra.WithClientSecret(secret);
}

var webFrontend = builder.AddDotnetProject("webfrontend", "../aspire-entra.Web/aspire-entra.Web.csproj")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(webEntra)
    .WithReference(apiService)
    .WithEnvironment("apiScope", apiEntra.Resource.GetScopeUri("access_as_user"))
    .WaitFor(apiService);

// The redirect URIs follow the web frontend's endpoint: its local https address when running, the deployed host when published.
webEntra.WithRedirectUris(webFrontend.GetEndpoint("https"), "/signin-oidc", "/signout-callback-oidc");

builder.Build().Run();
