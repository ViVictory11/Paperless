using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Minio;
using Minio.DataModel.Args;
using Paperless.Contracts;
using Paperless.DAL.Service.Data;

namespace Paperless.IntegrationTests;

public class DocumentUploadTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    private const string MinioEndpoint = "localhost:9000";
    private const string MinioAccessKey = "minioadmin";
    private const string MinioSecretKey = "minioadmin";
    private const string MinioBucket = "documents";

    public DocumentUploadTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task UploadPdf_Returns201_AndWritesDb_AndStoresInMinio()
    {
        var pdfBytes = Encoding.ASCII.GetBytes("%PDF-1.4\n%IntegrationTest\n");
        var originalFileName = $"test-{Guid.NewGuid():N}.pdf";

        var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pdfBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(fileContent, "files", originalFileName);

        var response = await _client.PostAsync("/api/documents/upload", form);

        var bodyText = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created,
            $"Expected 201 Created but got {(int)response.StatusCode} {response.StatusCode}. Body: {bodyText}");

        var created = await response.Content.ReadFromJsonAsync<List<DocumentDto>>();
        Assert.NotNull(created);
        Assert.Single(created);

        var dto = created![0];
        Assert.NotEqual(Guid.Empty, dto.Id);
        Assert.Equal(originalFileName, dto.FileName);
        Assert.Equal("application/pdf", dto.ContentType);
        Assert.Equal(pdfBytes.Length, dto.SizeBytes);
        Assert.False(string.IsNullOrWhiteSpace(dto.ObjectName));
        Assert.True(dto.UploadedAt > DateTime.UtcNow.AddMinutes(-5));

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var entity = await db.Documents.SingleOrDefaultAsync(d => d.Id == dto.Id);
            Assert.NotNull(entity);

            Assert.Equal(dto.FileName, entity!.FileName);
            Assert.Equal(dto.ObjectName, entity.ObjectName);
            Assert.Equal(dto.ContentType, entity.ContentType);
            Assert.Equal(dto.SizeBytes, entity.SizeBytes);
            Assert.True(entity.UploadedAt > DateTime.UtcNow.AddMinutes(-5));
        }

        var minio = new MinioClient()
            .WithEndpoint(MinioEndpoint)
            .WithCredentials(MinioAccessKey, MinioSecretKey)
            .Build();
        var exists = await minio.BucketExistsAsync(new BucketExistsArgs().WithBucket(MinioBucket));
        Assert.True(exists, $"MinIO bucket '{MinioBucket}' does not exist. Create it in docker-compose/init.");

        var stat = await minio.StatObjectAsync(new StatObjectArgs()
            .WithBucket(MinioBucket)
            .WithObject(dto.ObjectName));

        Assert.NotNull(stat);
        Assert.Equal(pdfBytes.Length, stat.Size);

        // Cleanup
        await CleanupAsync(dto.Id, dto.ObjectName);
    }

    private async Task CleanupAsync(Guid id, string objectName)
    {
        var minio = new MinioClient()
            .WithEndpoint(MinioEndpoint)
            .WithCredentials(MinioAccessKey, MinioSecretKey)
            .Build();

        try
        {
            await minio.RemoveObjectAsync(new RemoveObjectArgs()
                .WithBucket(MinioBucket)
                .WithObject(objectName));
        }
        catch
        {
        }

        try
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var entity = await db.Documents.SingleOrDefaultAsync(d => d.Id == id);
            if (entity != null)
            {
                db.Documents.Remove(entity);
                await db.SaveChangesAsync();
            }
        }
        catch
        {
        }
    }
}
