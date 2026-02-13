import React, { useState } from 'react';
import {
  Card, Upload, Typography, Row, Col, Steps, Button, Space, Tag, Alert, message,
} from 'antd';
import {
  InboxOutlined, FileTextOutlined, SafetyCertificateOutlined,
  CheckCircleOutlined, CloudUploadOutlined, FileExcelOutlined,
  FilePdfOutlined, FileImageOutlined,
} from '@ant-design/icons';

const { Title, Text, Paragraph } = Typography;
const { Dragger } = Upload;

const UploadInvoice: React.FC = () => {
  const [currentStep, setCurrentStep] = useState(0);
  const [fileList, setFileList] = useState<any[]>([]);

  const uploadProps = {
    name: 'file',
    multiple: true,
    accept: '.xml,.pdf,.jpg,.jpeg,.png',
    fileList,
    onChange(info: any) {
      setFileList(info.fileList);
      if (info.file.status === 'done') {
        message.success(`${info.file.name} tải lên thành công.`);
      }
    },
    beforeUpload: () => false,
  };

  const fileTypeIcons: Record<string, React.ReactNode> = {
    xml: <FileTextOutlined style={{ color: '#1a4b8c', fontSize: 24 }} />,
    pdf: <FilePdfOutlined style={{ color: '#d63031', fontSize: 24 }} />,
    jpg: <FileImageOutlined style={{ color: '#e6a817', fontSize: 24 }} />,
    png: <FileImageOutlined style={{ color: '#e6a817', fontSize: 24 }} />,
  };

  return (
    <div className="animate-fade-in-up">
      <div style={{ marginBottom: 24 }}>
        <Title level={4} style={{ margin: 0 }}>Tải lên hóa đơn</Title>
        <Text type="secondary">Upload hóa đơn XML, PDF hoặc ảnh để tự động trích xuất & rà soát</Text>
      </div>

      <Card bordered={false} style={{ borderRadius: 12, marginBottom: 24 }}>
        <Steps
          current={currentStep}
          items={[
            { title: 'Tải lên', icon: <CloudUploadOutlined /> },
            { title: 'Trích xuất', icon: <FileTextOutlined /> },
            { title: 'Rà soát', icon: <SafetyCertificateOutlined /> },
            { title: 'Hoàn tất', icon: <CheckCircleOutlined /> },
          ]}
          style={{ marginBottom: 32 }}
        />

        <Row gutter={24}>
          <Col xs={24} lg={16}>
            <Dragger
              {...uploadProps}
              style={{
                padding: '40px 20px',
                borderRadius: 12,
                borderColor: '#1a4b8c40',
                background: 'rgba(26,75,140,0.02)',
              }}
            >
              <p className="ant-upload-drag-icon">
                <InboxOutlined style={{ color: '#1a4b8c', fontSize: 48 }} />
              </p>
              <p className="ant-upload-text" style={{ fontSize: 16, fontWeight: 500 }}>
                Kéo thả file hoặc click để chọn
              </p>
              <p className="ant-upload-hint" style={{ color: '#8c8c8c' }}>
                Hỗ trợ XML, PDF, JPG, PNG. Tối đa 10MB/file.
              </p>
              <Space style={{ marginTop: 12 }}>
                <Tag color="blue">XML</Tag>
                <Tag color="red">PDF</Tag>
                <Tag color="orange">JPG/PNG</Tag>
              </Space>
            </Dragger>

            {fileList.length > 0 && (
              <div style={{ marginTop: 20 }}>
                <Button type="primary" size="large" style={{ width: '100%', height: 48, borderRadius: 10 }}
                  onClick={() => {
                    setCurrentStep(1);
                    setTimeout(() => setCurrentStep(2), 1500);
                    setTimeout(() => setCurrentStep(3), 3000);
                  }}
                >
                  <CloudUploadOutlined /> Bắt đầu xử lý ({fileList.length} file)
                </Button>
              </div>
            )}
          </Col>

          <Col xs={24} lg={8}>
            <Card
              size="small"
              title={<Text strong style={{ fontSize: 13 }}>Hướng dẫn</Text>}
              style={{ borderRadius: 10, background: '#fafbfd' }}
              bordered={false}
            >
              <Space direction="vertical" size={12}>
                <div>
                  <Text strong style={{ fontSize: 13, color: '#1a4b8c' }}>📄 Hóa đơn XML</Text>
                  <Paragraph type="secondary" style={{ fontSize: 12, marginBottom: 0 }}>
                    Định dạng chuẩn theo QĐ 1550/QĐ-TCT. Validate 3 lớp đầy đủ, kết quả chính xác nhất.
                  </Paragraph>
                </div>
                <div>
                  <Text strong style={{ fontSize: 13, color: '#d63031' }}>📑 Hóa đơn PDF</Text>
                  <Paragraph type="secondary" style={{ fontSize: 12, marginBottom: 0 }}>
                    Sử dụng AI (AWS Textract) để trích xuất. Cần kiểm tra lại dữ liệu sau OCR.
                  </Paragraph>
                </div>
                <div>
                  <Text strong style={{ fontSize: 13, color: '#e6a817' }}>🖼️ Ảnh hóa đơn</Text>
                  <Paragraph type="secondary" style={{ fontSize: 12, marginBottom: 0 }}>
                    Chụp rõ nét, đủ sáng. AI sẽ tự động nhận dạng và trích xuất thông tin.
                  </Paragraph>
                </div>

                <Alert
                  message="Lưu ý"
                  description="Hóa đơn PDF/Ảnh sẽ được đánh dấu Yellow do thiếu XML pháp lý gốc."
                  type="warning"
                  showIcon
                  style={{ borderRadius: 8, fontSize: 12 }}
                />
              </Space>
            </Card>
          </Col>
        </Row>
      </Card>
    </div>
  );
};

export default UploadInvoice;
