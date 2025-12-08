using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Mapping;
using Microsoft.Extensions.Logging;
using Paperless.Contracts;


namespace Paperless.DAL.Service.Services
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

            EnsureIndexExistsAsync().GetAwaiter().GetResult();
        }


        private async Task EnsureIndexExistsAsync()
        {
            var exists = await _client.Indices.ExistsAsync(IndexName);
            if (exists.Exists)
                return;

            var createResponse = await _client.Indices.CreateAsync<DocumentIndexModel>(c => c
                .Index(IndexName)
                .Mappings(m => m
                    .Properties(ps => ps
                        .Keyword(k => k.DocumentId)
                        .Text(t => t.FileName)
                        .Text(t => t.OriginalFileName)
                        .Text(t => t.SearchName)
                        .Text(t => t.Content)
                        .Text(t => t.Summary)
                    )
                )
            );


            if (createResponse.IsValidResponse)
                _logger.LogInformation("Created Elasticsearch index '{IndexName}' with mapping.", IndexName);
            else
                _logger.LogError("Failed to create index mapping: {Error}", createResponse.DebugInformation);
        }







        public async Task<bool> IndexDocumentAsync(DocumentIndexModel doc)
        {
            await EnsureIndexExistsAsync();
            _logger.LogInformation("Indexing document ID={Id}, file={Original}, minio={Minio}",doc.DocumentId,doc.OriginalFileName, doc.FileName);


            var response = await _client.IndexAsync(doc, i => i
                .Index(IndexName)
                .Id(doc.DocumentId)  
                .Refresh(Refresh.WaitFor)
            );

            if (!response.IsValidResponse)
            {
                _logger.LogError("Failed to index document {Id}: {Error}", doc.DocumentId, response.DebugInformation);
                return false;
            }

            _logger.LogInformation("Indexed document {Id} successfully.", doc.DocumentId);
            return true;
        }



        public async Task<IEnumerable<DocumentIndexModel>> SearchAsync(string query)
        {
            var normalized = NormalizeQuery(query);

            var response = await _client.SearchAsync<DocumentIndexModel>(s => s
                .Index(IndexName)
                .Query(q => q
                    .MultiMatch(mm => mm
                        .Fields(new Field[] { "content", "originalFileName", "summary", "searchName" })
                        .Query(normalized)
                    )
                )
                .Collapse(c => c.Field(f => f.DocumentId))
                .Size(50)
            );

            if (!response.IsValidResponse)
            {
                _logger.LogError("Elasticsearch search failed: {Error}", response.DebugInformation);
                return Enumerable.Empty<DocumentIndexModel>();
            }

            return response.Documents;
        }


        public async Task<bool> DeleteDocumentAsync(string id)
        {
            var response = await _client.DeleteAsync<Paperless.Contracts.DocumentIndexModel>(id, d => d
                .Index(IndexName)
                .Refresh(Refresh.WaitFor)
            );

            if (!response.IsValidResponse)
            {
                _logger.LogError("Failed to delete document {Id} from Elasticsearch: {Error}", id, response.DebugInformation);
                return false;
            }

            _logger.LogInformation("Deleted document {Id} from Elasticsearch.", id);
            return true;
        }



        private static string NormalizeQuery(string q)
        {
            if (string.IsNullOrWhiteSpace(q))
                return string.Empty;

            q = q.Trim();

            if (q.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                q = q[..^4];

            return q;
        }

    }
}
