using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Paperless.Contracts;
using Paperless.DAL.Service.Models;
using Paperless.DAL.Service.Profiles;
using Xunit;

namespace Paperless.Tests;

public class MappingTests
{
    private static IMapper BuildMapper()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAutoMapper(cfg => cfg.AddProfile<DocumentProfile>());
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IMapper>();
    }



    /*[Fact]
    public void AutoMapper_Config_Is_Valid()
    {
        var mapper = BuildMapper();
        mapper.ConfigurationProvider.AssertConfigurationIsValid();
    }
    */



    /*[Fact]
    public void Maps_CreateDto_To_Entity()
    {
        var mapper = BuildMapper();

        var dto = new CreateDocumentDto("hello.pdf", "application/pdf", 12345);
        var entity = mapper.Map<DocumentEntity>(dto);

        entity.FileName.Should().Be("hello.pdf");
        entity.ContentType.Should().Be("application/pdf");
        entity.SizeBytes.Should().Be(12345);
    }*/

}

