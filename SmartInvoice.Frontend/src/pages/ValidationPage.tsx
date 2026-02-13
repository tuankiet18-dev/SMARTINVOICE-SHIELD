import React from 'react';
import { Card, Row, Col, Typography, Tag, Timeline, Table, Badge, Space, Button } from 'antd';
import {
  SafetyCertificateOutlined, CheckCircleOutlined, WarningOutlined,
  CloseCircleOutlined, InfoCircleOutlined, FileSearchOutlined,
} from '@ant-design/icons';

const { Title, Text, Paragraph } = Typography;

const validationResults = [
  {
    key: '1',
    invoiceNo: 'INV-2026-001284',
    seller: 'Công ty TNHH ABC',
    layer1: 'pass',
    layer2: 'pass',
    layer3: 'pass',
    risk: 'Green',
    issues: 0,
  },
  {
    key: '2',
    invoiceNo: 'INV-2026-001283',
    seller: 'Công ty CP XYZ',
    layer1: 'pass',
    layer2: 'warning',
    layer3: 'pass',
    risk: 'Yellow',
    issues: 1,
  },
  {
    key: '3',
    invoiceNo: 'INV-2026-001282',
    seller: 'DN Tư nhân DEF',
    layer1: 'pass',
    layer2: 'pass',
    layer3: 'warning',
    risk: 'Orange',
    issues: 2,
  },
  {
    key: '4',
    invoiceNo: 'INV-2026-001281',
    seller: 'Công ty TNHH GHI',
    layer1: 'fail',
    layer2: 'fail',
    layer3: 'fail',
    risk: 'Red',
    issues: 5,
  },
];

const riskColors: Record<string, string> = {
  Green: '#2d9a5c', Yellow: '#e6a817', Orange: '#e17055', Red: '#d63031',
};

const layerIcon = (status: string) => {
  if (status === 'pass') return <CheckCircleOutlined style={{ color: '#2d9a5c' }} />;
  if (status === 'warning') return <WarningOutlined style={{ color: '#e6a817' }} />;
  return <CloseCircleOutlined style={{ color: '#d63031' }} />;
};

const columns = [
  {
    title: 'Hóa đơn',
    dataIndex: 'invoiceNo',
    key: 'invoiceNo',
    render: (text: string, record: any) => (
      <div>
        <Text strong style={{ color: '#1a4b8c' }}>{text}</Text>
        <br />
        <Text type="secondary" style={{ fontSize: 12 }}>{record.seller}</Text>
      </div>
    ),
  },
  {
    title: 'Lớp 1: Cấu trúc',
    dataIndex: 'layer1',
    key: 'layer1',
    align: 'center' as const,
    render: (v: string) => layerIcon(v),
  },
  {
    title: 'Lớp 2: Chữ ký',
    dataIndex: 'layer2',
    key: 'layer2',
    align: 'center' as const,
    render: (v: string) => layerIcon(v),
  },
  {
    title: 'Lớp 3: Nghiệp vụ',
    dataIndex: 'layer3',
    key: 'layer3',
    align: 'center' as const,
    render: (v: string) => layerIcon(v),
  },
  {
    title: 'Rủi ro',
    dataIndex: 'risk',
    key: 'risk',
    render: (risk: string) => (
      <Tag style={{
        background: `${riskColors[risk]}14`, color: riskColors[risk],
        border: `1px solid ${riskColors[risk]}30`, borderRadius: 6, fontWeight: 600,
      }}>
        {risk}
      </Tag>
    ),
  },
  {
    title: 'Vấn đề',
    dataIndex: 'issues',
    key: 'issues',
    render: (v: number) => v > 0 ? <Badge count={v} style={{ background: v > 2 ? '#d63031' : '#e6a817' }} /> : <Text type="secondary">—</Text>,
  },
  {
    title: '',
    key: 'action',
    render: () => <Button type="link" icon={<FileSearchOutlined />} size="small">Chi tiết</Button>,
  },
];

const ValidationPage: React.FC = () => {
  return (
    <div className="animate-fade-in-up">
      <div style={{ marginBottom: 24 }}>
        <Title level={4} style={{ margin: 0 }}>Rà soát rủi ro</Title>
        <Text type="secondary">Kiểm tra 3 lớp: Cấu trúc XSD → Chữ ký số → Nghiệp vụ</Text>
      </div>

      {/* Validation Layers Overview */}
      <Row gutter={16} style={{ marginBottom: 24 }}>
        {[
          { title: 'Lớp 1: Cấu trúc (XSD)', desc: 'XML well-formed, trường bắt buộc', icon: <SafetyCertificateOutlined />, color: '#1a4b8c', pass: 18, total: 20 },
          { title: 'Lớp 2: Chữ ký số', desc: 'Signature verify, Anti-Spoofing', icon: <SafetyCertificateOutlined />, color: '#2db791', pass: 16, total: 20 },
          { title: 'Lớp 3: Nghiệp vụ', desc: 'MST, toán học, ngày tháng', icon: <SafetyCertificateOutlined />, color: '#e6a817', pass: 15, total: 20 },
        ].map((layer, i) => (
          <Col xs={24} md={8} key={i}>
            <Card bordered={false} style={{ borderRadius: 12, borderTop: `3px solid ${layer.color}` }} bodyStyle={{ padding: 20 }}>
              <Space align="start">
                <div style={{
                  width: 40, height: 40, borderRadius: 10,
                  background: `${layer.color}14`, display: 'flex',
                  alignItems: 'center', justifyContent: 'center', color: layer.color, fontSize: 18,
                }}>
                  {layer.icon}
                </div>
                <div>
                  <Text strong style={{ fontSize: 14 }}>{layer.title}</Text>
                  <br />
                  <Text type="secondary" style={{ fontSize: 12 }}>{layer.desc}</Text>
                  <div style={{ marginTop: 8 }}>
                    <Text strong style={{ color: layer.color, fontSize: 20 }}>{layer.pass}</Text>
                    <Text type="secondary" style={{ fontSize: 13 }}> / {layer.total} đạt</Text>
                  </div>
                </div>
              </Space>
            </Card>
          </Col>
        ))}
      </Row>

      {/* Results Table */}
      <Card bordered={false} style={{ borderRadius: 12 }} title="Kết quả rà soát gần đây">
        <Table columns={columns} dataSource={validationResults} pagination={false} size="middle" />
      </Card>
    </div>
  );
};

export default ValidationPage;
