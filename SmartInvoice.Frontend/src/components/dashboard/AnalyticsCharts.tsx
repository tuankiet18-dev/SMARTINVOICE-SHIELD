import React from 'react';
import { Card, Row, Col, Typography } from 'antd';
import {
  BarChart, Bar, LineChart, Line, PieChart, Pie, Cell, AreaChart, Area,
  XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, Legend,
} from 'recharts';

const { Text } = Typography;

const monthlyData = [
  { month: 'T8', total: 780, approved: 680, rejected: 35, pending: 65 },
  { month: 'T9', total: 920, approved: 810, rejected: 42, pending: 68 },
  { month: 'T10', total: 1050, approved: 930, rejected: 38, pending: 82 },
  { month: 'T11', total: 1120, approved: 985, rejected: 45, pending: 90 },
  { month: 'T12', total: 980, approved: 870, rejected: 30, pending: 80 },
  { month: 'T1', total: 1150, approved: 1020, rejected: 48, pending: 82 },
  { month: 'T2', total: 1284, approved: 1089, rejected: 53, pending: 142 },
];

const riskTrendData = [
  { month: 'T8', green: 75, yellow: 13, orange: 8, red: 4 },
  { month: 'T9', green: 73, yellow: 14, orange: 9, red: 4 },
  { month: 'T10', green: 70, yellow: 16, orange: 10, red: 4 },
  { month: 'T11', green: 71, yellow: 15, orange: 9, red: 5 },
  { month: 'T12', green: 74, yellow: 14, orange: 8, red: 4 },
  { month: 'T1', green: 73, yellow: 14, orange: 9, red: 4 },
  { month: 'T2', green: 72, yellow: 15, orange: 9, red: 4 },
];

const pieData = [
  { name: 'Đã duyệt', value: 1089, color: '#2d9a5c' },
  { name: 'Chờ duyệt', value: 142, color: '#e6a817' },
  { name: 'Từ chối', value: 53, color: '#d63031' },
];

const amountByCategory = [
  { category: 'Hàng hóa', amount: 4520 },
  { category: 'Dịch vụ', amount: 3180 },
  { category: 'Vận tải', amount: 1850 },
  { category: 'Bảo hiểm', amount: 920 },
  { category: 'Khác', amount: 650 },
];

const tooltipStyle = {
  background: '#fff',
  border: '1px solid hsl(220, 15%, 88%)',
  borderRadius: 10,
  boxShadow: '0 4px 12px rgba(0,0,0,0.08)',
  fontSize: 12,
};

const AnalyticsCharts: React.FC = () => (
  <Row gutter={[16, 16]} style={{ marginTop: 16 }}>
    {/* Monthly Invoices Bar Chart */}
    <Col xs={24} lg={12}>
      <Card bordered={false} title="Hóa đơn theo tháng" style={{ borderRadius: 12, height: '100%' }}
        bodyStyle={{ padding: '8px 16px 16px' }}>
        <ResponsiveContainer width="100%" height={280}>
          <BarChart data={monthlyData} barGap={2}>
            <CartesianGrid strokeDasharray="3 3" stroke="hsl(220,15%,92%)" />
            <XAxis dataKey="month" fontSize={12} tickLine={false} axisLine={false} />
            <YAxis fontSize={12} tickLine={false} axisLine={false} />
            <Tooltip contentStyle={tooltipStyle} />
            <Legend wrapperStyle={{ fontSize: 12 }} />
            <Bar dataKey="approved" name="Đã duyệt" fill="#2d9a5c" radius={[4, 4, 0, 0]} />
            <Bar dataKey="pending" name="Chờ duyệt" fill="#e6a817" radius={[4, 4, 0, 0]} />
            <Bar dataKey="rejected" name="Từ chối" fill="#d63031" radius={[4, 4, 0, 0]} />
          </BarChart>
        </ResponsiveContainer>
      </Card>
    </Col>

    {/* Risk Trend Area Chart */}
    <Col xs={24} lg={12}>
      <Card bordered={false} title="Xu hướng rủi ro (%)" style={{ borderRadius: 12, height: '100%' }}
        bodyStyle={{ padding: '8px 16px 16px' }}>
        <ResponsiveContainer width="100%" height={280}>
          <AreaChart data={riskTrendData}>
            <CartesianGrid strokeDasharray="3 3" stroke="hsl(220,15%,92%)" />
            <XAxis dataKey="month" fontSize={12} tickLine={false} axisLine={false} />
            <YAxis fontSize={12} tickLine={false} axisLine={false} />
            <Tooltip contentStyle={tooltipStyle} />
            <Legend wrapperStyle={{ fontSize: 12 }} />
            <Area type="monotone" dataKey="green" name="An toàn" fill="#2d9a5c" fillOpacity={0.15} stroke="#2d9a5c" strokeWidth={2} />
            <Area type="monotone" dataKey="yellow" name="Lưu ý" fill="#e6a817" fillOpacity={0.15} stroke="#e6a817" strokeWidth={2} />
            <Area type="monotone" dataKey="orange" name="Cảnh báo" fill="#e17055" fillOpacity={0.15} stroke="#e17055" strokeWidth={2} />
            <Area type="monotone" dataKey="red" name="Nguy hiểm" fill="#d63031" fillOpacity={0.15} stroke="#d63031" strokeWidth={2} />
          </AreaChart>
        </ResponsiveContainer>
      </Card>
    </Col>

    {/* Pie Chart - Invoice Status */}
    <Col xs={24} lg={8}>
      <Card bordered={false} title="Tỷ lệ trạng thái" style={{ borderRadius: 12, height: '100%' }}
        bodyStyle={{ padding: '8px 16px 16px' }}>
        <ResponsiveContainer width="100%" height={260}>
          <PieChart>
            <Pie data={pieData} cx="50%" cy="50%" innerRadius={55} outerRadius={90}
              paddingAngle={4} dataKey="value" label={({ name, percent }) => `${name} ${(percent * 100).toFixed(0)}%`}
              labelLine={false} fontSize={11}>
              {pieData.map((entry, i) => (
                <Cell key={i} fill={entry.color} />
              ))}
            </Pie>
            <Tooltip contentStyle={tooltipStyle} />
          </PieChart>
        </ResponsiveContainer>
      </Card>
    </Col>

    {/* Amount by Category */}
    <Col xs={24} lg={8}>
      <Card bordered={false} title="Giá trị theo loại (triệu ₫)" style={{ borderRadius: 12, height: '100%' }}
        bodyStyle={{ padding: '8px 16px 16px' }}>
        <ResponsiveContainer width="100%" height={260}>
          <BarChart data={amountByCategory} layout="vertical" barSize={18}>
            <CartesianGrid strokeDasharray="3 3" stroke="hsl(220,15%,92%)" />
            <XAxis type="number" fontSize={12} tickLine={false} axisLine={false} />
            <YAxis type="category" dataKey="category" fontSize={12} tickLine={false} axisLine={false} width={70} />
            <Tooltip contentStyle={tooltipStyle} />
            <Bar dataKey="amount" fill="#1a4b8c" radius={[0, 4, 4, 0]} />
          </BarChart>
        </ResponsiveContainer>
      </Card>
    </Col>

    {/* Processing Line Chart */}
    <Col xs={24} lg={8}>
      <Card bordered={false} title="Tổng hóa đơn theo tháng" style={{ borderRadius: 12, height: '100%' }}
        bodyStyle={{ padding: '8px 16px 16px' }}>
        <ResponsiveContainer width="100%" height={260}>
          <LineChart data={monthlyData}>
            <CartesianGrid strokeDasharray="3 3" stroke="hsl(220,15%,92%)" />
            <XAxis dataKey="month" fontSize={12} tickLine={false} axisLine={false} />
            <YAxis fontSize={12} tickLine={false} axisLine={false} />
            <Tooltip contentStyle={tooltipStyle} />
            <Line type="monotone" dataKey="total" name="Tổng" stroke="#1a4b8c" strokeWidth={2.5} dot={{ fill: '#1a4b8c', r: 4 }} />
          </LineChart>
        </ResponsiveContainer>
      </Card>
    </Col>
  </Row>
);

export default AnalyticsCharts;
