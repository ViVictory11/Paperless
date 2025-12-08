using AutoMapper;
using Paperless.Contracts;
using Paperless.DAL.Service.Models;

namespace Paperless.DAL.Service.Profiles;
public class DocumentProfile : Profile
{
    public DocumentProfile()
    {
        CreateMap<DocumentEntity, DocumentDto>();
        CreateMap<DocumentEntity, DocumentDto>()
         .ConstructUsing(src => new DocumentDto(
             src.Id,
             src.FileName,     
             src.ObjectName,   
             src.ContentType,
             src.SizeBytes,
             src.UploadedAt,
             src.Summary
         ));


    }
}
