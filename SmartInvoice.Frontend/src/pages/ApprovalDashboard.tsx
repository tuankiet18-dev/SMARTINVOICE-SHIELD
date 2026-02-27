import React, { useState } from 'react';
import {
    Card, Table, Tag, Input, Typography, Row, Col, Tabs, Button, Space, Modal, Form, message
} from 'antd';
import {
    SearchOutlined, CheckCircleOutlined, WarningOutlined, ExclamationCircleOutlined, EyeOutlined, LoadingOutlined
} from '@ant-design/icons';
import type { TabsProps } from 'antd';
import { useQuery } from '@tanstack/react-query';
import { invoiceService } from '../services/invoice';

const { Title, Text } = Typography;
const { TextArea } = Input;

const riskColors: Record<string, string> = {
    Green: '#2d9a5c', Yellow: '#e6a817', Orange: '#e17055', Red: '#d63031',
};

const mockApprovalData = [
    { key: '1', invoiceNo: 'INV-2026-001290', seller: 'Công ty TNHH Thương mại ABC', amount: '25,400,000 ₫', date: '12/02/2026', risk: 'Yellow', reason: 'Sai lệch tiền thuế 5,000đ' },
    { key: '2', invoiceNo: 'INV-2026-001289', seller: 'Công ty CP Công nghệ XYZ', amount: '8,750,000 ₫', date: '11/02/2026', risk: 'Orange', reason: 'Ngày lập và ký lệch 5 ngày' },
    { key: '3', invoiceNo: 'INV-2026-001288', seller: 'DN Tư nhân Phát Đạt', amount: '42,100,000 ₫', date: '11/02/2026', risk: 'Yellow', reason: 'Khóa chữ ký số sắp hết hạn' },
    { key: '4', invoiceNo: 'INV-2026-001287', seller: 'Công ty CP Vận tải An Bình', amount: '12,000,000 ₫', date: '10/02/2026', risk: 'Orange', reason: 'Tổng tiền vượt định mức ngành' },
];

const ApprovalDashboard: React.FC = () => {
    const [isOverrideModalVisible, setIsOverrideModalVisible] = useState(false);
    const [selectedInvoice, setSelectedInvoice] = useState<any>(null);
    const [form] = Form.useForm();
    const [localData, setLocalData] = useState<any[]>([]); // To manage state after override

    const { data: apiData = [], isLoading, isError } = useQuery({
        queryKey: ['invoices-approval'],
        queryFn: () => invoiceService.getInvoices(),
    });

    // Determine which data to use: if API fails or is empty, use mock data. Otherwise use API data filtered for risks.
    // In a real app, you would have a specific endpoint for "/api/invoices/pending-approval"
    const dataToDisplay = apiData.length > 0 ? apiData.filter(i => i.risk === 'Yellow' || i.risk === 'Orange') : mockApprovalData;

    // Merge remote and local state logic for the "override" demo to work smoothly
    const currentData = localData.length > 0 ? localData : dataToDisplay;


    const handleOverrideClick = (record: any) => {
        setSelectedInvoice(record);
        setIsOverrideModalVisible(true);
    };

    const handleOverrideSubmit = () => {
        form.validateFields().then(values => {
            // Simulate API call to override risk
            message.success(`Đã duyệt ngoại lệ hóa đơn ${selectedInvoice.invoiceNo}. Lý do: ${values.reason}`);
            setLocalData(currentData.filter(item => item.key !== selectedInvoice.key));
            setIsOverrideModalVisible(false);
            form.resetFields();
        });
    };

    const columns = [
        {
            title: 'Số hóa đơn',
            dataIndex: 'invoiceNo',
            key: 'invoiceNo',
            render: (text: string) => <Text strong style={{ color: '#1a4b8c' }}>{text}</Text>,
        },
        {
            title: 'Người bán',
            dataIndex: 'seller',
            key: 'seller',
        },
        {
            title: 'Nội dung Cảnh báo',
            dataIndex: 'reason',
            key: 'reason',
            render: (text: string, record: any) => (
                <Space>
                    {record.risk === 'Orange' ? <ExclamationCircleOutlined style={{ color: '#e17055' }} /> : <WarningOutlined style={{ color: '#e6a817' }} />}
                    <Text type="secondary">{text}</Text>
                </Space>
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
            title: 'Rủi ro',
            dataIndex: 'risk',
            key: 'risk',
            width: 120,
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
            title: 'Hành động',
            key: 'action',
            width: 250,
            render: (_, record) => (
                <Space>
                    <Button size="small" icon={<EyeOutlined />}>Chi tiết</Button>
                    <Button
                        type="primary"
                        danger={record.risk === 'Orange'}
                        style={record.risk === 'Yellow' ? { background: '#e6a817', borderColor: '#e6a817' } : {}}
                        icon={<CheckCircleOutlined />}
                        size="small"
                        onClick={() => handleOverrideClick(record)}
                    >
                        Duyệt ngoại lệ
                    </Button>
                </Space>
            ),
        },
    ];

    const items: TabsProps['items'] = [
        {
            key: '1',
            label: 'Chờ duyệt (Lưu ý)',
            children: <Table loading={isLoading} columns={columns} dataSource={currentData.filter(i => i.risk === 'Yellow')} pagination={false} />,
        },
        {
            key: '2',
            label: 'Cảnh báo rủi ro cao',
            children: <Table loading={isLoading} columns={columns} dataSource={currentData.filter(i => i.risk === 'Orange')} pagination={false} />,
        },
    ];

    return (
        <div className="animate-fade-in-up">
            <div style={{ marginBottom: 24 }}>
                <Title level={4} style={{ margin: 0 }}>Bàn làm việc Quản lý (Approval Dashboard)</Title>
                <Text type="secondary">Kiểm soát rủi ro và phê duyệt các trường hợp ngoại lệ (Exceptions)</Text>
            </div>

            <Card bordered={false} style={{ borderRadius: 12 }}>
                <Row style={{ marginBottom: 16 }}>
                    <Col span={8}>
                        <Input prefix={<SearchOutlined />} placeholder="Tìm kiếm hóa đơn cần duyệt..." />
                    </Col>
                </Row>
                <Tabs defaultActiveKey="1" items={items} />
            </Card>

            <Modal
                title={
                    <Space>
                        <ExclamationCircleOutlined style={{ color: '#faad14', fontSize: 20 }} />
                        <Text strong>Xác nhận Duyệt Ngoại Lệ (Override Risk)</Text>
                    </Space>
                }
                open={isOverrideModalVisible}
                onOk={handleOverrideSubmit}
                onCancel={() => {
                    setIsOverrideModalVisible(false);
                    form.resetFields();
                }}
                okText="Xác nhận & Ghi nhận Audit Log"
                cancelText="Hủy"
                okButtonProps={{ danger: true }}
            >
                <div style={{ marginBottom: 20, padding: 12, background: '#f5f5f5', borderRadius: 8 }}>
                    <Text strong>Hóa đơn: </Text> <Text type="secondary">{selectedInvoice?.invoiceNo}</Text><br />
                    <Text strong>Cảnh báo hệ thống: </Text> <Text type="danger">{selectedInvoice?.reason}</Text>
                </div>

                <Form form={form} layout="vertical">
                    <Form.Item
                        name="reason"
                        label={<span><Text type="danger">*</Text> Lý do phê duyệt ngoại lệ (Bắt buộc)</span>}
                        rules={[{ required: true, message: 'Vui lòng nhập lý do để lưu Audit Log!' }]}
                    >
                        <TextArea
                            rows={4}
                            placeholder="Nhập giải trình lý do duyệt bỏ qua cảnh báo hệ thống. Ví dụ: Kế toán đã liên hệ NCC xác nhận sai sót nhỏ trên hóa đơn nhưng không ảnh hưởng quyền lợi..."
                        />
                    </Form.Item>
                </Form>
                <Text type="secondary" style={{ fontSize: 12 }}>
                    Lưu ý: Hành động này sẽ được ghi nhận vĩnh viễn vào hệ thống [InvoiceAuditLog] dưới quyền của bạn.
                </Text>
            </Modal>
        </div>
    );
};

export default ApprovalDashboard;
