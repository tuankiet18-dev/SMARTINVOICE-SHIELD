namespace SmartInvoice.API.Services.Interfaces;

public interface IAwsS3Service
{
    /// <summary>
    /// Upload file lên S3 theo đường dẫn exports/{companyId}/{yyyy-MM}/{fileName}
    /// </summary>
    Task<string> UploadExportFileAsync(Stream fileStream, string fileName, string contentType, Guid companyId);

    /// <summary>
    /// Upload invoice image to S3 under invoices/{companyId}/{yyyy-MM}/{guid}.ext
    /// Returns the S3 object key.
    /// </summary>
    Task<string> UploadInvoiceImageAsync(Stream fileStream, string fileName, string contentType, Guid companyId);

    /// <summary>
    /// Download raw file bytes from S3 by key. Used by OcrWorkerService to fetch images.
    /// </summary>
    Task<byte[]> DownloadFileAsync(string s3Key);

    /// <summary>
    /// Tạo Pre-signed URL để tải file trực tiếp từ S3
    /// </summary>
    string GeneratePreSignedUrl(string s3Key, int expireMinutes = 15);
}
