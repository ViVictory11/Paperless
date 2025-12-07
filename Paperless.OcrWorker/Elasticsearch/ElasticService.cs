using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Paperless.OcrWorker.Elasticsearch
{
    public class ElasticService : IElasticService
    {
        private readonly ElasticsearchClient _client;
        private readonly ILogger<ElasticService> _logger;

        private const string IndexName = "documents";

        public ElasticService(ElasticsearchClient client, ILogger<ElasticService> logger)
        {
            _client = client;
            _logger = logger;
        }

        public async Task<bool> IndexDocumentAsync(DocumentIndexModel doc)
        {
            var response = await _client.IndexAsync(doc, idx => idx.Index(IndexName));

            if (!response.IsValidResponse)
            {
                _logger.LogError("Failed to index document {Id}: {Error}",
                    doc.DocumentId, response.DebugInformation);
                return false;
            }

            _logger.LogInformation("Indexed document {Id} into Elasticsearch", doc.DocumentId);
            return true;
        }
    }
}
