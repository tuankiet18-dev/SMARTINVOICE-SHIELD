import React, { useState, useCallback } from 'react';
import {
  Card, Row, Col, Typography, Select, Button, Table, Tag, Space,
  DatePicker, Modal, Form, Input, Spin, message,
  Tooltip,
} from 'antd';
import {
  DownloadOutlined, SettingOutlined, FileExcelOutlined,
  ExclamationCircleOutlined, CheckCircleOutlined,
} from '@ant-design/icons';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { invoiceService } from '../services/invoice';
import {
  exportService,
  type ExportConfigDto,
  type UpdateExportConfigDto,
} from '../services/export';
import dayjs, { type Dayjs } from 'dayjs';

const { Title, Text } = Typography;
const { RangePicker } = DatePicker;

// ════════════════════════════════════════════════════════════════
//  MISA Config Modal
// ════════════════════════════════════════════════════════════════
const MisaConfigModal: React.FC<{ open: boolean; onClose: () => void }> = ({ open, onClose }) => {
  const [form] = Form.useForm<UpdateExportConfigDto>();
  const queryClient = useQueryClient();

  const { data: config, isLoading } = useQuery({
    queryKey: ['export-config'],
    queryFn: exportService.getExportConfig,
    enabled: open,
  });

  React.useEffect(() => {
    if (config) {
      form.setFieldsValue({
        defaultDebitAccount: config.defaultDebitAccount,
        defaultCreditAccount: config.defaultCreditAccount,
        defaultTaxAccount: config.defaultTaxAccount,
        defaultWarehouse: config.defaultWarehouse,
      });
    }
  }, [config, form]);

  const updateMutation = useMutation({
    mutationFn: exportService.updateExportConfig,
    onSuccess: () => {
      message.success('Đã lưu cấu hình MISA');
      queryClient.invalidateQueries({ queryKey: ['export-config'] });
      onClose();
    },
    onError: () => message.error('Lưu cấu hình thất bại'),
  });

  return (
    <Modal
      title={<><SettingOutlined /> Cấu hình tài khoản kế toán MISA</>}
      open={open}
      onCancel={onClose}
      onOk={() => form.submit()}
      confirmLoading={updateMutation.isPending}
      okText="Lưu cấu hình"
      cancelText="Hủy"
      destroyOnHidden
    >
      <Text type="secondary" style={{ display: 'block', marginBottom: 16 }}>
        Thiết lập tài khoản mặc định sẽ được điền tự động khi xuất file chuẩn MISA AMIS Accounting.
      </Text>
      <Spin spinning={isLoading}>
        <Form form={form} layout="vertical" onFinish={(v) => updateMutation.mutate(v)}>
          <Form.Item label="TK Nợ (TK kho/TK chi phí)" name="defaultDebitAccount">
            <Input placeholder="VD: 156, 6421, 242..." />
          </Form.Item>
          <Form.Item label="TK Có (TK công nợ/TK tiền)" name="defaultCreditAccount">
            <Input placeholder="VD: 331, 1121..." />
          </Form.Item>
          <Form.Item label="TK Thuế GTGT" name="defaultTaxAccount">
            <Input placeholder="VD: 1331" />
          </Form.Item>
          <Form.Item label="Mã kho" name="defaultWarehouse">
            <Input placeholder="VD: KHO1, KHOCHINH..." />
          </Form.Item>
        </Form>
      </Spin>
    </Modal>
  );
};

// ════════════════════════════════════════════════════════════════
//  Reports Page
// ════════════════════════════════════════════════════════════════
const ReportsPage: React.FC = () => {
  // --- State ---
  const [dateRange, setDateRange] = useState<[Dayjs, Dayjs]>([
    dayjs().startOf('month'),
    dayjs().endOf('month'),
  ]);
  const [statusFilter, setStatusFilter] = useState<string | undefined>(undefined);
  const [configModalOpen, setConfigModalOpen] = useState(false);
  const [isExporting, setIsExporting] = useState(false);

  // --- Query thống kê ---
  const { data: statsData, isLoading } = useQuery({
    queryKey: ['invoices-stats', dateRange[0].toISOString(), dateRange[1].toISOString(), statusFilter],
    queryFn: () =>
      invoiceService.getInvoiceStats(
        dateRange[0].startOf('day').toISOString(),
        dateRange[1].endOf('day').toISOString(),
        statusFilter
      ),
  });

  const { data: historyData } = useQuery({
    queryKey: ['export-history'],
    queryFn: exportService.getHistory,
  });

  const totalValue = statsData?.totalAmount || 0;
  const totalTax = statsData?.totalTaxAmount || 0;
  const validRatio = statsData?.totalCount ? Math.round((statsData.validCount / statsData.totalCount) * 100) : 0;
  const needReviewCount = statsData?.needReviewCount || 0;

  // Hàm format tiền chuẩn Việt Nam, không làm tròn láo
  const formatCurrency = (val: number) => {
    if (val === 0) return '0 ₫';
    if (val >= 1e9) return `${(val / 1e9).toFixed(2).replace('.', ',')} tỷ ₫`;
    if (val >= 1e6) return `${(val / 1e6).toFixed(2).replace('.', ',')} triệu ₫`; // Đổi từ 1 thành 2
    return val.toLocaleString('vi-VN') + ' ₫';
  };
  const ExactNumberTooltip = ({ value }: { value: number }) => (
    <Tooltip title={`${value.toLocaleString('vi-VN')} ₫`} placement="top">
      <span style={{ cursor: 'pointer' }}>{formatCurrency(value)}</span>
    </Tooltip>
  );

  // --- Trigger download from pre-signed URL ---
  const triggerDownload = useCallback((url: string, fileName: string) => {
    const a = document.createElement('a');
    a.href = url;
    a.target = '_blank';
    a.download = fileName;
    a.style.display = 'none';
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
  }, []);

  // --- Export handler ---
  const handleExport = useCallback((exportType: 'MISA' | 'STANDARD') => {
    const startLabel = dateRange[0].format('DD/MM/YYYY');
    const endLabel = dateRange[1].format('DD/MM/YYYY');

    Modal.confirm({
      title: 'Xác nhận xuất báo cáo',
      icon: <ExclamationCircleOutlined />,
      content: (
        <div>
          <p>Bạn đang yêu cầu xuất báo cáo <strong>{exportType}</strong></p>
          <p>Từ <strong>{startLabel}</strong> đến <strong>{endLabel}</strong></p>
          {statusFilter && <p>Lọc trạng thái: <Tag>{statusFilter}</Tag></p>}
          {/* Thêm dòng này để hiện số lượng hóa đơn */}
          <p>Số lượng dự kiến: <strong style={{ color: '#1a4b8c', fontSize: '16px' }}>{statsData?.totalCount || 0}</strong> hóa đơn</p>
          <p style={{ color: '#8c8c8c', marginTop: 8 }}>Quá trình này có thể tốn vài phút. Xác nhận xuất?</p>
        </div>
      ),
      okText: 'Xác nhận xuất',
      cancelText: 'Hủy',
      onOk: async () => {
        setIsExporting(true);
        try {
          const result = await exportService.generateExport({
            startDate: dateRange[0].startOf('day').toISOString(),
            endDate: dateRange[1].endOf('day').toISOString(),
            invoiceStatus: statusFilter || null,
            exportType,
          });

          if (result.downloadUrl) {
            triggerDownload(result.downloadUrl, result.fileName);
            message.success({
              content: `Xuất thành công ${result.totalRecords} hóa đơn — ${result.fileName}`,
              icon: <CheckCircleOutlined style={{ color: '#52c41a' }} />,
              duration: 5,
            });
          } else {
            message.warning('Xuất hoàn tất nhưng không có link tải.');
          }
        } catch {
          message.error('Xuất báo cáo thất bại. Vui lòng thử lại.');
        } finally {
          setIsExporting(false);
        }
      },
    });
  }, [dateRange, statusFilter, statsData, triggerDownload]);

  return (
    <div className="animate-fade-in-up">
      {/* ── Header & Filters ── */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: 24, flexWrap: 'wrap', gap: 12 }}>
        <div>
          <Title level={4} style={{ margin: 0 }}>Báo cáo & Xuất file</Title>
          <Text type="secondary">Tạo báo cáo và xuất dữ liệu hóa đơn</Text>
        </div>
        <Space wrap>
          <RangePicker
            value={dateRange}
            onChange={(vals) => { if (vals?.[0] && vals?.[1]) setDateRange([vals[0], vals[1]]); }}
            format="DD/MM/YYYY"
            allowClear={false}
            style={{ width: 280 }}
          />
          <Select
            value={statusFilter}
            onChange={setStatusFilter}
            allowClear
            placeholder="Trạng thái HĐ"
            style={{ width: 160 }}
            options={[
              { value: 'Draft', label: 'Nháp' },
              { value: 'Pending', label: 'Chờ duyệt' },
              { value: 'Approved', label: 'Đã duyệt' },
              { value: 'Rejected', label: 'Từ chối' },
              { value: 'Archived', label: 'Lưu trữ' },
            ]}
          />
          <Button icon={<SettingOutlined />} onClick={() => setConfigModalOpen(true)}>
            Cấu hình MISA
          </Button>
        </Space>
      </div>

      {/* ── Statistics Cards ── */}
      <Row gutter={16} style={{ marginBottom: 24 }}>
        {[
          { title: 'Tổng giá trị hóa đơn', value: <ExactNumberTooltip value={totalValue} />, color: '#1a4b8c' },
          { title: 'Tổng thuế GTGT', value: <ExactNumberTooltip value={totalTax} />, color: '#2db791' },
          { title: 'Hóa đơn hợp lệ', value: `${validRatio}%`, color: '#2d9a5c' },
          { title: 'Cần xem xét', value: needReviewCount.toString(), color: '#e6a817' },
        ].map((stat, i) => (
          <Col xs={12} lg={6} key={i}>
            <Card loading={isLoading} variant="borderless" style={{ borderRadius: 12, borderLeft: `3px solid ${stat.color}` }}>
              <Text type="secondary" style={{ fontSize: 12 }}>{stat.title}</Text>
              <div style={{ fontSize: 22, fontWeight: 700, color: stat.color, marginTop: 4 }}>
                {stat.value}
              </div>
            </Card>
          </Col>
        ))}
      </Row>

      {/* ── Export Actions ── */}
      <Row gutter={16}>
        <Col xs={24} lg={12}>
          <Card variant="borderless" style={{ borderRadius: 12 }} title="Xuất báo cáo nhanh">
            <Spin spinning={isExporting} description="Đang xử lý...">
              <Space orientation="vertical" style={{ width: '100%' }} size={12}>
                {/* MISA Export */}
                <div style={{
                  padding: '14px 16px', borderRadius: 10, border: '1px solid #f0f0f0',
                  display: 'flex', justifyContent: 'space-between', alignItems: 'center',
                }}>
                  <div>
                    <Text strong style={{ fontSize: 13 }}>Xuất danh sách phần mềm Kế toán (MISA)</Text>
                    <br />
                    <Text type="secondary" style={{ fontSize: 12 }}>Format chuẩn AMIS Accounting — import tự động</Text>
                  </div>
                  <Button
                    icon={<FileExcelOutlined />}
                    type="primary"
                    size="small"
                    disabled={isExporting || isLoading} // Thêm || isLoading vào đây
                    onClick={() => handleExport('MISA')}
                    style={{ background: '#2d9a5c', borderColor: '#2d9a5c' }}
                  >
                    Xuất MISA
                  </Button>
                </div>

                {/* STANDARD Export */}
                <div style={{
                  padding: '14px 16px', borderRadius: 10, border: '1px solid #f0f0f0',
                  display: 'flex', justifyContent: 'space-between', alignItems: 'center',
                }}>
                  <div>
                    <Text strong style={{ fontSize: 13 }}>Xuất danh sách hóa đơn chung (Excel)</Text>
                    <br />
                    <Text type="secondary" style={{ fontSize: 12 }}>Bảng kê chi tiết toàn bộ hóa đơn theo bộ lọc</Text>
                  </div>
                  <Button
                    icon={<DownloadOutlined />}
                    type="primary"
                    ghost
                    size="small"
                    disabled={isExporting || isLoading}
                    onClick={() => handleExport('STANDARD')}
                  >
                    Xuất Excel
                  </Button>
                </div>
              </Space>
            </Spin>
          </Card>
        </Col>

        {/* ── Export History (placeholder — có thể kết nối API sau) ── */}
        <Col xs={24} lg={12}>
          <Card variant="borderless" style={{ borderRadius: 12 }} title="Lịch sử xuất file">
            <Table
              size="small"
              pagination={{ 
                defaultPageSize: 5, // Mỗi trang hiện 5 dòng cho gọn
                showSizeChanger: true, // Cho phép người dùng chọn xem 10, 20 dòng
                pageSizeOptions: ['5', '10', '20'],
                position: ['bottomCenter'] // Nút chuyển trang nằm ở giữa cho đẹp
              }}
              rowKey="exportId"
              locale={{ emptyText: 'Chưa có lịch sử xuất file' }}
              dataSource={historyData || []} // Thay mảng rỗng [] bằng data gọi từ API
              columns={[
                { title: 'Tên file', dataIndex: 'fileName', key: 'fileName', render: (t: string) => <Text style={{ fontSize: 13 }}>{t}</Text> },
                { title: 'Loại', dataIndex: 'fileType', key: 'fileType', render: (t: string) => <Tag>{t}</Tag> },
                { title: 'Số bản ghi', dataIndex: 'totalRecords', key: 'totalRecords' },
                {
                  title: 'Trạng thái', dataIndex: 'status', key: 'status',
                  render: (s: string) => (
                    <Tag color={s === 'Completed' ? 'green' : s === 'Failed' ? 'red' : 'processing'}>
                      {s === 'Completed' ? 'Hoàn tất' : s === 'Failed' ? 'Lỗi' : 'Đang xử lý'}
                    </Tag>
                  ),
                },
                // Thêm nút Download có click event
                { 
                  title: '', 
                  key: 'dl', 
                  render: (_, record: any) => record.downloadUrl 
                    ? <Button type="link" icon={<DownloadOutlined />} size="small" onClick={() => triggerDownload(record.downloadUrl, record.fileName)} /> 
                    : null 
                },
              ]}
            />
          </Card>
        </Col>
      </Row>

      {/* ── MISA Config Modal ── */}
      <MisaConfigModal open={configModalOpen} onClose={() => setConfigModalOpen(false)} />
    </div>
  );
};

export default ReportsPage;
