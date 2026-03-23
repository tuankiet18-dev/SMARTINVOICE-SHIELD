import { apiClient } from '../lib/api-client';

export interface SubscriptionPackage {
  packageId: string;
  packageCode: string;
  packageName: string;
  description?: string;
  pricePerMonth: number;
  pricePerSixMonths: number;
  pricePerYear: number;
  maxUsers: number;
  maxInvoicesPerMonth: number;
  storageQuotaGB: number;
  packageLevel: number;
  hasAiProcessing: boolean;
  hasAdvancedWorkflow: boolean;
  hasRiskWarning: boolean;
  hasAuditLog: boolean;
  hasErpIntegration: boolean;
  isActive: boolean;
}

export interface CreateSubscriptionPackage {
  packageCode: string;
  packageName: string;
  description?: string;
  pricePerMonth: number;
  pricePerSixMonths: number;
  pricePerYear: number;
  maxUsers: number;
  maxInvoicesPerMonth: number;
  storageQuotaGB: number;
  packageLevel: number;
  hasAiProcessing: boolean;
  hasAdvancedWorkflow: boolean;
  hasRiskWarning: boolean;
  hasAuditLog: boolean;
  hasErpIntegration: boolean;
}

export interface UpdateSubscriptionPackage extends CreateSubscriptionPackage {
  isActive: boolean;
}

export const subscriptionPackageService = {
  getAll: async (activeOnly: boolean = false) => {
    const response = await apiClient.get<SubscriptionPackage[]>(`/subscription-packages?activeOnly=${activeOnly}`);
    return response.data;
  },

  getById: async (id: string) => {
    const response = await apiClient.get<SubscriptionPackage>(`/subscription-packages/${id}`);
    return response.data;
  },

  create: async (data: CreateSubscriptionPackage) => {
    const response = await apiClient.post<{ message: string; id: string }>('/subscription-packages', data);
    return response.data;
  },

  update: async (id: string, data: UpdateSubscriptionPackage) => {
    const response = await apiClient.put<{ message: string }>(`/subscription-packages/${id}`, data);
    return response.data;
  },

  delete: async (id: string) => {
    const response = await apiClient.delete<{ message: string }>(`/subscription-packages/${id}`);
    return response.data;
  },
};
