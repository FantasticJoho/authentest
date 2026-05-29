using AuthTest.Api.Data;
using AuthTest.Api.Middleware;
using AuthTest.Api.Services;
using Fido2NetLib;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseInMemoryDatabase("AuthTestDb"));

builder.Services.AddSingleton<IFido2>(_ => new Fido2NetLib.Fido2(new Fido2Configuration
{
    RPID    = builder.Configuration["Fido2:ServerDomain"] ?? "localhost",
    RPName  = builder.Configuration["Fido2:ServerName"] ?? "AuthTest",
    Origins = new HashSet<string>(
        builder.Configuration.GetSection("Fido2:Origins").Get<string[]>()
        ?? new[] { "http://localhost:8081" },
        StringComparer.OrdinalIgnoreCase)
}));

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
app.MapControllers();

app.Run();
