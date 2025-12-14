using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Search;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Paperless.Contracts;
using Paperless.DAL.Controllers;
using Paperless.DAL.Service.Repositories;
using Paperless.DAL.Service.Services;
using Paperless.DAL.Service.Services.FileStorage;
using Xunit;
using AutoMapper;
using Paperless.DAL.Service;

namespace Paperless.Tests
{
    public class ElasticSearchTests
    {
        [Fact]
        public async Task Search_Returns_Empty_List_When_Query_Is_Empty()
        {
            var controller = CreateController(out var elasticMock);

            var result = await controller.Search("   ");

            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();

            var list = ok!.Value as IEnumerable<object>;
            list.Should().NotBeNull();
            list!.Should().BeEmpty();

            elasticMock.Verify(e => e.SearchAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Search_Returns_Empty_List_When_Elastic_Returns_No_Results()
        {
            var controller = CreateController(out var elasticMock);

            elasticMock
                .Setup(e => e.SearchAsync("invoice"))
                .ReturnsAsync(new List<DocumentIndexModel>());

            var result = await controller.Search("invoice");

            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();

            var list = ok!.Value as IEnumerable<object>;
            list.Should().BeEmpty();
        }

        [Fact]
        public async Task Search_Returns_Documents_When_Elastic_Finds_Matches()
        {
            var controller = CreateController(out var elasticMock, out var repoMock);

            var docId = Guid.NewGuid();

            elasticMock
                .Setup(e => e.SearchAsync("invoice"))
                .ReturnsAsync(new[]
                {
                    new DocumentIndexModel
                    {
                        DocumentId = docId.ToString(),
                        OriginalFileName = "invoice.pdf"
                    }
                });

            repoMock
                .Setup(r => r.GetAsync(docId, default))
                .ReturnsAsync(new DAL.Service.Models.DocumentEntity
                {
                    Id = docId,
                    FileName = "invoice.pdf",
                    ContentType = "application/pdf",
                    SizeBytes = 123
                });

            var result = await controller.Search("invoice");

            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();

            var list = ok!.Value as IEnumerable<object>;
            list.Should().HaveCount(1);
        }


        [Fact]
        public async Task ElasticService_IndexDocument_Returns_True_When_Successful()
        {
            var elasticMock = new Mock<IElasticService>();

            elasticMock
                .Setup(e => e.IndexDocumentAsync(It.IsAny<DocumentIndexModel>()))
                .ReturnsAsync(true);

            var result = await elasticMock.Object.IndexDocumentAsync(
                new DocumentIndexModel { DocumentId = "1" });

            result.Should().BeTrue();
        }

        [Fact]
        public async Task ElasticService_Search_Returns_Empty_On_Failure()
        {
            var elasticMock = new Mock<IElasticService>();

            elasticMock
                .Setup(e => e.SearchAsync(It.IsAny<string>()))
                .ReturnsAsync(Array.Empty<DocumentIndexModel>());

            var result = await elasticMock.Object.SearchAsync("anything");

            result.Should().BeEmpty();
        }


        private static DocumentsController CreateController(
            out Mock<IElasticService> elasticMock,
            out Mock<IDocumentRepository> repoMock)
        {
            repoMock = new Mock<IDocumentRepository>();
            elasticMock = new Mock<IElasticService>();

            return new DocumentsController(
                repoMock.Object,
                Mock.Of<IMapper>(),
                Mock.Of<IRabbitMqService>(),
                Mock.Of<ILogger<DocumentsController>>(),
                Mock.Of<IDocumentStorage>(),
                elasticMock.Object
            );
        }

        private static DocumentsController CreateController(
            out Mock<IElasticService> elasticMock)
            => CreateController(out elasticMock, out _);
    }
}

