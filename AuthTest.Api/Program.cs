using AuthTest.Api.Data;
using AuthTest.Api.Health;
using AuthTest.Api.Middleware;
using AuthTest.Api.Services;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseInMemoryDatabase("AuthTestDb"));

builder.Services.AddMemoryCache();

builder.Services.AddCors(options =>
{
    var origins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
                  ?? Array.Empty<string>();
    options.AddPolicy("WebFormsPolicy", policy =>
        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod());
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });
builder.Services.AddScoped<ChallengeStore>();
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddCheck<DatabaseReadinessHealthCheck>("database", tags: ["ready"]);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var seedUsers = new[]
    {
        new AuthTest.Api.Models.User { Username = "jonathan", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Pas$word2", 12), MustChangePassword = true },
        new AuthTest.Api.Models.User { Username = "virginie",  PasswordHash = BCrypt.Net.BCrypt.HashPassword("Pas$word2", 12), MustChangePassword = true }
    };
    foreach (var u in seedUsers)
    {
        if (!db.Users.Any(x => x.Username.ToLower() == u.Username.ToLower()))
            db.Users.Add(u);
    }
    db.SaveChanges();
}

app.UseCors("WebFormsPolicy");
app.UseMiddleware<ApiKeyMiddleware>();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
app.MapControllers();

app.Run();

public partial class Program { }
