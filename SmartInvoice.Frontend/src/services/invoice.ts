import { apiClient } from '../lib/api-client';

// ════════════════════════════════════════════
//  Types
// ════════════════════════════════════════════

export interface InvoiceLineItem {
    stt: number;
    product_name: string;
    unit: string;
    quantity: number;
    unit_price: number;
    total_amount: number;
    vat_rate: number;
    vat_amount: number;
}

export interface InvoiceExtractedData {
    line_items: InvoiceLineItem[];
    payment_terms: string | null;
    delivery_address: string | null;
    exchange_rate: number | null;
}

export interface ValidationErrorDetail {
    errorCode: string | null;
    errorMessage: string | null;
    suggestion: string | null;
}

export interface ValidationResult {
    isValid: boolean;
    errors: string[]; // For backward compatibility
    warnings: string[]; // For backward compatibility
    errorDetails: ValidationErrorDetail[];
    warningDetails: ValidationErrorDetail[];
    signerSubject: string | null;
    extractedData: InvoiceExtractedData | null;
    // Returned by API after saving to DB
    invoiceId?: string;
    isReplacement?: boolean;
    newVersion?: number;
}

export interface BatchSubmitResult {
    successCount: number;
    failCount: number;
    results: { invoiceId: string; success: boolean; errorMessage?: string }[];
}

export interface UploadUrlResponse {
    uploadUrl: string;
    s3Key: string;
}

// --- List DTO ---
export interface InvoiceDto {
    invoiceId: string;
    invoiceNumber: string;
    serialNumber: string | null;
    invoiceDate: string;
    createdAt: string;
    sellerName: string | null;
    sellerTaxCode: string | null;
    totalAmount: number;
    invoiceCurrency: string;
    status: string;
    riskLevel: string;
    processingMethod: string;
    uploadedByName: string;
}

export interface PagedResult<T> {
    items: T[];
    totalCount: number;
    pageIndex: number;
    pageSize: number;
    totalPages: number;
}

// --- Detail DTO ---
export interface LineItemDto {
    lineNumber: number;
    itemName: string | null;
    unit: string | null;
    quantity: number;
    unitPrice: number;
    totalAmount: number;
    vatRate: number;
    vatAmount: number;
}

export interface ValidationLayerDto {
    layerName: string;
    layerOrder: number;
    isValid: boolean;
    validationStatus: string;
    errorCode: string | null;
    errorMessage: string | null;
    errorDetails: string | null; // This is a JSON string of ValidationErrorDetail[]
    layerData: string | null;
    checkedAt: string;
}

export interface RiskCheckDto {
    checkType: string;
    checkStatus: string;
    riskLevel: string;
    errorMessage: string | null;
    suggestion: string | null;
    checkDetails: string | null;
    checkedAt: string;
}

export interface AuditLogDto {
    auditId: string;
    userEmail: string | null;
    userRole: string | null;
    userFullName: string | null;
    ipAddress: string | null;
    action: string;
    createdAt: string;
    changes: { field: string; old_value: unknown; new_value: unknown; change_type: string }[] | null;
    reason: string | null;
    comment: string | null;
}

export interface RiskReason {
    layer: string | null;
    code: string | null;
    severity: string | null;
    message: string | null;
    auto_detected: boolean;
    checked_at: string | null;
}

export interface InvoiceDetailDto {
    invoiceId: string;
    invoiceNumber: string;
    serialNumber: string | null;
    formNumber: string | null;
    invoiceDate: string;
    status: string;
    riskLevel: string;
    processingMethod: string;
    invoiceCurrency: string;
    exchangeRate: number;
    mccqt: string | null;

    // Invoice Dossier
    hasOriginalFile: boolean;
    hasVisualFile: boolean;

    sellerName: string | null;
    sellerTaxCode: string | null;
    sellerAddress: string | null;
    sellerBankAccount: string | null;
    sellerBankName: string | null;

    buyerName: string | null;
    buyerTaxCode: string | null;
    buyerAddress: string | null;

    totalAmountBeforeTax: number | null;
    totalTaxAmount: number | null;
    totalAmount: number;
    totalAmountInWords: string | null;

    paymentMethod: string | null;
    notes: string | null;

    uploadedByName: string | null;
    createdAt: string;
    submittedByName: string | null;
    submittedAt: string | null;
    approvedByName: string | null;
    approvedAt: string | null;
    rejectedByName: string | null;
    rejectedAt: string | null;
    rejectionReason: string | null;

    riskReasons: RiskReason[] | null;

    lineItems: LineItemDto[];
    validationLayers: ValidationLayerDto[];
    riskChecks: RiskCheckDto[];
    auditLogs: AuditLogDto[];
}

export interface InvoiceStatsDto {
  totalAmount: number;
  totalTaxAmount: number;
  validCount: number;
  needReviewCount: number;
  totalCount: number;
}

// ════════════════════════════════════════════
//  Service
// ════════════════════════════════════════════

export const invoiceService = {
    // --- Upload ---
    async getUploadUrl(fileName: string, contentType: string): Promise<UploadUrlResponse> {
        const response = await apiClient.post<UploadUrlResponse>('/invoices/generate-upload-url', {
            fileName,
            contentType,
        });
        return response.data;
    },

    async uploadToS3(uploadUrl: string, file: File): Promise<void> {
        const response = await fetch(uploadUrl, {
            method: 'PUT',
            body: file,
            headers: { 'Content-Type': file.type },
        });
        if (!response.ok) throw new Error('Failed to upload file to S3');
    },

    async processXml(s3Key: string): Promise<ValidationResult> {
        const response = await apiClient.post<ValidationResult>('/invoices/process-xml', { s3Key });
        return response.data;
    },

    async uploadToLocal(file: File): Promise<ValidationResult> {
        const formData = new FormData();
        formData.append('file', file);
        const response = await apiClient.post<ValidationResult>('/invoices/test-process-local', formData, {
            headers: { 'Content-Type': 'multipart/form-data' },
        });
        return response.data;
    },

    async getInvoiceStats(startDate: string, endDate: string, status?: string): Promise<InvoiceStatsDto> {
        const res = await apiClient.get('/invoices/stats', {
            params: { startDate, endDate, status }
        });
        return res.data;
    },

    // --- List ---
    async getInvoices(
        page: number = 1,
        size: number = 10,
        keyword?: string,
        status?: string,
        riskLevel?: string,
        fromDate?: string,
        toDate?: string
    ): Promise<PagedResult<InvoiceDto>> {
        const params = new URLSearchParams();
        params.set('page', String(page));
        params.set('size', String(size));
        if (keyword) params.set('keyword', keyword);
        if (status) params.set('status', status);
        if (riskLevel) params.set('riskLevel', riskLevel);
        if (fromDate) params.set('fromDate', fromDate);
        if (toDate) params.set('toDate', toDate);

        const response = await apiClient.get<PagedResult<InvoiceDto>>(`/invoices?${params.toString()}`);
        return response.data;
    },

    // --- Detail ---
    async getInvoiceDetail(id: string): Promise<InvoiceDetailDto> {
        const response = await apiClient.get<InvoiceDetailDto>(`/invoices/${id}`);
        return response.data;
    },

    // --- Workflow ---
    async submitInvoice(id: string, comment?: string): Promise<void> {
        await apiClient.post(`/invoices/${id}/submit`, { comment });
    },

    async submitBatch(invoiceIds: string[], comment?: string): Promise<BatchSubmitResult> {
        const response = await apiClient.post<BatchSubmitResult>('/invoices/submit-batch', { invoiceIds: invoiceIds.map(id => id), comment });
        return response.data;
    },

    async approveInvoice(id: string, comment?: string): Promise<void> {
        await apiClient.post(`/invoices/${id}/approve`, { comment });
    },

    async rejectInvoice(id: string, reason: string, comment?: string): Promise<void> {
        await apiClient.post(`/invoices/${id}/reject`, { reason, comment });
    },

    // --- Delete ---
    async deleteInvoice(id: string): Promise<void> {
        await apiClient.delete(`/invoices/${id}`);
    },

    // --- Audit Logs ---
    async getAuditLogs(id: string): Promise<AuditLogDto[]> {
        const response = await apiClient.get<AuditLogDto[]>(`/invoices/${id}/audit-logs`);
        return response.data;
    },
};
