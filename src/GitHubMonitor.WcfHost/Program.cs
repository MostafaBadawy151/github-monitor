using CoreWCF;
using CoreWCF.Configuration;
using CoreWCF.Description;
using GitHubMonitor.Core.Caching;
using GitHubMonitor.Core.Configuration;
using GitHubMonitor.Core.DependencyInjection;
using GitHubMonitor.WcfHost.Contracts;
using GitHubMonitor.WcfHost.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// --- WCF (CoreWCF) ---
builder.Services.AddServiceModelServices();
builder.Services.AddServiceModelMetadata();
builder.Services.AddSingleton<IServiceBehavior, UseRequestHeadersForMetadataAddressBehavior>();
builder.Services.AddTransient<GitHubCommitService>();

// --- GitHub client + Quartz scheduler + cache (shared with the WCF service) ---
builder.Services.AddGitHubIntegration(builder.Configuration);
builder.Services.AddCommitSyncScheduler(builder.Configuration);

var app = builder.Build();

app.UseServiceModel(serviceBuilder =>
{
    serviceBuilder.AddService<GitHubCommitService>(options =>
    {
        options.DebugBehavior.IncludeExceptionDetailInFaults = app.Environment.IsDevelopment();
    });

    var binding = new BasicHttpBinding
    {
        MaxReceivedMessageSize = 1024 * 1024,
        SendTimeout = TimeSpan.FromSeconds(60),
        ReceiveTimeout = TimeSpan.FromSeconds(60)
    };
    serviceBuilder.AddServiceEndpoint<GitHubCommitService, IGitHubCommitService>(binding, "/GitHubCommitService.svc");

    var metadata = app.Services.GetRequiredService<ServiceMetadataBehavior>();
    metadata.HttpGetEnabled = true; // WSDL at /GitHubCommitService.svc?wsdl
});

// Lightweight diagnostics endpoint (no secrets).
app.MapGet("/health", (ICommitCache cache, IOptions<GitHubOptions> gitHub) => Results.Ok(new
{
    status = "ok",
    repository = $"{gitHub.Value.Owner}/{gitHub.Value.Repository}",
    tokenConfigured = gitHub.Value.HasToken,
    cachedCommits = cache.Current.Commits.Count,
    lastSyncTime = cache.Current.LastSyncTime,
    lastAttemptTime = cache.Current.LastAttemptTime,
    lastError = cache.Current.LastErrorMessage
}));

app.MapGet("/", () => Results.Redirect("/GitHubCommitService.svc?wsdl"));

app.Run();
