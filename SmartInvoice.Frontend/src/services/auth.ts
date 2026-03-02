import { apiClient } from '@/lib/api-client';

export interface LoginRequest {
    email: string;
    password: string;
}

export interface LoginResponse {
    accessToken: string;
    idToken: string;
    refreshToken: string;
    expiration: string;
    user?: {
        id: string;
        email: string;
        fullName: string;
        role: string;
        companyId: string;
    };
    challengeName?: string;
    session?: string;
}

export interface RespondToNewPasswordRequest {
    email: string;
    newPassword: string;
    session: string;
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

    async respondNewPassword(data: RespondToNewPasswordRequest) {
        const response = await apiClient.post<LoginResponse>('/auth/respond-new-password', data);
        return response.data;
    },

    async verifyEmail(email: string, token: string) {
        const response = await apiClient.post('/auth/verify-email', { email, token });
        return response.data;
    },

    async refreshToken(refreshToken: string, email: string) {
        const response = await apiClient.post<LoginResponse>('/auth/refresh-token', { refreshToken, email });
        return response.data;
    },

    async logout() {
        try {
            await apiClient.post('/auth/logout');
        } catch (error) {
            console.error('Backend logout failed', error);
        }
        localStorage.removeItem('token');
        localStorage.removeItem('idToken');
        localStorage.removeItem('user');
    },
};
