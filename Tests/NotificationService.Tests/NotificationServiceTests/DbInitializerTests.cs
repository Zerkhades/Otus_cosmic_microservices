using System;
using Microsoft.EntityFrameworkCore;
using NotificationService.Infrastructure.Persistence;
using Xunit;

namespace NotificationServiceTests;

public class DbInitializerTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public void Initialize_Creates_Database_Once()
    {
        using var db = CreateDb();

        // Инициализация должна создать БД
        DbInitializer.Initialize(db);

        // Повторный EnsureCreated должен вернуть false, т.к. уже создано
        var createdAgain = db.Database.EnsureCreated();
        Assert.False(createdAgain);
    }
}