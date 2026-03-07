import { apiClient } from '@/lib/api-client';

/* ── Types ─────────────────────────────────────────────── */

export interface InvoiceValidationSummary {
  invoiceId: string;
  invoiceNumber: string;
  sellerName: string | null;
  sellerTaxCode: string | null;
  riskLevel: string | null;
  issueCount: number;
  validatedAt: string | null;
  layer1Status: string | null;
  layer2Status: string | null;
  layer3Status: string | null;
  overallStatus: string;
}

export interface ValidationOverview {
  totalValidated: number;
  passCount: number;
  warningCount: number;
  failCount: number;
  passRate: number;
  layer1PassCount: number;
  layer2PassCount: number;
  layer3PassCount: number;
  greenCount: number;
  yellowCount: number;
  orangeCount: number;
  redCount: number;
  items: InvoiceValidationSummary[];
  totalCount: number;
  pageIndex: number;
  pageSize: number;
  totalPages: number;
}

export interface ValidationOverviewQuery {
  page?: number;
  pageSize?: number;
  keyword?: string;
  riskLevel?: string;
  validationStatus?: string;
}

/* ── API ───────────────────────────────────────────────── */

export const validationService = {
  getOverview: async (query: ValidationOverviewQuery = {}): Promise<ValidationOverview> => {
    const params = new URLSearchParams();
    if (query.page) params.append('page', String(query.page));
    if (query.pageSize) params.append('pageSize', String(query.pageSize));
    if (query.keyword) params.append('keyword', query.keyword);
    if (query.riskLevel) params.append('riskLevel', query.riskLevel);
    if (query.validationStatus) params.append('validationStatus', query.validationStatus);
    const { data } = await apiClient.get<ValidationOverview>(`/validation/overview?${params}`);
    return data;
  },
};
