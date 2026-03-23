import React from 'react';
import { Badge, Button, Dropdown, List, Typography, Space, Spin } from 'antd';
import { BellOutlined, CheckCircleOutlined, InfoCircleOutlined, WarningOutlined } from '@ant-design/icons';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { notificationService, NotificationDto } from '@/services/notificationService';
import { useNavigate } from 'react-router-dom';

const { Text } = Typography;

const NotificationBell: React.FC = () => {
  const queryClient = useQueryClient();
  const navigate = useNavigate();

  const { data: unreadCount = 0 } = useQuery({
    queryKey: ['notifications-unread-count'],
    queryFn: notificationService.getUnreadCount,
    refetchInterval: 30000, // Poll every 30 seconds
  });

  const { data: notificationsData, isLoading, refetch } = useQuery({
    queryKey: ['notifications-list'],
    queryFn: () => notificationService.getNotifications(false, 1, 10),
  });

  const markAsReadMutation = useMutation({
    mutationFn: notificationService.markAsRead,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['notifications-unread-count'] });
      queryClient.invalidateQueries({ queryKey: ['notifications-list'] });
    },
  });

  const handleOpenChange = (open: boolean) => {
    if (open) {
      // Khi chuông thả xuống (open = true), ép React Query tải lại data mới nhất
      refetch();
    }
  };

  const markAllAsReadMutation = useMutation({
    mutationFn: notificationService.markAllAsRead,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['notifications-unread-count'] });
      queryClient.invalidateQueries({ queryKey: ['notifications-list'] });
    },
  });

  const handleNotificationClick = (notification: NotificationDto) => {
    if (!notification.isRead) {
      markAsReadMutation.mutate(notification.notificationId);
    }
    
    if (notification.actionUrl) {
      navigate(notification.actionUrl);
    } else if (notification.relatedInvoiceId) {
      navigate(`/app/invoices/${notification.relatedInvoiceId}`);
    }
  };

  const notifications = notificationsData?.items || [];

  const getIcon = (type: string, priority: string) => {
    if (priority === 'High') return <WarningOutlined style={{ color: '#ef4444' }} />;
    if (type === 'Approval') return <CheckCircleOutlined style={{ color: '#10b981' }} />;
    return <InfoCircleOutlined style={{ color: '#3b82f6' }} />;
  };

  const dropdownRender = () => (
    <div style={{ width: 350, background: '#fff', borderRadius: 8, boxShadow: '0 4px 12px rgba(0,0,0,0.1)' }}>
      <div style={{ padding: '12px 16px', borderBottom: '1px solid #f0f0f0', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Text strong>Thông báo</Text>
        {unreadCount > 0 && (
          <Button type="link" size="small" onClick={() => markAllAsReadMutation.mutate()} loading={markAllAsReadMutation.isPending}>
            Đánh dấu tất cả đã đọc
          </Button>
        )}
      </div>
      <div style={{ maxHeight: 400, overflowY: 'auto' }}>
        {isLoading ? (
          <div style={{ textAlign: 'center', padding: '24px 0' }}>
            <Spin size="small" />
          </div>
        ) : notifications.length === 0 ? (
          <div style={{ textAlign: 'center', padding: '24px 0', color: '#9ca3af' }}>
            Không có thông báo nào.
          </div>
        ) : (
          <List
            dataSource={notifications}
            renderItem={(item) => (
              <List.Item 
                style={{ 
                  padding: '12px 16px', 
                  cursor: 'pointer', 
                  background: item.isRead ? '#fff' : '#f0fdf4',
                  borderBottom: '1px solid #f0f0f0',
                  transition: 'background 0.2s'
                }}
                className="hover:bg-gray-50"
                onClick={() => handleNotificationClick(item)}
              >
                <div style={{ display: 'flex', alignItems: 'flex-start', width: '100%', gap: 12 }}>
                  <div style={{ marginTop: 2 }}>{getIcon(item.type, item.priority)}</div>
                  <div style={{ flex: 1 }}>
                    <Text strong style={{ display: 'block', fontSize: 13, marginBottom: 2 }}>
                      {item.title}
                    </Text>
                    <Text style={{ display: 'block', fontSize: 12, color: '#4b5563', lineHeight: 1.4 }}>
                      {item.message}
                    </Text>
                    <div style={{ marginTop: 4 }}>
                      <Text type="secondary" style={{ fontSize: 10 }}>
                        {new Date(item.createdAt).toLocaleString('vi-VN')}
                      </Text>
                    </div>
                  </div>
                  {!item.isRead && (
                    <div style={{ width: 8, height: 8, borderRadius: '50%', background: '#3b82f6', marginTop: 6 }} />
                  )}
                </div>
              </List.Item>
            )}
          />
        )}
      </div>
    </div>
  );

  return (
    <Dropdown dropdownRender={dropdownRender} trigger={['click']} placement="bottomRight" onOpenChange={handleOpenChange}>
      <Badge count={unreadCount} size="small" offset={[-2, 2]}>
        <Button
          type="text"
          shape="circle"
          icon={<BellOutlined style={{ fontSize: 18 }} />}
          style={{ width: 36, height: 36, display: 'flex', alignItems: 'center', justifyContent: 'center' }}
        />
      </Badge>
    </Dropdown>
  );
};

export default NotificationBell;
