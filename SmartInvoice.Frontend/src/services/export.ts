import { apiClient } from '@/lib/api-client';

// ════════════════════════════════════════════
//  Types
// ════════════════════════════════════════════

export interface ExportConfigDto {
    configId: string;
    companyId: string;
    defaultDebitAccount: string | null;
    defaultCreditAccount: string | null;
    defaultTaxAccount: string | null;
    defaultWarehouse: string | null;
}

export interface UpdateExportConfigDto {
    defaultDebitAccount: string | null;
    defaultCreditAccount: string | null;
    defaultTaxAccount: string | null;
    defaultWarehouse: string | null;
}

export interface GenerateExportRequest {
    startDate: string; // ISO date
    endDate: string;
    invoiceStatus: string | null;
    exportType: string; // MISA | STANDARD
}

export interface ExportResultDto {
    exportId: string;
    fileName: string;
    status: string;
    totalRecords: number;
    downloadUrl: string | null;
    expiresAt: string | null;
}

export interface ExportHistoryDto {
    exportId: string;
    fileName: string;
    fileType: string;
    totalRecords: number;
    status: string;
    downloadUrl: string | null;
}

// ════════════════════════════════════════════
//  Service
// ════════════════════════════════════════════

export const exportService = {
    // --- Export Config ---
    async getExportConfig(): Promise<ExportConfigDto> {
        const response = await apiClient.get<ExportConfigDto>('/export-config');
        return response.data;
    },

    async updateExportConfig(data: UpdateExportConfigDto): Promise<ExportConfigDto> {
        const response = await apiClient.put<ExportConfigDto>('/export-config', data);
        return response.data;
    },

    // --- Generate Export ---
    async generateExport(data: GenerateExportRequest): Promise<ExportResultDto> {
        const response = await apiClient.post<ExportResultDto>('/exports/generate', data);
        return response.data;
    },

    async getHistory(): Promise<ExportHistoryDto[]> {
        const response = await apiClient.get<ExportHistoryDto[]>('/exports/history');
        return response.data;
    },
};
