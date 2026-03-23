import { apiClient } from '../lib/api-client';

export interface NotificationDto {
  notificationId: string;
  type: string;
  title: string;
  message: string;
  actionUrl?: string;
  actionText?: string;
  relatedInvoiceId?: string;
  priority: string;
  isRead: boolean;
  createdAt: string;
  readAt?: string;
}

export interface PaginatedResult<T> {
  items: T[];
  pageIndex: number;
  pageSize: number;
}

export const notificationService = {
  getNotifications: async (unreadOnly: boolean = false, pageIndex: number = 1, pageSize: number = 20): Promise<PaginatedResult<NotificationDto>> => {
    const response = await apiClient.get('/notifications', {
      params: { unreadOnly, pageIndex, pageSize },
    });
    return response.data;
  },

  getUnreadCount: async (): Promise<number> => {
    const response = await apiClient.get('/notifications/unread-count');
    return response.data.count;
  },

  markAsRead: async (id: string): Promise<void> => {
    await apiClient.put(`/notifications/${id}/read`);
  },

  markAllAsRead: async (): Promise<void> => {
    await apiClient.put('/notifications/read-all');
  },
};
