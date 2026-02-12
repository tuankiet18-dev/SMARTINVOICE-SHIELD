using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;

namespace SmartInvoice.API.Services
{
    public class StorageService
    {
        private readonly IAmazonS3 _s3Client;
        private readonly IConfiguration _config;

        public StorageService(IAmazonS3 s3Client, IConfiguration config)
        {
            _s3Client = s3Client;
            _config = config;
        }

        public string GeneratePresignedUrl(string fileName, string contentType)
        {
            var bucketName = _config["AWS:BucketName"];
            // Đổi tên file để tránh trùng: raw/guid_tenfile.pdf
            var key = $"raw/{Guid.NewGuid()}_{fileName}";

            var request = new GetPreSignedUrlRequest
            {
                BucketName = bucketName,
                Key = key,
                Verb = HttpVerb.PUT,
                Expires = DateTime.UtcNow.AddMinutes(15), // Link hết hạn sau 15p
                ContentType = contentType
            };

            return _s3Client.GetPreSignedURL(request);
        }
    }
}