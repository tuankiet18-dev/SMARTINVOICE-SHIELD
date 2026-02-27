import React, { useState } from 'react';
import {
    Card, Table, Tag, Input, Typography, Row, Col, Button, Space, Modal, Form, message
} from 'antd';
import {
    SearchOutlined, DeleteOutlined, PlusOutlined, SafetyCertificateOutlined
} from '@ant-design/icons';

const { Title, Text } = Typography;

const initialBlacklist = [
    { key: '1', taxCode: '0311854540-009', name: 'CÔNG TY CP ĐẦU TƯ VIÊN NGỌC MỚI', reason: 'Doanh nghiệp bỏ trốn khỏi địa chỉ kinh doanh', dateAdded: '15/01/2026', addedBy: 'SystemAdmin' },
    { key: '2', taxCode: '1100123456', name: 'Công ty TNHH Ma', reason: 'Nằm trong danh sách cảnh báo hóa đơn khống của TCT', dateAdded: '02/02/2026', addedBy: 'SystemAdmin' },
];

const GlobalBlacklist: React.FC = () => {
    const [data, setData] = useState(initialBlacklist);
    const [isModalOpen, setIsModalOpen] = useState(false);
    const [form] = Form.useForm();

    const handleAdd = () => {
        form.validateFields().then(values => {
            const newRecord = {
                key: Date.now().toString(),
                taxCode: values.taxCode,
                name: values.name,
                reason: values.reason,
                dateAdded: new Date().toLocaleDateString('vi-VN'),
                addedBy: 'SystemAdmin',
            };
            setData([...data, newRecord]);
            message.success(`Đã thêm MST ${values.taxCode} vào Blacklist toàn cầu.`);
            setIsModalOpen(false);
            form.resetFields();
        });
    };

    const handleRemove = (record: any) => {
        Modal.confirm({
            title: 'Xóa khỏi Blacklist?',
            content: `Bạn có chắc chắn muốn gỡ MST ${record.taxCode} khỏi danh sách đen? Các Tenant sẽ có thể nhận hóa đơn từ doanh nghiệp này trở lại.`,
            okText: 'Xác nhận xóa',
            okType: 'danger',
            cancelText: 'Hủy',
            onOk() {
                setData(prev => prev.filter(item => item.key !== record.key));
                message.success(`Đã xóa MST ${record.taxCode} khỏi Blacklist.`);
            }
        });
    };

    const columns = [
        {
            title: 'Mã Số Thuế (MST)',
            dataIndex: 'taxCode',
            key: 'taxCode',
            render: (text: string) => <Tag color="error" style={{ fontSize: 13, padding: '4px 8px' }}>{text}</Tag>,
        },
        {
            title: 'Tên Doanh nghiệp',
            dataIndex: 'name',
            key: 'name',
            render: (text: string) => <Text strong>{text}</Text>,
        },
        {
            title: 'Lý do đưa vào Blacklist',
            dataIndex: 'reason',
            key: 'reason',
            render: (text: string) => <Text type="secondary">{text}</Text>,
        },
        {
            title: 'Ngày thêm / Người thêm',
            key: 'audit',
            render: (_, record: any) => (
                <span style={{ fontSize: 12 }}>
                    {record.dateAdded} <br /> <Text type="secondary">bởi {record.addedBy}</Text>
                </span>
            ),
        },
        {
            title: 'Hành động',
            key: 'action',
            width: 100,
            render: (_, record: any) => (
                <Button danger icon={<DeleteOutlined />} size="small" onClick={() => handleRemove(record)}>Gỡ bỏ</Button>
            ),
        },
    ];

    return (
        <div className="animate-fade-in-up">
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 24 }}>
                <div>
                    <Title level={4} style={{ margin: 0, color: '#d63031' }}>Quản Trị Rủi Ro Tập Trung (Global Blacklist)</Title>
                    <Text type="secondary">Cập nhật danh sách đen các MST rủi ro. Mọi hóa đơn chứa MST này lập tức bị chặn (Red Risk) trên toàn bộ hệ thống các Tenant.</Text>
                </div>
                <Button type="primary" danger icon={<PlusOutlined />} onClick={() => setIsModalOpen(true)}>Thêm MST Rủi Ro</Button>
            </div>

            <Card bordered={false} style={{ borderRadius: 12, borderTop: '3px solid #d63031' }}>
                <Row style={{ marginBottom: 16 }}>
                    <Col span={8}>
                        <Input prefix={<SearchOutlined />} placeholder="Tra cứu MST, tên công ty trong Blacklist..." />
                    </Col>
                </Row>
                <Table columns={columns} dataSource={data} />
            </Card>

            <Modal
                title={
                    <Space>
                        <SafetyCertificateOutlined style={{ color: '#d63031', fontSize: 20 }} />
                        <Text strong>Thêm Mã Số Thuế vào Blacklist</Text>
                    </Space>
                }
                open={isModalOpen}
                onOk={handleAdd}
                onCancel={() => setIsModalOpen(false)}
                okText="Thêm vào Blacklist"
                okButtonProps={{ danger: true }}
            >
                <Form form={form} layout="vertical" style={{ marginTop: 24 }}>
                    <Form.Item name="taxCode" label="Mã Số Thuế (Bắt buộc)" rules={[{ required: true }]}>
                        <Input placeholder="Nhập mã số thuế (Vd: 0101234567)" />
                    </Form.Item>
                    <Form.Item name="name" label="Tên Doanh Nghiệp" rules={[{ required: true }]}>
                        <Input placeholder="Tên đơn vị rủi ro" />
                    </Form.Item>
                    <Form.Item name="reason" label="Lý do Vi phạm / Cảnh báo (Hiển thị cho Tenant)" rules={[{ required: true }]}>
                        <Input.TextArea rows={3} placeholder="Mô tả lý do: VD: Nợ thuế quá hạn, doanh nghiệp ma..." />
                    </Form.Item>
                </Form>
            </Modal>
        </div>
    );
};

export default GlobalBlacklist;
