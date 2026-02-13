import React from 'react';
import { Card, Row, Col, Typography, Statistic, Select, Button, Table, Tag, Space } from 'antd';
import { DownloadOutlined, CalendarOutlined } from '@ant-design/icons';

const { Title, Text } = Typography;

const exportHistory = [
  { key: '1', name: 'Báo cáo tháng 01/2026', type: 'Excel', date: '01/02/2026', size: '2.4 MB', status: 'done' },
  { key: '2', name: 'Báo cáo rủi ro Q4/2025', type: 'PDF', date: '15/01/2026', size: '1.8 MB', status: 'done' },
  { key: '3', name: 'DS hóa đơn GTGT 01/2026', type: 'Excel', date: '05/02/2026', size: '3.1 MB', status: 'done' },
];

const ReportsPage: React.FC = () => {
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
          { title: 'Tổng giá trị hóa đơn', value: '2.45 tỷ ₫', color: '#1a4b8c' },
          { title: 'Tổng thuế GTGT', value: '245 triệu ₫', color: '#2db791' },
          { title: 'Hóa đơn hợp lệ', value: '92%', color: '#2d9a5c' },
          { title: 'Cần xem xét', value: '23', color: '#e6a817' },
        ].map((stat, i) => (
          <Col xs={12} lg={6} key={i}>
            <Card bordered={false} style={{ borderRadius: 12, borderLeft: `3px solid ${stat.color}` }}>
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
                { label: 'Xuất danh sách hóa đơn (Excel)', desc: 'Format tương thích MISA/FAST', type: 'Excel' },
                { label: 'Báo cáo rủi ro tổng hợp (PDF)', desc: 'Phân tích chi tiết 3 lớp validation', type: 'PDF' },
                { label: 'Bảng kê thuế GTGT (Excel)', desc: 'Theo mẫu kê khai thuế', type: 'Excel' },
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
