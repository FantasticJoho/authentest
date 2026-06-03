using AuthTest.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AuthTest.Tests.Helpers;

public static class DbHelper
{
    public static AppDbContext CreateContext(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }
}
