import React from 'react';
import { Card, Typography, Timeline, Tag, Space, Avatar, Input } from 'antd';
import {
  UploadOutlined, EditOutlined, CheckCircleOutlined,
  CloseCircleOutlined, UserOutlined, SearchOutlined,
} from '@ant-design/icons';

const { Title, Text } = Typography;

const logs = [
  { time: '08:30', date: '12/02/2026', user: 'Nguyễn Văn A', action: 'Upload', detail: 'Tải lên hóa đơn INV-2026-001284 (XML)', icon: <UploadOutlined />, color: '#1a4b8c' },
  { time: '08:28', date: '12/02/2026', user: 'Trần Thị B', action: 'Approve', detail: 'Phê duyệt hóa đơn INV-2026-001280', icon: <CheckCircleOutlined />, color: '#2d9a5c' },
  { time: '08:15', date: '12/02/2026', user: 'Nguyễn Văn A', action: 'Edit', detail: 'Chỉnh sửa hóa đơn INV-2026-001279 (sửa MST)', icon: <EditOutlined />, color: '#e6a817' },
  { time: '17:45', date: '11/02/2026', user: 'Lê Văn C', action: 'Reject', detail: 'Từ chối hóa đơn INV-2026-001281 (MST không hợp lệ)', icon: <CloseCircleOutlined />, color: '#d63031' },
  { time: '16:30', date: '11/02/2026', user: 'Trần Thị B', action: 'Upload', detail: 'Tải lên 3 hóa đơn (PDF, OCR)', icon: <UploadOutlined />, color: '#1a4b8c' },
  { time: '15:00', date: '11/02/2026', user: 'Nguyễn Văn A', action: 'Approve', detail: 'Phê duyệt hàng loạt 5 hóa đơn', icon: <CheckCircleOutlined />, color: '#2d9a5c' },
];

const actionColors: Record<string, string> = {
  Upload: 'blue', Edit: 'gold', Approve: 'green', Reject: 'red',
};

const AuditLogPage: React.FC = () => {
  return (
    <div className="animate-fade-in-up">
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 24 }}>
        <div>
          <Title level={4} style={{ margin: 0 }}>Nhật ký hoạt động</Title>
          <Text type="secondary">Audit trail - Không thể xóa hoặc chỉnh sửa</Text>
        </div>
        <Input placeholder="Tìm kiếm nhật ký..." prefix={<SearchOutlined />} style={{ width: 280, borderRadius: 8 }} />
      </div>

      <Card bordered={false} style={{ borderRadius: 12 }}>
        <Timeline
          items={logs.map((log) => ({
            color: log.color,
            dot: <div style={{
              width: 32, height: 32, borderRadius: 8,
              background: `${log.color}14`, display: 'flex',
              alignItems: 'center', justifyContent: 'center', color: log.color,
            }}>
              {log.icon}
            </div>,
            children: (
              <div style={{ paddingBottom: 8 }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 4 }}>
                  <Tag color={actionColors[log.action]} style={{ margin: 0, fontSize: 11 }}>{log.action}</Tag>
                  <Text type="secondary" style={{ fontSize: 12 }}>{log.time} - {log.date}</Text>
                </div>
                <Text style={{ fontSize: 13 }}>{log.detail}</Text>
                <div style={{ marginTop: 4, display: 'flex', alignItems: 'center', gap: 6 }}>
                  <Avatar size={18} icon={<UserOutlined />} style={{ background: '#1a4b8c' }} />
                  <Text type="secondary" style={{ fontSize: 12 }}>{log.user}</Text>
                </div>
              </div>
            ),
          }))}
        />
      </Card>
    </div>
  );
};

export default AuditLogPage;
