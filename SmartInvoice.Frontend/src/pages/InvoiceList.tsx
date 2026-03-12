import React, { useState, useRef } from 'react';
import dayjs from 'dayjs';
import {
  Card, Table, Tag, Input, Select, DatePicker, Button, Space, Typography, Row, Col, Dropdown, Badge, message, Modal,
} from 'antd';
import {
  SearchOutlined, FilterOutlined, DownloadOutlined, PlusOutlined,
  EyeOutlined, EditOutlined, MoreOutlined, DeleteOutlined, SendOutlined, CloseOutlined, ExclamationCircleOutlined, WarningOutlined,
} from '@ant-design/icons';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { invoiceService } from '../services/invoice';
import StatusBadge from '../components/ui/StatusBadge';

const { Title, Text, Paragraph } = Typography;
const { RangePicker } = DatePicker;

const InvoiceList: React.FC = () => {
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const queryClient = useQueryClient();
  const [showFilters, setShowFilters] = useState(() => !!(searchParams.get('status') || searchParams.get('riskLevel') || searchParams.get('dateFrom')));

  // Derive filter state from URL search params
  const keyword = searchParams.get('keyword') || undefined;
  const status = searchParams.get('status') || undefined;
  const riskLevel = searchParams.get('riskLevel') || undefined;
  const dateFrom = searchParams.get('dateFrom') || undefined;
  const dateTo = searchParams.get('dateTo') || undefined;
  const dateRange: [string, string] | undefined = dateFrom && dateTo ? [dateFrom, dateTo] : undefined;
  const page = Number(searchParams.get('page')) || 1;
  const pageSize = Number(searchParams.get('pageSize')) || 10;

  const [searchText, setSearchText] = useState(keyword || '');
  const [selectedRowKeys, setSelectedRowKeys] = useState<React.Key[]>([]);

  const updateParams = (updates: Record<string, string | undefined>) => {
    const newParams = new URLSearchParams(searchParams);
    Object.entries(updates).forEach(([key, value]) => {
      if (value !== undefined && value !== '') newParams.set(key, value);
      else newParams.delete(key);
    });
    setSearchParams(newParams, { replace: true });
  };

  const { data: invoiceData, isLoading } = useQuery({
    queryKey: ['invoices', page, pageSize, keyword, status, riskLevel, dateRange],
    queryFn: () => invoiceService.getInvoices(
      page,
      pageSize,
      keyword,
      status,
      riskLevel,
      dateRange?.[0],
      dateRange?.[1]
    ),
  });

  const submitMutation = useMutation({
    mutationFn: ({ id, comment }: { id: string; comment?: string }) => invoiceService.submitInvoice(id, comment),
    onSuccess: () => {
      message.success('Dã gửi hóa đơn chờ duyệt thành công!');
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
    const isYellow = record.riskLevel === 'Yellow';

    if (isYellow) {
      // Yellow: requires comment/explanation
      let comment = '';
      Modal.confirm({
        title: <Space><WarningOutlined style={{ color: '#faad14' }} /><span>Gửi duyệt hóa đơn cảnh báo</span></Space>,
        icon: null,
        content: (
          <div>
            <Paragraph type="secondary" style={{ marginBottom: 12 }}>
              Hóa đơn <strong>{record.invoiceNumber}</strong> có rủi ro <Tag color="warning">Yellow</Tag>.
              Vui lòng nhập lý do giải trình để Admin xem xét.
            </Paragraph>
            <Input.TextArea
              rows={3}
              placeholder="Ví dụ: Hóa đơn xăng công tác, không có MST người mua..."
              onChange={e => { comment = e.target.value; }}
            />
          </div>
        ),
        okText: 'Xác nhận gửi duyệt',
        cancelText: 'Hủy',
        onOk: () => submitMutation.mutateAsync({ id: record.invoiceId, comment: comment || undefined }),
      });
    } else {
      // Green: simple confirm
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
        onOk: () => submitMutation.mutateAsync({ id: record.invoiceId }),
      });
    }
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

  // ── Bulk Operations ──
  const selectedInvoices = invoices.filter((inv: any) => selectedRowKeys.includes(inv.invoiceId));
  const allSelectedAreDraft = selectedInvoices.length > 0 && selectedInvoices.every((inv: any) => inv.status === 'Draft');

  const bulkDeleteMutation = useMutation({
    mutationFn: async (ids: string[]) => {
      await Promise.all(ids.map(id => invoiceService.deleteInvoice(id)));
    },
    onSuccess: () => {
      message.success(`Đã xóa ${selectedRowKeys.length} hóa đơn`);
      setSelectedRowKeys([]);
      queryClient.invalidateQueries({ queryKey: ['invoices'] });
    },
    onError: () => message.error('Có lỗi khi xóa hóa đơn'),
  });

  const bulkSubmitMutation = useMutation({
    mutationFn: async ({ ids, comment }: { ids: string[]; comment?: string }) => {
      const result = await invoiceService.submitBatch(ids, comment);
      return result;
    },
    onSuccess: (result) => {
      message.success(`Đã gửi duyệt ${result.successCount} hóa đơn` + (result.failCount > 0 ? `, ${result.failCount} lỗi` : ''));
      setSelectedRowKeys([]);
      queryClient.invalidateQueries({ queryKey: ['invoices'] });
    },
    onError: () => message.error('Có lỗi khi gửi duyệt hóa đơn'),
  });

  const handleBulkSubmit = async () => {
    const greens = selectedInvoices.filter((inv: any) => inv.status === 'Draft' && inv.riskLevel !== 'Yellow');
    const yellows = selectedInvoices.filter((inv: any) => inv.status === 'Draft' && inv.riskLevel === 'Yellow');

    // 1. Batch-submit all Green invoices at once via a single API call
    if (greens.length > 0) {
      Modal.confirm({
        title: `Gửi duyệt ${greens.length} hóa đơn hợp lệ?`,
        content: `${greens.length} hóa đơn Green sẽ chuyển sang trạng thái "Chờ duyệt".`,
        okText: 'Gửi duyệt',
        cancelText: 'Hủy',
        onOk: async () => {
          await bulkSubmitMutation.mutateAsync({ ids: greens.map((g: any) => g.invoiceId) });
          // After greens done, prompt yellows sequentially
          if (yellows.length > 0) promptYellowSequentially(yellows, 0);
        },
      });
    } else if (yellows.length > 0) {
      // No greens — go straight to prompting yellows
      message.info(`${yellows.length} hóa đơn Yellow cần giải trình từng cái.`);
      promptYellowSequentially(yellows, 0);
    }
  };

  // Sequentially open a giải trình modal for each yellow invoice
  const promptYellowSequentially = (yellows: any[], index: number) => {
    if (index >= yellows.length) return;
    const inv = yellows[index];
    let comment = '';
    Modal.confirm({
      title: <Space><WarningOutlined style={{ color: '#faad14' }} /><span>Giải trình: {inv.invoiceNumber} ({index + 1}/{yellows.length})</span></Space>,
      icon: null,
      content: (
        <div>
          <Paragraph type="secondary" style={{ marginBottom: 12 }}>
            Hóa đơn <strong>{inv.invoiceNumber}</strong> có rủi ro <Tag color="warning">Yellow</Tag>.<br />
            Nhập lý do giải trình để Admin xét duyệt.
          </Paragraph>
          <Input.TextArea
            rows={3}
            placeholder="Ví dụ: Hóa đơn xăng công tác, cây xăng không xuất hoá đơn có MST người mua..."
            onChange={e => { comment = e.target.value; }}
          />
        </div>
      ),
      okText: index < yellows.length - 1 ? 'Xác nhận → tiếp theo' : 'Xác nhận gửi duyệt',
      cancelText: 'Bỏ qua hóa đơn này',
      onOk: async () => {
        try {
          await invoiceService.submitInvoice(inv.invoiceId, comment || undefined);
          message.success(`Đã gửi duyệt: ${inv.invoiceNumber}`);
        } catch (err: any) {
          message.error(`Lỗi gửi duyệt ${inv.invoiceNumber}: ${err?.response?.data?.message || err.message}`);
        }
        // Regardless of success/fail, move to next yellow
        promptYellowSequentially(yellows, index + 1);
      },
      onCancel: () => {
        // Skip this yellow, continue with next
        promptYellowSequentially(yellows, index + 1);
      },
      afterClose: () => {
        // Refresh list after the last yellow is processed
        if (index === yellows.length - 1) {
          setSelectedRowKeys([]);
          queryClient.invalidateQueries({ queryKey: ['invoices'] });
        }
      },
    });
  };

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
      render: (st: string, record: any) => {
        // Support both shapes: `validationLayers` (detail DTO) or legacy `validationResults`.
        // The list DTO doesn't include `validationLayers`; treat a Draft as NOT pending
        // when the server already returned a `riskLevel` (i.e. validation completed server-side).
        const hasValidationLayers = Array.isArray(record.validationLayers) && record.validationLayers.length > 0;
        const hasLegacyValidation = record.validationResults && Array.isArray(record.validationResults.layerResults) && record.validationResults.layerResults.length > 0;
        const hasRiskLevel = !!record.riskLevel && record.riskLevel !== 'Unknown';
        const isPending = st === 'Draft' && !(hasValidationLayers || hasLegacyValidation || hasRiskLevel);
        return (
          <div style={{ whiteSpace: 'nowrap' }}>
            <StatusBadge type="status" value={st} isPending={isPending} />
          </div>
        );
      },
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
            { key: 'edit', icon: <EditOutlined />, label: 'Chỉnh sửa' },
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
            onClick: ({ key, domEvent }: any) => {
              domEvent.stopPropagation();
              if (key === 'view') navigate(`/app/invoices/${record.invoiceId}`);
              else if (key === 'edit') navigate(`/app/invoices/${record.invoiceId}`);
              else if (key === 'submit') handleSubmit(record);
              else if (key === 'delete') handleDelete(record);
            }
          }} trigger={['click']}>
            <Button type="text" icon={<MoreOutlined />} size="small" onClick={e => e.stopPropagation()} />
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
        {/* Bulk Action Bar */}
        {selectedRowKeys.length > 0 && (
          <div style={{ padding: '12px 24px', background: '#f0f5ff', borderBottom: '1px solid #d6e4ff', display: 'flex', alignItems: 'center', gap: 12 }}>
            <Text strong style={{ color: '#1677ff' }}>Đã chọn {selectedRowKeys.length} hóa đơn</Text>
            <Button
              size="small"
              icon={<SendOutlined />}
              type="primary"
              disabled={!allSelectedAreDraft}
              loading={bulkSubmitMutation.isPending}
              onClick={handleBulkSubmit}
              style={{ borderRadius: 8, fontWeight: 600 }}
            >
              Gửi duyệt
            </Button>
            <Button
              size="small"
              icon={<DeleteOutlined />}
              danger
              disabled={!allSelectedAreDraft}
              loading={bulkDeleteMutation.isPending}
              onClick={() => {
                Modal.confirm({
                  title: `Xóa ${selectedRowKeys.length} hóa đơn?`,
                  content: 'Hành động này không thể hoàn tác.',
                  okText: 'Xóa',
                  cancelText: 'Hủy',
                  okButtonProps: { danger: true },
                  onOk: () => bulkDeleteMutation.mutate(selectedRowKeys as string[]),
                });
              }}
              style={{ borderRadius: 8, fontWeight: 600 }}
            >
              Xóa
            </Button>
            {!allSelectedAreDraft && (
              <Text type="secondary" style={{ fontSize: 12 }}>Chỉ hóa đơn Nháp mới có thể gửi duyệt hoặc xóa hàng loạt</Text>
            )}
            <Button type="text" size="small" icon={<CloseOutlined />} style={{ marginLeft: 'auto' }} onClick={() => setSelectedRowKeys([])}>
              Bỏ chọn
            </Button>
          </div>
        )}

        {/* Search & Filter Bar */}
        <div style={{ padding: '16px 24px', borderBottom: '1px solid #E2E8F0' }}>
          <Row gutter={12} align="middle">
            <Col flex="auto">
              <Input.Search
                placeholder="Tìm kiếm theo số hóa đơn, MST, tên người bán..."
                value={searchText}
                onChange={e => setSearchText(e.target.value)}
                onSearch={val => updateParams({ keyword: val || undefined, page: '1' })}
                enterButton={<SearchOutlined />}
                style={{ borderRadius: 10 }}
                allowClear
                onClear={() => { setSearchText(''); updateParams({ keyword: undefined, page: '1' }); }}
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
                  value={status}
                  onChange={val => updateParams({ status: val, page: '1' })}
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
                  value={riskLevel}
                  onChange={val => updateParams({ riskLevel: val, page: '1' })}
                  options={[
                    { value: 'Green', label: '🟢 An toàn' },
                    { value: 'Yellow', label: '🟡 Lưu ý' },
                    { value: 'Red', label: '🔴 Nguy hiểm' },
                  ]}
                />
              </Col>
              <Col xs={24} sm={8}>
                <RangePicker style={{ width: '100%' }} placeholder={['Từ ngày', 'Đến ngày']}
                  value={dateFrom && dateTo ? [dayjs(dateFrom), dayjs(dateTo)] : undefined}
                  onChange={dates => {
                    if (dates && dates[0] && dates[1]) {
                      updateParams({ dateFrom: dates[0].toISOString(), dateTo: dates[1].toISOString(), page: '1' });
                    } else {
                      updateParams({ dateFrom: undefined, dateTo: undefined, page: '1' });
                    }
                  }}
                />
              </Col>
            </Row>
          )}
        </div>

        <Table
          columns={columns}
          dataSource={invoices}
          rowKey="invoiceId"
          loading={isLoading}
          onRow={(record: any) => ({
            onClick: () => navigate(`/app/invoices/${record.invoiceId}`),
            style: { cursor: 'pointer' },
          })}
          onChange={(newPagination) => updateParams({ page: String(newPagination.current || 1), pageSize: String(newPagination.pageSize || 10) })}
          pagination={{
            current: page,
            pageSize: pageSize,
            total: totalInvoices,
            showSizeChanger: true,
            showTotal: (total) => `Tổng ${total} hóa đơn`,
            style: { padding: '16px 24px', margin: 0, borderTop: '1px solid #E2E8F0' }
          }}
          rowSelection={{
            type: 'checkbox',
            columnWidth: 48,
            selectedRowKeys,
            onChange: (keys) => setSelectedRowKeys(keys),
          }}
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
