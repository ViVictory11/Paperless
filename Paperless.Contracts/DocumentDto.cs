using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Paperless.Contracts
{

    public record DocumentDto(
        Guid Id,
        string FileName,
        string ContentType,
        long SizeBytes,
        DateTime UploadedAt,
        string? Summary 
    );

}
