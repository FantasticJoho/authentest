#!/usr/bin/env python3
import os

directory = r'c:\Users\jonat\repo\authentest.worktrees\copilot-worktree-2026-05-29T07-03-16\AuthTest.Tests\Helpers'
os.makedirs(directory, exist_ok=True)

content = """using AuthTest.Api.Data;
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
"""

filepath = os.path.join(directory, 'DbHelper.cs')
with open(filepath, 'w') as f:
    f.write(content)

print(f"Created {filepath}")
print("Directory and file created successfully")
