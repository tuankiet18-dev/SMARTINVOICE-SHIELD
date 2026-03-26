import { apiClient } from '../lib/api-client';

export interface CompanySettings {
  companyId: string;
  companyName: string;
  taxCode: string;
  address?: string;
  phoneNumber?: string;
  isAutoApproveEnabled: boolean;
  autoApproveThreshold: number;
  requireTwoStepApproval: boolean;
  twoStepApprovalThreshold: number;
  hasAdvancedWorkflow: boolean;
}

export interface UpdateCompanySettings {
  isAutoApproveEnabled: boolean;
  autoApproveThreshold: number;
  requireTwoStepApproval: boolean;
  twoStepApprovalThreshold: number;
}

export interface UserProfileSettings {
  userId: string;
  fullName: string;
  email: string;
  employeeId?: string;
  receiveEmailNotifications: boolean;
  receiveInAppNotifications: boolean;
}

export interface UpdateUserProfileSettings {
  fullName: string;
  employeeId?: string;
  receiveEmailNotifications: boolean;
  receiveInAppNotifications: boolean;
}

export const settingsService = {
  getCompanySettings: async (): Promise<CompanySettings> => {
    const response = await apiClient.get('/settings/company');
    return response.data;
  },

  updateCompanySettings: async (data: UpdateCompanySettings): Promise<void> => {
    const response = await apiClient.put('/settings/company', data);
    return response.data;
  },

  getUserProfile: async (): Promise<UserProfileSettings> => {
    const response = await apiClient.get('/settings/profile');
    return response.data;
  },

  updateUserProfile: async (data: UpdateUserProfileSettings): Promise<void> => {
    const response = await apiClient.put('/settings/profile', data);
    return response.data;
  }
};
