# SYSTEM ARCHITECTURE DESIGN - PART 2
## Data Flow, Deployment, Security & Performance

**Version**: 1.0 Production  
**Continuation of**: SYSTEM_ARCHITECTURE_DETAILED.md

---

## 5. DATA FLOW ARCHITECTURE

### 5.1 Complete Request Flow (Happy Path - XML Upload)

```
┌──────────────────────────────────────────────────────────────────────┐
│  SCENARIO: Member uploads XML invoice & System auto-validates        │
└──────────────────────────────────────────────────────────────────────┘

Step 1: USER ACTION
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
[User Browser]
  │
  ├─ User clicks "Upload Invoice" button
  ├─ FileDropzone component opens
  ├─ User drags invoice_001_01GTKT.xml file
  ├─ Client-side validation:
  │  ✓ File extension check (.xml, .pdf, .jpg, .png)
  │  ✓ File size check (< 10MB)
  │  ✓ MIME type check
  │
  └─ Validation passed → Proceed to upload

Step 2: FILE UPLOAD TO S3
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
[React SPA] → [.NET API] → [AWS S3]

POST /api/invoices/upload
Headers:
  Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
  Content-Type: multipart/form-data
Body:
  file: invoice_001_01GTKT.xml

[InvoiceController.UploadAsync()]
  │
  ├─ Extract JWT → Get UserId, CompanyId
  ├─ Validate user permissions (Member+ role)
  ├─ Read file stream
  ├─ Generate S3 key: "{CompanyId}/invoices/{year}/{month}/{guid}.xml"
  │  Example: "abc123/invoices/2025/01/550e8400-e29b-41d4-a716.xml"
  │
  └─ Call S3Service.UploadFileAsync(stream, s3Key)
       │
       ├─ AWS SDK: PutObjectAsync()
       ├─ S3 Bucket: smartinvoice-prod
       ├─ Server-side encryption: AES-256
       ├─ Storage class: S3 Standard
       │
       └─ Returns: S3 URL, ETag, VersionId

[S3Service returns success]
  │
  └─ Create FileStorages record in DB:
       INSERT INTO FileStorages (
         FileId, CompanyId, UploadedBy,
         OriginalFileName, FileExtension, FileSize, MimeType,
         S3BucketName, S3Key, S3VersionId,
         IsProcessed = false,
         CreatedAt
       )

Step 3: CREATE INVOICE RECORD (Draft)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
[InvoiceController continues]
  │
  └─ Create Invoice record:
       INSERT INTO Invoices (
         InvoiceId, CompanyId, DocumentTypeId,
         OriginalFileId, ProcessingMethod = 'XML',
         Status = 'Draft',
         UploadedBy, CreatedAt
       )
       RETURNING InvoiceId

[AuditLogService.LogActionAsync()]
  │
  └─ INSERT INTO InvoiceAuditLogs (
       InvoiceId, UserId, Action = 'UPLOAD',
       NewData = { "status": "Draft", "file_name": "invoice_001_01GTKT.xml" },
       IpAddress, UserAgent, CreatedAt
     )

Response to frontend:
{
  "success": true,
  "invoiceId": "550e8400-e29b-41d4-a716-446655440000",
  "message": "File uploaded successfully",
  "nextStep": "Processing"
}

Step 4: BACKGROUND PROCESSING (3-Layer Validation)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
[InvoiceProcessorService.ValidateXmlAsync(s3Key)]
  │
  ├─ STEP 4.1: Download XML from S3
  │  │
  │  ├─ S3Service.DownloadFileAsync(s3Key)
  │  ├─ Save to temp file: /tmp/invoice-{guid}.xml
  │  └─ Returns: temp file path
  │
  ├─ STEP 4.2: LAYER 1 - STRUCTURE VALIDATION (XSD)
  │  │
  │  ├─ Load XSD schema: InvoiceSchema.xsd
  │  ├─ XmlDocument.Load(tempFilePath)
  │  ├─ XmlDocument.Schemas.Add(xsd)
  │  ├─ XmlDocument.Validate(validationEventHandler)
  │  │
  │  └─ Result:
  │       {
  │         "isValid": true,
  │         "errors": [],
  │         "duration": "45ms"
  │       }
  │
  ├─ STEP 4.3: LAYER 2 - SIGNATURE VALIDATION
  │  │
  │  ├─ Load signature node: <DSCKS><NBan><Signature>
  │  ├─ SignedXml.LoadXml(signatureNode)
  │  ├─ SignedXml.CheckSignature(cert, true)
  │  │
  │  ├─ Extract signer MST from certificate Subject:
  │  │  Example: "CN=CÔNG TY ABC, MST=0123456789, C=VN"
  │  │  Regex: MST=(\d{10,13})
  │  │  Signer MST: "0123456789"
  │  │
  │  ├─ Extract seller MST from XML:
  │  │  XPath: //NBan/MST
  │  │  Seller MST: "0123456789"
  │  │
  │  ├─ ANTI-SPOOFING CHECK:
  │  │  IF (signerMST != sellerMST) THEN
  │  │    RETURN {
  │  │      "isValid": false,
  │  │      "errorCode": "ANTI_SPOOFING",
  │  │      "message": "MST người ký không khớp MST người bán",
  │  │      "severity": "RED"
  │  │    }
  │  │
  │  └─ Result (if all pass):
  │       {
  │         "isValid": true,
  │         "signerSubject": "CN=CÔNG TY ABC, MST=0123456789",
  │         "signatureAlgorithm": "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256",
  │         "duration": "120ms"
  │       }
  │
  └─ STEP 4.4: LAYER 3 - BUSINESS LOGIC VALIDATION
     │
     ├─ Auto-detect invoice type:
     │  │
     │  ├─ Read XPath: //TTChung/KHMSHDon
     │  │  Value: "01GTKT" → Type = GTGT (VAT invoice)
     │  │
     │  └─ Load validation rules from DocumentTypes table:
     │       {
     │         "required_fields": ["InvoiceDate", "InvoiceNumber", ...],
     │         "vat_rates": [0, 5, 8, 10]
     │       }
     │
     ├─ A. CHECK MANDATORY FIELDS
     │  │
     │  ├─ Required for GTGT:
     │  │  ✓ KHMSHDon (Form number)
     │  │  ✓ KHHDon (Serial number)
     │  │  ✓ SHDon (Invoice number)
     │  │  ✓ NLap (Invoice date)
     │  │  ✓ NBan/MST (Seller tax code)
     │  │  ✓ NMua/MST (Buyer tax code)
     │  │  ✓ TgTTTBSo (Total amount)
     │  │
     │  └─ All present? ✓ Pass
     │
     ├─ B. VALIDATE MATH
     │  │
     │  ├─ For each line item in DSHHDVu/HHDVu:
     │  │  │
     │  │  ├─ SLuong (Quantity): 10
     │  │  ├─ DGia (Unit price): 15000000
     │  │  ├─ ThTien (Total): 150000000
     │  │  ├─ Expected: 10 × 15000000 = 150000000
     │  │  ├─ Difference: |Expected - Actual| = 0
     │  │  └─ Tolerance: 10 VND → ✓ Pass
     │  │
     │  ├─ Total validation (GTGT):
     │  │  │
     │  │  ├─ TgTCThue (Total before tax): 300000000
     │  │  ├─ TgTThue (Total VAT): 30000000
     │  │  ├─ TgTTTBSo (Grand total): 330000000
     │  │  ├─ Expected: 300000000 + 30000000 = 330000000
     │  │  └─ ✓ Pass
     │  │
     │  └─ All math correct? ✓ Pass
     │
     ├─ C. VALIDATE MST (Tax Code)
     │  │
     │  ├─ Seller MST: "0123456789"
     │  │
     │  ├─ Format validation (10 digits with Mod-11 checksum):
     │  │  │
     │  │  ├─ Base digits: "012345678"
     │  │  ├─ Weights: [31, 29, 23, 19, 17, 13, 7, 5, 3]
     │  │  ├─ Sum: 0×31 + 1×29 + 2×23 + ... = 358
     │  │  ├─ Remainder: 358 % 11 = 6
     │  │  ├─ Checksum: (10 - 6) % 10 = 4
     │  │  ├─ Expected last digit: 4
     │  │  ├─ Actual last digit: 9
     │  │  └─ ✗ FAIL (checksum mismatch)
     │  │
     │  └─ VietQR API verification (if checksum pass):
     │       │
     │       ├─ GET https://api.vietqr.io/v2/business/0123456789
     │       ├─ Response:
     │       │  {
     │       │    "code": "00",
     │       │    "data": {
     │       │      "name": "CÔNG TY TNHH ABC",
     │       │      "address": "123 Nguyễn Huệ, Q1, TP.HCM",
     │       │      "status": "Active"
     │       │    }
     │       │  }
     │       │
     │       └─ Cache result for 24h (in-memory cache)
     │
     └─ D. VALIDATE DATE
        │
        ├─ NLap (Invoice date): "2025-01-15"
        ├─ Current date: "2025-02-06"
        ├─ Is future? NO → ✓ Pass
        │
        └─ Result: All business logic checks passed

Step 5: CALCULATE RISK LEVEL
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
[ValidationService.CalculateRiskLevel(validationResults)]
  │
  ├─ Layer 1 (Structure): ✓ Pass
  ├─ Layer 2 (Signature): ✓ Pass (no anti-spoofing)
  ├─ Layer 3 (Business Logic): ⚠ 1 warning (MST checksum mismatch)
  │
  └─ Risk Level Decision Tree:
       │
       ├─ IF Layer 1 FAIL → RED (invalid XML)
       ├─ ELSE IF Anti-Spoofing detected → RED (security threat)
       ├─ ELSE IF Layer 2 FAIL → ORANGE (signature invalid)
       ├─ ELSE IF Layer 3 has CRITICAL errors → ORANGE
       │  (e.g., math error, future date, inactive MST)
       ├─ ELSE IF Layer 3 has WARNINGS → YELLOW
       │  (e.g., checksum mismatch, MST not verified)
       ├─ ELSE → GREEN (all passed)
       │
       └─ **Result: YELLOW** (checksum warning)

Step 6: SAVE VALIDATION RESULTS
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
[InvoiceProcessorService continues]
  │
  ├─ Parse extracted data from XML:
  │  │
  │  ├─ InvoiceNumber: "0001234"
  │  ├─ SerialNumber: "C24T"
  │  ├─ FormNumber: "01GTKT"
  │  ├─ InvoiceDate: "2025-01-15"
  │  ├─ SellerName: "CÔNG TY TNHH ABC"
  │  ├─ SellerTaxCode: "0123456789"
  │  ├─ BuyerName: "CÔNG TY CP XYZ"
  │  ├─ BuyerTaxCode: "9876543210"
  │  ├─ TotalAmount: 330000000
  │  └─ ... (other fields)
  │
  ├─ UPDATE Invoices SET
  │    InvoiceNumber = '0001234',
  │    SerialNumber = 'C24T',
  │    FormNumber = '01GTKT',
  │    InvoiceDate = '2025-01-15',
  │    SellerName = 'CÔNG TY TNHH ABC',
  │    SellerTaxCode = '0123456789',
  │    BuyerName = 'CÔNG TY CP XYZ',
  │    BuyerTaxCode = '9876543210',
  │    TotalAmount = 330000000,
  │    RawData = '{...full XML...}'::jsonb,
  │    ExtractedData = '{...parsed data...}'::jsonb,
  │    ValidationResult = '{
  │      "structure_valid": true,
  │      "signature_valid": true,
  │      "business_logic_valid": true,
  │      "errors": [],
  │      "warnings": [
  │        {
  │          "code": "MST_CHECKSUM_MISMATCH",
  │          "message": "MST 0123456789: Checksum không khớp",
  │          "severity": "YELLOW"
  │        }
  │      ]
  │    }'::jsonb,
  │    RiskLevel = 'Yellow',
  │    RiskReasons = '[
  │      {
  │        "code": "MST_CHECKSUM_MISMATCH",
  │        "severity": "Yellow",
  │        "message": "Mã số thuế người bán có checksum không hợp lệ"
  │      }
  │    ]'::jsonb,
  │    Status = 'Draft',
  │    UpdatedAt = NOW()
  │  WHERE InvoiceId = '550e8400-e29b-41d4-a716-446655440000'
  │
  ├─ INSERT INTO ValidationLayers (3 records):
  │  │
  │  ├─ Record 1:
  │  │  LayerName = 'Structure',
  │  │  LayerOrder = 1,
  │  │  IsValid = true,
  │  │  ValidationStatus = 'Pass',
  │  │  ValidationDurationMs = 45
  │  │
  │  ├─ Record 2:
  │  │  LayerName = 'Signature',
  │  │  LayerOrder = 2,
  │  │  IsValid = true,
  │  │  ValidationStatus = 'Pass',
  │  │  SignatureValidationData = '{...cert info...}',
  │  │  ValidationDurationMs = 120
  │  │
  │  └─ Record 3:
  │     LayerName = 'BusinessLogic',
  │     LayerOrder = 3,
  │     IsValid = true,
  │     ValidationStatus = 'Warning',
  │     ErrorCode = 'MST_CHECKSUM_MISMATCH',
  │     ErrorMessage = 'MST checksum không hợp lệ',
  │     BusinessLogicValidationData = '{...details...}',
  │     ValidationDurationMs = 350
  │
  └─ INSERT INTO RiskCheckResults:
       {
         "CheckType": "LEGAL",
         "CheckSubType": "MST_VERIFICATION",
         "CheckStatus": "WARNING",
         "RiskLevel": "Yellow",
         "ErrorCode": "MST_CHECKSUM_MISMATCH",
         "CheckDetails": {...}
       }

Step 7: CLEANUP & NOTIFICATION
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
[InvoiceProcessorService finalizes]
  │
  ├─ Delete temp file: File.Delete(tempFilePath)
  │
  ├─ UPDATE FileStorages SET
  │    IsProcessed = true,
  │    ProcessedAt = NOW()
  │  WHERE FileId = ...
  │
  └─ Create notification for user:
       INSERT INTO Notifications (
         UserId, Type = 'INVOICE_SUBMITTED',
         Title = 'Invoice processed',
         Message = 'Invoice #0001234 has been processed. Risk level: Yellow',
         ActionUrl = '/invoices/550e8400-...',
         ActionText = 'View Invoice'
       )

Step 8: RESPONSE TO FRONTEND
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
[API returns to React]

HTTP 200 OK
Content-Type: application/json

{
  "success": true,
  "invoice": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "invoiceNumber": "0001234",
    "invoiceDate": "2025-01-15",
    "status": "Draft",
    "riskLevel": "Yellow",
    "riskReasons": [
      {
        "code": "MST_CHECKSUM_MISMATCH",
        "message": "Mã số thuế người bán có checksum không hợp lệ",
        "severity": "Yellow"
      }
    ],
    "validationResult": {
      "structureValid": true,
      "signatureValid": true,
      "businessLogicValid": true,
      "warnings": [...]
    },
    "totalAmount": 330000000
  },
  "processingTimeMs": 515,
  "message": "Invoice validated successfully"
}

Step 9: FRONTEND UPDATES UI
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
[React SPA]
  │
  ├─ UploadPage receives response
  │
  ├─ Show success notification:
  │  "✓ Invoice #0001234 uploaded and validated"
  │
  ├─ Display ProcessingResult component:
  │  ┌────────────────────────────────────────┐
  │  │  VALIDATION RESULT                     │
  │  ├────────────────────────────────────────┤
  │  │  ⚠ RISK LEVEL: YELLOW                  │
  │  │                                         │
  │  │  ✓ Structure: Valid                    │
  │  │  ✓ Signature: Valid                    │
  │  │  ⚠ Business Logic: 1 warning           │
  │  │                                         │
  │  │  Warnings:                              │
  │  │  • MST checksum không hợp lệ           │
  │  │                                         │
  │  │  [View Details] [Edit] [Submit]        │
  │  └────────────────────────────────────────┘
  │
  └─ User can:
     ├─ View Details → Navigate to /invoices/:id
     ├─ Edit → Navigate to /invoices/:id/edit
     └─ Submit → Change status to Pending
```

---

### 5.2 Alternative Flow: PDF Upload (OCR Path)

```
[User uploads PDF file instead of XML]
  │
  ├─ Same Step 1-3 (Upload to S3, Create Invoice record)
  │
  └─ DIFFERENT: Step 4 - Textract Processing
      │
      ├─ [TextractService.AnalyzeExpenseAsync(s3Key)]
      │  │
      │  ├─ AWS SDK: StartExpenseAnalysisAsync()
      │  │  Request:
      │  │  {
      │  │    "DocumentLocation": {
      │  │      "S3Object": {
      │  │        "Bucket": "smartinvoice-prod",
      │  │        "Name": "abc123/invoices/2025/01/xxx.pdf"
      │  │      }
      │  │    }
      │  │  }
      │  │
      │  ├─ Textract processes (2-5 seconds)
      │  │
      │  └─ Response:
      │     {
      │       "ExpenseDocuments": [{
      │         "SummaryFields": [
      │           {
      │             "Type": { "Text": "INVOICE_RECEIPT_ID" },
      │             "ValueDetection": {
      │               "Text": "0001234",
      │               "Confidence": 95.5
      │             }
      │           },
      │           {
      │             "Type": { "Text": "INVOICE_RECEIPT_DATE" },
      │             "ValueDetection": {
      │               "Text": "15/01/2025",
      │               "Confidence": 92.3
      │             }
      │           },
      │           {
      │             "Type": { "Text": "TOTAL" },
      │             "ValueDetection": {
      │               "Text": "330,000,000",
      │               "Confidence": 98.1
      │             }
      │           },
      │           {
      │             "Type": { "Text": "VENDOR_NAME" },
      │             "ValueDetection": {
      │               "Text": "CÔNG TY TNHH ABC",
      │               "Confidence": 88.7
      │             }
      │           }
      │         ],
      │         "LineItemGroups": [...]
      │       }]
      │     }
      │
      ├─ Map Textract response to Invoice model:
      │  │
      │  ├─ InvoiceNumber: "0001234" (confidence: 95.5%)
      │  ├─ InvoiceDate: Parse "15/01/2025" → "2025-01-15"
      │  ├─ TotalAmount: Parse "330,000,000" → 330000000
      │  ├─ SellerName: "CÔNG TY TNHH ABC" (confidence: 88.7%)
      │  └─ ...
      │
      ├─ Calculate overall confidence:
      │  Average = (95.5 + 92.3 + 98.1 + 88.7) / 4 = 93.65%
      │
      └─ Risk Level for OCR:
         │
         ├─ IF no XML → YELLOW (thiếu hóa đơn pháp lý)
         ├─ IF confidence < 70% → ORANGE (low confidence)
         ├─ IF confidence < 50% → RED (very low confidence)
         ├─ ELSE → YELLOW (OK but missing XML)
         │
         └─ **Result: YELLOW** (no XML, but good confidence)
```

---

## 6. DEPLOYMENT ARCHITECTURE (AWS Production)

### 6.1 AWS Multi-AZ Production Environment (2 AZs)

Hệ thống tuân thủ chuẩn Enterprise với kiến trúc Multi-AZ (2 Availability Zones) để đảm bảo High Availability và Fault Tolerance trong AWS Region (ví dụ: `ap-southeast-1`).

```
┌─────────────────────────────────────────────────────────────────────────────────────────────┐
│                                AWS CLOUD (Region: ap-southeast-1)                            │
├─────────────────────────────────────────────────────────────────────────────────────────────┤
│                                                                                             │
│  ┌───────────────────────────────────────────────────────────────────────────────────────┐  │
│  │ ROUTE 53 (DNS)                                                                        │  │
│  │ ├─ smartinvoice.example.com → Amplify (Frontend, Global CDN)                          │  │
│  │ └─ api.smartinvoice.example.com → Application Load Balancer (ALB)                     │  │
│  └───────────────────────────────────────────────────────────────────────────────────────┘  │
│                                           │                                                 │
│  ┌────────────────────────────────────────▼──────────────────────────────────────────────┐  │
│  │ VPC (Virtual Private Cloud) - CIDR: 10.0.0.0/16                                       │  │
│  │                                                                                       │  │
│  │  ┌───────────────────────────────┐           ┌───────────────────────────────┐        │  │
│  │  │ AVAILABILITY ZONE A           │           │ AVAILABILITY ZONE B           │        │  │
│  │  │ (ap-southeast-1a)             │           │ (ap-southeast-1b)             │        │  │
│  │  │                               │           │                               │        │  │
│  │  │ ┌───────────────────────────┐ │   CROSS   │ ┌───────────────────────────┐ │        │  │
│  │  │ │ PUBLIC SUBNET             │ │◄─ ZONE ─► │ │ PUBLIC SUBNET             │ │        │  │
│  │  │ │ 10.0.1.0/24               │ │  BALANCING│ │ 10.0.2.0/24               │ │        │  │
│  │  │ │ ┌─────────────────────┐   │ │           │ │ ┌─────────────────────┐   │ │        │  │
│  │  │ │ │ ALB Node A          │   │ │           │ │ │ ALB Node B          │   │ │        │  │
│  │  │ │ └──────────┬──────────┘   │ │           │ │ └──────────┬──────────┘   │ │        │  │
│  │  │ │            │              │ │           │ │            │              │ │        │  │
│  │  │ │ ┌──────────▼──────────┐   │ │           │ │ ┌──────────▼──────────┐   │ │        │  │
│  │  │ │ │ Auto Scaling Group  │   │ │           │ │ │ Auto Scaling Group  │   │ │        │  │
│  │  │ │ │ .NET Web API (EC2)  │   │ │           │ │ │ .NET Web API (EC2)  │   │ │        │  │
│  │  │ │ └──────────┬──────────┘   │ │           │ │ └──────────┬──────────┘   │ │        │  │
│  │  │ └────────────┼──────────────┘ │           │ └────────────┼──────────────┘ │        │  │
│  │  │              │                │           │              │                │        │  │
│  │  │ ┌────────────▼──────────────┐ │           │ ┌────────────▼──────────────┐ │        │  │
│  │  │ │ PRIVATE DATA SUBNET       │ │  SYNC     │ │ PRIVATE DATA SUBNET       │ │        │  │
│  │  │ │ 10.0.21.0/24              │ │◄─ REPL ─► │ │ 10.0.22.0/24              │ │        │  │
│  │  │ │ ┌─────────────────────┐   │ │           │ │ ┌─────────────────────┐   │ │        │  │
│  │  │ │ │ RDS PostgreSQL      │   │ │           │ │ │ RDS PostgreSQL      │   │ │        │  │
│  │  │ │ │ (Primary)           │   │ │           │ │ │ (Standby/Failover)  │   │ │        │  │
│  │  │ │ └─────────────────────┘   │ │           │ │ └─────────────────────┘   │ │        │  │
│  │  │ └───────────────────────────┘ │           │ └───────────────────────────┘ │        │  │
│  │  └───────────────────────────────┘           └───────────────────────────────┘        │  │
│  └───────────────────────────────────────────────────────────────────────────────────────┘  │
│                                                                                             │
│  ┌───────────────────────────────────────────────────────────────────────────────────────┐  │
│  │ GLOBAL / REGIONAL MANAGED SERVICES                                                    │  │
│  │                                                                                       │  │
│  │ ┌────────────────┐ ┌────────────────┐ ┌────────────────┐ ┌────────────────┐           │  │
│  │ │ AWS COGNITO    │ │ AMAZON S3      │ │ SYSTEMS MGR    │ │ INTERNAL OCR   │           │  │
│  │ │ (Auth & OTP)   │ │ (File Storage) │ │ (Param Store)  │ │ API (Custom)   │           │  │
│  │ └────────────────┘ └────────────────┘ └────────────────┘ └────────────────┘           │  │
│  └───────────────────────────────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────────────────────────┘

**AWS Resource Details**:
1. **Cost-Optimized Network (VPC)**: 1 VPC, 4 Subnets trên 2 AZs. Loại bỏ *NAT Gateway* (vốn rất đắt) bằng cách đặt Web API Server vào Public Subnets. Bảo mật API Server bằng strict Security Groups (chỉ cho phép ingress từ ALB port 80/443). DB vẫn nằm ở Private Subnets.
2. **Compute (Elastic Beanstalk)**: Auto Scaling Group (Min=2, Max=4) sử dụng 2 node `t3.micro` nhỏ nhẹ nhưng đủ High Availability.
3. **Database (RDS)**: Kích hoạt Multi-AZ trên instance `db.t3.micro`. Vẫn đảm bảo tính chịu lỗi nếu rớt 1 zone.
4. **Storage (S3)**: Mặc định Cross-AZ High Availability.


TOTAL MONTHLY COST ESTIMATE (Cost-Optimized Multi-AZ):
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
✓ Amplify: $0 (Free Tier)
✓ Application Load Balancer: ~$16.00/month
✓ NAT Gateways: $0 (Đã loại bỏ để tối ưu chi phí)
✓ Elastic Beanstalk (2x t3.micro EC2): ~$15.00/month
✓ RDS PostgreSQL (db.t3.micro - Multi-AZ): ~$30.00/month
✓ S3: ~$0.50 (5GB storage)
✓ Cognito: Miễn phí (Dưới 50,000 MAUs)
✓ Systems Manager (Parameter Store): $0
✓ CloudWatch: $0 (Free Tier - 5GB logs)
✓ Data transfer: ~$1.50 (estimate)

GRAND TOTAL: ~$63.00/month (Cost-Optimized Multi-AZ Setup)
```

---

### 6.2 Deployment Process (Step-by-Step)

**WEEK 10: PRODUCTION DEPLOYMENT**

```bash
# ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
# DAY 1: DATABASE DEPLOYMENT (Person 3 Lead)
# ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

# Step 1: Create RDS PostgreSQL instance (Multi-AZ Enabled)
aws rds create-db-instance \
  --db-instance-identifier smartinvoice-prod-db \
  --db-instance-class db.t3.micro \
  --engine postgres \
  --engine-version 14.7 \
  --master-username postgres \
  --master-user-password "STRONG_PASSWORD_HERE" \
  --allocated-storage 20 \
  --storage-type gp3 \
  --multi-az \
  --backup-retention-period 7 \
  --preferred-backup-window "03:00-04:00" \
  --publicly-accessible false \
  --vpc-security-group-ids sg-xxxxxx \
  --db-subnet-group-name private-data-subnets \
  --storage-encrypted \
  --enable-cloudwatch-logs-exports '["postgresql"]'

# Wait ~10 minutes for RDS instance to be available
aws rds wait db-instance-available \
  --db-instance-identifier smartinvoice-prod-db

# Step 2: Get RDS endpoint
RDS_ENDPOINT=$(aws rds describe-db-instances \
  --db-instance-identifier smartinvoice-prod-db \
  --query 'DBInstances[0].Endpoint.Address' \
  --output text)

echo "RDS Endpoint: $RDS_ENDPOINT"
# Output: smartinvoice-prod-db.xxxxxxxx.ap-southeast-1.rds.amazonaws.com

# Step 3: Connect from local machine (update Security Group first)
psql -h $RDS_ENDPOINT \
     -U postgres \
     -d postgres

# Step 4: Run migration script
\i database_migration_production.sql

# Verify tables created
\dt
# Should show 12 tables

# Step 5: Insert seed data
INSERT INTO DocumentTypes (...) VALUES (...);

# Step 6: Create database user for application
CREATE USER smartinvoice_app WITH PASSWORD 'APP_PASSWORD';
GRANT CONNECT ON DATABASE smartinvoice_prod TO smartinvoice_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO smartinvoice_app;

# ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
# DAY 2: BACKEND DEPLOYMENT (Person 3 + Person 1)
# ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

# Step 1: Install Elastic Beanstalk CLI
pip install awsebcli --upgrade

# Step 2: Initialize EB application (from backend directory)
cd SmartInvoice.API
eb init -p "64bit Amazon Linux 2 v2.5.4 running .NET Core" \
        -r ap-southeast-1 \
        smartinvoice-api

# Step 3: Create environment with ALB and Auto Scaling (Multi-AZ)
eb create smartinvoice-api-prod \
  --instance-type t3.micro \
  --scale 2 \
  --elb-type application \
  --vpc.ec2subnets subnet-public1,subnet-public2 \
  --vpc.elbsubnets subnet-public1,subnet-public2 \
  --envvars \
    ConnectionStrings__DefaultConnection="Host=$RDS_ENDPOINT;Database=smartinvoice_prod;Username=smartinvoice_app;Password=APP_PASSWORD" \
    AWS__Region=ap-southeast-1 \
    AWS__S3BucketName=smartinvoice-prod \
    OCR_API_ENDPOINT="http://internal-ocr.com/api/v1/extract" \
    COGNITO_USER_POOL_ID="ap-southeast-1_xxxxxx" \
    COGNITO_CLIENT_ID="xxxxxxx"

# Wait ~5 minutes for environment creation
eb status

# Step 4: Build and publish .NET app
dotnet publish -c Release -o ./publish

# Step 5: Deploy to Elastic Beanstalk
cd publish
zip -r ../deploy.zip .
cd ..
eb deploy

# Step 6: Verify deployment
eb open  # Opens browser to Elastic Beanstalk URL
curl https://smartinvoice-api-prod.ap-southeast-1.elasticbeanstalk.com/api/health

# Expected response:
# {
#   "status": "Healthy",
#   "database": "Connected",
#   "s3": "Accessible",
#   "timestamp": "2025-02-06T10:30:00Z"
# }

# ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
# DAY 3: S3 SETUP (Person 3)
# ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

# Step 1: Create S3 bucket
aws s3 mb s3://smartinvoice-prod --region ap-southeast-1

# Step 2: Enable versioning
aws s3api put-bucket-versioning \
  --bucket smartinvoice-prod \
  --versioning-configuration Status=Enabled

# Step 3: Enable server-side encryption
aws s3api put-bucket-encryption \
  --bucket smartinvoice-prod \
  --server-side-encryption-configuration '{
    "Rules": [{
      "ApplyServerSideEncryptionByDefault": {
        "SSEAlgorithm": "AES256"
      }
    }]
  }'

# Step 4: Configure lifecycle policy (archive old files)
aws s3api put-bucket-lifecycle-configuration \
  --bucket smartinvoice-prod \
  --lifecycle-configuration file://lifecycle-policy.json

# lifecycle-policy.json:
{
  "Rules": [
    {
      "Id": "ArchiveOldInvoices",
      "Status": "Enabled",
      "Transitions": [
        {
          "Days": 30,
          "StorageClass": "INTELLIGENT_TIERING"
        },
        {
          "Days": 90,
          "StorageClass": "GLACIER_DEEP_ARCHIVE"
        }
      ]
    }
  ]
}

# Step 5: Configure CORS (for frontend uploads)
aws s3api put-bucket-cors \
  --bucket smartinvoice-prod \
  --cors-configuration file://cors-config.json

# cors-config.json:
{
  "CORSRules": [
    {
      "AllowedOrigins": ["https://main.xxxxxxx.amplifyapp.com"],
      "AllowedMethods": ["GET", "PUT", "POST", "DELETE"],
      "AllowedHeaders": ["*"],
      "MaxAgeSeconds": 3000
    }
  ]
}

# Step 6: Update IAM role for Elastic Beanstalk to access S3
# (Beanstalk automatically creates role: aws-elasticbeanstalk-ec2-role)
# Also attach S3 read/write policy
aws iam attach-role-policy \
  --role-name aws-elasticbeanstalk-ec2-role \
  --policy-arn arn:aws:iam::aws:policy/AmazonS3FullAccess

# ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
# DAY 4: FRONTEND DEPLOYMENT (Person 4)
# ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

# Step 1: Update frontend environment variables
# Create file: .env.production
VITE_API_URL=https://smartinvoice-api-prod.ap-southeast-1.elasticbeanstalk.com/api
VITE_APP_NAME=SmartInvoice Shield

# Step 2: Build frontend
cd smartinvoice-frontend
npm run build
# Output: dist/ folder

# Step 3: Deploy to AWS Amplify (via Console)
# 3a. Go to AWS Amplify Console
# 3b. Click "Host web app"
# 3c. Connect GitHub repository
# 3d. Select branch: main
# 3e. Build settings (auto-detected):
version: 1
frontend:
  phases:
    preBuild:
      commands:
        - npm ci
    build:
      commands:
        - npm run build
  artifacts:
    baseDirectory: dist
    files:
      - '**/*'
  cache:
    paths:
      - node_modules/**/*

# 3f. Add environment variables in Amplify Console:
#     VITE_API_URL = https://smartinvoice-api-prod...
# 3g. Deploy

# Amplify URL will be: https://main.xxxxxxx.amplifyapp.com

# Step 4: Test frontend
# Visit https://main.xxxxxxx.amplifyapp.com
# Try login, upload invoice, view dashboard

# ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
# DAY 5: MONITORING & FINAL CHECKS
# ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

# Step 1: Setup CloudWatch alarms
aws cloudwatch put-metric-alarm \
  --alarm-name smartinvoice-api-high-errors \
  --alarm-description "Alert when API error rate > 5%" \
  --metric-name 5XXError \
  --namespace AWS/ElasticBeanstalk \
  --statistic Average \
  --period 300 \
  --threshold 5 \
  --comparison-operator GreaterThanThreshold \
  --evaluation-periods 2

# Step 2: Setup billing alarm
aws cloudwatch put-metric-alarm \
  --alarm-name smartinvoice-cost-exceeded \
  --alarm-description "Alert when estimated charges exceed $5" \
  --metric-name EstimatedCharges \
  --namespace AWS/Billing \
  --statistic Maximum \
  --period 21600 \
  --threshold 5 \
  --comparison-operator GreaterThanThreshold \
  --evaluation-periods 1

# Step 3: Test all critical flows
# Test 1: Upload XML invoice
curl -X POST https://smartinvoice-api-prod.../api/invoices/upload \
  -H "Authorization: Bearer $JWT_TOKEN" \
  -F "file=@invoice_001.xml"

# Test 2: Upload PDF invoice
curl -X POST https://smartinvoice-api-prod.../api/invoices/upload \
  -H "Authorization: Bearer $JWT_TOKEN" \
  -F "file=@invoice_002.pdf"

# Test 3: Dashboard stats
curl https://smartinvoice-api-prod.../api/dashboard/stats \
  -H "Authorization: Bearer $JWT_TOKEN"

# Test 4: Export Excel
curl https://smartinvoice-api-prod.../api/export/excel \
  -H "Authorization: Bearer $JWT_TOKEN" \
  -d '{"dateFrom":"2025-01-01","dateTo":"2025-01-31"}'

# Step 4: Load testing (Apache Bench)
ab -n 100 -c 10 \
  -H "Authorization: Bearer $JWT_TOKEN" \
  https://smartinvoice-api-prod.../api/invoices

# Expected: Response time < 2s (p95)

# Step 5: Security check
# - HTTPS enforced? ✓
# - CORS configured? ✓
# - IAM roles minimal permissions? ✓
# - Secrets not in code? ✓
# - Database not publicly accessible? ✓

echo "✅ DEPLOYMENT COMPLETE!"
echo "Frontend: https://main.xxxxxxx.amplifyapp.com"
echo "API: https://smartinvoice-api-prod.ap-southeast-1.elasticbeanstalk.com"
```

---

**[Tiếp tục với Phần 7-8: Security & Performance trong file tiếp theo]**

Tôi đã tạo xong phần 2 với:
- ✅ Data Flow chi tiết (Happy Path XML + Alternative OCR Path)
- ✅ AWS Deployment Architecture (Production-ready)
- ✅ Step-by-step deployment guide (Week 10)

Bạn muốn tôi tiếp tục với Security Architecture và Performance Optimization không?
