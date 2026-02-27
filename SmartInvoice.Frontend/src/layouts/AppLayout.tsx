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
  BlockOutlined,
  ToolOutlined,
  StopOutlined,
} from '@ant-design/icons';
import { useNavigate, useLocation, Outlet } from 'react-router-dom';
import { authService } from '@/services/auth';

const { Sider, Header, Content } = Layout;
const { Text } = Typography;

const AppLayout: React.FC = () => {
  const [collapsed, setCollapsed] = useState(false);
  const navigate = useNavigate();
  const location = useLocation();

  const menuItems = [{ key: '/app/dashboard', icon: <DashboardOutlined />, label: 'Tổng quan' },
  { key: '/app/invoices', icon: <FileTextOutlined />, label: 'Hóa đơn' },
  { key: '/app/upload', icon: <UploadOutlined />, label: 'Tải lên' },
  { key: '/app/validation', icon: <SafetyCertificateOutlined />, label: 'Rà soát rủi ro' },
  { key: '/app/reports', icon: <BarChartOutlined />, label: 'Báo cáo' },
  { key: '/app/approval-dashboard', icon: <AppstoreOutlined />, label: 'Duyệt ngoại lệ' },
  { key: '/app/team', icon: <TeamOutlined />, label: 'Quản lý Team' },
  { key: 'divider-1', type: 'divider' as const },
  { key: '/app/audit-log', icon: <AuditOutlined />, label: 'Nhật ký Audit' },
  { key: 'divider-2', type: 'divider' as const },
  { key: '/app/tenants', icon: <BlockOutlined />, label: 'Tenant (SA)' },
  { key: '/app/global-blacklist', icon: <StopOutlined />, label: 'Blacklist (SA)' },
  { key: '/app/system-config', icon: <ToolOutlined />, label: 'Cấu hình (SA)' },
  { key: '/app/settings', icon: <SettingOutlined />, label: 'Cài đặt' },
  ];

  const userMenuItems = [
    { key: 'profile', icon: <UserOutlined />, label: 'Hồ sơ cá nhân' },
    { key: 'settings', icon: <SettingOutlined />, label: 'Cài đặt' },
    { type: 'divider' as const, key: 'div' },
    { key: 'logout', icon: <LogoutOutlined />, label: 'Đăng xuất', danger: true },
  ];

  const handleUserMenuClick = ({ key }: { key: string }) => {
    if (key === 'logout') {
      authService.logout();
      navigate('/login');
    } else {
      // Handle other menu actions
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
          background: 'hsl(215 80% 18%)',
          borderRight: '1px solid hsl(215 60% 25%)',
          overflow: 'auto',
          height: '100vh',
          position: 'fixed',
          left: 0,
          top: 0,
          bottom: 0,
          zIndex: 100,
        }}
      >
        <div style={{
          padding: collapsed ? '20px 12px' : '20px 20px',
          display: 'flex',
          alignItems: 'center',
          gap: 12,
          borderBottom: '1px solid rgba(255,255,255,0.08)',
          marginBottom: 8,
        }}>
          <div style={{
            width: 36,
            height: 36,
            borderRadius: 10,
            background: 'linear-gradient(135deg, #2db791, #1a8a6a)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            flexShrink: 0,
          }}>
            <SafetyCertificateOutlined style={{ color: '#fff', fontSize: 18 }} />
          </div>
          {!collapsed && (
            <div>
              <Text strong style={{ color: '#fff', fontSize: 15, display: 'block', lineHeight: 1.2 }}>
                SmartInvoice
              </Text>
              <Text style={{ color: 'rgba(200,210,225,0.6)', fontSize: 11 }}>Shield</Text>
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
            padding: '0 8px',
          }}
        />
      </Sider>

      <Layout style={{ marginLeft: collapsed ? 80 : 250, transition: 'margin-left 0.2s' }}>
        <Header style={{
          background: '#fff',
          padding: '0 24px',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          borderBottom: '1px solid hsl(220 15% 88%)',
          position: 'sticky',
          top: 0,
          zIndex: 99,
          height: 64,
          boxShadow: '0 1px 3px rgba(0,0,0,0.04)',
        }}>
          <Button
            type="text"
            icon={collapsed ? <MenuUnfoldOutlined /> : <MenuFoldOutlined />}
            onClick={() => setCollapsed(!collapsed)}
            style={{ fontSize: 16, width: 40, height: 40 }}
          />

          <div style={{ display: 'flex', alignItems: 'center', gap: 16 }}>
            <Badge count={3} size="small">
              <Button type="text" icon={<BellOutlined />} style={{ fontSize: 18 }} />
            </Badge>

            <Dropdown menu={{ items: userMenuItems, onClick: handleUserMenuClick }} placement="bottomRight" trigger={['click']}>
              <div style={{
                display: 'flex', alignItems: 'center', gap: 10, cursor: 'pointer',
                padding: '4px 12px', borderRadius: 8, transition: 'background 0.2s',
              }}>
                <Avatar size={34} style={{ background: 'hsl(215 80% 28%)' }}>
                  NV
                </Avatar>
                <div style={{ lineHeight: 1.3 }}>
                  <Text strong style={{ fontSize: 13, display: 'block' }}>Nguyễn Văn A</Text>
                  <Text type="secondary" style={{ fontSize: 11 }}>Admin</Text>
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
