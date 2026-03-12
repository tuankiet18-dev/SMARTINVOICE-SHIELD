using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SmartInvoice.API.DTOs.Invoice;

public class OcrField<T>
{
    [JsonPropertyName("confidence")]
    public float Confidence { get; set; }

    [JsonPropertyName("value")]
    public T? Value { get; set; }
}

public class OcrBuyer
{
    [JsonPropertyName("address")]
    public OcrField<string>? Address { get; set; }

    [JsonPropertyName("full_name")]
    public OcrField<string>? FullName { get; set; }

    [JsonPropertyName("name")]
    public OcrField<string>? Name { get; set; }

    [JsonPropertyName("tax_code")]
    public OcrField<string>? TaxCode { get; set; }
}

public class OcrSeller
{
    [JsonPropertyName("address")]
    public OcrField<string>? Address { get; set; }

    [JsonPropertyName("bank_account")]
    public OcrField<string>? BankAccount { get; set; }

    [JsonPropertyName("bank_name")]
    public OcrField<string>? BankName { get; set; }

    [JsonPropertyName("name")]
    public OcrField<string>? Name { get; set; }

    [JsonPropertyName("phone")]
    public OcrField<string>? Phone { get; set; }

    [JsonPropertyName("tax_authority_code")]
    public OcrField<string>? TaxAuthorityCode { get; set; }

    [JsonPropertyName("tax_code")]
    public OcrField<string>? TaxCode { get; set; }
}

public class OcrInvoiceData
{
    [JsonPropertyName("currency")]
    public OcrField<string>? Currency { get; set; }

    [JsonPropertyName("date")]
    public OcrField<string>? Date { get; set; }

    [JsonPropertyName("number")]
    public OcrField<string>? Number { get; set; }

    [JsonPropertyName("payment_method")]
    public OcrField<string>? PaymentMethod { get; set; }

    [JsonPropertyName("subtotal")]
    public OcrField<decimal?>? Subtotal { get; set; }

    [JsonPropertyName("symbol")]
    public OcrField<string>? Symbol { get; set; }

    [JsonPropertyName("total_amount")]
    public OcrField<decimal?>? TotalAmount { get; set; }

    [JsonPropertyName("type")]
    public OcrField<string>? Type { get; set; }

    [JsonPropertyName("vat_amount")]
    public OcrField<decimal?>? VatAmount { get; set; }

    [JsonPropertyName("vat_rate")]
    public OcrField<string>? VatRate { get; set; }
}

public class OcrItem
{
    [JsonPropertyName("discount")]
    public OcrField<decimal?>? Discount { get; set; }

    [JsonPropertyName("line_tax")]
    public OcrField<decimal?>? LineTax { get; set; }

    [JsonPropertyName("name")]
    public OcrField<string>? Name { get; set; }

    [JsonPropertyName("quantity")]
    public OcrField<decimal?>? Quantity { get; set; }

    [JsonPropertyName("row_total")]
    public OcrField<decimal?>? RowTotal { get; set; }

    [JsonPropertyName("total")]
    public OcrField<decimal?>? Total { get; set; }

    [JsonPropertyName("unit")]
    public OcrField<string>? Unit { get; set; }

    [JsonPropertyName("unit_price")]
    public OcrField<decimal?>? UnitPrice { get; set; }

    [JsonPropertyName("vat_rate")]
    public OcrField<string>? VatRate { get; set; }
}

public class OcrInvoiceResult
{
    [JsonPropertyName("buyer")]
    public OcrBuyer? Buyer { get; set; }

    [JsonPropertyName("document_confidence")]
    public float DocumentConfidence { get; set; }

    [JsonPropertyName("invoice")]
    public OcrInvoiceData? Invoice { get; set; }

    [JsonPropertyName("items")]
    public List<OcrItem>? Items { get; set; }

    [JsonPropertyName("seller")]
    public OcrSeller? Seller { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }
}

public class ProcessOcrRequestDto
{
    [JsonPropertyName("s3Key")]
    public string? S3Key { get; set; }

    [JsonPropertyName("bucketName")]
    public string? BucketName { get; set; }

    [JsonPropertyName("ocrResult")]
    public OcrInvoiceResult? OcrResult { get; set; }
}
