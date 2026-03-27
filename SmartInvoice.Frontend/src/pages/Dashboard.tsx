import React, { useEffect, useState } from 'react';
import { Row, Col, Spin, Select, Card } from 'antd';
import { Panel, PanelGroup, PanelResizeHandle } from 'react-resizable-panels';
import AnalyticsCharts from '@/components/dashboard/AnalyticsCharts';
import { useQuery } from '@tanstack/react-query';
import { dashboardService, type DashboardPeriod, type ChartPeriod} from '../services/dashboard';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '@/contexts/AuthContext';
import dayjs from 'dayjs';
import {
  FileTextOutlined,
  CheckCircleOutlined,
  WarningOutlined,
} from '@ant-design/icons';

// Import extracted components
import StatCard from '../components/ui/StatCard';
import RiskDistributionCard from '../components/dashboard/RiskDistributionCard';
import RecentInvoicesTable from '../components/dashboard/RecentInvoicesTable';

const periodOptions: { value: DashboardPeriod; label: string }[] = [
  { value: '7d', label: '7 ngày' },
  { value: '30d', label: '30 ngày' },
  { value: '90d', label: '90 ngày' },
  { value: '6m', label: '6 tháng' },
  { value: '1y', label: '1 năm' },
  { value: 'all', label: 'Tất cả' },
];

const periodChangeText: Record<DashboardPeriod, string> = {
  '7d': 'so với 7 ngày trước',
  '30d': 'so với 30 ngày trước',
  '90d': 'so với 90 ngày trước',
  '6m': 'so với 6 tháng trước',
  '1y': 'so với năm trước',
  all: 'so với kỳ trước',
};

const Dashboard: React.FC = () => {
  const { user } = useAuth();
  const navigate = useNavigate();
  const [period, setPeriod] = useState<DashboardPeriod>('30d');
  const [chartPeriod, setChartPeriod] = useState<ChartPeriod>('6m');
  const [lastRefreshTime, setLastRefreshTime] = useState<Date>(new Date());

  const { data: stats, isLoading, isFetching } = useQuery({
    queryKey: ['dashboard-stats', period, chartPeriod],
    queryFn: () => dashboardService.getStats(period, chartPeriod),
    refetchInterval: 30_000, 
    meta: {
      onSuccess: () => setLastRefreshTime(new Date()),
    },
  });

  // Sửa lại hàm này, đưa các biến đếm số vào trong chuỗi (dùng dấu backtick `)
  const getTimeAgoText = (): string => {
    const diffSeconds = Math.floor((new Date().getTime() - lastRefreshTime.getTime()) / 1000);
    if (diffSeconds < 5) return 'vừa xong';
    if (diffSeconds < 60) return `${diffSeconds} giây trước`;
    return `${Math.floor(diffSeconds / 60)} phút trước`;
  };

  const changeText = periodChangeText[period];

  const kpiData = [
    {
      title: 'Tổng hóa đơn',
      value: stats?.totalInvoices ?? 0,
      icon: <FileTextOutlined />,
      iconColorClass: 'text-dash-primary',
      iconBgClass: 'bg-dash-primary/10',
      changeValue: Math.abs(stats?.totalChange ?? 0),
      isUp: (stats?.totalChange ?? 0) >= 0,
    },
    {
      title: 'Hợp lệ (Green)',
      value: stats?.greenInvoices ?? 0,
      icon: <CheckCircleOutlined />,
      iconColorClass: 'text-dash-success',
      iconBgClass: 'bg-dash-success/10',
      changeValue: Math.abs(stats?.greenChange ?? 0),
      isUp: (stats?.greenChange ?? 0) >= 0,
    },
    {
      title: 'Cần lưu ý (Warning)',
      value: stats?.yellowOrangeInvoices ?? 0,
      icon: <WarningOutlined />,
      iconColorClass: 'text-dash-warning',
      iconBgClass: 'bg-dash-warning/10',
      changeValue: Math.abs(stats?.yellowOrangeChange ?? 0),
      isUp: (stats?.yellowOrangeChange ?? 0) >= 0,
    },
    {
      title: 'Nguy hiểm (Red)',
      value: stats?.redInvoices ?? 0,
      icon: <WarningOutlined />,
      iconColorClass: 'text-dash-danger',
      iconBgClass: 'bg-dash-danger/10',
      changeValue: Math.abs(stats?.redChange ?? 0),
      isUp: (stats?.redChange ?? 0) >= 0,
    },
  ];

  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <Spin size="large" description="Đang tải dữ liệu..." />
      </div>
    );
  }

  return (
    <div className="bg-dash-bg p-6 md:p-8 min-h-screen">
      <div className="mb-8 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div>
          <h1 className="text-3xl text-dash-textMain font-bold mb-1 tracking-tight">
            {user?.role === 'Member' ? 'Tổng quan cá nhân' : 'Tổng quan hệ thống'}
          </h1>
          {/* Gọi hàm ra đây */}
          <p className="text-dash-textMuted font-medium text-sm">
            {isFetching ? '⏳ Đang cập nhật...' : `✓ Cập nhật ${getTimeAgoText()}`}
          </p>
        </div>
        <Select
          value={period}
          onChange={(val) => setPeriod(val)}
          options={periodOptions}
          style={{ width: 140 }}
          size="middle"
        />
      </div>

      <Row gutter={[24, 24]} className="mb-6">
        <Col xs={24} md={8}>
          <Card
            className="rounded-[14px] shadow-dash border-none overflow-hidden relative"
            styles={{ body: { padding: '24px', background: 'linear-gradient(135deg, #1a4b8c 0%, #2a6bbd 100%)', color: 'white' } }}
          >
            <div className="text-white/80 font-medium mb-2 text-sm uppercase tracking-wider">Tổng giá trị hóa đơn (Kỳ này)</div>
            <div className="text-3xl font-bold text-white mb-1">
              {stats?.totalAmount?.toLocaleString('vi-VN')} ₫
            </div>
            <FileTextOutlined className="absolute right-[-20px] bottom-[-20px] text-[100px] text-white/10" />
          </Card>
        </Col>
        
        <Col xs={24} md={8}>
          <Card 
            className="rounded-[14px] shadow-dash border-none overflow-hidden relative"
            styles={{ body: { padding: '24px', background: 'linear-gradient(135deg, #2d9a5c 0%, #3bc175 100%)', color: 'white' } }}
          >
            <div className="text-white/80 font-medium mb-2 text-sm uppercase tracking-wider">Đã phê duyệt (Ghi nhận chi phí)</div>
            <div className="text-3xl font-bold text-white mb-1">
              {stats?.approvedAmount?.toLocaleString('vi-VN')} ₫
            </div>
            <CheckCircleOutlined className="absolute right-[-20px] bottom-[-20px] text-[100px] text-white/10" />
          </Card>
        </Col>

        <Col xs={24} md={8}>
          <Card 
            className="rounded-[14px] shadow-dash border-none overflow-hidden relative"
            styles={{ body: { padding: '24px', background: 'linear-gradient(135deg, #e6a817 0%, #facc32 100%)', color: 'white' } }}
          >
            <div className="text-white/80 font-medium mb-2 text-sm uppercase tracking-wider">Đang chờ duyệt</div>
            <div className="text-3xl font-bold text-white mb-1">
              {stats?.pendingAmount?.toLocaleString('vi-VN')} ₫
            </div>
            <WarningOutlined className="absolute right-[-20px] bottom-[-20px] text-[100px] text-white/10" />
          </Card>
        </Col>
      </Row>

      {/* KPI Cards */}
      <Row gutter={[24, 24]} className="mb-8">
        {kpiData.map((kpi, index) => (
          <Col xs={24} sm={12} lg={6} key={index}>
            <StatCard {...kpi} changeText={changeText} />
          </Col>
        ))}
      </Row>

      {/* Resizable: Risk Distribution + Recent Invoices */}
      <div className="mb-8">
        <PanelGroup direction="horizontal" className="rounded-xl">
          <Panel defaultSize={33} minSize={20}>
            <div className="h-full pr-2">
              <RiskDistributionCard data={stats?.riskDistribution?.filter(r => !r.label.includes('Orange')) ?? []} />
            </div>
          </Panel>
          <PanelResizeHandle className="w-2 flex items-center justify-center group cursor-col-resize">
            <div className="w-1 h-8 rounded-full bg-gray-300 group-hover:bg-blue-400 transition-colors" />
          </PanelResizeHandle>
          <Panel defaultSize={67} minSize={30}>
            <div className="h-full pl-2">
              <RecentInvoicesTable
                invoices={stats?.recentInvoices ?? []}
                isLoading={false}
                onViewAll={() => navigate('/app/invoices')}
              />
            </div>
          </Panel>
        </PanelGroup>
      </div>

      <div className="mt-10 mb-6 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div>
          <h2 className="text-xl text-dash-textMain font-bold tracking-tight">
            Phân tích xu hướng (Biểu đồ)
          </h2>
          <p className="text-dash-textMuted text-sm">
            Theo dõi sự thay đổi dòng tiền và mức độ rủi ro theo tháng.
          </p>
        </div>
        <Select
          value={chartPeriod}
          onChange={(val) => setChartPeriod(val as ChartPeriod)}
          options={[
            { value: '3m', label: '3 tháng qua' },
            { value: '6m', label: '6 tháng qua' },
            { value: '12m', label: '12 tháng qua' },
          ]}
          style={{ width: 140 }}
          size="middle"
        />
      </div>

      <AnalyticsCharts
        monthlyTrends={stats?.monthlyTrends ?? []}
        riskTrends={stats?.riskTrends ?? []}
        statusDistribution={stats?.statusDistribution ?? []}
      />
    </div>
  );
};

export default Dashboard;