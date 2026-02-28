import React from 'react';
import { Card, Row, Col, Typography, Statistic, Select, Button, Table, Tag, Space } from 'antd';
import { DownloadOutlined, CalendarOutlined } from '@ant-design/icons';
import { useQuery } from '@tanstack/react-query';
import { invoiceService } from '../services/invoice';

const { Title, Text } = Typography;

const exportHistory = [
  { key: '1', name: 'Báo cáo tháng 01/2026', type: 'Excel', date: '01/02/2026', size: '2.4 MB', status: 'done' },
  { key: '2', name: 'Báo cáo rủi ro Q4/2025', type: 'PDF', date: '15/01/2026', size: '1.8 MB', status: 'done' },
  { key: '3', name: 'DS hóa đơn GTGT 01/2026', type: 'Excel', date: '05/02/2026', size: '3.1 MB', status: 'done' },
];

const ReportsPage: React.FC = () => {
  const { data: apiData = [], isLoading } = useQuery({
    queryKey: ['invoices-reports'],
    queryFn: () => invoiceService.getInvoices(),
  });

  const totalValue = apiData.length > 0
    ? apiData.reduce((sum, item) => sum + parseInt(item.amount.replace(/\D/g, '')), 0)
    : 2450000000;

  const validCount = apiData.length > 0 ? apiData.filter(i => i.risk === 'Green').length : 18;
  const totalCount = apiData.length > 0 ? apiData.length : 20;
  const validRatio = Math.round((validCount / totalCount) * 100) || 92;
  const needReviewCount = apiData.length > 0 ? apiData.filter(i => i.risk === 'Yellow' || i.risk === 'Orange').length : 23;

  return (
    <div className="animate-fade-in-up">
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 24 }}>
        <div>
          <Title level={4} style={{ margin: 0 }}>Báo cáo & Xuất file</Title>
          <Text type="secondary">Tạo báo cáo và xuất dữ liệu hóa đơn</Text>
        </div>
        <Select defaultValue="2026-02" style={{ width: 160 }} prefix={<CalendarOutlined />}
          options={[
            { value: '2026-02', label: 'Tháng 02/2026' },
            { value: '2026-01', label: 'Tháng 01/2026' },
            { value: '2025-12', label: 'Tháng 12/2025' },
          ]}
        />
      </div>

      <Row gutter={16} style={{ marginBottom: 24 }}>
        {[
          { title: 'Tổng giá trị hóa đơn', value: `${(totalValue / 1000000000).toFixed(2)} tỷ ₫`, color: '#1a4b8c' },
          { title: 'Tổng thuế GTGT', value: `${(totalValue * 0.1 / 1000000).toFixed(0)} triệu ₫`, color: '#2db791' },
          { title: 'Hóa đơn hợp lệ', value: `${validRatio}%`, color: '#2d9a5c' },
          { title: 'Cần xem xét', value: needReviewCount.toString(), color: '#e6a817' },
        ].map((stat, i) => (
          <Col xs={12} lg={6} key={i}>
            <Card loading={isLoading} bordered={false} style={{ borderRadius: 12, borderLeft: `3px solid ${stat.color}` }}>
              <Text type="secondary" style={{ fontSize: 12 }}>{stat.title}</Text>
              <div style={{ fontSize: 22, fontWeight: 700, color: stat.color, marginTop: 4 }}>{stat.value}</div>
            </Card>
          </Col>
        ))}
      </Row>

      <Row gutter={16}>
        <Col xs={24} lg={12}>
          <Card bordered={false} style={{ borderRadius: 12 }} title="Xuất báo cáo nhanh">
            <Space direction="vertical" style={{ width: '100%' }} size={12}>
              {[
                { label: 'Xuất danh sách phần mềm Kế toán (MISA/FAST)', desc: 'Format cấu trúc dành riêng cho import tự động', type: 'Xuất MISA' },
                { label: 'Xuất danh sách hóa đơn chung (Excel)', desc: 'Bảng kê chi tiết các hóa đơn đã duyệt', type: 'Xuất Excel' },
                { label: 'Báo cáo cảnh báo rủi ro (PDF)', desc: 'Phân tích chi tiết 3 lớp validation hệ thống', type: 'PDF' },
              ].map((item, i) => (
                <div key={i} style={{
                  padding: '14px 16px', borderRadius: 10, border: '1px solid #f0f0f0',
                  display: 'flex', justifyContent: 'space-between', alignItems: 'center',
                }}>
                  <div>
                    <Text strong style={{ fontSize: 13 }}>{item.label}</Text>
                    <br />
                    <Text type="secondary" style={{ fontSize: 12 }}>{item.desc}</Text>
                  </div>
                  <Button icon={<DownloadOutlined />} type="primary" ghost size="small">
                    {item.type}
                  </Button>
                </div>
              ))}
            </Space>
          </Card>
        </Col>

        <Col xs={24} lg={12}>
          <Card bordered={false} style={{ borderRadius: 12 }} title="Lịch sử xuất file">
            <Table
              size="small"
              pagination={false}
              dataSource={exportHistory}
              columns={[
                { title: 'Tên file', dataIndex: 'name', key: 'name', render: (t: string) => <Text style={{ fontSize: 13 }}>{t}</Text> },
                { title: 'Loại', dataIndex: 'type', key: 'type', render: (t: string) => <Tag>{t}</Tag> },
                { title: 'Ngày', dataIndex: 'date', key: 'date', render: (t: string) => <Text type="secondary" style={{ fontSize: 12 }}>{t}</Text> },
                { title: '', key: 'dl', render: () => <Button type="link" icon={<DownloadOutlined />} size="small" /> },
              ]}
            />
          </Card>
        </Col>
      </Row>
    </div>
  );
};

export default ReportsPage;
