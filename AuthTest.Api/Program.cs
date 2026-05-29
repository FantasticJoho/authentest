using AuthTest.Api.Data;
using AuthTest.Api.Middleware;
using AuthTest.Api.Services;
using Fido2NetLib;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseInMemoryDatabase("AuthTestDb"));

builder.Services.AddSingleton<Fido2>(_ =>
    new Fido2(new Fido2.Configuration
    {
        ServerDomain = builder.Configuration["Fido2:ServerDomain"] ?? "localhost",
        ServerName   = builder.Configuration["Fido2:ServerName"] ?? "AuthTest",
        Origin       = builder.Configuration.GetSection("Fido2:Origins").Get<string[]>()?[0]
                       ?? "https://localhost:5000"
    }));

builder.Services.AddMemoryCache();

builder.Services.AddCors(options =>
{
    var origins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
                  ?? Array.Empty<string>();
    options.AddPolicy("WebFormsPolicy", policy =>
        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod());
});

builder.Services.AddControllers();
builder.Services.AddSingleton<SessionStore>();
builder.Services.AddSingleton<ChallengeStore>();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseCors("WebFormsPolicy");
app.UseMiddleware<ApiKeyMiddleware>();
app.MapControllers();

app.Run();
