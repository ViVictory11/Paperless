using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;
using Paperless.Contracts;

namespace Paperless.IntegrationTests;

public class DocumentUploadValidationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public DocumentUploadValidationTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Upload_NoFiles_Returns400()
    {
        var form = new MultipartFormDataContent();

        var res = await _client.PostAsync("/api/documents/upload", form);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Upload_NotPdfExtension_Returns400()
    {
        var bytes = Encoding.ASCII.GetBytes("%PDF-1.4\n%StillLooksPdf\n");
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");

        // wrong extension
        form.Add(file, "files", "test.txt");

        var res = await _client.PostAsync("/api/documents/upload", form);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Upload_WrongPdfHeader_Returns400()
    {
        var bytes = Encoding.ASCII.GetBytes("HELLO_NOT_A_PDF");
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");

        // correct extension, wrong header -> should fail
        form.Add(file, "files", "test.pdf");

        var res = await _client.PostAsync("/api/documents/upload", form);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Upload_TooLarge_Returns400()
    {
        // 10MB limit in controller. Make it just over.
        var bytes = new byte[(10 * 1024 * 1024) + 1];
        // ensure it passes header check at least
        var sig = Encoding.ASCII.GetBytes("%PDF-");
        Array.Copy(sig, 0, bytes, 0, sig.Length);

        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "files", "big.pdf");

        var res = await _client.PostAsync("/api/documents/upload", form);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Upload_EmptyFile_IsSkipped_AndReturns201WithEmptyList()
    {
        var form = new MultipartFormDataContent();
        var empty = new ByteArrayContent(Array.Empty<byte>());
        empty.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(empty, "files", "empty.pdf");

        var res = await _client.PostAsync("/api/documents/upload", form);

        // Your controller "continue;" for length=0 and then returns CreatedAtAction with created.Count
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);

        var body = await res.Content.ReadAsStringAsync();
        // We don’t parse JSON here to keep it simple; but it should be an empty array.
        var created = await res.Content.ReadFromJsonAsync<List<DocumentDto>>();
        Assert.NotNull(created);
        Assert.Empty(created);
    }
}

