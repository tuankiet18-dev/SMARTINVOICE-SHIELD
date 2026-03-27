import { apiClient } from '@/lib/api-client';

// ── Types ──
export interface RiskDistributionItem {
  label: string;
  percent: number;
  color: string;
}

export interface StatusDistributionItem {
  name: string;
  value: number;
  color: string;
}

export interface MonthlyTrendItem {
  month: string;
  total: number;
  approved: number;
  rejected: number;
  pending: number;
  totalAmount: number;
  totalTaxAmount: number;
}

export interface RiskTrendItem {
  month: string;
  green: number;
  yellow: number;
  orange: number;
  red: number;
}

export interface RecentInvoiceItem {
  invoiceId: string;
  invoiceNo: string;
  seller: string;
  amount: string;
  date: string;
  status: string;
  risk: string;
}

export type DashboardPeriod = '7d' | '30d' | '90d' | '6m' | '1y' | 'all';

export interface DashboardStats {
  period: string;
  totalInvoices: number;
  greenInvoices: number;
  yellowOrangeInvoices: number;
  redInvoices: number;

  totalChange: number;
  greenChange: number;
  yellowOrangeChange: number;
  redChange: number;

  riskDistribution: RiskDistributionItem[];
  statusDistribution: StatusDistributionItem[];
  monthlyTrends: MonthlyTrendItem[];
  riskTrends: RiskTrendItem[];
  recentInvoices: RecentInvoiceItem[];

  totalAmount: number;
  approvedAmount: number;
  pendingAmount: number;
}

export type ChartPeriod = '3m' | '6m' | '12m';

// ── Service ──
export const dashboardService = {
  getStats: async (period: DashboardPeriod = '30d', chartPeriod: ChartPeriod = '6m'): Promise<DashboardStats> => {
    const { data } = await apiClient.get('/dashboard/stats', { params: { period, chartPeriod } });
    return data;
  },
};
