using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;
using Paperless.Contracts;
using Paperless.DAL.Service.Data;
using Xunit;

namespace Paperless.IntegrationTests;

public class DocumentLifecycleTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    private const string MinioEndpoint = "localhost:9000";
    private const string MinioAccessKey = "minioadmin";
    private const string MinioSecretKey = "minioadmin";
    private const string MinioBucket = "documents";

    public DocumentLifecycleTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Upload_GetById_Delete_RemovesFromDbAndMinio_AndGetReturns404()
    {
        // ---------- Upload ----------
        var pdfBytes = Encoding.ASCII.GetBytes("%PDF-1.4\n%LifecycleTest\n");
        var originalFileName = $"lifecycle-{Guid.NewGuid():N}.pdf";

        var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pdfBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(fileContent, "files", originalFileName);

        var uploadRes = await _client.PostAsync("/api/documents/upload", form);
        var uploadBody = await uploadRes.Content.ReadAsStringAsync();

        Assert.True(uploadRes.StatusCode == HttpStatusCode.Created,
            $"Expected 201 Created but got {(int)uploadRes.StatusCode} {uploadRes.StatusCode}. Body: {uploadBody}");

        var created = await uploadRes.Content.ReadFromJsonAsync<List<DocumentDto>>();
        Assert.NotNull(created);
        Assert.Single(created);

        var dto = created![0];
        Assert.NotEqual(Guid.Empty, dto.Id);
        Assert.False(string.IsNullOrWhiteSpace(dto.ObjectName));

        // ---------- GET by id (should exist) ----------
        var getRes = await _client.GetAsync($"/api/documents/{dto.Id}");
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);

        var fetched = await getRes.Content.ReadFromJsonAsync<DocumentDto>();
        Assert.NotNull(fetched);

        Assert.Equal(dto.Id, fetched!.Id);
        Assert.Equal(originalFileName, fetched.FileName);
        Assert.Equal("application/pdf", fetched.ContentType);
        Assert.Equal(pdfBytes.Length, fetched.SizeBytes);
        Assert.Equal(dto.ObjectName, fetched.ObjectName);

        // ---------- Verify DB row exists ----------
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var entity = await db.Documents.SingleOrDefaultAsync(d => d.Id == dto.Id);
            Assert.NotNull(entity);
            Assert.Equal(dto.ObjectName, entity!.ObjectName);
        }

        // ---------- Verify MinIO object exists ----------
        var minio = new MinioClient()
            .WithEndpoint(MinioEndpoint)
            .WithCredentials(MinioAccessKey, MinioSecretKey)
            .Build();

        var bucketExists = await minio.BucketExistsAsync(new BucketExistsArgs().WithBucket(MinioBucket));
        Assert.True(bucketExists, $"MinIO bucket '{MinioBucket}' does not exist.");

        var statBefore = await minio.StatObjectAsync(new StatObjectArgs()
            .WithBucket(MinioBucket)
            .WithObject(dto.ObjectName));

        Assert.Equal(pdfBytes.Length, statBefore.Size);

        // ---------- DELETE ----------
        var delRes = await _client.DeleteAsync($"/api/documents/{dto.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delRes.StatusCode);

        // ---------- Verify DB row removed ----------
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var entity = await db.Documents.SingleOrDefaultAsync(d => d.Id == dto.Id);
            Assert.Null(entity);
        }

        // ---------- Verify MinIO object removed ----------
        await Assert.ThrowsAnyAsync<MinioException>(async () =>
        {
            await minio.StatObjectAsync(new StatObjectArgs()
                .WithBucket(MinioBucket)
                .WithObject(dto.ObjectName));
        });


        // ---------- GET by id should now be 404 ----------
        var getAfterDel = await _client.GetAsync($"/api/documents/{dto.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getAfterDel.StatusCode);
    }

    [Fact]
    public async Task Delete_NonExisting_Returns404()
    {
        var randomId = Guid.NewGuid();

        var res = await _client.DeleteAsync($"/api/documents/{randomId}");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task GetAll_ShowsUploadedDocument_AndAfterDeleteItIsGone()
    {
        // Upload one doc
        var pdfBytes = Encoding.ASCII.GetBytes("%PDF-1.4\n%GetAllTest\n");
        var originalFileName = $"getall-{Guid.NewGuid():N}.pdf";

        var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pdfBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(fileContent, "files", originalFileName);

        var uploadRes = await _client.PostAsync("/api/documents/upload", form);
        Assert.Equal(HttpStatusCode.Created, uploadRes.StatusCode);

        var created = await uploadRes.Content.ReadFromJsonAsync<List<DocumentDto>>();
        Assert.NotNull(created);
        Assert.Single(created);
        var dto = created![0];

        // GET ALL should contain it
        var allRes = await _client.GetAsync("/api/documents");
        Assert.Equal(HttpStatusCode.OK, allRes.StatusCode);

        var all = await allRes.Content.ReadFromJsonAsync<List<DocumentDto>>();
        Assert.NotNull(all);
        Assert.Contains(all!, d => d.Id == dto.Id);

        // DELETE it
        var delRes = await _client.DeleteAsync($"/api/documents/{dto.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delRes.StatusCode);

        // GET ALL should not contain it anymore
        var allResAfter = await _client.GetAsync("/api/documents");
        Assert.Equal(HttpStatusCode.OK, allResAfter.StatusCode);

        var allAfter = await allResAfter.Content.ReadFromJsonAsync<List<DocumentDto>>();
        Assert.NotNull(allAfter);
        Assert.DoesNotContain(allAfter!, d => d.Id == dto.Id);
    }

}
