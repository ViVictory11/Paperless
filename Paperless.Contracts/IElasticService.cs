using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Paperless.Contracts
{
    public interface IElasticService
    {
        Task<bool> IndexDocumentAsync(DocumentIndexModel doc);
        Task<IEnumerable<DocumentIndexModel>> SearchAsync(string query);


    }
}
