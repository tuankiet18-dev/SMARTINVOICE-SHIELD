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

    // For Internal OCR API
    [JsonPropertyName("ocr_job_id")]
    public string? OcrJobId { get; set; }

    [JsonPropertyName("confidence_scores")]
    public Dictionary<string, float>? ConfidenceScores { get; set; }
}

public class InvoiceExtractedData
{
    [JsonPropertyName("seller_name")]
    public string? SellerName { get; set; }

    [JsonPropertyName("seller_tax_code")]
    public string? SellerTaxCode { get; set; }

    [JsonPropertyName("seller_address")]
    public string? SellerAddress { get; set; }

    [JsonPropertyName("seller_phone")]
    public string? SellerPhone { get; set; }

    [JsonPropertyName("seller_email")]
    public string? SellerEmail { get; set; }

    [JsonPropertyName("seller_bank_account")]
    public string? SellerBankAccount { get; set; }

    [JsonPropertyName("seller_bank_name")]
    public string? SellerBankName { get; set; }

    [JsonPropertyName("buyer_name")]
    public string? BuyerName { get; set; }

    [JsonPropertyName("buyer_tax_code")]
    public string? BuyerTaxCode { get; set; }

    [JsonPropertyName("buyer_address")]
    public string? BuyerAddress { get; set; }

    [JsonPropertyName("buyer_phone")]
    public string? BuyerPhone { get; set; }

    [JsonPropertyName("buyer_email")]
    public string? BuyerEmail { get; set; }

    [JsonPropertyName("buyer_contact_person")]
    public string? BuyerContactPerson { get; set; }

    [JsonPropertyName("invoice_date")]
    public DateTime? InvoiceDate { get; set; }

    [JsonPropertyName("invoice_number")]
    public string? InvoiceNumber { get; set; }

    [JsonPropertyName("invoice_symbol")]
    public string? InvoiceSymbol { get; set; }

    [JsonPropertyName("invoice_template_code")]
    public string? InvoiceTemplateCode { get; set; }

    [JsonPropertyName("total_pre_tax")]
    public decimal TotalPreTax { get; set; }

    [JsonPropertyName("total_tax_amount")]
    public decimal TotalTaxAmount { get; set; }

    [JsonPropertyName("total_amount")]
    public decimal TotalAmount { get; set; }

    [JsonPropertyName("total_amount_in_words")]
    public string? TotalAmountInWords { get; set; }

    [JsonPropertyName("mccqt")]
    public string? MCCQT { get; set; } // Mã cơ quan thuế

    [JsonPropertyName("line_items")]
    public List<InvoiceLineItem>? LineItems { get; set; }

    [JsonPropertyName("payment_terms")]
    public string? PaymentTerms { get; set; }

    [JsonPropertyName("delivery_address")]
    public string? DeliveryAddress { get; set; }

    [JsonPropertyName("invoice_currency")]
    public string? InvoiceCurrency { get; set; }

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

    [JsonPropertyName("ocr_engine_version")]
    public string? OcrEngineVersion { get; set; }

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
