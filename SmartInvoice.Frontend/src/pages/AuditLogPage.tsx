import React, { useState } from 'react';
import { Card, Typography, Timeline, Tag, Avatar, Input, Select, DatePicker, Row, Col, Pagination, Spin, Empty, Tooltip, Button } from 'antd';
import {
  UploadOutlined, EditOutlined, CheckCircleOutlined, SendOutlined,
  CloseCircleOutlined, UserOutlined, SearchOutlined, FilterOutlined, ClearOutlined,
} from '@ant-design/icons';
import { useQuery } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import dayjs from 'dayjs';
import { auditLogService, type SystemAuditLog } from '../services/audit-log';

const { Title, Text } = Typography;
const { RangePicker } = DatePicker;

const actionConfig: Record<string, { color: string; tagColor: string; icon: React.ReactNode; label: string }> = {
  UPLOAD:  { color: '#1a4b8c', tagColor: 'blue',   icon: <UploadOutlined />,       label: 'Tải lên' },
  EDIT:    { color: '#e6a817', tagColor: 'gold',   icon: <EditOutlined />,         label: 'Chỉnh sửa' },
  SUBMIT:  { color: '#7c3aed', tagColor: 'purple', icon: <SendOutlined />,         label: 'Gửi duyệt' },
  APPROVE: { color: '#2d9a5c', tagColor: 'green',  icon: <CheckCircleOutlined />,  label: 'Phê duyệt' },
  REJECT:  { color: '#d63031', tagColor: 'red',    icon: <CloseCircleOutlined />,  label: 'Từ chối' },
};

const getActionInfo = (action: string) => actionConfig[action] || { color: '#94a3b8', tagColor: 'default', icon: <EditOutlined />, label: action };

const AuditLogPage: React.FC = () => {
  const navigate = useNavigate();
  const [showFilters, setShowFilters] = useState(false);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [searchText, setSearchText] = useState('');
  const [keyword, setKeyword] = useState<string>();
  const [action, setAction] = useState<string>();
  const [dateRange, setDateRange] = useState<[string, string]>();

  const hasActiveFilters = !!(keyword || action || dateRange);

  const clearAllFilters = () => {
    setSearchText('');
    setKeyword(undefined);
    setAction(undefined);
    setDateRange(undefined);
    setPage(1);
  };

  const { data, isLoading } = useQuery({
    queryKey: ['audit-logs', page, pageSize, keyword, action, dateRange],
    queryFn: () => auditLogService.getAuditLogs({
      page,
      pageSize,
      keyword,
      action,
      dateFrom: dateRange?.[0],
      dateTo: dateRange?.[1],
    }),
  });

  const logs = data?.items ?? [];
  const totalCount = data?.totalCount ?? 0;

  const renderLogItem = (log: SystemAuditLog) => {
    const info = getActionInfo(log.action);
    const detailParts: string[] = [];
    detailParts.push(`${info.label} hóa đơn ${log.invoiceNumber || 'N/A'}`);
    if (log.reason) detailParts.push(`— Lý do: ${log.reason}`);
    if (log.comment) detailParts.push(`(${log.comment})`);

    // Determine Upload subtype for visual distinction
    const isUpload = log.action === 'UPLOAD';
    const isValidUpload = isUpload && log.changes?.some(c => c.field === 'Status' && c.new_value === 'Draft');
    const isInvalidUpload = isUpload && log.changes?.some(c => c.field === 'Status' && c.new_value === 'Rejected');
    const uploadTagColor = isValidUpload ? 'green' : isInvalidUpload ? 'red' : info.tagColor;
    const uploadLabel = isValidUpload ? 'Tải lên (hợp lệ)' : isInvalidUpload ? 'Tải lên (lỗi)' : info.label;

    const formatChangeValue = (value: string | null | undefined, changeType?: string) => {
      if (value === null || value === undefined || value === '') {
        return changeType === 'INSERT' ? null : 'Không có';
      }
      return value;
    };

    return {
      color: info.color,
      dot: (
        <div style={{
          width: 32, height: 32, borderRadius: 8,
          background: `${info.color}14`, display: 'flex',
          alignItems: 'center', justifyContent: 'center', color: info.color,
        }}>
          {info.icon}
        </div>
      ),
      children: (
        <div style={{ paddingBottom: 8 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 4, flexWrap: 'wrap' }}>
            <Tag color={isUpload ? uploadTagColor : info.tagColor} style={{ margin: 0, fontSize: 11 }}>{isUpload ? uploadLabel : info.label}</Tag>
            <Text type="secondary" style={{ fontSize: 12 }}>
              {dayjs(log.createdAt).format('HH:mm - DD/MM/YYYY')}
            </Text>
            {log.ipAddress && (
              <Tooltip title="Địa chỉ IP">
                <Text type="secondary" style={{ fontSize: 11, fontFamily: 'monospace' }}>({log.ipAddress})</Text>
              </Tooltip>
            )}
          </div>

          <div style={{ cursor: 'pointer' }} onClick={() => navigate(`/app/invoices/${log.invoiceId}`)}>
            <Text style={{ fontSize: 13, color: '#0f172a' }}>{detailParts.join(' ')}</Text>
          </div>

          {/* Show field changes */}
          {log.changes && log.changes.length > 0 && (
            <div style={{ marginTop: 4, padding: '4px 8px', background: '#f8fafc', borderRadius: 6, fontSize: 12 }}>
              {log.changes.map((c, i) => {
                const oldDisplay = formatChangeValue(c.old_value, c.change_type);
                const newDisplay = formatChangeValue(c.new_value, c.change_type);
                return (
                  <div key={i} style={{ color: '#64748b' }}>
                    <strong>{c.field}</strong>:{' '}
                    {oldDisplay && <span style={{ textDecoration: 'line-through', color: '#d63031' }}>{oldDisplay}</span>}
                    {oldDisplay && ' → '}
                    <span style={{ color: '#2d9a5c' }}>{newDisplay || 'Không có'}</span>
                  </div>
                );
              })}
            </div>
          )}

          <div style={{ marginTop: 4, display: 'flex', alignItems: 'center', gap: 6 }}>
            <Avatar size={18} icon={<UserOutlined />} style={{ background: '#1a4b8c' }} />
            <Text type="secondary" style={{ fontSize: 12 }}>
              {log.userFullName || log.userEmail || 'N/A'}
              {log.userRole && <Tag style={{ marginLeft: 6, fontSize: 10 }}>{log.userRole}</Tag>}
            </Text>
          </div>
        </div>
      ),
    };
  };

  return (
    <div className="animate-fade-in-up">
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 24 }}>
        <div>
          <Title level={3} className="text-dash-textMain font-bold tracking-tight m-0">Nhật ký hoạt động</Title>
          <Text className="text-dash-textMuted text-sm font-medium block mt-1">
            Audit trail — Không thể xóa hoặc chỉnh sửa • {totalCount} bản ghi
          </Text>
        </div>
      </div>

      <Card bordered={false} className="bg-dash-card rounded-[14px] shadow-dash" bodyStyle={{ padding: 0 }}>
        {/* Search & Filter */}
        <div style={{ padding: '16px 24px', borderBottom: '1px solid #E2E8F0' }}>
          <Row gutter={12} align="middle">
            <Col flex="auto">
              <Input.Search
                placeholder="Tìm kiếm theo số hóa đơn, email, lý do..."
                value={searchText}
                onChange={e => setSearchText(e.target.value)}
                onSearch={val => { setKeyword(val || undefined); setPage(1); }}
                enterButton={<SearchOutlined />}
                style={{ borderRadius: 10 }}
                allowClear
                onClear={() => { setSearchText(''); setKeyword(undefined); setPage(1); }}
              />
            </Col>
            <Col>
              <Button
                icon={<FilterOutlined />}
                onClick={() => setShowFilters(!showFilters)}
                type={showFilters ? 'primary' : 'default'}
                style={{ borderRadius: 10, height: 32, background: showFilters ? '#4880FF' : '#fff', color: showFilters ? '#fff' : '#202224', borderColor: showFilters ? '#4880FF' : '#E2E8F0', fontWeight: 600 }}
              >
                Bộ lọc
              </Button>
            </Col>
            {hasActiveFilters && (
              <Col>
                <Button
                  icon={<ClearOutlined />}
                  onClick={clearAllFilters}
                  style={{ borderRadius: 10, height: 32, fontWeight: 600, color: '#d63031', borderColor: '#d63031' }}
                >
                  Xóa bộ lọc
                </Button>
              </Col>
            )}
          </Row>

          {showFilters && (
            <Row gutter={12} style={{ marginTop: 12 }}>
              <Col xs={24} sm={8}>
                <Select
                  placeholder="Loại hành động"
                  style={{ width: '100%' }}
                  allowClear
                  value={action}
                  onChange={val => { setAction(val); setPage(1); }}
                  options={[
                    { value: 'UPLOAD', label: 'Tải lên' },
                    { value: 'EDIT', label: 'Chỉnh sửa' },
                    { value: 'SUBMIT', label: 'Gửi duyệt' },
                    { value: 'APPROVE', label: 'Phê duyệt' },
                    { value: 'REJECT', label: 'Từ chối' },
                  ]}
                />
              </Col>
              <Col xs={24} sm={16}>
                <RangePicker
                  style={{ width: '100%' }}
                  placeholder={['Từ ngày', 'Đến ngày']}
                  onChange={dates => {
                    if (dates && dates[0] && dates[1]) {
                      setDateRange([dates[0].toISOString(), dates[1].toISOString()]);
                    } else {
                      setDateRange(undefined);
                    }
                    setPage(1);
                  }}
                />
              </Col>
            </Row>
          )}
        </div>

        {/* Timeline */}
        <div style={{ padding: 24 }}>
          {isLoading ? (
            <div style={{ textAlign: 'center', padding: 48 }}><Spin size="large" /></div>
          ) : logs.length === 0 ? (
            <Empty description="Chưa có nhật ký hoạt động" />
          ) : (
            <Timeline items={logs.map(renderLogItem)} />
          )}
        </div>

        {/* Pagination - always visible */}
        <div style={{ padding: '16px 24px', borderTop: '1px solid #E2E8F0', textAlign: 'right' }}>
          <Pagination
            current={page}
            pageSize={pageSize}
            total={totalCount}
            showSizeChanger
            showTotal={(total) => `Tổng ${total} bản ghi`}
            onChange={(p, ps) => { setPage(p); setPageSize(ps); }}
          />
        </div>
      </Card>
    </div>
  );
};

export default AuditLogPage;
