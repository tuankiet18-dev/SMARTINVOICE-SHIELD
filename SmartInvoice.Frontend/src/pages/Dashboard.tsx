import React, { useEffect } from 'react';
import { Row, Col } from 'antd';
import AnalyticsCharts from '@/components/dashboard/AnalyticsCharts';
import { useQuery } from '@tanstack/react-query';
import { invoiceService } from '../services/invoice';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '@/contexts/AuthContext';
import {
  FileTextOutlined,
  CheckCircleOutlined,
  WarningOutlined,
} from '@ant-design/icons';

// Import extracted components
import StatCard from '../components/ui/StatCard';
import RiskDistributionCard from '../components/dashboard/RiskDistributionCard';
import RecentInvoicesTable from '../components/dashboard/RecentInvoicesTable';

const mockRecentInvoices = [
  { key: '1', invoiceNo: 'INV-2026-001284', seller: 'Công ty TNHH ABC', amount: '25,400,000 ₫', date: '12/02/2026', status: 'Approved', risk: 'Green' },
  { key: '2', invoiceNo: 'INV-2026-001283', seller: 'Công ty CP XYZ', amount: '8,750,000 ₫', date: '11/02/2026', status: 'Pending', risk: 'Yellow' },
  { key: '3', invoiceNo: 'INV-2026-001282', seller: 'DN Tư nhân DEF', amount: '42,100,000 ₫', date: '11/02/2026', status: 'Approved', risk: 'Green' },
  { key: '4', invoiceNo: 'INV-2026-001281', seller: 'Công ty TNHH GHI', amount: '3,200,000 ₫', date: '10/02/2026', status: 'Rejected', risk: 'Red' },
  { key: '5', invoiceNo: 'INV-2026-001280', seller: 'Công ty CP JKL', amount: '15,600,000 ₫', date: '10/02/2026', status: 'Pending', risk: 'Orange' },
];

const riskDistribution = [
  { label: 'An toàn (Green)', percent: 72, color: '#00B69B' },
  { label: 'Lưu ý (Yellow)', percent: 15, color: '#FF9500' },
  { label: 'Cảnh báo (Orange)', percent: 9, color: '#FD7E14' },
  { label: 'Nguy hiểm (Red)', percent: 4, color: '#FC2A46' },
];

const Dashboard: React.FC = () => {
  const { user } = useAuth();
  const navigate = useNavigate();

  // Redirect non-admin users to upload page
  useEffect(() => {
    if (user && user.role !== 'CompanyAdmin' && user.role !== 'SuperAdmin') {
      navigate('/app/upload');
    }
  }, [user, navigate]);

  const { data: apiData = [], isLoading } = useQuery({
    queryKey: ['invoices-dashboard'],
    queryFn: () => invoiceService.getInvoices(),
  });

  const recentInvoices = apiData.length > 0 ? apiData.slice(0, 5) : mockRecentInvoices;
  const statsTotal = apiData.length > 0 ? apiData.length : 128;
  const statsGreen = apiData.length > 0 ? apiData.filter(i => i.risk === 'Green').length : 105;
  const statsYellowOrange = apiData.length > 0 ? apiData.filter(i => i.risk === 'Yellow' || i.risk === 'Orange').length : 18;
  const statsRed = apiData.length > 0 ? apiData.filter(i => i.risk === 'Red').length : 5;

  const dynamicKpiData = [
    {
      title: 'Tổng hóa đơn',
      value: statsTotal,
      icon: <FileTextOutlined />,
      iconColorClass: 'text-dash-primary',
      iconBgClass: 'bg-dash-primary/10',
      changeValue: 8.5,
      isUp: true,
    },
    {
      title: 'Hợp lệ (Green)',
      value: statsGreen,
      icon: <CheckCircleOutlined />,
      iconColorClass: 'text-dash-success',
      iconBgClass: 'bg-dash-success/10',
      changeValue: 1.3,
      isUp: true,
    },
    {
      title: 'Cần lưu ý (Warning)',
      value: statsYellowOrange,
      icon: <WarningOutlined />,
      iconColorClass: 'text-dash-warning',
      iconBgClass: 'bg-dash-warning/10',
      changeValue: 4.3,
      isUp: false,
    },
    {
      title: 'Lỗi cấu trúc (Red)',
      value: statsRed,
      icon: <WarningOutlined />,
      iconColorClass: 'text-dash-danger',
      iconBgClass: 'bg-dash-danger/10',
      changeValue: 15.1,
      isUp: true,
    },
  ];

  return (
    <div className="bg-dash-bg p-6 md:p-8 min-h-screen">
      <div className="mb-8">
        <h1 className="text-3xl text-dash-textMain font-bold mb-1 tracking-tight">Tổng quan hệ thống</h1>
        <p className="text-dash-textMuted font-medium text-sm">Cập nhật lúc 12/02/2026 08:30</p>
      </div>

      {/* KPI Cards */}
      <Row gutter={[24, 24]} className="mb-8">
        {dynamicKpiData.map((kpi, index) => (
          <Col xs={24} sm={12} lg={6} key={index}>
            <StatCard {...kpi} />
          </Col>
        ))}
      </Row>

      <Row gutter={[24, 24]}>
        {/* Risk Distribution */}
        <Col xs={24} lg={8}>
          <RiskDistributionCard data={riskDistribution} />
        </Col>

        {/* Recent Invoices */}
        <Col xs={24} lg={16}>
          <RecentInvoicesTable
            invoices={recentInvoices}
            isLoading={isLoading}
            onViewAll={() => navigate('/app/invoices')}
          />
        </Col>
      </Row>

      <div className="mt-8">
        <AnalyticsCharts />
      </div>
    </div>
  );
};

export default Dashboard;
