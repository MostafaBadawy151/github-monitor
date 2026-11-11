using GitHubMonitor.Api.Configuration;
using GitHubMonitor.Api.Infrastructure;
using GitHubMonitor.Api.Models;
using GitHubMonitor.Api.Services;
using GitHubMonitor.Api.Wcf;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// --- Options ---
builder.Services.AddOptions<WcfClientOptions>()
    .Bind(builder.Configuration.GetSection(WcfClientOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// --- Dependency injection ---
builder.Services.AddSingleton<IGitHubCommitServiceClient, WcfGitHubCommitServiceClient>();
builder.Services.AddScoped<IGitHubCommitsService, GitHubCommitsService>();

// --- MVC + unified error envelope ---
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(o =>
    {
        // Model-validation errors use the { success, message, data } envelope too.
        o.InvalidModelStateResponseFactory = ctx =>
        {
            var errors = ctx.ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            return new BadRequestObjectResult(ApiResponse.Fail("Validation failed: " + string.Join("; ", errors)));
        };
    });
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.SwaggerDoc("v1", new() { Title = "GitHub Repository Monitor API", Version = "v1" }));

// --- CORS for the Angular dev server (the dev proxy makes it optional, but it helps direct calls) ---
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:4200"];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(allowedOrigins).AllowAnyHeader().WithMethods("GET")));

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.MapControllers();

app.Run();

public partial class Program; // Exposed for WebApplicationFactory in integration tests.
