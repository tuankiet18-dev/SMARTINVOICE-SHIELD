import React, { useState } from 'react';
import {
  Card, Table, Tag, Input, Select, DatePicker, Button, Space, Typography, Row, Col, Dropdown, Badge,
} from 'antd';
import {
  SearchOutlined, FilterOutlined, DownloadOutlined, PlusOutlined,
  EyeOutlined, EditOutlined, MoreOutlined, FileTextOutlined, LoadingOutlined,
} from '@ant-design/icons';
import { useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { invoiceService } from '../services/invoice';

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
          <Text strong style={{ color: '#1a4b8c', cursor: 'pointer' }}>{text}</Text>
          <br />
          <Text type="secondary" style={{ fontSize: 11 }}>
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
          <Text style={{ fontSize: 13 }}>{text}</Text>
          <br />
          <Text type="secondary" style={{ fontSize: 11 }}>MST: {record.mst}</Text>
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
      render: (status: string) => {
        const map: Record<string, { color: string; label: string }> = {
          Approved: { color: 'green', label: 'Đã duyệt' },
          Pending: { color: 'gold', label: 'Chờ duyệt' },
          Draft: { color: 'default', label: 'Nháp' },
          Rejected: { color: 'red', label: 'Từ chối' },
        };
        const s = map[status] || { color: 'default', label: status };
        return <Tag color={s.color}>{s.label}</Tag>;
      },
    },
    {
      title: 'Rủi ro',
      dataIndex: 'risk',
      key: 'risk',
      render: (risk: string) => (
        <Tag style={{
          background: `${riskColors[risk]}14`, color: riskColors[risk],
          border: `1px solid ${riskColors[risk]}30`, borderRadius: 6, fontWeight: 600, fontSize: 12,
        }}>
          {risk}
        </Tag>
      ),
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
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 20 }}>
        <div>
          <Title level={4} style={{ margin: 0 }}>Quản lý hóa đơn</Title>
          <Text type="secondary">Tổng cộng {invoiceData?.length || 0} hóa đơn</Text>
        </div>
        <Space>
          <Button icon={<DownloadOutlined />}>Xuất Excel</Button>
          <Button type="primary" icon={<PlusOutlined />} onClick={() => navigate('/upload')}>
            Tải lên hóa đơn
          </Button>
        </Space>
      </div>

      <Card bordered={false} style={{ borderRadius: 12 }} bodyStyle={{ padding: 0 }}>
        {/* Search & Filter Bar */}
        <div style={{ padding: '16px 20px', borderBottom: '1px solid #f0f0f0' }}>
          <Row gutter={12} align="middle">
            <Col flex="auto">
              <Input
                placeholder="Tìm kiếm theo số hóa đơn, MST, tên người bán..."
                prefix={<SearchOutlined style={{ color: '#bfbfbf' }} />}
                style={{ borderRadius: 8 }}
                allowClear
              />
            </Col>
            <Col>
              <Button
                icon={<FilterOutlined />}
                onClick={() => setShowFilters(!showFilters)}
                type={showFilters ? 'primary' : 'default'}
                ghost={showFilters}
              >
                Bộ lọc
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
          }}
          size="middle"
          rowSelection={{ type: 'checkbox' }}
          style={{ padding: '0 4px' }}
        />
      </Card>
    </div>
  );
};

export default InvoiceList;
