import React, { useState } from 'react';
import {
    Card, Table, Tag, Input, Typography, Row, Col, Button, Space, Drawer, Switch, message, Avatar
} from 'antd';
import {
    SearchOutlined, TeamOutlined, UserAddOutlined, KeyOutlined, CheckOutlined, CloseOutlined
} from '@ant-design/icons';

const { Title, Text } = Typography;

const initialTeamData = [
    { key: '1', name: 'Nguyễn Văn A', email: 'vana@congtyabc.com', role: 'Member', status: 'Active', perms: { upload: true, edit: true, view: true } },
    { key: '2', name: 'Trần Thị B', email: 'tranb@congtyabc.com', role: 'Member', status: 'Active', perms: { upload: false, edit: true, view: true } },
    { key: '3', name: 'Lê Văn C', email: 'levanc@congtyabc.com', role: 'CompanyAdmin', status: 'Active', perms: { upload: true, edit: true, view: true, approve: true } },
    { key: '4', name: 'Hoàng Thị D', email: 'hoangd@congtyabc.com', role: 'Member', status: 'Inactive', perms: { upload: false, edit: false, view: false } },
];

const TeamManagement: React.FC = () => {
    const [data, setData] = useState(initialTeamData);
    const [isDrawerOpen, setIsDrawerOpen] = useState(false);
    const [selectedUser, setSelectedUser] = useState<any>(null);

    const handleManagePerms = (record: any) => {
        setSelectedUser(record);
        setIsDrawerOpen(true);
    };

    const handleTogglePerm = (permKey: string, checked: boolean) => {
        if (!selectedUser) return;

        const updatedUser = {
            ...selectedUser,
            perms: { ...selectedUser.perms, [permKey]: checked }
        };

        setSelectedUser(updatedUser);
        setData(prev => prev.map(u => u.key === updatedUser.key ? updatedUser : u));
        message.success(`Đã cập nhật quyền [${permKey}] cho nhân viên ${updatedUser.name}`);
    };

    const columns = [
        {
            title: 'Nhân viên',
            dataIndex: 'name',
            key: 'name',
            render: (text: string, record: any) => (
                <Space>
                    <Avatar style={{ backgroundColor: '#1a4b8c' }}>{text.charAt(0)}</Avatar>
                    <div>
                        <Text strong>{text}</Text>
                        <br />
                        <Text type="secondary" style={{ fontSize: 12 }}>{record.email}</Text>
                    </div>
                </Space>
            ),
        },
        {
            title: 'Phân quyền',
            dataIndex: 'role',
            key: 'role',
            render: (role: string) => (
                <Tag color={role === 'CompanyAdmin' ? 'geekblue' : 'default'} style={{ borderRadius: 12 }}>
                    {role === 'CompanyAdmin' ? 'Quản trị viên' : 'Nhân sự'}
                </Tag>
            ),
        },
        {
            title: 'Trạng thái',
            dataIndex: 'status',
            key: 'status',
            render: (status: string) => (
                <Tag color={status === 'Active' ? 'green' : 'red'}>{status}</Tag>
            ),
        },
        {
            title: 'Quyền hạn (Permissions)',
            key: 'action',
            render: (_, record: any) => (
                <Button
                    type="default"
                    icon={<KeyOutlined />}
                    size="small"
                    onClick={() => handleManagePerms(record)}
                    disabled={record.role === 'CompanyAdmin'} // Admin has explicit full access typically
                >
                    Tùy chỉnh phân quyền
                </Button>
            ),
        },
    ];

    return (
        <div className="animate-fade-in-up">
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 24 }}>
                <div>
                    <Title level={4} style={{ margin: 0 }}>Quản lý đội ngũ & Phân quyền</Title>
                    <Text type="secondary">Cấp phát tài khoản và điều chỉnh quyền thao tác nghiệp vụ</Text>
                </div>
                <Button type="primary" icon={<UserAddOutlined />}>Mời nhân viên</Button>
            </div>

            <Card bordered={false} style={{ borderRadius: 12 }}>
                <Row style={{ marginBottom: 16 }}>
                    <Col span={8}>
                        <Input prefix={<SearchOutlined />} placeholder="Tìm kiếm theo Tên hoặc Email..." />
                    </Col>
                </Row>
                <Table columns={columns} dataSource={data} />
            </Card>

            <Drawer
                title={
                    <Space>
                        <KeyOutlined style={{ color: '#1a4b8c' }} />
                        <Text strong>Cấu hình quyền hạn: {selectedUser?.name}</Text>
                    </Space>
                }
                placement="right"
                onClose={() => setIsDrawerOpen(false)}
                open={isDrawerOpen}
                width={400}
            >
                {selectedUser && (
                    <Space direction="vertical" style={{ width: '100%' }} size={24}>
                        <div style={{ background: '#f5f5f5', padding: 16, borderRadius: 8 }}>
                            <Text strong style={{ fontSize: 13, color: '#1a4b8c' }}>THÔNG TIN NHÂN SỰ</Text>
                            <div style={{ marginTop: 8 }}>
                                <Text type="secondary">Email: </Text><Text>{selectedUser.email}</Text><br />
                                <Text type="secondary">Vai trò: </Text><Tag color="blue">{selectedUser.role}</Tag>
                            </div>
                        </div>

                        <div>
                            <Text strong style={{ fontSize: 13, color: '#1a4b8c' }}>CHI TIẾT QUYỀN HẠN (RBAC)</Text>
                            <div style={{ marginTop: 16, display: 'flex', flexDirection: 'column', gap: 16 }}>

                                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                                    <div>
                                        <Text strong>invoice:view</Text><br />
                                        <Text type="secondary" style={{ fontSize: 12 }}>Được phép xem danh sách và chi tiết hóa đơn</Text>
                                    </div>
                                    <Switch
                                        checkedChildren={<CheckOutlined />} unCheckedChildren={<CloseOutlined />}
                                        checked={selectedUser.perms.view}
                                        onChange={(checked) => handleTogglePerm('view', checked)}
                                    />
                                </div>

                                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                                    <div>
                                        <Text strong>invoice:upload</Text><br />
                                        <Text type="secondary" style={{ fontSize: 12 }}>Được phép tải lên hóa đơn mới (XML/PDF/Ảnh)</Text>
                                    </div>
                                    <Switch
                                        checkedChildren={<CheckOutlined />} unCheckedChildren={<CloseOutlined />}
                                        checked={selectedUser.perms.upload}
                                        onChange={(checked) => handleTogglePerm('upload', checked)}
                                    />
                                </div>

                                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                                    <div>
                                        <Text strong>invoice:edit</Text><br />
                                        <Text type="secondary" style={{ fontSize: 12 }}>Được chỉnh sửa thông tin rác từ OCR (Inline Edit)</Text>
                                    </div>
                                    <Switch
                                        checkedChildren={<CheckOutlined />} unCheckedChildren={<CloseOutlined />}
                                        checked={selectedUser.perms.edit}
                                        onChange={(checked) => handleTogglePerm('edit', checked)}
                                    />
                                </div>

                            </div>
                        </div>
                    </Space>
                )}
            </Drawer>
        </div>
    );
};

export default TeamManagement;
