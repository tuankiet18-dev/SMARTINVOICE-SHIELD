import { apiClient } from '@/lib/api-client';

export interface LoginRequest {
    email: string;
    password: string;
}

export interface LoginResponse {
    accessToken: string;
    refreshToken: string;
    expiration: string;
    user: {
        userId: string;
        email: string;
        fullName: string;
        role: string;
        companyId: string;
    };
}

export interface RegisterCompanyRequest {
    // Company Info
    companyName: string;
    taxCode: string;
    address: string;
    companyEmail?: string;
    phoneNumber?: string;
    businessType?: string;
    legalRepresentative?: string;
    // Admin Info
    adminFullName: string;
    adminEmail: string;
    password: string;
}

export const authService = {
    async checkTaxCode(taxCode: string) {
        const response = await apiClient.post('/auth/check-tax-code', { taxCode });
        return response.data;
    },

    async register(data: RegisterCompanyRequest) {
        const response = await apiClient.post('/auth/register-company', data);
        return response.data;
    },

    async login(data: LoginRequest) {
        const response = await apiClient.post<LoginResponse>('/auth/login', data);
        return response.data;
    },

    async verifyEmail(email: string, token: string) {
        const response = await apiClient.post('/auth/verify-email', { email, token });
        return response.data;
    },

    logout() {
        localStorage.removeItem('token');
        localStorage.removeItem('user');
    },
};
