import React, { useState } from 'react';
import {
  Card, Upload, Typography, Row, Col, Steps, Button, Space, Tag, Alert, message, Result, Drawer, Descriptions, Table, Input, InputNumber, Tooltip
} from 'antd';
import {
  InboxOutlined, FileTextOutlined, SafetyCertificateOutlined,
  CheckCircleOutlined, CloudUploadOutlined,
  FilePdfOutlined, FileImageOutlined, LoadingOutlined, WarningOutlined, CloseCircleOutlined, EditOutlined, SaveOutlined, DeleteOutlined, ClockCircleOutlined
} from '@ant-design/icons';
// Lưu ý: Cập nhật lại đường dẫn import nếu cần
import { invoiceService, ValidationResult } from '../services/invoice';

const { Title, Text, Paragraph } = Typography;
const { Dragger } = Upload;

interface ExtractedData {
  payment_terms?: string;
  delivery_address?: string;
  seller_name?: string;
  seller_tax_code?: string;
  invoice_date?: string;
  invoice_number?: string;
  invoice_symbol?: string;
  invoice_template_code?: string;
  total_pre_tax?: number;
  total_tax_amount?: number;
  total_amount?: number;
  line_items: Array<{
    stt: number;
    product_name: string;
    unit: string;
    quantity: number;
    unit_price: number;
    total_amount: number;
    vat_rate: number;
    vat_amount: number;
  }>;
}
interface ValidationResultExtended extends Omit<ValidationResult, 'extractedData'> {
  extractedData?: ExtractedData;
}

interface ProcessResult {
  fileName: string;
  fileSize: number;
  status: 'pending' | 'processing' | 'success' | 'error' | 'warning';
  result?: ValidationResultExtended;
  errorMessage?: string;
}

const UploadInvoice: React.FC = () => {
  const [currentStep, setCurrentStep] = useState(0);
  const [fileList, setFileList] = useState<any[]>([]);
  const [isProcessing, setIsProcessing] = useState(false);
  const [results, setResults] = useState<ProcessResult[]>([]);
  const [drawerVisible, setDrawerVisible] = useState(false);
  const [selectedInvoice, setSelectedInvoice] = useState<ProcessResult | null>(null);

  const handleViewDetails = (item: ProcessResult) => {
    setSelectedInvoice(item);
    setDrawerVisible(true);
  };

  const uploadProps = {
    name: 'file',
    multiple: true,
    accept: '.xml,.pdf,.jpg,.jpeg,.png',
    fileList,
    onChange(info: any) {
      setFileList(info.fileList);
    },
    beforeUpload: () => false, // Ngăn auto upload để xử lý thủ công
    showUploadList: false // Tự render list ở dưới để tránh lỗi layout
  };

  const handleReset = () => {
    setFileList([]);
    setResults([]);
    setCurrentStep(0);
  };

  const handleProcessFiles = async () => {
    if (fileList.length === 0) return;

    setIsProcessing(true);
    setCurrentStep(1);

    const initialResults: ProcessResult[] = fileList.map(f => ({
      fileName: f.name,
      fileSize: f.size,
      status: 'pending',
    }));
    setResults(initialResults);

    try {
      for (let i = 0; i < fileList.length; i++) {
        const fileObj = fileList[i].originFileObj as File;
        if (!fileObj) continue;

        setResults(prev => prev.map((item, idx) => idx === i ? { ...item, status: 'processing' } : item));

        if (!fileObj.name.toLowerCase().endsWith('.xml')) {
          setResults(prev => prev.map((item, idx) => idx === i ? {
            ...item,
            status: 'warning',
            errorMessage: 'File PDF/Ảnh đang trong quá trình thử nghiệm OCR, cần kiểm tra thủ công.'
          } : item));
          continue;
        }

        try {
          const { uploadUrl, s3Key } = await invoiceService.getUploadUrl(fileObj.name, fileObj.type || 'application/xml');
          await invoiceService.uploadToS3(uploadUrl, fileObj);

          if (currentStep < 2) setCurrentStep(2);

          const validation = await invoiceService.processXml(s3Key);

          setResults(prev => prev.map((item, idx) => idx === i ? {
            ...item,
            status: validation.isValid ? 'success' : 'error',
            result: validation as ValidationResultExtended,
            errorMessage: !validation.isValid ? validation.errors.join(', ') : undefined
          } : item));
        } catch (error: any) {
          const resData = error.response?.data;

          let errMsg = resData?.errors?.join(', ')
            || resData?.message
            || resData?.title
            || error.message
            || 'Lỗi không xác định';

          if (resData?.warnings && resData.warnings.length > 0) {
            errMsg += (errMsg ? ' | Cảnh báo: ' : 'Cảnh báo: ') + resData.warnings.join(', ');
          }

          setResults(prev => prev.map((item, idx) => idx === i ? {
            ...item,
            status: 'error',
            result: resData,
            errorMessage: errMsg
          } : item));
        }
      }
      setCurrentStep(3);
    } catch (err) {
      message.error('Quá trình tổng thể gặp lỗi. Vui lòng thử lại.');
    } finally {
      setIsProcessing(false);
    }
  };

  // --- UI COMPONENTS ---

  const renderStatusTag = (status: string, result?: ValidationResultExtended) => {
    if (status === 'pending') return <Tag icon={<ClockCircleOutlined />} color="default">Chờ xử lý</Tag>;
    if (status === 'processing') return <Tag icon={<LoadingOutlined />} color="processing">Đang xử lý</Tag>;
    if (status === 'warning') return <Tag icon={<WarningOutlined />} color="warning">Cần kiểm tra (Yellow)</Tag>;
    if (status === 'error') return <Tag icon={<CloseCircleOutlined />} color="error">Lỗi (Red)</Tag>;

    const hasWarnings = result?.warnings && result.warnings.length > 0;
    if (hasWarnings) return <Tag icon={<WarningOutlined />} color="warning">Cảnh báo (Yellow)</Tag>;
    return <Tag icon={<CheckCircleOutlined />} color="success">Hợp lệ (Green)</Tag>;
  };

  const columns = [
    {
      title: 'Tên File',
      dataIndex: 'fileName',
      key: 'fileName',
      render: (text: string) => <Text strong>{text}</Text>,
    },
    {
      title: 'Trạng thái',
      key: 'status',
      width: 180,
      render: (_: any, record: ProcessResult) => renderStatusTag(record.status, record.result),
    },
    {
      title: 'Thông điệp / Cảnh báo',
      key: 'message',
      render: (_: any, record: ProcessResult) => {
        if (record.status === 'pending') return <Text type="secondary">Đang chờ xử lý...</Text>;
        if (record.status === 'processing') return <Text type="secondary">Đang bóc tách dữ liệu...</Text>;
        if (record.status === 'error' || record.status === 'warning') return <Text type="danger">{record.errorMessage}</Text>;
        if (record.result?.warnings?.length) return <Text type="warning">{record.result.warnings[0]} {record.result.warnings.length > 1 ? '(+)' : ''}</Text>;
        return <Text type="success">Dữ liệu chuẩn xác</Text>;
      },
    },
    {
      title: 'Hành động',
      key: 'action',
      width: 250,
      render: (_: any, record: ProcessResult) => (
        <Space>
          {(record.status === 'success' || record.status === 'warning' || record.status === 'error') && (
            <Button size="small" type="primary" ghost icon={<EditOutlined />} onClick={() => handleViewDetails(record)}>
              Chi tiết
            </Button>
          )}
          {record.status === 'success' && !record.result?.warnings?.length && (
            <Button size="small" type="primary" icon={<SaveOutlined />}>Lưu</Button>
          )}
          <Tooltip title="Xóa file">
            <Button size="small" danger type="text" icon={<DeleteOutlined />} onClick={() => setResults(prev => prev.filter(r => r.fileName !== record.fileName))} />
          </Tooltip>
        </Space>
      ),
    },
  ];

  return (
    <div className="animate-fade-in-up">
      <div style={{ marginBottom: 24 }}>
        <Title level={4} style={{ margin: 0 }}>Xử lý Hóa đơn Đầu vào</Title>
        <Text type="secondary">Tải lên file XML/PDF/Ảnh để hệ thống tự động bóc tách và rà soát rủi ro.</Text>
      </div>

      <Card bordered={false} style={{ borderRadius: 12, marginBottom: 24, boxShadow: '0 2px 8px rgba(0,0,0,0.04)' }}>
        <Steps
          current={currentStep}
          size="small"
          items={[
            { title: 'Tải lên', icon: <CloudUploadOutlined /> },
            { title: 'Bóc tách', icon: <FileTextOutlined /> },
            { title: 'Rà soát', icon: <SafetyCertificateOutlined /> },
            { title: 'Hoàn tất', icon: <CheckCircleOutlined /> },
          ]}
          style={{ marginBottom: 32, maxWidth: 800, margin: '0 auto 32px' }}
        />

        {results.length === 0 ? (
          <Row gutter={24} align="stretch">
            <Col xs={24} lg={fileList.length > 0 ? 8 : 16}>
              <Dragger
                {...uploadProps}
                style={{ padding: fileList.length > 0 ? '60px 10px' : '30px 20px', borderRadius: 8, background: '#fafbfc', height: '100%' }}
              >
                <p className="ant-upload-drag-icon"><InboxOutlined style={{ color: '#1677ff', fontSize: 48 }} /></p>
                <p className="ant-upload-text" style={{ fontSize: 16, fontWeight: 500 }}>
                  {fileList.length > 0 ? 'Thêm file khác' : 'Kéo thả hoặc click để chọn file'}
                </p>
                <p className="ant-upload-hint">Hỗ trợ XML (Đề xuất), PDF, JPG, PNG. Tối đa 10MB/file.</p>
              </Dragger>

              {fileList.length === 0 && (
                <Alert
                  message="Khuyến nghị"
                  description="Để đảm bảo tính pháp lý và độ chính xác 100%, vui lòng ưu tiên upload file định dạng XML (QĐ 1550/QĐ-TCT). Các định dạng PDF/Ảnh sẽ được xử lý qua AI và yêu cầu bạn kiểm tra lại mắt thường."
                  type="info" showIcon
                  style={{ marginTop: 24 }}
                />
              )}
            </Col>

            {fileList.length > 0 ? (
              <Col xs={24} lg={16}>
                <Card
                  size="small"
                  title={<Text strong style={{ fontSize: 16 }}>Danh sách tải lên ({fileList.length} file)</Text>}
                  extra={
                    <Button type="primary" onClick={handleProcessFiles} loading={isProcessing} icon={<CloudUploadOutlined />}>
                      Bắt đầu xử lý {fileList.length} file
                    </Button>
                  }
                  style={{ borderColor: '#e2e8f0', borderRadius: 8, height: '100%', display: 'flex', flexDirection: 'column', boxShadow: '0 2px 6px rgba(0,0,0,0.02)' }}
                  bodyStyle={{ padding: 0, flex: 1, display: 'flex', flexDirection: 'column' }}
                >
                  <div style={{ maxHeight: 400, overflowY: 'auto', padding: 16, background: '#fafbfc', flex: 1 }}>
                    {fileList.map((f, i) => {
                      const lowerName = f.name.toLowerCase();
                      const isXml = lowerName.endsWith('.xml');
                      const isPdf = lowerName.endsWith('.pdf');
                      const isImage = lowerName.endsWith('.jpg') || lowerName.endsWith('.png') || lowerName.endsWith('.jpeg');

                      let Icon = FileTextOutlined;
                      let color = '#1677ff';
                      let tagLabel = 'HÓA ĐƠN';

                      if (isXml) { Icon = FileTextOutlined; color = '#52c41a'; tagLabel = 'XML'; }
                      else if (isPdf) { Icon = FilePdfOutlined; color = '#ff4d4f'; tagLabel = 'PDF'; }
                      else if (isImage) { Icon = FileImageOutlined; color = '#faad14'; tagLabel = 'IMAGE'; }

                      const sizeKb = (f.size / 1024).toFixed(1);

                      return (
                        <div key={i} className="animate-fade-in-up" style={{
                          padding: '12px 16px',
                          background: '#fff',
                          border: '1px solid #f0f0f0',
                          borderRadius: 8,
                          marginBottom: 12,
                          display: 'flex',
                          justifyContent: 'space-between',
                          alignItems: 'center',
                          boxShadow: '0 1px 2px rgba(0,0,0,0.02)',
                          animationDelay: `${i * 0.05}s`
                        }}>
                          <div style={{ display: 'flex', alignItems: 'center', gap: 16, overflow: 'hidden' }}>
                            <div style={{
                              width: 44, height: 44, borderRadius: 8,
                              background: `${color}15`, color: color,
                              display: 'flex', alignItems: 'center', justifyContent: 'center',
                              fontSize: 22, flexShrink: 0
                            }}>
                              <Icon />
                            </div>
                            <div style={{ minWidth: 0 }}>
                              <Text strong ellipsis style={{ display: 'block', maxWidth: 300, fontSize: 14 }}>{f.name}</Text>
                              <Space size="middle" style={{ marginTop: 2 }}>
                                <Tag bordered={false} color={color} style={{ margin: 0 }}>{tagLabel}</Tag>
                                <Text type="secondary" style={{ fontSize: 12 }}>{sizeKb} KB</Text>
                              </Space>
                            </div>
                          </div>

                          <Tooltip title="Xóa file">
                            <Button
                              type="text"
                              danger
                              icon={<DeleteOutlined />}
                              onClick={(e) => {
                                e.stopPropagation();
                                setFileList(prev => prev.filter((_, idx) => idx !== i));
                              }}
                            />
                          </Tooltip>
                        </div>
                      );
                    })}
                  </div>
                </Card>
              </Col>
            ) : (
              <Col xs={24} lg={8}>
                {/* Placeholder Col when fileList is empty so the Dragger takes 16 cols and is centered alongside Alert */}
              </Col>
            )}
          </Row>
        ) : (
          <div>
            <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 16 }}>
              <Title level={5}>Kết quả xử lý ({results.length} file)</Title>
              <Button onClick={handleReset} icon={<CloudUploadOutlined />}>Tải lên file khác</Button>
            </div>
            <Table
              dataSource={results}
              columns={columns}
              rowKey="fileName"
              pagination={false}
              size="middle"
              bordered
            />
          </div>
        )}
      </Card>

      {/* --- DRAWER CHI TIẾT SIDE-BY-SIDE --- */}
      <Drawer
        title={
          <Space>
            {renderStatusTag(selectedInvoice?.status || '', selectedInvoice?.result)}
            <Text strong>{selectedInvoice?.fileName}</Text>
          </Space>
        }
        width="95%" // Mở rộng tối đa để có không gian so sánh
        onClose={() => setDrawerVisible(false)}
        open={drawerVisible}
        bodyStyle={{ padding: '16px 24px', background: '#f5f5f5' }}
        extra={
          <Space>
            <Button onClick={() => setDrawerVisible(false)}>Hủy</Button>
            <Button type="primary" icon={<SaveOutlined />}>Xác nhận & Lưu hệ thống</Button>
          </Space>
        }
      >
        <Row gutter={24} style={{ height: '100%' }}>
          {/* CỘT TRÁI: HIỂN THỊ FILE GỐC */}
          <Col span={12} style={{ height: '100%', display: 'flex', flexDirection: 'column' }}>
            <div style={{ background: '#fff', padding: 16, borderRadius: 8, height: '100%', border: '1px solid #d9d9d9', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
              <Space direction="vertical" align="center">
                <FilePdfOutlined style={{ fontSize: 64, color: '#bfbfbf' }} />
                <Text type="secondary">Khu vực View bản thể hiện PDF / Ảnh gốc</Text>
                <Text type="secondary" style={{ fontSize: 12 }}>(Tích hợp thư viện react-pdf ở đây)</Text>
              </Space>
            </div>
          </Col>

          {/* CỘT PHẢI: FORM DỮ LIỆU BÓC TÁCH */}
          <Col span={12}>
            <Card size="small" title="Thông tin chung" style={{ marginBottom: 16, borderRadius: 8 }}>
              {selectedInvoice?.result?.warnings?.length ? (
                <Alert message="Cảnh báo rủi ro" description={
                  <ul style={{ paddingLeft: 16, margin: 0 }}>
                    {selectedInvoice.result.warnings.map((w, i) => <li key={i}>{w}</li>)}
                  </ul>
                } type="warning" showIcon style={{ marginBottom: 16 }} />
              ) : null}

              <Descriptions column={2} size="small" bordered>
                <Descriptions.Item label="Người bán" span={2}>
                  <Text strong>{selectedInvoice?.result?.extractedData?.seller_name || selectedInvoice?.result?.signerSubject?.split('CN=')[1]?.split(',')[0] || 'Chưa trích xuất được'}</Text>
                </Descriptions.Item>
                <Descriptions.Item label="Mã Số Thuế">{selectedInvoice?.result?.extractedData?.seller_tax_code || 'Chưa có'}</Descriptions.Item>
                <Descriptions.Item label="Ngày lập">
                  {selectedInvoice?.result?.extractedData?.invoice_date
                    ? new Date(selectedInvoice.result.extractedData.invoice_date).toLocaleDateString('vi-VN')
                    : 'Chưa có'}
                </Descriptions.Item>
                <Descriptions.Item label="Mẫu số">{selectedInvoice?.result?.extractedData?.invoice_template_code || 'Chưa có'}</Descriptions.Item>
                <Descriptions.Item label="Ký hiệu">{selectedInvoice?.result?.extractedData?.invoice_symbol || 'Chưa có'}</Descriptions.Item>
                <Descriptions.Item label="Số hóa đơn">{selectedInvoice?.result?.extractedData?.invoice_number || 'Chưa có'}</Descriptions.Item>
              </Descriptions>
            </Card>

            <Card size="small" title="Chi tiết hàng hóa" style={{ borderRadius: 8 }}>
              {selectedInvoice?.result?.extractedData?.line_items ? (
                <Table
                  dataSource={selectedInvoice.result.extractedData.line_items}
                  rowKey={(record) => `${selectedInvoice.fileName}_${record.stt}`}
                  pagination={false}
                  size="small"
                  scroll={{ y: 300 }} // Scroll dọc nếu nhiều item
                  columns={[
                    { title: 'Tên hàng', dataIndex: 'product_name', width: '35%', render: (val) => <Input defaultValue={val} style={{ width: '100%' }} /> as any },
                    { title: 'ĐVT', dataIndex: 'unit', width: '10%', render: (val) => <Input defaultValue={val || ''} size="small" style={{ width: '100%' }} /> as any },
                    { title: 'SL', dataIndex: 'quantity', width: '10%', render: (val) => <InputNumber defaultValue={val} size="small" style={{ width: '100%' }} /> },
                    { title: 'Đơn giá', dataIndex: 'unit_price', width: '15%', render: (val) => <InputNumber defaultValue={val} size="small" style={{ width: '100%' }} formatter={value => `${value}`.replace(/\B(?=(\d{3})+(?!\d))/g, ',')} /> },
                    { title: 'Thuế', dataIndex: 'vat_rate', width: '10%', align: 'center', render: (val) => <Text>{val}%</Text> },
                    { title: 'Thành tiền', dataIndex: 'total_amount', width: '20%', align: 'right', render: (val) => <Text strong>{val?.toLocaleString()}</Text> }
                  ]}
                  summary={(pageData) => {
                    let total = 0;
                    pageData.forEach(({ total_amount }) => { total += total_amount || 0; });
                    return (
                      <Table.Summary.Row style={{ background: '#fafafa' }}>
                        <Table.Summary.Cell index={0} colSpan={5}><Text strong style={{ float: 'right', paddingRight: 16 }}>Tổng cộng:</Text></Table.Summary.Cell>
                        <Table.Summary.Cell index={1}>
                          <Space direction="vertical" size={2} style={{ width: '100%', textAlign: 'right' }}>
                            {selectedInvoice?.result?.extractedData?.total_pre_tax !== undefined && (
                              <Text type="secondary">Cộng tiền hàng: {selectedInvoice.result.extractedData.total_pre_tax.toLocaleString()} ₫</Text>
                            )}
                            {selectedInvoice?.result?.extractedData?.total_tax_amount !== undefined && (
                              <Text type="secondary">Tiền thuế: {selectedInvoice.result.extractedData.total_tax_amount.toLocaleString()} ₫</Text>
                            )}
                            <Text className="text-red-500" strong>
                              Tổng cộng: {selectedInvoice?.result?.extractedData?.total_amount?.toLocaleString() || total.toLocaleString()} ₫
                            </Text>
                          </Space>
                        </Table.Summary.Cell>
                      </Table.Summary.Row>
                    );
                  }}
                />
              ) : (
                <Result status="info" title="Chưa có dữ liệu hàng hóa" />
              )}
            </Card>
          </Col>
        </Row>
      </Drawer>
    </div>
  );
};

export default UploadInvoice;