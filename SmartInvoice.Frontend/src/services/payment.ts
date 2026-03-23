import { apiClient } from '@/lib/api-client';

// ── Types ──

export interface SubscriptionPackageDto {
  packageId: string;
  packageCode: string;
  packageName: string;
  description: string | null;
  packageLevel: number;
  pricePerMonth: number;
  pricePerSixMonths: number;
  pricePerYear: number;
  maxUsers: number;
  maxInvoicesPerMonth: number;
  storageQuotaGB: number;
  hasAiProcessing: boolean;
  hasAdvancedWorkflow: boolean;
  hasRiskWarning: boolean;
  hasAuditLog: boolean;
  hasErpIntegration: boolean;
  isActive: boolean;
}

export interface CurrentSubscriptionDto {
  packageCode: string | null;
  packageName: string | null;
  packageLevel: number;
  subscriptionTier: string;
  billingCycle: string;
  subscriptionStartDate: string | null;
  subscriptionExpiredAt: string | null;
  maxUsers: number;
  maxInvoicesPerMonth: number;
  storageQuotaGB: number;
  hasAiProcessing: boolean;
  hasAdvancedWorkflow: boolean;
  hasRiskWarning: boolean;
  hasAuditLog: boolean;
  hasErpIntegration: boolean;
  usedInvoicesThisMonth: number;
  extraInvoicesBalance: number;
}

export interface CreatePaymentRequest {
  packageId: string;
  billingCycle: 'Monthly' | 'SemiAnnual' | 'Annual';
}

export interface CreatePaymentResponse {
  paymentUrl: string;
  transactionId: string;
}

export interface PaymentResultDto {
  transactionId: string;
  status: string;
  packageName: string | null;
  billingCycle: string | null;
  amount: number;
  vnpTransactionNo: string | null;
  bankCode: string | null;
  payDate: string | null;
  message: string | null;
}

export interface PaymentHistoryDto {
  transactionId: string;
  packageName: string | null;
  billingCycle: string;
  amount: number;
  status: string;
  vnpTransactionNo: string | null;
  paymentType: string;
  createdAt: string;
}

export interface AddonInfoDto {
  addonCode: string;
  addonName: string;
  description: string;
  invoiceCount: number;
  price: number;
}

export interface CreateAddonPaymentRequest {
  addonCode: string;
}

// ── API Calls ──

export const paymentService = {
  getPackages: async (): Promise<SubscriptionPackageDto[]> => {
    const res = await apiClient.get('/payment/packages');
    return res.data;
  },

  getCurrentSubscription: async (): Promise<CurrentSubscriptionDto> => {
    const res = await apiClient.get('/payment/current-subscription');
    return res.data;
  },

  createPayment: async (data: CreatePaymentRequest): Promise<CreatePaymentResponse> => {
    const res = await apiClient.post('/payment/create', data);
    return res.data;
  },

  getVnPayResult: async (queryString: string): Promise<PaymentResultDto> => {
    const res = await apiClient.get(`/payment/vnpay-return?${queryString}`);
    return res.data;
  },

  getPaymentHistory: async (): Promise<PaymentHistoryDto[]> => {
    const res = await apiClient.get('/payment/history');
    return res.data;
  },

  getAddons: async (): Promise<AddonInfoDto[]> => {
    const res = await apiClient.get('/payment/addons');
    return res.data;
  },

  createAddonPayment: async (data: CreateAddonPaymentRequest): Promise<CreatePaymentResponse> => {
    const res = await apiClient.post('/payment/addon/create', data);
    return res.data;
  },
};
