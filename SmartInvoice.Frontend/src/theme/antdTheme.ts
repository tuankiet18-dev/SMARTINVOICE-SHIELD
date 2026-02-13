import type { ThemeConfig } from 'antd';

const theme: ThemeConfig = {
  token: {
    colorPrimary: '#1a4b8c',
    colorSuccess: '#2d9a5c',
    colorWarning: '#e6a817',
    colorError: '#d63031',
    colorInfo: '#1a4b8c',
    borderRadius: 8,
    fontFamily: "'IBM Plex Sans', -apple-system, BlinkMacSystemFont, sans-serif",
    fontSize: 14,
    colorBgContainer: '#ffffff',
    colorBgLayout: '#f3f5f8',
    colorBorder: '#dde2ea',
    controlHeight: 38,
    colorLink: '#1a4b8c',
  },
  components: {
    Button: {
      primaryShadow: '0 2px 8px rgba(26, 75, 140, 0.25)',
      borderRadius: 8,
      controlHeight: 40,
    },
    Card: {
      borderRadiusLG: 12,
      boxShadowTertiary: '0 1px 3px rgba(0,0,0,0.06), 0 1px 2px rgba(0,0,0,0.04)',
    },
    Table: {
      borderRadius: 12,
      headerBg: '#f8f9fb',
      headerColor: '#4a5568',
    },
    Menu: {
      darkItemBg: 'transparent',
      darkItemSelectedBg: 'rgba(45, 183, 145, 0.15)',
      darkItemSelectedColor: '#2db791',
      darkItemHoverBg: 'rgba(255,255,255,0.06)',
      darkItemColor: 'rgba(200, 210, 225, 0.85)',
      itemBorderRadius: 8,
    },
    Input: {
      borderRadius: 8,
      controlHeight: 40,
    },
    Select: {
      borderRadius: 8,
      controlHeight: 40,
    },
  },
};

export default theme;
