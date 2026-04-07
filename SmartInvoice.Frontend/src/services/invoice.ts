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
    suggestion: string | null;
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

export interface InvoiceVersionDto {
    invoiceId: string;
    version: number;
    status: string;
    riskLevel: string;
    createdAt: string;
}

export interface InvoiceDetailDto {
    invoiceId: string;
    invoiceNumber: string;
    serialNumber: string | null;
    formNumber: string | null;

    version?: number;
    isReplaced?: boolean;
    replacedBy?: string;
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
    extractedData: InvoiceExtractedData | null;
}

export interface InvoiceStatsDto {
  totalAmount: number;
  totalTaxAmount: number;
  validCount: number;
  needReviewCount: number;
  totalCount: number;
  approvedCount: number;
}

export interface UpdateInvoiceDto {
    invoiceNumber?: string;
    serialNumber?: string;
    formNumber?: string;
    invoiceDate: string;
    totalAmount: number;
    totalAmountBeforeTax?: number | null;
    totalTaxAmount?: number | null;
    status?: string;
    notes?: string;

    // Seller
    sellerName?: string | null;
    sellerTaxCode?: string | null;
    sellerAddress?: string | null;

    // Buyer
    buyerName?: string | null;
    buyerTaxCode?: string | null;
    buyerAddress?: string | null;

    // Items
    lineItems?: LineItemDto[];
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

    // --- Async OCR Upload (Event-Driven Pipeline) ---

    /** Upload image → S3 → SQS → OcrWorkerService (async). Returns 202 immediately. */
    async uploadImage(file: File): Promise<{ invoiceId: string; s3Key: string; status: string; message: string }> {
        const formData = new FormData();
        formData.append('file', file);
        const response = await apiClient.post('/invoices/upload-image', formData, {
            headers: { 'Content-Type': 'multipart/form-data' },
        });
        return response.data;
    },

    /** Poll invoice status until it leaves "Processing". */
    async pollInvoiceUntilDone(
        invoiceId: string,
        onStatusChange?: (status: string) => void,
        maxAttempts: number = 180,
        intervalMs: number = 5000
    ): Promise<InvoiceDetailDto> {
        let consecutiveNotFound = 0;
        for (let attempt = 0; attempt < maxAttempts; attempt++) {
            await new Promise(resolve => setTimeout(resolve, intervalMs));
            try {
                const detail = await this.getInvoiceDetail(invoiceId);
                consecutiveNotFound = 0; // reset on success
                onStatusChange?.(detail.status);

                // Detect merge: backend redirects and returns a DIFFERENT invoiceId
                // (the target XML invoice) instead of the original draft invoiceId.
                if (detail.invoiceId && detail.invoiceId.toLowerCase() !== invoiceId.toLowerCase()) {
                    // Return with a synthetic 'Merged' flag for the UI to detect
                    return { ...detail, status: 'Merged' } as InvoiceDetailDto;
                }

                if (detail.status !== 'Processing') {
                    return detail;
                }
            } catch (err: any) {
                if (err?.response?.status === 404) {
                    consecutiveNotFound++;
                    if (consecutiveNotFound >= 2) {
                        // Invoice was hard-deleted (fatal error like LogicOwner or Duplicate).
                        // This is NOT a merge — merged invoices now use soft-delete + redirect.
                        throw Object.assign(new Error('INVOICE_HARD_DELETED'), { 
                            isHardDeleted: true,
                            response: { status: 410 } // 410 Gone = permanently deleted
                        });
                    }
                    // Otherwise retry — might be a race condition
                }
                // Otherwise retry on transient network errors
            }
        }
        throw new Error('OCR processing timed out after ' + (maxAttempts * intervalMs / 1000) + 's');
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

    async getVisualFileUrl(id: string): Promise<{ url: string }> {
        const response = await apiClient.get<{ url: string }>(`/invoices/${id}/visual`);
        return response.data;
    },

    async updateInvoice(id: string, data: UpdateInvoiceDto): Promise<void> {
        await apiClient.put(`/invoices/${id}`, data);
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

    // --- Delete / Trash ---
    async deleteInvoice(id: string): Promise<void> {
        await apiClient.delete(`/invoices/${id}`);
    },

    async getTrashInvoices(
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

        const response = await apiClient.get<PagedResult<InvoiceDto>>(`/invoices/trash?${params.toString()}`);
        return response.data;
    },

    async restoreInvoice(id: string): Promise<void> {
        await apiClient.post(`/invoices/${id}/restore`);
    },

    async hardDeleteInvoice(id: string): Promise<void> {
        await apiClient.delete(`/invoices/${id}/hard`);
    },

    async emptyTrash(): Promise<{ message: string; deletedCount: number }> {
        const response = await apiClient.delete<{ message: string; deletedCount: number }>('/invoices/trash/empty');
        return response.data;
    },

    // --- Audit Logs ---
    async getAuditLogs(id: string): Promise<AuditLogDto[]> {
        const response = await apiClient.get<AuditLogDto[]>(`/invoices/${id}/audit-logs`);
        return response.data;
    },

    // --- Versions ---
    async getInvoiceVersions(id: string): Promise<InvoiceVersionDto[]> {
        const response = await apiClient.get<InvoiceVersionDto[]>(`/invoices/${id}/versions`);
        return response.data;
    },
};
