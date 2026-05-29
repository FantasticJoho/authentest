using AuthTest.Api.Data;
using AuthTest.Api.Middleware;
using AuthTest.Api.Services;
using Fido2NetLib;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseInMemoryDatabase("AuthTestDb"));

builder.Services.AddSingleton<Fido2>(_ =>
{
    var origins = builder.Configuration.GetSection("Fido2:Origins").Get<string[]>()
                  ?? new[] { "http://localhost:5050" };
    return new Fido2(new Fido2.Configuration
    {
        ServerDomain = builder.Configuration["Fido2:ServerDomain"] ?? "localhost",
        ServerName   = builder.Configuration["Fido2:ServerName"] ?? "AuthTest",
        Origins      = new HashSet<string>(origins, StringComparer.OrdinalIgnoreCase)
    });
});

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
builder.Services.AddSingleton<SessionStore>();
builder.Services.AddSingleton<ChallengeStore>();

var app = builder.Build();

app.UseCors("WebFormsPolicy");
app.UseMiddleware<ApiKeyMiddleware>();
app.MapControllers();

app.Run();
