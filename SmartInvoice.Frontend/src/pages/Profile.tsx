import React, { useState, useEffect } from 'react';
import {
    Card, Typography, Tag, Tabs, message, Alert, Spin, Descriptions
} from 'antd';
import {
    UserOutlined, IdcardOutlined, SafetyCertificateOutlined, CodeSandboxOutlined
} from '@ant-design/icons';
import { userService, UserProfileDto, UpdateUserRequest } from '@/services/user';

const { Title, Text } = Typography;

const Profile: React.FC = () => {
    const [profile, setProfile] = useState<UserProfileDto | null>(null);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        fetchProfile();
    }, []);

    const fetchProfile = async () => {
        setLoading(true);
        try {
            const data = await userService.getMe();
            setProfile(data);
            setProfile(data);

            // Update local storage in case global avatar needs it
            const localUserStr = localStorage.getItem('user');
            if (localUserStr) {
                const localUser = JSON.parse(localUserStr);
                localUser.fullName = data.fullName;
                localUser.employeeId = data.employeeId;
                localStorage.setItem('user', JSON.stringify(localUser));
                window.dispatchEvent(new Event('storage')); // trigger update across tabs if needed
            }
        } catch (error: any) {
            message.error(error.response?.data?.message || 'Không thể tải thông tin cá nhân');
        } finally {
            setLoading(false);
        }
    };

    if (loading) {
        return <div style={{ textAlign: 'center', marginTop: 100 }}><Spin size="large" /></div>;
    }

    return (
        <div className="animate-fade-in-up" style={{ maxWidth: 800, margin: '0 auto' }}>
            <div style={{ marginBottom: 24, display: 'flex', alignItems: 'center', gap: 16 }}>
                <div style={{
                    width: 64, height: 64, borderRadius: 16,
                    background: 'linear-gradient(135deg, #1a4b8c, #15396d)',
                    display: 'flex', alignItems: 'center', justifyContent: 'center',
                    color: '#fff', fontSize: 24, fontWeight: 600
                }}>
                    {profile?.fullName.charAt(0)}
                </div>
                <div>
                    <Title level={3} style={{ margin: 0 }}>Hồ sơ cá nhân</Title>
                    <Text type="secondary">Quản lý nhận dạng và phân quyền hệ thống</Text>
                </div>
            </div>

            <Card bordered={false} style={{ borderRadius: 12 }}>
                <Tabs defaultActiveKey="1">
                    <Tabs.TabPane tab={<span><UserOutlined /> Thông tin cơ bản</span>} key="1">
                        <Descriptions
                            bordered
                            column={1}
                            style={{ marginTop: 16 }}
                            labelStyle={{ width: '250px', fontWeight: 500, backgroundColor: '#f8fafc' }}
                        >
                            <Descriptions.Item label={<span><UserOutlined style={{ marginRight: 8 }} />Email đăng nhập</span>}>
                                <Text strong>{profile?.email}</Text>
                            </Descriptions.Item>
                            <Descriptions.Item label={<span><CodeSandboxOutlined style={{ marginRight: 8 }} />Vai trò hệ thống</span>}>
                                <Tag color={profile?.role === 'CompanyAdmin' ? 'geekblue' : 'default'} style={{ borderRadius: 12 }}>
                                    {profile?.role === 'CompanyAdmin' ? 'Quản trị viên' : (profile?.role === 'SuperAdmin' ? 'Super Admin' : 'Nhân sự')}
                                </Tag>
                            </Descriptions.Item>
                            <Descriptions.Item label="Họ và Tên">
                                {profile?.fullName}
                            </Descriptions.Item>
                            <Descriptions.Item label={<span><IdcardOutlined style={{ marginRight: 8 }} />Mã Nhân sự</span>}>
                                {profile?.employeeId || <Text type="secondary">Chưa cập nhật</Text>}
                            </Descriptions.Item>
                        </Descriptions>
                    </Tabs.TabPane>

                    <Tabs.TabPane tab={<span><SafetyCertificateOutlined /> Quyền hạn & Bảo mật</span>} key="2">
                        <div style={{ marginTop: 16 }}>
                            <Alert
                                type="info"
                                showIcon
                                message="Bảo vệ hệ thống"
                                description="Quyền hạn của bạn được quản lý bởi Quản trị viên chi nhánh/công ty. Các điều chỉnh vui lòng liên hệ trực tiếp quản trị viên."
                                style={{ marginBottom: 24, borderRadius: 8 }}
                            />

                            <Title level={5}>Quyền hạn hiện tại (RBAC)</Title>
                            {profile?.permissions?.length ? (
                                <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8, marginTop: 16 }}>
                                    {profile.permissions.map((perm) => (
                                        <Tag color="geekblue" key={perm} style={{ padding: '4px 12px', fontSize: 13, borderRadius: 16 }}>
                                            {perm}
                                        </Tag>
                                    ))}
                                </div>
                            ) : (
                                <Text type="secondary">Chưa được cấp quyền hạn cụ thể nào.</Text>
                            )}
                        </div>
                    </Tabs.TabPane>
                </Tabs>
            </Card>
        </div>
    );
};

export default Profile;
