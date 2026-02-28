using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using System.IO;

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

        public (string Url, string Key) GeneratePresignedUrl(string fileName, string contentType)
        {
            var bucketName = _config["AWS:BucketName"] ?? "smartinvoice-default-bucket";
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

            return (_s3Client.GetPreSignedURL(request), key);
        }

        public async Task<string> DownloadToTempFileAsync(string s3Key)
        {
            var bucketName = _config["AWS:BucketName"] ?? "smartinvoice-default-bucket";
            var request = new GetObjectRequest
            {
                BucketName = bucketName,
                Key = s3Key
            };

            using var response = await _s3Client.GetObjectAsync(request);
            var tempFilePath = Path.GetTempFileName();
            using var fileStream = File.Create(tempFilePath);
            await response.ResponseStream.CopyToAsync(fileStream);

            return tempFilePath;
        }

        public async Task DeleteFileAsync(string s3Key)
        {
            try
            {
                var bucketName = _config["AWS:BucketName"] ?? "smartinvoice-default-bucket";
                var request = new DeleteObjectRequest
                {
                    BucketName = bucketName,
                    Key = s3Key
                };
                await _s3Client.DeleteObjectAsync(request);
            }
            catch (Exception)
            {
                // Log exception if needed
            }
        }
    }
}