namespace SmartInvoice.API.Services.Interfaces;

public interface IAwsS3Service
{
    /// <summary>
    /// Upload file lên S3 theo đường dẫn exports/{companyId}/{yyyy-MM}/{fileName}
    /// </summary>
    Task<string> UploadExportFileAsync(Stream fileStream, string fileName, string contentType, Guid companyId);

    /// <summary>
    /// Tạo Pre-signed URL để tải file trực tiếp từ S3
    /// </summary>
    string GeneratePreSignedUrl(string s3Key, int expireMinutes = 15);
}
