import React from 'react';
import {
    Card, Form, Input, InputNumber, Button, Typography, Switch, Select, message, Space, Divider
} from 'antd';
import {
    SettingOutlined, SaveOutlined, ReloadOutlined, DatabaseOutlined, ApiOutlined, RobotOutlined
} from '@ant-design/icons';

const { Title, Text } = Typography;

const SystemConfig: React.FC = () => {
    const [form] = Form.useForm();

    const handleSave = (values: any) => {
        console.log('Saved Configs: ', values);
        message.success('Đã cấu hình hệ thống thành công (Cập nhật SystemConfiguration)');
    };

    return (
        <div className="animate-fade-in-up">
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 24 }}>
                <div>
                    <Title level={4} style={{ margin: 0, color: '#1a4b8c' }}>Cấu Hình Hệ Thống Chung</Title>
                    <Text type="secondary">Quản trị các tham số lõi, cổng Server, Ngưỡng AI. Áp dụng cho toàn bộ Tenant.</Text>
                </div>
                <Space>
                    <Button icon={<ReloadOutlined />} onClick={() => form.resetFields()}>Hoàn tác</Button>
                    <Button type="primary" icon={<SaveOutlined />} onClick={() => form.submit()}>Lưu thay đổi</Button>
                </Space>
            </div>

            <Form
                form={form}
                layout="vertical"
                onFinish={handleSave}
                initialValues={{
                    ocrApiEndpoint: 'http://localhost:5000/process_invoice',
                    confidenceThreshold: 0.85,
                    maxUploadSizeMB: 10,
                    allowMachineLearning: true,
                    cloudSyncInterval: 15,
                }}
            >
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(400px, 1fr))', gap: 24 }}>
                    {/* Card 1: Cấu hình AI & OCR */}
                    <Card bordered={false} style={{ borderRadius: 12 }}>
                        <Title level={5} style={{ marginBottom: 16 }}>
                            <RobotOutlined style={{ marginRight: 8, color: '#1a4b8c' }} />
                            Cấu hình Trí Tuệ Nhân Tạo (AI / OCR)
                        </Title>

                        <Form.Item
                            name="ocrApiEndpoint"
                            label={<Text strong>OCR Python API Endpoint (VietOCR/PaddleOCR)</Text>}
                            rules={[{ required: true }]}
                        >
                            <Input prefix={<ApiOutlined />} placeholder="http://192.168.1.100:5000/api/v1/extract" />
                        </Form.Item>

                        <Form.Item
                            name="confidenceThreshold"
                            label={
                                <Space direction="vertical" size={0}>
                                    <Text strong>Ngưỡng Tự Động Duyệt (Confidence Threshold)</Text>
                                    <Text type="secondary" style={{ fontSize: 12 }}>Hóa đơn OCR nếu có độ tin cậy thấp hơn mức này sẽ bị cảnh báo Vàng (Yellow Risk).</Text>
                                </Space>
                            }
                        >
                            <InputNumber min={0.5} max={1.0} step={0.05} style={{ width: '100%' }} />
                        </Form.Item>

                        <Form.Item
                            name="allowMachineLearning"
                            label="Cho phép AI tự học từ lịch sử chỉnh sửa của Kế toán"
                            valuePropName="checked"
                        >
                            <Switch />
                        </Form.Item>
                    </Card>

                    {/* Card 2: Cầu hình Upload & Cơ sở dữ liệu */}
                    <Card bordered={false} style={{ borderRadius: 12 }}>
                        <Title level={5} style={{ marginBottom: 16 }}>
                            <DatabaseOutlined style={{ marginRight: 8, color: '#e6a817' }} />
                            Cấu hình Nền Tảng (Storage & Sync)
                        </Title>

                        <Form.Item
                            name="maxUploadSizeMB"
                            label={<Text strong>Dung lượng Upload Tối đa (MB) / file</Text>}
                        >
                            <InputNumber min={1} max={50} addonAfter="MB" style={{ width: '100%' }} />
                        </Form.Item>

                        <Form.Item
                            name="cloudSyncInterval"
                            label={
                                <Space direction="vertical" size={0}>
                                    <Text strong>Tần suất Sync S3 Bucket (phút)</Text>
                                    <Text type="secondary" style={{ fontSize: 12 }}>Thời gian CloudWatch trigger dọn dẹp và nén file XML cũ.</Text>
                                </Space>
                            }
                        >
                            <Select
                                options={[
                                    { label: '5 Phút (Realtime/Aggressive)', value: 5 },
                                    { label: '15 Phút (Khuyến cáo)', value: 15 },
                                    { label: '60 Phút (Tiết kiệm Server)', value: 60 },
                                ]}
                            />
                        </Form.Item>
                    </Card>
                </div>
            </Form>
        </div>
    );
};

export default SystemConfig;
