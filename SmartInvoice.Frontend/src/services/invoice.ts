import { apiClient } from '../lib/api-client';

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

export interface ValidationResult {
    isValid: boolean;
    errors: string[];
    warnings: string[];
    signerSubject: string | null;
    extractedData: InvoiceExtractedData | null;
}

export interface UploadUrlResponse {
    uploadUrl: string;
    s3Key: string;
}

export const invoiceService = {
    // B1: Lấy Pre-signed URL từ Backend
    async getUploadUrl(fileName: string, contentType: string): Promise<UploadUrlResponse> {
        const response = await apiClient.post<UploadUrlResponse>('/invoices/generate-upload-url', {
            fileName,
            contentType,
        });
        return response.data;
    },

    // B2: Upload file trực tiếp lên S3 bằng Fetch (Khuyến nghị dùng native fetch để không dính Authorization Header của Axios)
    async uploadToS3(uploadUrl: string, file: File): Promise<void> {
        const response = await fetch(uploadUrl, {
            method: 'PUT',
            body: file,
            headers: {
                'Content-Type': file.type,
            },
        });

        if (!response.ok) {
            throw new Error('Failed to upload file to S3');
        }
    },

    // B3: Gọi Backend đọc S3Key và trích xuất dữ liệu
    async processXml(s3Key: string): Promise<ValidationResult> {
        const response = await apiClient.post<ValidationResult>('/invoices/process-xml', {
            s3Key,
        });
        return response.data;
    },

    // B4: Upload thẳng file cho Backend (Không qua S3) dành cho việc test nhanh
    async uploadToLocal(file: File): Promise<ValidationResult> {
        const formData = new FormData();
        formData.append('file', file);

        const response = await apiClient.post<ValidationResult>('/invoices/test-process-local', formData, {
            headers: {
                'Content-Type': 'multipart/form-data',
            }
        });
        return response.data;
    },

    // B5: Fetch list of invoices
    async getInvoices(): Promise<any[]> {
        const response = await apiClient.get<any[]>('/invoices');
        return response.data;
    }
};
