using AutoMapper;
using Paperless.Contracts;
using Paperless.DAL.Service.Contracts;
using Paperless.DAL.Service.Models;

namespace Paperless.DAL.Service.Profiles;
public class DocumentProfile : Profile
{
    public DocumentProfile()
    {
        CreateMap<DocumentEntity, DocumentDto>();
        CreateMap<CreateDocumentDto, DocumentEntity>();
    }
}
