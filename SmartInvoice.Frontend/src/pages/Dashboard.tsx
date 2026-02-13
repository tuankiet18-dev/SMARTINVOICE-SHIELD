import React from 'react';
import { Card, Row, Col, Statistic, Table, Tag, Typography, Progress, Space } from 'antd';
import AnalyticsCharts from '@/components/dashboard/AnalyticsCharts';
import {
  FileTextOutlined,
  CheckCircleOutlined,
  WarningOutlined,
  ClockCircleOutlined,
  ArrowUpOutlined,
  ArrowDownOutlined,
} from '@ant-design/icons';

const { Title, Text } = Typography;

const riskColors: Record<string, string> = {
  Green: '#2d9a5c',
  Yellow: '#e6a817',
  Orange: '#e17055',
  Red: '#d63031',
};

const statusColors: Record<string, string> = {
  Approved: 'green',
  Pending: 'gold',
  Draft: 'default',
  Rejected: 'red',
};

const kpiData = [
  {
    title: 'Tổng hóa đơn',
    value: 1284,
    prefix: <FileTextOutlined />,
    color: '#1a4b8c',
    bg: 'linear-gradient(135deg, rgba(26,75,140,0.08), rgba(26,75,140,0.02))',
    change: 12.5,
    up: true,
  },
  {
    title: 'Đã phê duyệt',
    value: 1089,
    prefix: <CheckCircleOutlined />,
    color: '#2d9a5c',
    bg: 'linear-gradient(135deg, rgba(45,154,92,0.08), rgba(45,154,92,0.02))',
    change: 8.3,
    up: true,
  },
  {
    title: 'Chờ xử lý',
    value: 142,
    prefix: <ClockCircleOutlined />,
    color: '#e6a817',
    bg: 'linear-gradient(135deg, rgba(230,168,23,0.08), rgba(230,168,23,0.02))',
    change: 3.2,
    up: false,
  },
  {
    title: 'Cảnh báo rủi ro',
    value: 53,
    prefix: <WarningOutlined />,
    color: '#d63031',
    bg: 'linear-gradient(135deg, rgba(214,48,49,0.08), rgba(214,48,49,0.02))',
    change: 15.1,
    up: true,
  },
];

const recentInvoices = [
  { key: '1', invoiceNo: 'INV-2026-001284', seller: 'Công ty TNHH ABC', amount: '25,400,000 ₫', date: '12/02/2026', status: 'Approved', risk: 'Green' },
  { key: '2', invoiceNo: 'INV-2026-001283', seller: 'Công ty CP XYZ', amount: '8,750,000 ₫', date: '11/02/2026', status: 'Pending', risk: 'Yellow' },
  { key: '3', invoiceNo: 'INV-2026-001282', seller: 'DN Tư nhân DEF', amount: '42,100,000 ₫', date: '11/02/2026', status: 'Approved', risk: 'Green' },
  { key: '4', invoiceNo: 'INV-2026-001281', seller: 'Công ty TNHH GHI', amount: '3,200,000 ₫', date: '10/02/2026', status: 'Rejected', risk: 'Red' },
  { key: '5', invoiceNo: 'INV-2026-001280', seller: 'Công ty CP JKL', amount: '15,600,000 ₫', date: '10/02/2026', status: 'Pending', risk: 'Orange' },
];

const columns = [
  {
    title: 'Số hóa đơn',
    dataIndex: 'invoiceNo',
    key: 'invoiceNo',
    render: (text: string) => <Text strong style={{ color: '#1a4b8c' }}>{text}</Text>,
  },
  {
    title: 'Người bán',
    dataIndex: 'seller',
    key: 'seller',
  },
  {
    title: 'Tổng tiền',
    dataIndex: 'amount',
    key: 'amount',
    render: (text: string) => <Text strong>{text}</Text>,
  },
  {
    title: 'Ngày lập',
    dataIndex: 'date',
    key: 'date',
    render: (text: string) => <Text type="secondary">{text}</Text>,
  },
  {
    title: 'Trạng thái',
    dataIndex: 'status',
    key: 'status',
    render: (status: string) => (
      <Tag color={statusColors[status]}>{status === 'Approved' ? 'Đã duyệt' : status === 'Pending' ? 'Chờ duyệt' : status === 'Draft' ? 'Nháp' : 'Từ chối'}</Tag>
    ),
  },
  {
    title: 'Rủi ro',
    dataIndex: 'risk',
    key: 'risk',
    render: (risk: string) => (
      <Tag
        style={{
          background: `${riskColors[risk]}14`,
          color: riskColors[risk],
          border: `1px solid ${riskColors[risk]}30`,
          borderRadius: 6,
          fontWeight: 600,
          fontSize: 12,
        }}
      >
        {risk}
      </Tag>
    ),
  },
];

const riskDistribution = [
  { label: 'An toàn (Green)', percent: 72, color: '#2d9a5c' },
  { label: 'Lưu ý (Yellow)', percent: 15, color: '#e6a817' },
  { label: 'Cảnh báo (Orange)', percent: 9, color: '#e17055' },
  { label: 'Nguy hiểm (Red)', percent: 4, color: '#d63031' },
];

const Dashboard: React.FC = () => {
  return (
    <div className="animate-fade-in-up">
      <div style={{ marginBottom: 24 }}>
        <Title level={4} style={{ margin: 0 }}>Tổng quan hệ thống</Title>
        <Text type="secondary">Cập nhật lúc 12/02/2026 08:30</Text>
      </div>

      {/* KPI Cards */}
      <Row gutter={[16, 16]} style={{ marginBottom: 24 }}>
        {kpiData.map((kpi, index) => (
          <Col xs={24} sm={12} lg={6} key={index}>
            <Card
              bordered={false}
              style={{
                borderRadius: 12,
                background: kpi.bg,
                border: `1px solid ${kpi.color}15`,
              }}
              bodyStyle={{ padding: 20 }}
            >
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
                <Statistic
                  title={<Text type="secondary" style={{ fontSize: 13 }}>{kpi.title}</Text>}
                  value={kpi.value}
                  valueStyle={{ color: kpi.color, fontSize: 28, fontWeight: 700 }}
                />
                <div style={{
                  width: 44, height: 44, borderRadius: 12,
                  background: `${kpi.color}18`, display: 'flex',
                  alignItems: 'center', justifyContent: 'center',
                  fontSize: 20, color: kpi.color,
                }}>
                  {kpi.prefix}
                </div>
              </div>
              <div style={{ marginTop: 8, display: 'flex', alignItems: 'center', gap: 4 }}>
                {kpi.up ? <ArrowUpOutlined style={{ color: '#2d9a5c', fontSize: 12 }} /> : <ArrowDownOutlined style={{ color: '#d63031', fontSize: 12 }} />}
                <Text style={{ color: kpi.up ? '#2d9a5c' : '#d63031', fontSize: 12, fontWeight: 600 }}>
                  {kpi.change}%
                </Text>
                <Text type="secondary" style={{ fontSize: 12 }}> so với tháng trước</Text>
              </div>
            </Card>
          </Col>
        ))}
      </Row>

      <Row gutter={[16, 16]}>
        {/* Risk Distribution */}
        <Col xs={24} lg={8}>
          <Card
            title="Phân bổ rủi ro"
            bordered={false}
            style={{ borderRadius: 12, height: '100%' }}
            bodyStyle={{ padding: '12px 20px 20px' }}
          >
            <Space direction="vertical" style={{ width: '100%' }} size={16}>
              {riskDistribution.map((item, i) => (
                <div key={i}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 4 }}>
                    <Text style={{ fontSize: 13 }}>{item.label}</Text>
                    <Text strong style={{ fontSize: 13 }}>{item.percent}%</Text>
                  </div>
                  <Progress
                    percent={item.percent}
                    showInfo={false}
                    strokeColor={item.color}
                    trailColor="#f0f0f0"
                    size="small"
                  />
                </div>
              ))}
            </Space>

            <div style={{
              marginTop: 20, padding: 16, borderRadius: 10,
              background: 'rgba(45,154,92,0.06)', border: '1px solid rgba(45,154,92,0.15)',
            }}>
              <Text style={{ fontSize: 13, color: '#2d9a5c' }}>
                <CheckCircleOutlined style={{ marginRight: 6 }} />
                72% hóa đơn đạt chuẩn an toàn
              </Text>
            </div>
          </Card>
        </Col>

        {/* Recent Invoices */}
        <Col xs={24} lg={16}>
          <Card
            title="Hóa đơn gần đây"
            bordered={false}
            style={{ borderRadius: 12 }}
            extra={<a style={{ color: '#1a4b8c', fontSize: 13 }}>Xem tất cả →</a>}
          >
            <Table
              columns={columns}
              dataSource={recentInvoices}
              pagination={false}
              size="middle"
              style={{ marginTop: -8 }}
            />
          </Card>
        </Col>
      </Row>

      {/* Analytics Charts */}
      <AnalyticsCharts />
    </div>
  );
};

export default Dashboard;

