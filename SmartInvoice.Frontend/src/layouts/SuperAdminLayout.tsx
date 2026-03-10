import React, { useState } from 'react';
import { Layout, Menu, Avatar, Dropdown, Button, Typography } from 'antd';
import {
  BlockOutlined,
  StopOutlined,
  ToolOutlined,
  SafetyCertificateOutlined,
  MenuFoldOutlined,
  MenuUnfoldOutlined,
  UserOutlined,
  SettingOutlined,
  LogoutOutlined,
  CrownOutlined,
} from '@ant-design/icons';
import { useNavigate, useLocation, Outlet } from 'react-router-dom';
import { useAuth } from '@/contexts/AuthContext';

const { Sider, Header, Content } = Layout;
const { Text } = Typography;

const SuperAdminLayout: React.FC = () => {
  const [collapsed, setCollapsed] = useState(false);
  const navigate = useNavigate();
  const location = useLocation();
  const { user, logout } = useAuth();

  const menuItems = [
    { key: '/admin/tenants', icon: <BlockOutlined />, label: 'Quản lý Tenants' },
    { key: '/admin/global-blacklist', icon: <StopOutlined />, label: 'Global Blacklist' },
    { key: '/admin/system-config', icon: <ToolOutlined />, label: 'Cấu hình hệ thống' },
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
          background: '#0f172a',
          overflow: 'auto',
          height: '100vh',
          position: 'fixed',
          left: 0,
          top: 0,
          bottom: 0,
          zIndex: 100,
        }}
      >
        {/* Logo */}
        <div style={{
          padding: collapsed ? '20px 12px' : '24px 20px',
          display: 'flex',
          alignItems: 'center',
          justifyContent: collapsed ? 'center' : 'flex-start',
          gap: 12,
          borderBottom: '1px solid rgba(255,255,255,0.08)',
          marginBottom: 12,
          height: 80,
        }}>
          {collapsed ? (
            <img src="/logo-transparent.png" alt="Logo" style={{ width: 38, height: 38, objectFit: 'contain' }} />
          ) : (
            <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
               <img src="/logo-transparent.png" alt="SmartInvoice Logo" style={{ height: 38, width: 'auto', objectFit: 'contain' }} />
               <div>
                 <Text strong style={{ color: '#fff', fontSize: 15, display: 'block', lineHeight: 1.1 }}>
                   SuperAdmin
                 </Text>
                 <Text style={{ color: '#f59e0b', fontSize: 11, fontWeight: 600 }}>
                   SmartInvoice Shield
                 </Text>
               </div>
            </div>
          )}
        </div>

        <Menu
          theme="dark"
          mode="inline"
          selectedKeys={[location.pathname]}
          items={menuItems}
          onClick={({ key }) => navigate(key)}
          style={{
            background: 'transparent',
            border: 'none',
            padding: '0 12px',
          }}
        />
      </Sider>

      <Layout style={{ marginLeft: collapsed ? 80 : 250, transition: 'margin-left 0.2s cubic-bezier(0.2, 0, 0, 1)' }}>
        <Header style={{
          background: '#0f172a',
          padding: '0 24px',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          position: 'sticky',
          top: 0,
          zIndex: 99,
          height: 64,
          borderBottom: '1px solid rgba(255,255,255,0.08)',
        }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 16 }}>
            <Button
              type="text"
              icon={collapsed ? <MenuUnfoldOutlined /> : <MenuFoldOutlined />}
              onClick={() => setCollapsed(!collapsed)}
              style={{ fontSize: 16, width: 40, height: 40, color: '#fff' }}
            />
            <div style={{
              display: 'flex', alignItems: 'center', gap: 8,
              padding: '4px 12px', background: 'rgba(245, 158, 11, 0.15)',
              borderRadius: 8, border: '1px solid rgba(245, 158, 11, 0.3)',
            }}>
              <SafetyCertificateOutlined style={{ color: '#f59e0b' }} />
              <Text strong style={{ color: '#f59e0b', fontSize: 13 }}>Chế độ Quản trị hệ thống</Text>
            </div>
          </div>

          <Dropdown menu={{ items: userMenuItems, onClick: handleUserMenuClick }} placement="bottomRight" trigger={['click']}>
            <div style={{
              display: 'flex', alignItems: 'center', gap: 12, cursor: 'pointer',
              padding: '4px 12px', borderRadius: 12,
            }}>
              <Avatar size={36} style={{ background: '#f59e0b', fontWeight: 600, color: '#0f172a' }}>
                {user?.fullName?.charAt(0) || 'A'}
              </Avatar>
              <div style={{ lineHeight: 1.2 }}>
                <Text strong style={{ fontSize: 13, display: 'block', color: '#fff' }}>{user?.fullName || 'Admin'}</Text>
                <Text style={{ fontSize: 11, color: '#f59e0b' }}>Super Administrator</Text>
              </div>
            </div>
          </Dropdown>
        </Header>

        <Content style={{ padding: 24, minHeight: 'calc(100vh - 64px)', background: '#f8fafc' }}>
          <Outlet />
        </Content>
      </Layout>
    </Layout>
  );
};

export default SuperAdminLayout;
