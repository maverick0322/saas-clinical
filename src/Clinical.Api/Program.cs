using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy("Process is running."), tags: ["live", "ready"]);

var app = builder.Build();

// Technical probes report process availability only ("process-only").
// They do not imply PostgreSQL connectivity, tenant provisioning, or clinical readiness.
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live"),
    ResponseWriter = (context, report) => WriteHealthResponseAsync(context, report, probe: "live", scope: "process-only")
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = (context, report) => WriteHealthResponseAsync(context, report, probe: "ready", scope: "process-only")
});

app.Logger.LogInformation(
    "Clinical.Api initialized (Environment={EnvironmentName}, HealthScope={HealthScope}).",
    app.Environment.EnvironmentName,
    "process-only");

await app.RunAsync();

static Task WriteHealthResponseAsync(HttpContext context, HealthReport report, string probe, string scope)
{
    context.Response.ContentType = "application/json; charset=utf-8";

    var payload = new
    {
        status = report.Status.ToString(),
        service = "saas-clinical",
        probe,
        scope
    };

    return context.Response.WriteAsync(JsonSerializer.Serialize(payload), context.RequestAborted);
}

public partial class Program;
