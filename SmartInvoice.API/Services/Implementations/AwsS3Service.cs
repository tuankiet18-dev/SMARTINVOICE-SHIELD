using Amazon.S3;
using Amazon.S3.Model;
using SmartInvoice.API.Services.Interfaces;

namespace SmartInvoice.API.Services.Implementations;

public class AwsS3Service : IAwsS3Service
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;

    public AwsS3Service(IAmazonS3 s3Client, IConfiguration configuration)
    {
        _s3Client = s3Client;
        _bucketName = configuration["AWS:BucketName"] ?? "smartinvoice-default-bucket";
    }

    public async Task<string> UploadExportFileAsync(Stream fileStream, string fileName, string contentType, Guid companyId)
    {
        var datePath = DateTime.UtcNow.ToString("yyyy-MM");
        var s3Key = $"exports/{companyId}/{datePath}/{fileName}";

        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = s3Key,
            InputStream = fileStream,
            ContentType = contentType,
            ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256
        };

        await _s3Client.PutObjectAsync(request);

        return s3Key;
    }

    public string GeneratePreSignedUrl(string s3Key, int expireMinutes = 15)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = s3Key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.AddMinutes(expireMinutes)
        };

        return _s3Client.GetPreSignedURL(request);
    }
}
