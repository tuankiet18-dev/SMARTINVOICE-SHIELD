import React, { useState } from 'react';
import { Card, Row, Col, Typography, Tag, Table, Badge, Space, Button, Input, Select, Spin, Empty, Pagination, Tooltip } from 'antd';
import {
  SafetyCertificateOutlined, CheckCircleOutlined, WarningOutlined,
  CloseCircleOutlined, FileSearchOutlined, SearchOutlined, FilterOutlined,
} from '@ant-design/icons';
import { useQuery } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { validationService, type InvoiceValidationSummary } from '../services/validation';

const { Title, Text } = Typography;

const riskColors: Record<string, string> = {
  Green: '#2d9a5c', Yellow: '#e6a817', Orange: '#e17055', Red: '#d63031',
};

const statusColors: Record<string, string> = {
  Pass: '#2d9a5c', Warning: '#e6a817', Fail: '#d63031',
};

const layerIcon = (status: string | null) => {
  if (!status || status === 'Skipped') return <span style={{ color: '#94a3b8' }}>—</span>;
  if (status === 'Pass') return <CheckCircleOutlined style={{ color: '#2d9a5c', fontSize: 16 }} />;
  if (status === 'Warning') return <WarningOutlined style={{ color: '#e6a817', fontSize: 16 }} />;
  return <CloseCircleOutlined style={{ color: '#d63031', fontSize: 16 }} />;
};

const ValidationPage: React.FC = () => {
  const navigate = useNavigate();
  const [showFilters, setShowFilters] = useState(false);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [searchText, setSearchText] = useState('');
  const [keyword, setKeyword] = useState<string>();
  const [riskLevel, setRiskLevel] = useState<string>();
  const [validationStatus, setValidationStatus] = useState<string>();

  const { data, isLoading } = useQuery({
    queryKey: ['validation-overview', page, pageSize, keyword, riskLevel, validationStatus],
    queryFn: () => validationService.getOverview({ page, pageSize, keyword, riskLevel, validationStatus }),
  });

  const columns = [
    {
      title: 'Hóa đơn',
      key: 'invoiceNo',
      render: (_: unknown, record: InvoiceValidationSummary) => (
        <div>
          <Text strong style={{ color: '#1a4b8c' }}>{record.invoiceNumber || 'N/A'}</Text>
          <br />
          <Text type="secondary" style={{ fontSize: 12 }}>{record.sellerName || '—'}</Text>
          {record.sellerTaxCode && (
            <Text type="secondary" style={{ fontSize: 11, display: 'block', fontFamily: 'monospace' }}>{record.sellerTaxCode}</Text>
          )}
        </div>
      ),
    },
    {
      title: 'Lớp 1: Cấu trúc',
      dataIndex: 'layer1Status',
      key: 'layer1',
      align: 'center' as const,
      width: 120,
      render: (v: string | null) => layerIcon(v),
    },
    {
      title: 'Lớp 2: Chữ ký',
      dataIndex: 'layer2Status',
      key: 'layer2',
      align: 'center' as const,
      width: 120,
      render: (v: string | null) => layerIcon(v),
    },
    {
      title: 'Lớp 3: Nghiệp vụ',
      dataIndex: 'layer3Status',
      key: 'layer3',
      align: 'center' as const,
      width: 120,
      render: (v: string | null) => layerIcon(v),
    },
    {
      title: 'Rủi ro',
      dataIndex: 'riskLevel',
      key: 'risk',
      width: 100,
      render: (risk: string | null) => {
        if (!risk) return <Text type="secondary">—</Text>;
        return (
          <Tag style={{
            background: `${riskColors[risk] || '#94a3b8'}14`,
            color: riskColors[risk] || '#94a3b8',
            border: `1px solid ${riskColors[risk] || '#94a3b8'}30`,
            borderRadius: 6, fontWeight: 600,
          }}>
            {risk}
          </Tag>
        );
      },
    },
    {
      title: <Tooltip title="Số lớp kiểm tra có lỗi hoặc cảnh báo + số kiểm tra rủi ro phát hiện vấn đề">Vấn đề</Tooltip>,
      dataIndex: 'issueCount',
      key: 'issues',
      width: 80,
      align: 'center' as const,
      render: (v: number) => v > 0
        ? <Badge count={v} style={{ background: v > 2 ? '#d63031' : '#e6a817' }} />
        : <Text type="secondary">—</Text>,
    },
    {
      title: 'Kết quả',
      dataIndex: 'overallStatus',
      key: 'overall',
      width: 100,
      render: (s: string) => (
        <Tag style={{
          background: `${statusColors[s] || '#94a3b8'}14`,
          color: statusColors[s] || '#94a3b8',
          border: `1px solid ${statusColors[s] || '#94a3b8'}30`,
          borderRadius: 6, fontWeight: 600,
        }}>
          {s === 'Pass' ? 'Đạt' : s === 'Warning' ? 'Cảnh báo' : 'Không đạt'}
        </Tag>
      ),
    },
    {
      title: '',
      key: 'action',
      width: 90,
      render: (_: unknown, record: InvoiceValidationSummary) => (
        <Button type="link" icon={<FileSearchOutlined />} size="small"
          onClick={() => navigate(`/app/invoices/${record.invoiceId}`)}>
          Chi tiết
        </Button>
      ),
    },
  ];

  // ── Summary data ──
  const stats = data ? [
    {
      title: 'Lớp 1: Cấu trúc (XSD)',
      desc: 'XML well-formed, trường bắt buộc',
      color: '#1a4b8c',
      passCount: data.layer1PassCount,
      totalCount: data.totalValidated,
    },
    {
      title: 'Lớp 2: Chữ ký số',
      desc: 'Signature verify, Anti-Spoofing',
      color: '#2db791',
      passCount: data.layer2PassCount,
      totalCount: data.totalValidated,
    },
    {
      title: 'Lớp 3: Nghiệp vụ',
      desc: 'MST, toán học, ngày tháng',
      color: '#e6a817',
      passCount: data.layer3PassCount,
      totalCount: data.totalValidated,
    },
  ] : [];

  return (
    <div className="animate-fade-in-up">
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 24 }}>
        <div>
          <Title level={3} className="text-dash-textMain font-bold tracking-tight m-0">
            Rà soát rủi ro
          </Title>
          <Text className="text-dash-textMuted text-sm font-medium block mt-1">
            Kiểm tra 3 lớp: Cấu trúc XSD &rarr; Chữ ký số &rarr; Nghiệp vụ
            {data ? ` • ${data.totalValidated} hóa đơn đã kiểm tra` : ''}
          </Text>
        </div>
      </div>

      {isLoading ? (
        <div style={{ textAlign: 'center', padding: 80 }}><Spin size="large" /></div>
      ) : !data ? (
        <Empty description="Không có dữ liệu" />
      ) : (
        <>
          {/* ── Layer Overview Cards ────────────────────────── */}
          <Row gutter={16} style={{ marginBottom: 24 }}>
            {stats.map((layer, i) => (
              <Col xs={24} md={8} key={i}>
                <Card bordered={false} className="bg-dash-card rounded-[14px] shadow-dash"
                  style={{ borderTop: `3px solid ${layer.color}` }} bodyStyle={{ padding: 20 }}>
                  <Space align="start">
                    <div style={{
                      width: 40, height: 40, borderRadius: 10,
                      background: `${layer.color}14`, display: 'flex',
                      alignItems: 'center', justifyContent: 'center', color: layer.color, fontSize: 18,
                    }}>
                      <SafetyCertificateOutlined />
                    </div>
                    <div>
                      <Text strong style={{ fontSize: 14 }}>{layer.title}</Text>
                      <br />
                      <Text type="secondary" style={{ fontSize: 12 }}>{layer.desc}</Text>
                      <div style={{ marginTop: 8 }}>
                        <Text strong style={{ color: layer.color, fontSize: 20 }}>{layer.passCount}</Text>
                        <Text type="secondary" style={{ fontSize: 13 }}> / {layer.totalCount} đạt</Text>
                      </div>
                    </div>
                  </Space>
                </Card>
              </Col>
            ))}
          </Row>

          {/* ── Risk Distribution Summary ──────────────────── */}
          <Row gutter={16} style={{ marginBottom: 24 }}>
            {[
              { label: 'An toàn', count: data.greenCount, color: '#2d9a5c', pct: data.totalValidated ? Math.round(data.greenCount * 100 / data.totalValidated) : 0 },
              { label: 'Lưu ý', count: data.yellowCount, color: '#e6a817', pct: data.totalValidated ? Math.round(data.yellowCount * 100 / data.totalValidated) : 0 },
              { label: 'Cảnh báo', count: data.orangeCount, color: '#e17055', pct: data.totalValidated ? Math.round(data.orangeCount * 100 / data.totalValidated) : 0 },
              { label: 'Nguy hiểm', count: data.redCount, color: '#d63031', pct: data.totalValidated ? Math.round(data.redCount * 100 / data.totalValidated) : 0 },
            ].map((item, i) => (
              <Col xs={12} sm={6} key={i}>
                <Card bordered={false} className="bg-dash-card rounded-[14px] shadow-dash" bodyStyle={{ padding: 16, textAlign: 'center' }}>
                  <div style={{
                    width: 48, height: 48, borderRadius: '50%',
                    background: `${item.color}14`, display: 'inline-flex',
                    alignItems: 'center', justifyContent: 'center', marginBottom: 8,
                  }}>
                    <Text strong style={{ color: item.color, fontSize: 18 }}>{item.count}</Text>
                  </div>
                  <div>
                    <Text strong style={{ fontSize: 13 }}>{item.label}</Text>
                    <br />
                    <Text type="secondary" style={{ fontSize: 12 }}>{item.pct}%</Text>
                  </div>
                </Card>
              </Col>
            ))}
          </Row>

          {/* ── Results Table ──────────────────────────────── */}
          <Card bordered={false} className="bg-dash-card rounded-[14px] shadow-dash" bodyStyle={{ padding: 0 }}>
            {/* Search & Filters */}
            <div style={{ padding: '16px 24px', borderBottom: '1px solid #E2E8F0' }}>
              <Row gutter={12} align="middle">
                <Col flex="auto">
                  <Input.Search
                    placeholder="Tìm kiếm theo số hóa đơn, tên công ty, MST..."
                    value={searchText}
                    onChange={e => setSearchText(e.target.value)}
                    onSearch={val => { setKeyword(val || undefined); setPage(1); }}
                    enterButton={<SearchOutlined />}
                    style={{ borderRadius: 10 }}
                    allowClear
                    onClear={() => { setSearchText(''); setKeyword(undefined); setPage(1); }}
                  />
                </Col>
                <Col>
                  <Button
                    icon={<FilterOutlined />}
                    onClick={() => setShowFilters(!showFilters)}
                    type={showFilters ? 'primary' : 'default'}
                    style={{ borderRadius: 10, height: 32, background: showFilters ? '#4880FF' : '#fff', color: showFilters ? '#fff' : '#202224', borderColor: showFilters ? '#4880FF' : '#E2E8F0', fontWeight: 600 }}
                  >
                    Bộ lọc
                  </Button>
                </Col>
              </Row>

              {showFilters && (
                <Row gutter={12} style={{ marginTop: 12 }}>
                  <Col xs={24} sm={12}>
                    <Select
                      placeholder="Mức rủi ro"
                      style={{ width: '100%' }}
                      allowClear
                      value={riskLevel}
                      onChange={val => { setRiskLevel(val); setPage(1); }}
                      options={[
                        { value: 'Green', label: 'Green — An toàn' },
                        { value: 'Yellow', label: 'Yellow — Lưu ý' },
                        { value: 'Orange', label: 'Orange — Cảnh báo' },
                        { value: 'Red', label: 'Red — Nguy hiểm' },
                      ]}
                    />
                  </Col>
                  <Col xs={24} sm={12}>
                    <Select
                      placeholder="Kết quả kiểm tra"
                      style={{ width: '100%' }}
                      allowClear
                      value={validationStatus}
                      onChange={val => { setValidationStatus(val); setPage(1); }}
                      options={[
                        { value: 'Pass', label: 'Đạt (Pass)' },
                        { value: 'Warning', label: 'Cảnh báo (Warning)' },
                        { value: 'Fail', label: 'Không đạt (Fail)' },
                      ]}
                    />
                  </Col>
                </Row>
              )}
            </div>

            {/* Table */}
            <Table
              columns={columns}
              dataSource={data.items}
              rowKey="invoiceId"
              pagination={false}
              size="middle"
              style={{ padding: '0 8px' }}
              onRow={(record) => ({
                style: { cursor: 'pointer' },
                onClick: () => navigate(`/app/invoices/${record.invoiceId}`),
              })}
            />

            {/* Pagination - always visible */}
            <div style={{ padding: '16px 24px', borderTop: '1px solid #E2E8F0', textAlign: 'right' }}>
              <Pagination
                current={page}
                pageSize={pageSize}
                total={data.totalCount}
                showSizeChanger
                showTotal={(total) => `Tổng ${total} hóa đơn`}
                onChange={(p, ps) => { setPage(p); setPageSize(ps); }}
              />
            </div>
          </Card>
        </>
      )}
    </div>
  );
};

export default ValidationPage;
