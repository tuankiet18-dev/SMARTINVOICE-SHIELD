import { apiClient } from '../lib/api-client';

export interface SystemConfig {
  configId: number;
  configKey: string;
  configValue: string;
  configType: string;
  category?: string;
  description?: string;
  defaultValue?: string;
  isReadOnly: boolean;
  requiresRestart: boolean;
  updatedAt: string;
}

export interface UpdateSystemConfig {
  configValue: string;
}

export const systemConfigService = {
  getAll: async () => {
    const response = await apiClient.get<SystemConfig[]>('/system-config');
    return response.data;
  },

  update: async (configKey: string, data: UpdateSystemConfig) => {
    const response = await apiClient.put<{ message: string; requiresRestart: boolean }>(`/system-config/${configKey}`, data);
    return response.data;
  },
};
