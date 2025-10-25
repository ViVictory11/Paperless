using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Paperless.OcrWorker.Options
{
    public sealed record MinioOptions(
        string Endpoint,
        string AccessKey,
        string SecretKey,
        string BucketName,
        bool UseSSL,
        int PresignExpirySeconds = 3600);
}

