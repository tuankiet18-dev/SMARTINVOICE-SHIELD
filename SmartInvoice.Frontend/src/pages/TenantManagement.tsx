import React, { useState } from 'react';
import {
    Card, Table, Tag, Input, Typography, Row, Col, Button, Space, Modal, message, Progress, Badge
} from 'antd';
import {
    SearchOutlined, StopOutlined, CloudServerOutlined, SafetyCertificateOutlined
} from '@ant-design/icons';

const { Title, Text } = Typography;

const tenantData = [
    { key: '1', name: 'Công ty TNHH Phần mềm ABC', email: 'admin@abc.com', plan: 'Pro', quota: 85, status: 'Active', invoices: 12500 },
    { key: '2', name: 'Công ty CP Công nghệ XYZ', email: 'contact@xyz.com', plan: 'Free', quota: 40, status: 'Active', invoices: 450 },
    { key: '3', name: 'DN Tư nhân Vận tải Minh Phương', email: 'ketoan@minhphuong.vn', plan: 'Pro', quota: 95, status: 'Warning', invoices: 22000 },
    { key: '4', name: 'Công ty TNHH SX TM Dịch vụ AAA', email: 'hello@aaa.com', plan: 'Free', quota: 15, status: 'Locked', invoices: 120 },
];

const TenantManagement: React.FC = () => {
    const [data, setData] = useState(tenantData);

    const handleToggleLock = (record: any) => {
        Modal.confirm({
            title: record.status === 'Locked' ? 'Mở khóa tài khoản?' : 'Khóa tài khoản Tenant?',
            icon: record.status === 'Locked' ? <SafetyCertificateOutlined style={{ color: '#52c41a' }} /> : <StopOutlined style={{ color: '#ff4d4f' }} />,
            content: `Bạn có chắc chắn muốn ${record.status === 'Locked' ? 'mở khóa' : 'khóa'} hệ thống của Doanh nghiệp "${record.name}"?`,
            okText: 'Xác nhận',
            cancelText: 'Hủy',
            okType: record.status === 'Locked' ? 'primary' : 'danger',
            onOk() {
                const newStatus = record.status === 'Locked' ? 'Active' : 'Locked';
                setData(prev => prev.map(t => t.key === record.key ? { ...t, status: newStatus } : t));
                message.success(`Đã ${record.status === 'Locked' ? 'mở khóa' : 'khóa'} thành công Tenant: ${record.name}`);
            },
        });
    };

    const columns = [
        {
            title: 'Doanh nghiệp (Tenant)',
            dataIndex: 'name',
            key: 'name',
            render: (text: string, record: any) => (
                <div>
                    <Text strong style={{ color: '#1a4b8c' }}>{text}</Text>
                    <br />
                    <Text type="secondary" style={{ fontSize: 12 }}>{record.email}</Text>
                </div>
            ),
        },
        {
            title: 'Gói Cước',
            dataIndex: 'plan',
            key: 'plan',
            render: (plan: string) => (
                <Tag color={plan === 'Pro' ? 'gold' : 'default'} style={{ borderRadius: 12, fontWeight: 'bold' }}>
                    {plan.toUpperCase()}
                </Tag>
            ),
        },
        {
            title: 'Tổng hóa đơn',
            dataIndex: 'invoices',
            key: 'invoices',
            render: (val: number) => <Text>{val.toLocaleString()} HĐ</Text>,
        },
        {
            title: 'Dung lượng S3 (Storage)',
            dataIndex: 'quota',
            key: 'quota',
            render: (quota: number) => (
                <div style={{ width: 150 }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 4, fontSize: 12 }}>
                        <Text type="secondary">Đã dùng</Text>
                        <Text strong type={quota > 90 ? 'danger' : 'secondary'}>{quota}%</Text>
                    </div>
                    <Progress percent={quota} showInfo={false} size="small" status={quota > 90 ? 'exception' : 'active'} />
                </div>
            ),
        },
        {
            title: 'Trạng thái',
            dataIndex: 'status',
            key: 'status',
            render: (status: string) => {
                let color = 'green';
                if (status === 'Locked') color = 'red';
                if (status === 'Warning') color = 'warning';
                return <Badge status={color as any} text={status} />;
            },
        },
        {
            title: 'Hành động',
            key: 'action',
            render: (_, record: any) => (
                <Space>
                    <Button size="small" icon={<CloudServerOutlined />}>Chi tiết</Button>
                    <Button
                        size="small"
                        danger={record.status !== 'Locked'}
                        type={record.status === 'Locked' ? 'primary' : 'default'}
                        icon={record.status === 'Locked' ? <SafetyCertificateOutlined /> : <StopOutlined />}
                        onClick={() => handleToggleLock(record)}
                    >
                        {record.status === 'Locked' ? 'Mở khóa' : 'Khóa'}
                    </Button>
                </Space>
            ),
        },
    ];

    return (
        <div className="animate-fade-in-up">
            <div style={{ marginBottom: 24 }}>
                <Title level={4} style={{ margin: 0 }}>Quản lý Doanh Nghiệp (Tenants)</Title>
                <Text type="secondary">Giám sát tài nguyên S3 và trạng thái đăng ký của các công ty trên nền tảng SmartInvoice Shield.</Text>
            </div>

            <Card bordered={false} style={{ borderRadius: 12 }}>
                <Row style={{ marginBottom: 16 }}>
                    <Col span={8}>
                        <Input prefix={<SearchOutlined />} placeholder="Tìm kiếm công ty, email..." />
                    </Col>
                </Row>
                <Table columns={columns} dataSource={data} />
            </Card>
        </div>
    );
};

export default TenantManagement;
