import { apiClient } from '@/lib/api-client';

export interface AuditChange {
  field: string;
  old_value: string | null;
  new_value: string | null;
  change_type?: string;
}

export interface SystemAuditLog {
  auditId: string;
  invoiceId: string;
  invoiceNumber: string | null;
  userEmail: string | null;
  userRole: string | null;
  userFullName: string | null;
  action: string;
  reason: string | null;
  comment: string | null;
  ipAddress: string | null;
  changes: AuditChange[] | null;
  createdAt: string;
}

export interface AuditLogPagedResult {
  items: SystemAuditLog[];
  totalCount: number;
  pageIndex: number;
  pageSize: number;
  totalPages: number;
}

export interface AuditLogQuery {
  page?: number;
  pageSize?: number;
  keyword?: string;
  action?: string;
  dateFrom?: string;
  dateTo?: string;
}

export const auditLogService = {
  getAuditLogs: async (query: AuditLogQuery = {}): Promise<AuditLogPagedResult> => {
    const params = new URLSearchParams();
    if (query.page) params.append('page', String(query.page));
    if (query.pageSize) params.append('pageSize', String(query.pageSize));
    if (query.keyword) params.append('keyword', query.keyword);
    if (query.action) params.append('action', query.action);
    if (query.dateFrom) params.append('dateFrom', query.dateFrom);
    if (query.dateTo) params.append('dateTo', query.dateTo);

    const { data } = await apiClient.get(`/auditlog?${params.toString()}`);
    return data;
  },
};
