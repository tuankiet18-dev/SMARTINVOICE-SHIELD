import React, { useState, useEffect } from 'react';
import {
    Card, Table, Tag, Input, Typography, Row, Col, Button, Space, Drawer, Switch, message, Avatar, Modal, Form, Select, Popconfirm
} from 'antd';
import {
    SearchOutlined, TeamOutlined, UserAddOutlined, KeyOutlined, CheckOutlined, CloseOutlined, DeleteOutlined
} from '@ant-design/icons';
import { userService, CompanyMemberDto, CreateCompanyMemberDto } from '@/services/user';

const { Title, Text } = Typography;
const { Option } = Select;

const AVAILABLE_PERMISSIONS = [
    { key: 'dashboard:view', label: 'Xem Tổng quan (Dashboard)', desc: 'Được phép xem các biểu đồ và thống kê hệ thống.' },
    { key: 'company:view', label: 'Xem cài đặt công ty', desc: 'Được phép xem cấu hình, thông tin công ty.' },
    { key: 'company:manage', label: 'Quản lý cài đặt công ty', desc: 'Được phép thay đổi cấu hình, thông tin công ty.' },
    { key: 'invoice:view', label: 'Xem hóa đơn', desc: 'Được phép xem danh sách và chi tiết hóa đơn trên hệ thống.' },
    { key: 'invoice:upload', label: 'Tải lên hóa đơn', desc: 'Được phép tải file XML/PDF hoặc tạo mới hóa đơn.' },
    { key: 'invoice:edit', label: 'Chỉnh sửa hóa đơn', desc: 'Được phép cập nhật, sửa đổi thông tin của hóa đơn.' },
    { key: 'invoice:approve', label: 'Duyệt hóa đơn', desc: 'Được phép phê duyệt hóa đơn hợp lệ (Cấp 1 hoặc Cấp 2).' },
    { key: 'invoice:reject', label: 'Từ chối hóa đơn', desc: 'Được phép từ chối và yêu cầu làm lại hóa đơn sai sót.' },
    { key: 'invoice:override_risk', label: 'Bỏ qua rủi ro', desc: 'Được phép ép duyệt các hóa đơn bị hệ thống cảnh báo rủi ro.' },
    { key: 'report:export', label: 'Xuất báo cáo', desc: 'Được phép trích xuất dữ liệu ra file Excel MISA/Tổng hợp.' }
];

const TeamManagement: React.FC = () => {
    const [data, setData] = useState<CompanyMemberDto[]>([]);
    const [loading, setLoading] = useState(false);
    
    // Search and Filter State
    const [searchText, setSearchText] = useState('');
    const [inputSearch, setInputSearch] = useState(''); // Thêm state trung gian để giữ giá trị ô input
    const [statusFilter, setStatusFilter] = useState('all'); // all, active, inactive, deleted

    // Manage Permissions Drawer
    const [isDrawerOpen, setIsDrawerOpen] = useState(false);
    const [selectedUser, setSelectedUser] = useState<CompanyMemberDto | null>(null);

    // Create User Modal
    const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
    const [createForm] = Form.useForm();
    const [submitting, setSubmitting] = useState(false);

    useEffect(() => {
        fetchMembers();
    }, []);

    const fetchMembers = async () => {
        setLoading(true);
        try {
            const members = await userService.getCompanyMembers();
            setData(members);
        } catch (error: any) {
            message.error(error.response?.data?.message || 'Lỗi khi tải danh sách nhân sự');
        } finally {
            setLoading(false);
        }
    };

    const handleManagePerms = (record: CompanyMemberDto) => {
        setSelectedUser(record);
        setIsDrawerOpen(true);
    };

    const hasPermission = (user: CompanyMemberDto, permValue: string) => {
        if (!user.permissions) return false;
        return user.permissions.includes(permValue);
    };

    const handleTogglePerm = async (permKey: string, permValue: string, checked: boolean) => {
        if (!selectedUser) return;

        let currentPerms = selectedUser.permissions ? [...selectedUser.permissions] : [];
        if (checked && !currentPerms.includes(permValue)) {
            currentPerms.push(permValue);
        } else if (!checked) {
            currentPerms = currentPerms.filter(p => p !== permValue);
        }

        const updatedUser = { ...selectedUser, permissions: currentPerms };
        setSelectedUser(updatedUser);
        setData(prev => prev.map(u => u.id === updatedUser.id ? updatedUser : u));

        try {
            await userService.updateCompanyMember(updatedUser.id, {
                fullName: updatedUser.fullName,
                employeeId: updatedUser.employeeId || undefined,
                role: updatedUser.role,
                isActive: updatedUser.isActive,
                permissions: currentPerms
            });
            message.success(`Đã cập nhật quyền cho nhân viên ${updatedUser.fullName}`);
        } catch (error: any) {
            message.error(error.response?.data?.message || 'Cập nhật thất bại, đang hoàn tác...');
            // Rollback UI
            fetchMembers();
            if (isDrawerOpen) setIsDrawerOpen(false);
        }
    };

    const handleCreateUser = async (values: CreateCompanyMemberDto) => {
        setSubmitting(true);
        try {
            await userService.createCompanyMember(values);
            message.success('Đã gửi lời mời và tạo tài khoản thành công!');
            setIsCreateModalOpen(false);
            createForm.resetFields();
            fetchMembers();
        } catch (error: any) {
            message.error(error.response?.data?.message || 'Tạo nhân viên thất bại');
        } finally {
            setSubmitting(false);
        }
    };

    const handleToggleActive = async (record: CompanyMemberDto, checked: boolean) => {
        try {
            await userService.updateCompanyMember(record.id, {
                fullName: record.fullName,
                employeeId: record.employeeId || undefined,
                role: record.role,
                permissions: record.permissions || undefined,
                isActive: checked // Gửi trạng thái mới (true/false) lên Backend
            });
            message.success(`Đã ${checked ? 'mở khóa' : 'ngừng hoạt động'} tài khoản ${record.fullName}`);
            fetchMembers(); // Tải lại danh sách để UI tự động cập nhật màu sắc
        } catch (error: any) {
            message.error(error.response?.data?.message || 'Thao tác thất bại');
        }
    };

    const handleDeleteUser = async (id: string) => {
        try {
            await userService.deleteCompanyMember(id);
            message.success('Đã xóa nhân viên khỏi hệ thống');
            fetchMembers();
        } catch (error: any) {
            message.error(error.response?.data?.message || 'Xóa nhân viên thất bại');
        }
    };

    const columns = [
        {
            title: 'Nhân viên',
            dataIndex: 'fullName',
            key: 'fullName',
            render: (text: string, record: CompanyMemberDto) => (
                <Space>
                    <Avatar style={{ backgroundColor: '#1a4b8c' }}>{text?.charAt(0)}</Avatar>
                    <div>
                        <Text strong>{text}</Text>
                        <br />
                        <Text type="secondary" style={{ fontSize: 12 }}>{record.email}</Text>
                    </div>
                </Space>
            ),
        },
        {
            title: 'Mã NV',
            dataIndex: 'employeeId',
            key: 'employeeId',
            render: (text: string) => <Text>{text || '-'}</Text>
        },
        {
            title: 'Phân quyền',
            dataIndex: 'role',
            key: 'role',
            render: (role: string) => {
                let roleName = 'Nhân sự';
                let color = 'default';

                if (role === 'CompanyAdmin') {
                    roleName = 'Quản trị viên';
                    color = 'geekblue';
                } else if (role === 'ChiefAccountant') {
                    roleName = 'Kế toán trưởng';
                    color = 'purple';
                } else if (role === 'Accountant') {
                    roleName = 'Kế toán viên';
                    color = 'cyan';
                }

                return (
                    <Tag color={color} style={{ borderRadius: 12 }}>
                        {roleName}
                    </Tag>
                );
            },
        },
        {
            title: 'Trạng thái',
            dataIndex: 'isActive',
            key: 'isActive',
            render: (isActive: boolean, record: CompanyMemberDto) => {
                if (record.isDeleted) {
                    return <Tag color="error">Đã bị xóa</Tag>;
                }
                return (
                <Popconfirm
                    title={isActive ? "Ngừng hoạt động nhân viên này?" : "Mở khóa nhân viên này?"}
                    description={isActive 
                        ? "Nhân viên này sẽ không thể đăng nhập" 
                        : "Đảm bảo quyền đăng nhập của nhân viên khi mở khóa"}
                    onConfirm={() => handleToggleActive(record, !isActive)}
                    okText="Đồng ý"
                    cancelText="Hủy"
                    // Chặn không cho Admin tự khóa chính mình
                    disabled={record.role === 'CompanyAdmin'} 
                >
                    <Switch 
                        checked={isActive} 
                        checkedChildren="Hoạt động" 
                        unCheckedChildren="Đã khóa"
                        disabled={record.role === 'CompanyAdmin'}
                        style={!isActive ? { backgroundColor: '#ff4d4f' } : {}}
                    />
                </Popconfirm>
            )},
        },
        {
            title: 'Hành động',
            key: 'action',
            render: (_, record: CompanyMemberDto) => (
                <Space>
                    <Button
                        type="default"
                        icon={<KeyOutlined />}
                        size="small"
                        onClick={() => handleManagePerms(record)}
                        disabled={record.role === 'CompanyAdmin' || record.isDeleted}
                    >
                        Phân quyền
                    </Button>
                    <Popconfirm
                        title="Xóa vĩnh viễn nhân viên này?"
                        description="Dữ liệu sẽ bị ẩn khỏi hệ thống. Chỉ nên dùng khi nhập sai email!"
                        onConfirm={() => handleDeleteUser(record.id)}
                        okText="Xóa vĩnh viễn"
                        cancelText="Hủy"
                        okButtonProps={{ danger: true }}
                        disabled={record.role === 'CompanyAdmin' || record.isDeleted}
                    >
                        {/* Nút Xóa giờ chỉ mang tính chất dọn dẹp data rác */}
                        <Button type="text" danger icon={<DeleteOutlined />} size="small" disabled={record.role === 'CompanyAdmin' || record.isDeleted} />
                    </Popconfirm>
                </Space>
            ),
        },
    ];

    const filteredData = data.filter(user => {
        const matchText = user.fullName.toLowerCase().includes(searchText.toLowerCase()) || 
                          user.email.toLowerCase().includes(searchText.toLowerCase()) || 
                          (user.employeeId && user.employeeId.toLowerCase().includes(searchText.toLowerCase()));
        
        let matchStatus = true;
        if (statusFilter === 'active') matchStatus = user.isActive && !user.isDeleted;
        else if (statusFilter === 'inactive') matchStatus = !user.isActive && !user.isDeleted;
        else if (statusFilter === 'deleted') matchStatus = user.isDeleted;
        else if (statusFilter === 'all') matchStatus = !user.isDeleted; // Mặc định ẩn bị xóa

        return matchText && matchStatus;
    });

    return (
        <div className="animate-fade-in-up">
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 24 }}>
                <div>
                    <Title level={4} style={{ margin: 0 }}>Quản lý đội ngũ & Phân quyền</Title>
                    <Text type="secondary">Cấp phát tài khoản và điều chỉnh quyền thao tác nghiệp vụ</Text>
                </div>
                <Button type="primary" icon={<UserAddOutlined />} onClick={() => setIsCreateModalOpen(true)}>
                    Mời nhân viên
                </Button>
            </div>

            <Card variant="borderless" style={{ borderRadius: 12 }}>
                <Row style={{ marginBottom: 16 }}>
                    <Col span={8}>
                        <Input.Search 
                            placeholder="Tìm kiếm theo Tên hoặc Email..." 
                            value={inputSearch}
                            onChange={(e) => setInputSearch(e.target.value)}
                            onSearch={(value) => setSearchText(value)}
                            enterButton={<SearchOutlined />}
                            allowClear
                            onClear={() => {
                                setInputSearch('');
                                setSearchText('');
                            }}
                        />
                    </Col>
                    <Col span={6}>
                        <Select 
                            value={statusFilter} 
                            onChange={setStatusFilter} 
                            style={{ width: '100%' }}
                            options={[
                                { value: 'all', label: 'Tất cả nhân sự (Trừ bị xóa)' },
                                { value: 'active', label: 'Đang hoạt động' },
                                { value: 'inactive', label: 'Đã khóa' },
                                { value: 'deleted', label: 'Đã bị xóa' },
                            ]}
                        />
                    </Col>
                </Row>
                <Table columns={columns} dataSource={filteredData} rowKey="id" loading={loading} />
            </Card>

            {/* Create User Modal */}
            <Modal
                title="Mời nhân sự mới"
                open={isCreateModalOpen}
                onCancel={() => setIsCreateModalOpen(false)}
                footer={null}
                destroyOnHidden
            >
                <Form
                    form={createForm}
                    layout="vertical"
                    onFinish={handleCreateUser}
                    initialValues={{ role: 'Accountant' }}
                >
                    <Form.Item name="fullName" label="Họ và Tên" rules={[{ required: true, message: 'Vui lòng nhập họ tên' }]}>
                        <Input placeholder="Vd: Nguyễn Văn A" />
                    </Form.Item>
                    <Form.Item name="email" label="Email công việc" rules={[{ required: true, message: 'Vui lòng nhập email', type: 'email' }]}>
                        <Input placeholder="Vd: nva@congty.com" />
                    </Form.Item>
                    <Row gutter={16}>
                        <Col span={12}>
                            <Form.Item name="employeeId" label="Mã Nhân sự" rules={[{ required: true, message: 'Vui lòng nhập mã nhân viên' }]}>
                                <Input placeholder="Vd: NV001" />
                            </Form.Item>
                        </Col>
                        <Col span={12}>
                            <Form.Item 
                                name="role" 
                                label="Chức danh (Vai trò)" 
                                rules={[{ required: true, message: 'Vui lòng chọn chức danh' }]}
                            >
                                <Select 
                                    popupMatchSelectWidth={false} // Cho phép bảng xổ xuống to hơn cái ô input
                                    optionLabelProp="label"       // Khi chọn xong, chỉ hiển thị cái nhãn ngắn gọn
                                >
                                    <Option value="CompanyAdmin" label="Giám đốc">
                                        <div style={{ whiteSpace: 'normal', lineHeight: '1.4', padding: '4px 0', maxWidth: 300 }}>
                                            <Text strong>Giám đốc / Quản trị công ty</Text><br/>
                                            <Text type="secondary" style={{ fontSize: 12 }}>Toàn quyền hệ thống, nhân sự và duyệt hóa đơn.</Text>
                                        </div>
                                    </Option>
                                    <Option value="ChiefAccountant" label="Kế toán trưởng">
                                        <div style={{ whiteSpace: 'normal', lineHeight: '1.4', padding: '4px 0', maxWidth: 300 }}>
                                            <Text strong>Kế toán trưởng</Text><br/>
                                            <Text type="secondary" style={{ fontSize: 12 }}>Quản lý phòng kế toán, xét duyệt hóa đơn cấp cao.</Text>
                                        </div>
                                    </Option>
                                    <Option value="Accountant" label="Kế toán viên">
                                        <div style={{ whiteSpace: 'normal', lineHeight: '1.4', padding: '4px 0', maxWidth: 300 }}>
                                            <Text strong>Kế toán viên</Text><br/>
                                            <Text type="secondary" style={{ fontSize: 12 }}>Tải lên, xử lý dữ liệu và gửi yêu cầu duyệt.</Text>
                                        </div>
                                    </Option>
                                </Select>
                            </Form.Item>
                        </Col>
                    </Row>
                    <div style={{ marginTop: 16, display: 'flex', justifyContent: 'flex-end', gap: 8 }}>
                        <Button onClick={() => setIsCreateModalOpen(false)}>Hủy bỏ</Button>
                        <Button type="primary" htmlType="submit" loading={submitting}>Tạo tài khoản</Button>
                    </div>
                </Form>
            </Modal>

            <Drawer
                title={
                    <Space>
                        <KeyOutlined style={{ color: '#1a4b8c' }} />
                        <Text strong>Cấu hình quyền hạn: {selectedUser?.fullName}</Text>
                    </Space>
                }
                placement="right"
                onClose={() => setIsDrawerOpen(false)}
                open={isDrawerOpen}
                size={400}
            >
                {selectedUser && (
                    <Space orientation="vertical" style={{ width: '100%' }} size={24}>
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
                                {AVAILABLE_PERMISSIONS.map(perm => (
                                    <div key={perm.key} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                                        <div style={{ paddingRight: 16 }}>
                                            <Text strong>{perm.label}</Text> <Text type="secondary" style={{ fontSize: 12 }}>({perm.key})</Text><br />
                                            <Text type="secondary" style={{ fontSize: 13 }}>{perm.desc}</Text>
                                        </div>
                                        <Switch
                                            checkedChildren={<CheckOutlined />} 
                                            unCheckedChildren={<CloseOutlined />}
                                            checked={hasPermission(selectedUser, perm.key)}
                                            onChange={(checked) => handleTogglePerm(perm.key, perm.key, checked)}
                                        />
                                    </div>
                                ))}
                            </div>
                        </div>
                    </Space>
                )}
            </Drawer>
        </div>
    );
};

export default TeamManagement;
