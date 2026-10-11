using LoftViewer.Configuration;
using LoftViewer.Data;
using LoftViewer.Extensions;

// The listening port comes from configuration, never code: the container defaults to 8080
// (ASPNETCORE_HTTP_PORTS in the Dockerfile) and Railway's domain targets that port.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services
    .AddMongo(builder.Configuration)
    .AddJwtAuthentication(builder.Configuration, builder.Environment)
    .AddWeather(builder.Configuration)
    .AddFrontendCors(builder.Configuration)
    .AddLoftViewerRateLimiting()
    .AddProxySupport();

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());
builder.Services.AddHealthChecks().AddCheck<MongoHealthCheck>("mongodb");

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "LoftViewer API"));
}

app.UseCors(CorsSettings.PolicyName);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

await app.RunAsync();

// Exposes the entry point to WebApplicationFactory in integration tests.
public partial class Program;
