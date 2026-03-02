import React, { useState } from 'react';
import {
  Card, Table, Tag, Input, Select, DatePicker, Button, Space, Typography, Row, Col, Dropdown, Badge,
} from 'antd';
import {
  SearchOutlined, FilterOutlined, DownloadOutlined, PlusOutlined,
  EyeOutlined, EditOutlined, MoreOutlined,
} from '@ant-design/icons';
import { useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { invoiceService } from '../services/invoice';
import StatusBadge from '../components/ui/StatusBadge';

const { Title, Text } = Typography;
const { RangePicker } = DatePicker;

const riskColors: Record<string, string> = {
  Green: '#2d9a5c', Yellow: '#e6a817', Orange: '#e17055', Red: '#d63031',
};

const InvoiceList: React.FC = () => {
  const navigate = useNavigate();
  const [showFilters, setShowFilters] = useState(false);

  const { data: invoiceData = [], isLoading, isError } = useQuery({
    queryKey: ['invoices'],
    queryFn: () => invoiceService.getInvoices(),
  });

  const columns = [
    {
      title: 'Số hóa đơn',
      dataIndex: 'invoiceNo',
      key: 'invoiceNo',
      render: (text: string, record: any) => (
        <div>
          <Text strong style={{ color: '#0f172a', cursor: 'pointer', fontSize: 14 }}>{text}</Text>
          <br />
          <Text style={{ fontSize: 12, color: '#64748b' }}>
            {record.type} • {record.method}
          </Text>
        </div>
      ),
    },
    {
      title: 'Người bán',
      dataIndex: 'seller',
      key: 'seller',
      render: (text: string, record: any) => (
        <div>
          <Text style={{ fontSize: 14, color: '#0f172a', fontWeight: 500 }}>{text}</Text>
          <br />
          <Text style={{ fontSize: 12, color: '#64748b' }}>MST: {record.mst}</Text>
        </div>
      ),
    },
    {
      title: 'Tổng tiền',
      dataIndex: 'amount',
      key: 'amount',
      align: 'right' as const,
      render: (text: string) => <Text strong>{text}</Text>,
    },
    {
      title: 'Ngày lập',
      dataIndex: 'date',
      key: 'date',
      render: (text: string) => <Text type="secondary">{text}</Text>,
    },
    {
      title: 'Trạng thái',
      dataIndex: 'status',
      key: 'status',
      render: (status: string) => <StatusBadge type="status" value={status} />,
    },
    {
      title: 'Rủi ro',
      dataIndex: 'risk',
      key: 'risk',
      render: (risk: string) => <StatusBadge type="risk" value={risk} />,
    },
    {
      title: '',
      key: 'actions',
      width: 48,
      render: () => (
        <Dropdown menu={{
          items: [
            { key: 'view', icon: <EyeOutlined />, label: 'Xem chi tiết' },
            { key: 'edit', icon: <EditOutlined />, label: 'Chỉnh sửa' },
            { key: 'download', icon: <DownloadOutlined />, label: 'Tải xuống' },
          ],
        }} trigger={['click']}>
          <Button type="text" icon={<MoreOutlined />} size="small" />
        </Dropdown>
      ),
    },
  ];

  return (
    <div className="animate-fade-in-up">
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 24 }}>
        <div>
          <Title level={3} className="text-dash-textMain font-bold tracking-tight m-0">Quản lý hóa đơn</Title>
          <Text className="text-dash-textMuted text-sm font-medium block mt-1">Tổng cộng {invoiceData?.length || 0} hóa đơn trong hệ thống</Text>
        </div>
        <Space size={12}>
          <Button icon={<DownloadOutlined />} style={{ borderRadius: 10, fontWeight: 600, height: 42, color: '#4880FF', borderColor: '#4880FF' }}>
            Xuất Excel
          </Button>
          <Button type="primary" icon={<PlusOutlined />} onClick={() => navigate('/upload')} style={{ borderRadius: 10, fontWeight: 600, height: 42, background: '#4880FF', border: 'none' }}>
            Tải lên hóa đơn
          </Button>
        </Space>
      </div>

      <Card bordered={false} className="bg-dash-card rounded-[14px] shadow-dash overflow-hidden" bodyStyle={{ padding: 0 }}>
        {/* Search & Filter Bar */}
        <div style={{ padding: '16px 24px', borderBottom: '1px solid #E2E8F0' }}>
          <Row gutter={12} align="middle">
            <Col flex="auto">
              <Input
                placeholder="Tìm kiếm theo số hóa đơn, MST, tên người bán..."
                prefix={<SearchOutlined style={{ color: '#828282' }} />}
                style={{ borderRadius: 10, padding: '10px 16px', background: '#F5F6FA', border: '1px solid #E2E8F0' }}
                allowClear
              />
            </Col>
            <Col>
              <Button
                icon={<FilterOutlined />}
                onClick={() => setShowFilters(!showFilters)}
                type={showFilters ? 'primary' : 'default'}
                style={{ borderRadius: 10, height: 44, background: showFilters ? '#4880FF' : '#fff', color: showFilters ? '#fff' : '#202224', borderColor: showFilters ? '#4880FF' : '#E2E8F0', fontWeight: 600 }}
              >
                Bộ lọc nâng cao
              </Button>
            </Col>
          </Row>

          {showFilters && (
            <Row gutter={12} style={{ marginTop: 12 }}>
              <Col xs={24} sm={8}>
                <Select placeholder="Trạng thái" style={{ width: '100%' }} allowClear
                  options={[
                    { value: 'Draft', label: 'Nháp' },
                    { value: 'Pending', label: 'Chờ duyệt' },
                    { value: 'Approved', label: 'Đã duyệt' },
                    { value: 'Rejected', label: 'Từ chối' },
                  ]}
                />
              </Col>
              <Col xs={24} sm={8}>
                <Select placeholder="Mức rủi ro" style={{ width: '100%' }} allowClear
                  options={[
                    { value: 'Green', label: '🟢 An toàn' },
                    { value: 'Yellow', label: '🟡 Lưu ý' },
                    { value: 'Orange', label: '🟠 Cảnh báo' },
                    { value: 'Red', label: '🔴 Nguy hiểm' },
                  ]}
                />
              </Col>
              <Col xs={24} sm={8}>
                <RangePicker style={{ width: '100%' }} placeholder={['Từ ngày', 'Đến ngày']} />
              </Col>
            </Row>
          )}
        </div>

        <Table
          columns={columns}
          dataSource={invoiceData}
          loading={isLoading}
          pagination={{
            pageSize: 10,
            showSizeChanger: true,
            showTotal: (total) => `Tổng ${total} hóa đơn`,
            style: { padding: '16px 24px', margin: 0, borderTop: '1px solid #E2E8F0' }
          }}
          rowSelection={{ type: 'checkbox', columnWidth: 48 }}
          rowClassName={() => 'hover:bg-dash-bg/50 transition-colors'}
          components={{
            header: {
              cell: (props: any) => (
                <th {...props} className="bg-[#F9F9FB] text-dash-textMain font-semibold border-y border-dash-border py-4 px-6 text-left" />
              )
            },
            body: {
              cell: (props: any) => (
                <td {...props} className="py-5 px-6 border-b border-dash-border bg-dash-card" />
              )
            }
          }}
        />
      </Card>
    </div>
  );
};

export default InvoiceList;
