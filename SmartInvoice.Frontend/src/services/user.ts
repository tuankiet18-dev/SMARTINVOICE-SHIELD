import { apiClient } from '@/lib/api-client';

export interface CompanyMemberDto {
    id: string;
    email: string;
    fullName: string;
    employeeId: string | null;
    companyId: string;
    role: string;
    permissions: string[] | null;
    isActive: boolean;
    createdAt: string;
    lastLoginAt: string | null;
}

export interface UserProfileDto {
    id: string;
    email: string;
    fullName: string;
    employeeId: string | null;
    companyId: string;
    role: string;
    permissions: string[] | null;
    isActive: boolean;
}

export interface UpdateUserRequest {
    fullName: string;
    employeeId?: string;
}

export interface CreateCompanyMemberDto {
    email: string;
    fullName: string;
    employeeId: string;
    role: string;
    permissions?: string[];
}

export interface UpdateCompanyMemberDto {
    fullName: string;
    employeeId?: string;
    role: string;
    permissions?: string[];
    isActive: boolean;
}

export const userService = {
    async getMe() {
        const response = await apiClient.get<UserProfileDto>('/users/me');
        return response.data;
    },

    async updateMe(id: string, data: UpdateUserRequest) {
        const response = await apiClient.put<UserProfileDto>(`/users/${id}`, data);
        return response.data;
    },

    async getCompanyMembers() {
        const response = await apiClient.get<CompanyMemberDto[]>('/users/company-member');
        return response.data;
    },

    async createCompanyMember(data: CreateCompanyMemberDto) {
        const response = await apiClient.post<CompanyMemberDto>('/users/company-member', data);
        return response.data;
    },

    async updateCompanyMember(id: string, data: UpdateCompanyMemberDto) {
        const response = await apiClient.put(`/users/company-member/${id}`, data);
        return response.data;
    },

    async deleteCompanyMember(id: string) {
        const response = await apiClient.delete(`/users/company-member/${id}`);
        return response.data;
    }
};
