import React, { useState } from 'react';
import dayjs from 'dayjs';
import {
  Card, Table, Tag, Input, Select, DatePicker, Button, Space, Typography, Row, Col, Dropdown, Badge, Modal, message,
} from 'antd';
import {
  SearchOutlined, FilterOutlined, DownloadOutlined, PlusOutlined,
  EyeOutlined, MoreOutlined, SendOutlined, DeleteOutlined, ExclamationCircleOutlined,
} from '@ant-design/icons';
import { useNavigate } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { invoiceService } from '../services/invoice';
import StatusBadge from '../components/ui/StatusBadge';

const { Title, Text } = Typography;
const { RangePicker } = DatePicker;

const InvoiceList: React.FC = () => {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [showFilters, setShowFilters] = useState(false);

  const [pagination, setPagination] = useState({ current: 1, pageSize: 10 });
  const [keyword, setKeyword] = useState<string>();
  const [status, setStatus] = useState<string>();
  const [riskLevel, setRiskLevel] = useState<string>();
  const [dateRange, setDateRange] = useState<[string, string]>();

  const { data: invoiceData, isLoading } = useQuery({
    queryKey: ['invoices', pagination.current, pagination.pageSize, keyword, status, riskLevel, dateRange],
    queryFn: () => invoiceService.getInvoices(
      pagination.current,
      pagination.pageSize,
      keyword,
      status,
      riskLevel,
      dateRange?.[0],
      dateRange?.[1]
    ),
  });

  const submitMutation = useMutation({
    mutationFn: (id: string) => invoiceService.submitInvoice(id),
    onSuccess: () => {
      message.success('Đã gửi hóa đơn chờ duyệt thành công!');
      queryClient.invalidateQueries({ queryKey: ['invoices'] });
    },
    onError: (err: any) => {
      message.error(`Lỗi gửi duyệt: ${err?.response?.data?.message || err.message}`);
    }
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => invoiceService.deleteInvoice(id),
    onSuccess: () => {
      message.success('Đã xóa hóa đơn thành công!');
      queryClient.invalidateQueries({ queryKey: ['invoices'] });
    },
    onError: (err: any) => {
      message.error(`Lỗi xóa: ${err?.response?.data?.message || err.message}`);
    }
  });

  const handleSubmit = (record: any) => {
    Modal.confirm({
      title: 'Gửi hóa đơn chờ duyệt?',
      icon: <ExclamationCircleOutlined />,
      content: (
        <div>
          <p>Bạn sắp gửi hóa đơn <strong>{record.invoiceNumber}</strong> cho Admin duyệt.</p>
          <p style={{ color: '#888' }}>Sau khi gửi, trạng thái sẽ chuyển từ <Tag color="default">Draft</Tag> sang <Tag color="processing">Pending</Tag></p>
        </div>
      ),
      okText: 'Gửi duyệt',
      cancelText: 'Hủy',
      onOk: () => submitMutation.mutateAsync(record.invoiceId),
    });
  };

  const handleDelete = (record: any) => {
    Modal.confirm({
      title: 'Xóa hóa đơn?',
      icon: <ExclamationCircleOutlined style={{ color: '#ff4d4f' }} />,
      content: (
        <div>
          <p>Bạn có chắc muốn xóa hóa đơn <strong>{record.invoiceNumber}</strong>?</p>
          <p style={{ color: '#ff4d4f' }}>Hành động này không thể hoàn tác.</p>
        </div>
      ),
      okText: 'Xóa',
      okType: 'danger',
      cancelText: 'Hủy',
      onOk: () => deleteMutation.mutateAsync(record.invoiceId),
    });
  };

  const invoices = invoiceData?.items || [];
  const totalInvoices = invoiceData?.totalCount || 0;

  const columns = [
    {
      title: 'Số hóa đơn',
      dataIndex: 'invoiceNumber',
      key: 'invoiceNumber',
      render: (text: string, record: any) => (
        <div>
          <Text strong style={{ color: '#0f172a', cursor: 'pointer', fontSize: 14 }}>
            {text || 'N/A'} {record.serialNumber ? `- ${record.serialNumber}` : ''}
          </Text>
          <br />
          <Text style={{ fontSize: 12, color: '#64748b' }}>
            {record.processingMethod || 'XML'}
          </Text>
        </div>
      ),
    },
    {
      title: 'Người bán',
      dataIndex: 'sellerName',
      key: 'sellerName',
      render: (text: string, record: any) => (
        <div>
          <Text style={{ fontSize: 14, color: '#0f172a', fontWeight: 500 }}>{text || 'N/A'}</Text>
          <br />
          <Text style={{ fontSize: 12, color: '#64748b' }}>MST: {record.sellerTaxCode || 'N/A'}</Text>
        </div>
      ),
    },
    {
      title: 'Tổng tiền',
      dataIndex: 'totalAmount',
      key: 'totalAmount',
      align: 'right' as const,
      width: 150,
      render: (amount: number) => <Text strong style={{ whiteSpace: 'nowrap' }}>{amount?.toLocaleString('vi-VN')} ₫</Text>,
    },
    {
      title: 'Ngày lập & Tải lên',
      dataIndex: 'invoiceDate',
      key: 'invoiceDate',
      render: (dateStr: string, record: any) => (
        <div>
          <Text style={{ color: '#0f172a', fontSize: 14 }}>
            {dateStr ? dayjs(dateStr).format('DD/MM/YYYY') : 'N/A'}
          </Text>
          <br />
          <Text style={{ fontSize: 12, color: '#64748b' }}>
            Tải lên: {record.createdAt ? dayjs(record.createdAt).format('DD/MM/YYYY HH:mm') : 'N/A'}
          </Text>
        </div>
      ),
    },
    {
      title: 'Trạng thái',
      dataIndex: 'status',
      key: 'status',
      width: 140,
      render: (st: string) => <div style={{ whiteSpace: 'nowrap' }}><StatusBadge type="status" value={st} /></div>,
    },
    {
      title: 'Rủi ro',
      dataIndex: 'riskLevel',
      key: 'riskLevel',
      width: 120,
      render: (risk: string) => <StatusBadge type="risk" value={risk} />,
    },
    {
      title: '',
      key: 'actions',
      width: 48,
      render: (_: any, record: any) => {
        const isDraft = record.status === 'Draft';
        const menuItems: any[] = [
          { key: 'view', icon: <EyeOutlined />, label: 'Xem chi tiết' },
        ];

        if (isDraft) {
          menuItems.push(
            { key: 'submit', icon: <SendOutlined />, label: 'Gửi duyệt' },
            { type: 'divider' },
            { key: 'delete', icon: <DeleteOutlined />, label: 'Xóa hóa đơn', danger: true },
          );
        } else {
          menuItems.push(
            { key: 'download', icon: <DownloadOutlined />, label: 'Tải xuống' },
          );
        }

        return (
          <Dropdown menu={{
            items: menuItems,
            onClick: ({ key }) => {
              if (key === 'submit') handleSubmit(record);
              if (key === 'delete') handleDelete(record);
            }
          }} trigger={['click']}>
            <Button type="text" icon={<MoreOutlined />} size="small" />
          </Dropdown>
        );
      },
    },
  ];

  return (
    <div className="animate-fade-in-up">
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 24 }}>
        <div>
          <Title level={3} className="text-dash-textMain font-bold tracking-tight m-0">Quản lý hóa đơn</Title>
          <Text className="text-dash-textMuted text-sm font-medium block mt-1">Tổng cộng {totalInvoices} hóa đơn trong hệ thống</Text>
        </div>
        <Space size={12}>
          <Button icon={<DownloadOutlined />} style={{ borderRadius: 10, fontWeight: 600, height: 42, color: '#4880FF', borderColor: '#4880FF' }}>
            Xuất Excel
          </Button>
          <Button type="primary" icon={<PlusOutlined />} onClick={() => navigate('/app/upload')} style={{ borderRadius: 10, fontWeight: 600, height: 42, background: '#4880FF', border: 'none' }}>
            Tải lên hóa đơn
          </Button>
        </Space>
      </div>

      <Card bordered={false} className="bg-dash-card rounded-[14px] shadow-dash overflow-hidden" bodyStyle={{ padding: 0 }}>
        {/* Search & Filter Bar */}
        <div style={{ padding: '16px 24px', borderBottom: '1px solid #E2E8F0' }}>
          <Row gutter={12} align="middle">
            <Col flex="auto">
              <Input.Search
                placeholder="Tìm kiếm theo số hóa đơn, MST, tên người bán..."
                onSearch={val => { setKeyword(val); setPagination(prev => ({ ...prev, current: 1 })); }}
                enterButton={<SearchOutlined />}
                style={{ borderRadius: 10 }}
                allowClear
              />
            </Col>
            <Col>
              <Button
                icon={<FilterOutlined />}
                onClick={() => setShowFilters(!showFilters)}
                type={showFilters ? 'primary' : 'default'}
                style={{ borderRadius: 10, height: 32, background: showFilters ? '#4880FF' : '#fff', color: showFilters ? '#fff' : '#202224', borderColor: showFilters ? '#4880FF' : '#E2E8F0', fontWeight: 600 }}
              >
                Bộ lọc nâng cao
              </Button>
            </Col>
          </Row>

          {showFilters && (
            <Row gutter={12} style={{ marginTop: 12 }}>
              <Col xs={24} sm={8}>
                <Select placeholder="Trạng thái" style={{ width: '100%' }} allowClear
                  onChange={val => { setStatus(val); setPagination(prev => ({ ...prev, current: 1 })); }}
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
                  onChange={val => { setRiskLevel(val); setPagination(prev => ({ ...prev, current: 1 })); }}
                  options={[
                    { value: 'Green', label: '🟢 An toàn' },
                    { value: 'Yellow', label: '🟡 Lưu ý' },
                    { value: 'Orange', label: '🟠 Cảnh báo' },
                    { value: 'Red', label: '🔴 Nguy hiểm' },
                  ]}
                />
              </Col>
              <Col xs={24} sm={8}>
                <RangePicker style={{ width: '100%' }} placeholder={['Từ ngày', 'Đến ngày']}
                  onChange={dates => {
                    if (dates && dates[0] && dates[1]) {
                      setDateRange([dates[0].toISOString(), dates[1].toISOString()]);
                    } else {
                      setDateRange(undefined);
                    }
                    setPagination(prev => ({ ...prev, current: 1 }));
                  }}
                />
              </Col>
            </Row>
          )}
        </div>

        <Table
          columns={columns}
          dataSource={invoices}
          loading={isLoading}
          rowKey="invoiceId"
          onChange={(newPagination) => setPagination({ current: newPagination.current || 1, pageSize: newPagination.pageSize || 10 })}
          pagination={{
            current: pagination.current,
            pageSize: pagination.pageSize,
            total: totalInvoices,
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
