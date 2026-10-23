var builder = DistributedApplication.CreateBuilder(args);
builder.AddAzureContainerAppEnvironment("env");

var apiService = builder.AddDotnetProject("apiservice", "../aspire-entra.ApiService/aspire-entra.ApiService.csproj")
    .WithHttpHealthCheck("/health");

builder.AddDotnetProject("webfrontend", "../aspire-entra.Web/aspire-entra.Web.csproj")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
