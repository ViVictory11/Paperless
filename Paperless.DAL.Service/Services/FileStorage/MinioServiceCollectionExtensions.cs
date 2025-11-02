using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Minio;
using Paperless.DAL.Service.Options;

namespace Paperless.DAL.Service.Services.FileStorage
{
    public static class MinioServiceCollectionExtensions
    {
        public static IServiceCollection AddMinioStorage(this IServiceCollection services, IConfiguration cfg)
        {
            services.Configure<MinioOptions>(cfg.GetSection("Minio"));

            services.AddSingleton<IMinioClient>(sp =>
            {
                var opt = sp.GetRequiredService<IOptions<MinioOptions>>().Value;

                var client = new MinioClient()
                    .WithEndpoint(opt.Endpoint)
                    .WithCredentials(opt.AccessKey, opt.SecretKey)
                    .WithSSL(opt.UseSSL);

                return client.Build();
            });

            services.AddSingleton<IDocumentStorage, MinioDocumentStorage>();

            return services;
        }
    }
}
