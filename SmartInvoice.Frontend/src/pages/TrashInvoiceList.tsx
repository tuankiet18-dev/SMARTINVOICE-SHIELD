import React, { useState } from 'react';
import { Card, Table, Typography, Space, Button, Input, Modal, message, Tag, Tabs } from 'antd';
import { RetweetOutlined, DeleteOutlined, ExclamationCircleOutlined, SearchOutlined } from '@ant-design/icons';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import dayjs from 'dayjs';

import { invoiceService } from '../services/invoice';
import { exportService, ExportHistoryDto } from '../services/export';
import StatusBadge from '../components/ui/StatusBadge';

const { Title, Text } = Typography;
const { confirm } = Modal;

const TrashInvoiceList: React.FC = () => {
  const queryClient = useQueryClient();
  const [activeTab, setActiveTab] = useState('1');

  // Invoices state
  const [invoicePage, setInvoicePage] = useState(1);
  const [invoicePageSize, setInvoicePageSize] = useState(10);
  const [invoiceSearch, setInvoiceSearch] = useState('');

  // Exports state
  const [exportPage, setExportPage] = useState(1);
  const [exportPageSize, setExportPageSize] = useState(10);
  const [exportSearch, setExportSearch] = useState('');

  // 1. Queries cho Invoices
  const { data: invoiceData, isLoading: isLoadingInvoices } = useQuery({
    queryKey: ['trash-invoices', invoicePage, invoicePageSize, invoiceSearch],
    queryFn: () => invoiceService.getTrashInvoices(invoicePage, invoicePageSize, invoiceSearch),
  });

  const restoreInvoiceMutation = useMutation({
    mutationFn: (id: string) => invoiceService.restoreInvoice(id),
    onSuccess: () => {
      message.success('Đã khôi phục hóa đơn thành công');
      queryClient.invalidateQueries({ queryKey: ['trash-invoices'] });
      queryClient.invalidateQueries({ queryKey: ['invoices'] });
    },
    onError: (error: any) => {
      message.error(error.response?.data?.message || 'Khôi phục hóa đơn thất bại');
    },
  });

  const hardDeleteInvoiceMutation = useMutation({
    mutationFn: (id: string) => invoiceService.hardDeleteInvoice(id),
    onSuccess: () => {
      message.success('Đã xóa vĩnh viễn hóa đơn');
      queryClient.invalidateQueries({ queryKey: ['trash-invoices'] });
    },
    onError: (error: any) => {
      message.error(error.response?.data?.message || 'Xóa vĩnh viễn thất bại');
    },
  });

  const handleRestoreInvoice = (record: any) => {
    confirm({
      title: 'Khôi phục hóa đơn?',
      icon: <ExclamationCircleOutlined style={{ color: '#1890ff' }} />,
      content: <div>Hóa đơn <strong>{record.invoiceNumber}</strong> sẽ được đưa trở lại danh sách quản lý.</div>,
      okText: 'Khôi phục',
      cancelText: 'Hủy',
      onOk() {
        restoreInvoiceMutation.mutate(record.invoiceId);
      },
    });
  };

  const handleHardDeleteInvoice = (record: any) => {
    confirm({
      title: 'Xóa vĩnh viễn hóa đơn?',
      icon: <ExclamationCircleOutlined style={{ color: '#ff4d4f' }} />,
      content: (
          <div>
              <p>Bạn có chắc muốn xóa vĩnh viễn hóa đơn <strong>{record.invoiceNumber}</strong>?</p>
              <p style={{ color: '#ff4d4f', fontWeight: 'bold' }}>Hành động này không thể hoàn tác, file gốc sẽ bị xóa vĩnh viễn và dung lượng hệ thống sẽ được hoàn trả lại cho công ty.</p>
          </div>
      ),
      okText: 'Xóa vĩnh viễn',
      okType: 'danger',
      cancelText: 'Hủy',
      onOk() {
        hardDeleteInvoiceMutation.mutate(record.invoiceId);
      },
    });
  };

  // 2. Queries cho Exports
  const { data: exportData, isLoading: isLoadingExports } = useQuery({
    queryKey: ['trash-exports'],
    queryFn: () => exportService.getTrashExports(),
  });

  const restoreExportMutation = useMutation({
    mutationFn: (id: string) => exportService.restoreExport(id),
    onSuccess: () => {
      message.success('Đã khôi phục file export thành công');
      queryClient.invalidateQueries({ queryKey: ['trash-exports'] });
      queryClient.invalidateQueries({ queryKey: ['exports'] });
    },
    onError: (error: any) => {
      message.error(error.response?.data?.message || 'Khôi phục file thất bại');
    },
  });

  const hardDeleteExportMutation = useMutation({
    mutationFn: (id: string) => exportService.hardDeleteExport(id),
    onSuccess: () => {
      message.success('Đã xóa vĩnh viễn file export');
      queryClient.invalidateQueries({ queryKey: ['trash-exports'] });
    },
    onError: (error: any) => {
      message.error(error.response?.data?.message || 'Xóa vĩnh viễn thất bại');
    },
  });

  const handleRestoreExport = (record: ExportHistoryDto) => {
    confirm({
      title: 'Xác nhận khôi phục',
      icon: <ExclamationCircleOutlined style={{ color: '#1890ff' }} />,
      content: <div>Bạn có chắc chắn muốn khôi phục file xuất <strong>{record.fileName}</strong> này không?</div>,
      okText: 'Khôi phục',
      cancelText: 'Hủy',
      onOk() {
        restoreExportMutation.mutate(record.exportId);
      },
    });
  };

  const handleHardDeleteExport = (record: ExportHistoryDto) => {
    confirm({
      title: 'Xóa vĩnh viễn',
      icon: <ExclamationCircleOutlined style={{ color: '#ff4d4f' }} />,
      content: 'Hành động này không thể hoàn tác. Bạn có chắc chắn muốn xóa vĩnh viễn file xuất này?',
      okText: 'Xóa vĩnh viễn',
      okType: 'danger',
      cancelText: 'Hủy',
      onOk() {
        hardDeleteExportMutation.mutate(record.exportId);
      },
    });
  };

  // Columns cho Invoices
  const invoiceColumns = [
    {
        title: 'Số HĐ',
        dataIndex: 'invoiceNumber',
        key: 'invoiceNumber',
        render: (text: string, record: any) => (
            <Space direction="vertical" size={0}>
                <Text strong>{text || 'N/A'}</Text>
                <Text type="secondary" style={{ fontSize: 12 }}>{record.serialNumber}</Text>
            </Space>
        ),
    },
    {
        title: 'Ngày HĐ',
        dataIndex: 'invoiceDate',
        key: 'invoiceDate',
        render: (date: string) => {
            const dateObj = new Date(date);
            return isNaN(dateObj.getTime()) ? 'N/A' : dateObj.toLocaleDateString('vi-VN');
        },
    },
    {
        title: 'Người bán',
        dataIndex: 'sellerName',
        key: 'sellerName',
        ellipsis: true,
        render: (text: string, record: any) => (
            <Space direction="vertical" size={0}>
                <Text ellipsis>{text || 'N/A'}</Text>
                <Text type="secondary" style={{ fontSize: 12 }}>MST: {record.sellerTaxCode}</Text>
            </Space>
        ),
    },
    {
        title: 'Số tiền',
        dataIndex: 'totalAmount',
        key: 'totalAmount',
        align: 'right' as const,
        render: (val: number, record: any) => (
            <Text strong>{new Intl.NumberFormat('vi-VN').format(val || 0)} {record.invoiceCurrency}</Text>
        ),
    },
    {
        title: 'Rủi ro',
        dataIndex: 'riskLevel',
        key: 'riskLevel',
        render: (risk: string) => (
            <StatusBadge
              type="risk"
              value={risk || 'Không xác định'}
            />
        ),
    },
    {
        title: 'Trạng thái cũ',
        dataIndex: 'status',
        key: 'status',
        render: (status: string) => <StatusBadge value={status || 'Draft'} type="status" />,
    },
    {
        title: 'Hành động',
        key: 'actions',
        align: 'center' as const,
        render: (_: any, record: any) => (
            <Space size="middle">
                <Button
                    type="text"
                    icon={<RetweetOutlined style={{ color: '#1890ff' }} />}
                    onClick={() => handleRestoreInvoice(record)}
                    title="Khôi phục"
                />
                <Button
                    type="text"
                    danger
                    icon={<DeleteOutlined />}
                    onClick={() => handleHardDeleteInvoice(record)}
                    title="Xóa vĩnh viễn"
                />
            </Space>
        ),
    },
  ];

  // Columns cho Exports
  const exportColumns = [
    {
      title: 'Tên file',
      dataIndex: 'fileName',
      key: 'fileName',
      render: (text: string) => <Text strong>{text || 'Không có tên'}</Text>,
    },
    {
      title: 'Loại export',
      dataIndex: 'fileType',
      key: 'fileType',
      render: (type: string) => <Tag color="blue">{type}</Tag>,
    },
    {
      title: 'Số bản ghi',
      dataIndex: 'totalRecords',
      key: 'totalRecords',
    },
    {
      title: 'Trạng thái',
      key: 'status',
      render: (_: any, record: ExportHistoryDto) => {
        let color = 'default';
        if (record.status === 'Completed') color = 'success';
        if (record.status === 'Failed') color = 'error';
        if (record.status === 'Processing') color = 'processing';
        return <Tag color={color}>{record.status}</Tag>;
      },
    },
    {
      title: 'Thao tác',
      key: 'actions',
      fixed: 'right' as const,
      render: (_: any, record: ExportHistoryDto) => (
        <Space size="middle">
          <Button
            type="text"
            icon={<RetweetOutlined style={{ color: '#1890ff' }} />}
            onClick={() => handleRestoreExport(record)}
            title="Khôi phục"
          />
          <Button
            type="text"
            danger
            icon={<DeleteOutlined />}
            onClick={() => handleHardDeleteExport(record)}
            title="Xóa vĩnh viễn"
          />
        </Space>
      ),
    },
  ];

  const filteredExportData = exportData?.filter(item => 
    !exportSearch || 
    item.fileName?.toLowerCase().includes(exportSearch.toLowerCase()) ||
    item.fileType?.toLowerCase().includes(exportSearch.toLowerCase())
  );

  const items = [
    {
      key: '1',
      label: 'Hóa đơn đã xóa',
      children: (
        <>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
            <Input
              placeholder="Tìm kiếm mã số thuế, số hóa đơn..."
              prefix={<SearchOutlined className="text-gray-400" />}
              value={invoiceSearch}
              onChange={(e) => setInvoiceSearch(e.target.value)}
              style={{ maxWidth: 400 }}
              allowClear
            />
          </div>
          <Table
            columns={invoiceColumns}
            dataSource={invoiceData?.items || []}
            rowKey="invoiceId"
            loading={isLoadingInvoices}
            pagination={{
              current: invoicePage,
              pageSize: invoicePageSize,
              total: invoiceData?.totalCount || 0,
              showSizeChanger: true,
              showTotal: (total) => `Tổng ${total} hóa đơn`,
              onChange: (page, pageSize) => {
                setInvoicePage(page);
                setInvoicePageSize(pageSize);
              },
            }}
            scroll={{ x: true }}
            bordered
          />
        </>
      ),
    },
    {
      key: '2',
      label: 'Lịch sử xuất (Export) đã xóa',
      children: (
        <>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
            <Input
              placeholder="Tìm kiếm theo tên file..."
              prefix={<SearchOutlined className="text-gray-400" />}
              value={exportSearch}
              onChange={(e) => setExportSearch(e.target.value)}
              style={{ maxWidth: 400 }}
              allowClear
            />
          </div>
          <Table
            columns={exportColumns}
            dataSource={filteredExportData || []}
            rowKey="exportId"
            loading={isLoadingExports}
            pagination={{
              current: exportPage,
              pageSize: exportPageSize,
              showSizeChanger: true,
              showTotal: (total) => `Tổng ${total} file export`,
              onChange: (page, pageSize) => {
                setExportPage(page);
                setExportPageSize(pageSize);
              },
            }}
            scroll={{ x: true }}
            bordered
          />
        </>
      ),
    }
  ];

  return (
    <Space direction="vertical" style={{ width: '100%' }} size="large">
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
        <div>
          <Title level={2} style={{ margin: 0 }}>Thùng rác hệ thống</Title>
          <Text type="secondary">Quản lý và khôi phục các dữ liệu đã bị xóa</Text>
        </div>
      </div>

      <Card>
        <Tabs 
          activeKey={activeTab} 
          onChange={setActiveTab} 
          items={items} 
          type="line"
        />
      </Card>
    </Space>
  );
};

export default TrashInvoiceList;
