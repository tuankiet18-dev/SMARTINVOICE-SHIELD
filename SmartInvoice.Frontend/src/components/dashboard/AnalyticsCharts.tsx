import React from 'react';
import { Card } from 'antd';
import { Panel, PanelGroup, PanelResizeHandle } from 'react-resizable-panels';
import {
  BarChart, Bar, LineChart, Line, PieChart, Pie, Cell, AreaChart, Area,
  XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, Legend,
} from 'recharts';
import type { MonthlyTrendItem, RiskTrendItem, StatusDistributionItem } from '../../services/dashboard';

interface AnalyticsChartsProps {
  monthlyTrends: MonthlyTrendItem[];
  riskTrends: RiskTrendItem[];
  statusDistribution: StatusDistributionItem[];
}

const tooltipStyle = {
  background: '#fff',
  border: '1px solid hsl(220, 15%, 88%)',
  borderRadius: 10,
  boxShadow: '0 4px 12px rgba(0,0,0,0.08)',
  fontSize: 12,
};

const ResizeHandle = () => (
  <PanelResizeHandle className="w-2 flex items-center justify-center group cursor-col-resize">
    <div className="w-1 h-8 rounded-full bg-gray-300 group-hover:bg-blue-400 transition-colors" />
  </PanelResizeHandle>
);

const AnalyticsCharts: React.FC<AnalyticsChartsProps> = ({ monthlyTrends, riskTrends, statusDistribution }) => (
  <div className="mt-4 flex flex-col gap-4">
    {/* Row 1: Bar Chart + Area Chart */}
    <PanelGroup direction="horizontal">
      <Panel defaultSize={50} minSize={25}>
        <div className="h-full pr-2">
          <Card bordered={false} title="Hóa đơn theo tháng" style={{ borderRadius: 12, height: '100%' }}
            bodyStyle={{ padding: '8px 16px 16px' }}>
            <ResponsiveContainer width="100%" height={280}>
              <BarChart data={monthlyTrends} barGap={2}>
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
        </div>
      </Panel>
      <ResizeHandle />
      <Panel defaultSize={50} minSize={25}>
        <div className="h-full pl-2">
          <Card bordered={false} title="Xu hướng rủi ro (%)" style={{ borderRadius: 12, height: '100%' }}
            bodyStyle={{ padding: '8px 16px 16px' }}>
            <ResponsiveContainer width="100%" height={280}>
              <AreaChart data={riskTrends}>
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
        </div>
      </Panel>
    </PanelGroup>

    {/* Row 2: Pie Chart + Line Chart */}
    <PanelGroup direction="horizontal">
      <Panel defaultSize={50} minSize={25}>
        <div className="h-full pr-2">
          <Card bordered={false} title="Tỷ lệ trạng thái" style={{ borderRadius: 12, height: '100%' }}
            bodyStyle={{ padding: '8px 16px 16px' }}>
            <ResponsiveContainer width="100%" height={280}>
              <PieChart>
                <Pie data={statusDistribution} cx="50%" cy="50%" innerRadius={55} outerRadius={95}
                  paddingAngle={4} dataKey="value" label={({ name, percent }) => `${name} ${(percent * 100).toFixed(0)}%`}
                  labelLine={false} fontSize={11}>
                  {statusDistribution.map((entry, i) => (
                    <Cell key={i} fill={entry.color} />
                  ))}
                </Pie>
                <Tooltip contentStyle={tooltipStyle} />
              </PieChart>
            </ResponsiveContainer>
          </Card>
        </div>
      </Panel>
      <ResizeHandle />
      <Panel defaultSize={50} minSize={25}>
        <div className="h-full pl-2">
          <Card bordered={false} title="Tổng hóa đơn theo tháng" style={{ borderRadius: 12, height: '100%' }}
            bodyStyle={{ padding: '8px 16px 16px' }}>
            <ResponsiveContainer width="100%" height={280}>
              <LineChart data={monthlyTrends}>
                <CartesianGrid strokeDasharray="3 3" stroke="hsl(220,15%,92%)" />
                <XAxis dataKey="month" fontSize={12} tickLine={false} axisLine={false} />
                <YAxis fontSize={12} tickLine={false} axisLine={false} />
                <Tooltip contentStyle={tooltipStyle} />
                <Line type="monotone" dataKey="total" name="Tổng" stroke="#1a4b8c" strokeWidth={2.5} dot={{ fill: '#1a4b8c', r: 4 }} />
              </LineChart>
            </ResponsiveContainer>
          </Card>
        </div>
      </Panel>
    </PanelGroup>
  </div>
);

export default AnalyticsCharts;
