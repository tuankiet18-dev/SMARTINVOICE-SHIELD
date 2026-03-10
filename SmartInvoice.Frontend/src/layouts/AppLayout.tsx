import React, { useState } from 'react';
import { Layout, Menu, Avatar, Dropdown, Badge, Button, Typography } from 'antd';
import {
  DashboardOutlined,
  FileTextOutlined,
  UploadOutlined,
  BarChartOutlined,
  SettingOutlined,
  BellOutlined,
  UserOutlined,
  LogoutOutlined,
  MenuFoldOutlined,
  MenuUnfoldOutlined,
  SafetyCertificateOutlined,
  AuditOutlined,
  TeamOutlined,
  AppstoreOutlined,
  BankOutlined,
} from '@ant-design/icons';
import { useNavigate, useLocation, Outlet } from 'react-router-dom';
import { useAuth } from '@/contexts/AuthContext';

const { Sider, Header, Content } = Layout;
const { Text } = Typography;

const AppLayout: React.FC = () => {
  const [collapsed, setCollapsed] = useState(false);
  const navigate = useNavigate();
  const location = useLocation();
  const { user, logout } = useAuth();

  const isCompanyAdmin = user?.role === 'CompanyAdmin';

  const menuItems = [
    { key: '/app/dashboard', icon: <DashboardOutlined />, label: 'Tổng quan' },
    { key: '/app/invoices', icon: <FileTextOutlined />, label: 'Hóa đơn' },
    { key: '/app/upload', icon: <UploadOutlined />, label: 'Tải lên' },
    { key: '/app/validation', icon: <SafetyCertificateOutlined />, label: 'Rà soát rủi ro' },
    { key: '/app/reports', icon: <BarChartOutlined />, label: 'Báo cáo' },
    ...(isCompanyAdmin ? [
      { key: '/app/approval-dashboard', icon: <AppstoreOutlined />, label: 'Duyệt ngoại lệ' },
      { key: '/app/team', icon: <TeamOutlined />, label: 'Quản lý Team' },
      { key: 'divider-1', type: 'divider' as const },
      { key: '/app/audit-log', icon: <AuditOutlined />, label: 'Nhật ký Audit' },
    ] : []),
    { key: 'divider-settings', type: 'divider' as const },
    { key: '/app/settings', icon: <SettingOutlined />, label: 'Cài đặt' },
  ];

  const userMenuItems = [
    { key: 'profile', icon: <UserOutlined />, label: 'Hồ sơ cá nhân' },
    { key: 'settings', icon: <SettingOutlined />, label: 'Cài đặt' },
    { type: 'divider' as const, key: 'div' },
    { key: 'logout', icon: <LogoutOutlined />, label: 'Đăng xuất', danger: true },
  ];

  const handleUserMenuClick = async ({ key }: { key: string }) => {
    if (key === 'logout') {
      await logout();
      navigate('/login');
    } else if (key === 'profile') {
      navigate('/app/profile');
    } else {
      console.log('Clicked', key);
    }
  };

  return (
    <Layout style={{ minHeight: '100vh' }}>
      <Sider
        trigger={null}
        collapsible
        collapsed={collapsed}
        width={250}
        style={{
          background: '#FFFFFF',
          borderRight: '1px solid #E2E8F0',
          overflow: 'auto',
          height: '100vh',
          position: 'fixed',
          left: 0,
          top: 0,
          bottom: 0,
          zIndex: 100,
          boxShadow: '0 4px 20px rgba(0,0,0,0.04)',
        }}
      >
        <div style={{
          padding: collapsed ? '20px 12px' : '24px 20px',
          display: 'flex',
          alignItems: 'center',
          justifyContent: collapsed ? 'center' : 'flex-start',
          gap: 12,
          borderBottom: '1px solid rgba(0,0,0,0.03)',
          marginBottom: 12,
          height: 80,
        }}>
          {collapsed ? (
            <img src="/logo-transparent.png" alt="Logo" style={{ width: 38, height: 38, objectFit: 'contain' }} />
          ) : (
            <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
               <img src="/logo-transparent.png" alt="SmartInvoice Logo" style={{ height: 38, width: 'auto', objectFit: 'contain' }} />
               <div>
                  <Text strong style={{ color: '#202224', fontSize: 16, display: 'block', lineHeight: 1.1, letterSpacing: '-0.02em' }}>
                     SmartInvoice
                  </Text>
                  <Text style={{ color: '#828282', fontSize: 12, fontWeight: 500 }}>Shield</Text>
               </div>
            </div>
          )}
        </div>

        <Menu
          theme="light"
          mode="inline"
          selectedKeys={[location.pathname]}
          items={menuItems}
          onClick={({ key }) => navigate(key)}
          style={{
            background: 'transparent',
            border: 'none',
            padding: '0 12px',
          }}
          className="saas-menu"
        />
      </Sider>

      <Layout style={{ marginLeft: collapsed ? 80 : 250, transition: 'margin-left 0.2s cubic-bezier(0.2, 0, 0, 1)' }}>
        <Header style={{
          background: '#FFFFFF',
          padding: '0 24px',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          position: 'sticky',
          top: 0,
          zIndex: 99,
          height: 64,
          borderBottom: '1px solid #E2E8F0',
          boxShadow: '0 2px 10px rgba(0,0,0,0.02)',
        }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 16 }}>
            <Button
              type="text"
              icon={collapsed ? <MenuUnfoldOutlined /> : <MenuFoldOutlined />}
              onClick={() => setCollapsed(!collapsed)}
              style={{ fontSize: 16, width: 40, height: 40 }}
            />
            {user?.companyName && (
              <div style={{ 
                display: 'flex', 
                alignItems: 'center', 
                gap: 8, 
                padding: '6px 16px', 
                background: '#F8FAFC', 
                borderRadius: '8px', 
                border: '1px solid #E2E8F0' 
              }}>
                <BankOutlined style={{ color: '#4880FF' }} />
                <Text strong style={{ color: '#1E293B', fontSize: 14 }}>{user.companyName}</Text>
              </div>
            )}
          </div>

          <div style={{ display: 'flex', alignItems: 'center', gap: 16 }}>
            <Badge count={3} size="small">
              <Button type="text" icon={<BellOutlined />} style={{ fontSize: 18 }} />
            </Badge>

            <Dropdown menu={{ items: userMenuItems, onClick: handleUserMenuClick }} placement="bottomRight" trigger={['click']}>
              <div style={{
                display: 'flex', alignItems: 'center', gap: 12, cursor: 'pointer',
                padding: '4px 12px', borderRadius: 12, transition: 'background 0.2s',
              }} className="hover:bg-slate-100/80">
                <Avatar size={36} style={{ background: '#4880FF', fontWeight: 600 }}>
                  {user?.fullName?.charAt(0) || 'U'}
                </Avatar>
                <div style={{ lineHeight: 1.2 }}>
                  <Text strong style={{ fontSize: 13, display: 'block', color: '#202224' }}>{user?.fullName || 'User'}</Text>
                  <Text style={{ fontSize: 11, color: '#828282' }}>
                    {user?.role === 'CompanyAdmin' ? 'Admin' : (user?.role === 'SuperAdmin' ? 'Super Admin' : 'Member')}
                  </Text>
                </div>
              </div>
            </Dropdown>
          </div>
        </Header>

        <Content style={{ padding: 24, minHeight: 'calc(100vh - 64px)' }}>
          <Outlet />
        </Content>
      </Layout>
    </Layout>
  );
};

export default AppLayout;
