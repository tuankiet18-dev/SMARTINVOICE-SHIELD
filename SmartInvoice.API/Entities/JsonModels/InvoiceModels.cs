using System.Text.Json.Serialization;

namespace SmartInvoice.API.Entities.JsonModels;

// --- INVOICE DATA ---

public class InvoiceRawData
{
    [JsonPropertyName("xml_version")]
    public string? XmlVersion { get; set; }

    [JsonPropertyName("invoice_template")]
    public string? InvoiceTemplate { get; set; }

    [JsonPropertyName("bucket_name")]
    public string? BucketName { get; set; }

    [JsonPropertyName("object_key")]
    public string? ObjectKey { get; set; }

    // Can store full XML string or key parts
    [JsonPropertyName("xml_content_base64")]
    public string? XmlContentBase64 { get; set; }

    // For Textract
    [JsonPropertyName("textract_job_id")]
    public string? TextractJobId { get; set; }

    [JsonPropertyName("confidence_scores")]
    public Dictionary<string, float>? ConfidenceScores { get; set; }
}

public class InvoiceExtractedData
{
    [JsonPropertyName("line_items")]
    public List<InvoiceLineItem>? LineItems { get; set; }

    [JsonPropertyName("payment_terms")]
    public string? PaymentTerms { get; set; }

    [JsonPropertyName("delivery_address")]
    public string? DeliveryAddress { get; set; }

    [JsonPropertyName("exchange_rate")] 
    public decimal? ExchangeRate { get; set; }
}

public class InvoiceLineItem
{
    [JsonPropertyName("stt")]
    public int Stt { get; set; }

    [JsonPropertyName("product_name")]
    public string? ProductName { get; set; }

    [JsonPropertyName("unit")]
    public string? Unit { get; set; }

    [JsonPropertyName("quantity")]
    public decimal Quantity { get; set; }

    [JsonPropertyName("unit_price")]
    public decimal UnitPrice { get; set; }

    [JsonPropertyName("total_amount")]
    public decimal TotalAmount { get; set; }

    [JsonPropertyName("vat_rate")]
    public int VatRate { get; set; }

    [JsonPropertyName("vat_amount")]
    public decimal VatAmount { get; set; }
}

// --- VALIDATION & RISK ---

public class ValidationResultModel
{
    [JsonPropertyName("structure_valid")]
    public bool StructureValid { get; set; }

    [JsonPropertyName("signature_valid")]
    public bool SignatureValid { get; set; }

    [JsonPropertyName("business_logic_valid")]
    public bool BusinessLogicValid { get; set; }

    [JsonPropertyName("errors")]
    public List<RiskReason>? Errors { get; set; }

    [JsonPropertyName("warnings")]
    public List<RiskReason>? Warnings { get; set; }

    [JsonPropertyName("validation_timestamp")]
    public DateTime? ValidationTimestamp { get; set; }
}

public class RiskReason
{
    [JsonPropertyName("layer")]
    public string? Layer { get; set; } // Signature, Structure...

    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("severity")]
    public string? Severity { get; set; } // Green, Yellow, Orange, Red

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("auto_detected")]
    public bool AutoDetected { get; set; }

    [JsonPropertyName("checked_at")]
    public DateTime? CheckedAt { get; set; }
}

// --- CONFIGURATION ---

public class ValidationRuleConfig
{
    [JsonPropertyName("required_fields")]
    public List<string>? RequiredFields { get; set; }

    [JsonPropertyName("optional_fields")]
    public List<string>? OptionalFields { get; set; }

    [JsonPropertyName("regex_patterns")]
    public Dictionary<string, string>? RegexPatterns { get; set; }

    [JsonPropertyName("amount_limits")]
    public AmountLimits? AmountLimits { get; set; }

    [JsonPropertyName("vat_rates")]
    public List<int>? VatRates { get; set; }
}

public class AmountLimits
{
    [JsonPropertyName("min")]
    public decimal? Min { get; set; }

    [JsonPropertyName("max")]
    public decimal? Max { get; set; }
}

public class ProcessingConfig
{
    [JsonPropertyName("ocr_enabled")]
    public bool OcrEnabled { get; set; }

    [JsonPropertyName("textract_model")]
    public string? TextractModel { get; set; }

    [JsonPropertyName("confidence_threshold")]
    public float ConfidenceThreshold { get; set; }

    [JsonPropertyName("auto_approve_threshold")]
    public float AutoApproveThreshold { get; set; }
}

// --- AUDIT ---

public class AuditChange
{
    [JsonPropertyName("field")]
    public string? Field { get; set; }

    [JsonPropertyName("old_value")]
    public object? OldValue { get; set; }

    [JsonPropertyName("new_value")]
    public object? NewValue { get; set; }

    [JsonPropertyName("change_type")]
    public string? ChangeType { get; set; } // UPDATE, INSERT
}
