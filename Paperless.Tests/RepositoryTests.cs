using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paperless.DAL.Service.Data;
using Paperless.DAL.Service.Models;
using Paperless.DAL.Service.Repositories;
using Xunit;

namespace Paperless.Tests;

public class RepositoryTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly DbContextOptions<AppDbContext> _options;

    public RepositoryTests()
    {
        // One shared open connection per test class instance
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();

        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_conn)
            .Options;

        // Create schema once
        using var setup = new AppDbContext(_options);
        setup.Database.EnsureCreated();
    }

    private AppDbContext NewDb() => new AppDbContext(_options);

    public void Dispose()
    {
        _conn.Dispose();
    }

    [Fact]
    public async Task AddAsync_Saves_And_Returns_Entity()
    {
        using var db = NewDb();
        var repo = new DocumentRepository(db);
        var now = DateTime.UtcNow;

        var entity = new DocumentEntity
        {
            Id = Guid.NewGuid(),
            FileName = "a.pdf",
            ContentType = "application/pdf",
            SizeBytes = 123,
            UploadedAt = now
        };

        var saved = await repo.AddAsync(entity, CancellationToken.None);

        saved.Should().NotBeNull();
        (await db.Documents.CountAsync()).Should().Be(1);
        (await db.Documents.AsNoTracking().FirstAsync()).Id.Should().Be(entity.Id);
    }

    [Fact]
    public async Task GetAsync_Returns_Null_When_Not_Found()
    {
        using var db = NewDb();
        var repo = new DocumentRepository(db);

        var result = await repo.GetAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_Returns_In_UploadedAt_Ascending_Order()
    {
        using var db = NewDb();
        var repo = new DocumentRepository(db);

        var older = new DateTime(2022, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var newer = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        db.Documents.AddRange(
            new DocumentEntity { Id = Guid.NewGuid(), FileName = "b", ContentType = "x", SizeBytes = 1, UploadedAt = newer },
            new DocumentEntity { Id = Guid.NewGuid(), FileName = "a", ContentType = "x", SizeBytes = 1, UploadedAt = older }
        );
        await db.SaveChangesAsync();

        var list = await repo.GetAllAsync();

        list.Should().HaveCount(2);
        list.Select(d => d.FileName).Should().ContainInOrder("a", "b");
    }

    [Fact]
    public async Task DeleteAsync_Removes_And_Returns_True()
    {
        using var db = NewDb();
        var repo = new DocumentRepository(db);
        var id = Guid.NewGuid();
        db.Documents.Add(new DocumentEntity
        {
            Id = id,
            FileName = "x",
            ContentType = "y",
            SizeBytes = 1,
            UploadedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var removed = await repo.DeleteAsync(id);

        removed.Should().BeTrue();
        var reloaded = await db.Documents.FindAsync(id);
        reloaded.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_Returns_False_When_Not_Found()
    {
        using var db = NewDb();
        var repo = new DocumentRepository(db);

        var removed = await repo.DeleteAsync(Guid.NewGuid());

        removed.Should().BeFalse();
    }
}
