using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Paperless.OcrWorker.Elasticsearch
{
    public interface IElasticService
    {
        Task<bool> IndexDocumentAsync(DocumentIndexModel doc);

    }
}
