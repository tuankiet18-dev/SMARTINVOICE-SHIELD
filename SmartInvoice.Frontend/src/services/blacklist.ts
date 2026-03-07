import { apiClient } from '@/lib/api-client';

// --- Types ---

export interface BlacklistDto {
    blacklistId: string;
    taxCode: string;
    companyName: string | null;
    reason: string | null;
    isActive: boolean;
    addedBy: string | null;
    addedDate: string;
    removedDate: string | null;
}

export interface CreateBlacklistDto {
    taxCode: string;
    companyName?: string;
    reason?: string;
}

export interface UpdateBlacklistDto {
    companyName?: string;
    reason?: string;
    isActive?: boolean;
}

// --- Service ---

export const blacklistService = {
    async getAll() {
        const response = await apiClient.get<BlacklistDto[]>('/blacklist');
        return response.data;
    },

    async getById(id: string) {
        const response = await apiClient.get<BlacklistDto>(`/blacklist/${id}`);
        return response.data;
    },

    async getByTaxCode(taxCode: string) {
        const response = await apiClient.get<BlacklistDto>(`/blacklist/tax-code/${taxCode}`);
        return response.data;
    },

    async create(data: CreateBlacklistDto) {
        const response = await apiClient.post<BlacklistDto>('/blacklist', data);
        return response.data;
    },

    async update(id: string, data: UpdateBlacklistDto) {
        const response = await apiClient.put<BlacklistDto>(`/blacklist/${id}`, data);
        return response.data;
    },

    async remove(id: string) {
        const response = await apiClient.delete(`/blacklist/${id}`);
        return response.data;
    },
};
