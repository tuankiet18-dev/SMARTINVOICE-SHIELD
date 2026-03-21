import React, { useState } from 'react';
import { Card, Row, Col, Typography, Tag, Table, Badge, Space, Button, Input, Select, Spin, Empty, Pagination, Tooltip } from 'antd';
import {
  SafetyCertificateOutlined, CheckCircleOutlined, WarningOutlined,
  CloseCircleOutlined, FileSearchOutlined, SearchOutlined, FilterOutlined,
} from '@ant-design/icons';
import { useQuery } from '@tanstack/react-query';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { validationService, type InvoiceValidationSummary } from '../services/validation';

const { Title, Text } = Typography;

const riskColors: Record<string, string> = {
  Green: '#2d9a5c', Yellow: '#e6a817', Red: '#d63031',
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
  const [searchParams, setSearchParams] = useSearchParams();
  const [showFilters, setShowFilters] = useState(() => !!(searchParams.get('layerIssue') || searchParams.get('validationStatus')));

  const keyword = searchParams.get('keyword') || undefined;
  const layerIssue = searchParams.get('layerIssue') || undefined;
  const validationStatus = searchParams.get('validationStatus') || undefined;
  const page = Number(searchParams.get('page')) || 1;
  const pageSize = Number(searchParams.get('pageSize')) || 20;

  const [searchText, setSearchText] = useState(keyword || '');

  const updateParams = (updates: Record<string, string | undefined>) => {
    const newParams = new URLSearchParams(searchParams);
    Object.entries(updates).forEach(([key, value]) => {
      if (value !== undefined && value !== '') newParams.set(key, value);
      else newParams.delete(key);
    });
    setSearchParams(newParams, { replace: true });
  };

  const { data, isLoading } = useQuery({
    queryKey: ['validation-overview', page, pageSize, keyword, layerIssue, validationStatus],
    queryFn: () => validationService.getOverview({ page, pageSize, keyword, layerIssue, validationStatus }),
  });

  const columns = [
    {
      title: 'Hóa đơn',
      key: 'invoiceNo',
      render: (_: unknown, record: InvoiceValidationSummary) => (
        <div>
          <Space size={6} align="center">
            <Text strong style={{ color: '#1a4b8c' }}>{record.invoiceNumber || 'N/A'}</Text>
            <Tag color="blue" style={{ marginRight: 0, fontSize: 11, lineHeight: '18px', padding: '0 5px' }}>
              v{record.version}{record.isLatest ? ' (Mới nhất)' : ''}
            </Tag>
          </Space>
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
      width: 95,
      render: (v: string | null) => layerIcon(v),
    },
    {
      title: 'Lớp 2: Chữ ký',
      dataIndex: 'layer2Status',
      key: 'layer2',
      align: 'center' as const,
      width: 95,
      render: (v: string | null) => layerIcon(v),
    },
    {
      title: 'Lớp 3: Nghiệp vụ',
      dataIndex: 'layer3Status',
      key: 'layer3',
      align: 'center' as const,
      width: 95,
      render: (v: string | null) => layerIcon(v),
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
      title: 'Chi tiết',
      key: 'action',
      width: 100,
      render: (_: unknown, record: InvoiceValidationSummary) => (
        <Button
          type="text"
          icon={<FileSearchOutlined style={{ fontSize: 16 }} />}
          size="small"
          onClick={(e) => { e.stopPropagation(); navigate(`/app/invoices/${record.invoiceId}`); }}
          style={{
            color: '#1a4b8c',
            fontWeight: 600,
            borderRadius: 8,
            transition: 'all 0.2s',
          }}
          className="validation-detail-btn"
        >
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
      {/* Inline styles for CTA hover effect */}
      <style>{`
        .validation-detail-btn:hover {
          background: #e8f0fe !important;
          color: #1a4b8c !important;
        }
        .validation-expandable-table .ant-table-row-expand-icon-cell,
        .validation-expandable-table .ant-table-expand-icon-th {
          width: 32px !important;
          min-width: 32px !important;
          padding-left: 8px !important;
        }
        .validation-expandable-table .ant-table-expanded-row > td {
          background: #f8fafc !important;
        }
      `}</style>

      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 24 }}>
        <div>
          <Title level={3} className="text-dash-textMain font-bold tracking-tight m-0">
            Rà soát rủi ro
          </Title>
          <Text className="text-dash-textMuted text-sm font-medium block mt-1">
            Kiểm tra 3 lớp: Cấu trúc XSD &rarr; Chữ ký số &rarr; Nghiệp vụ
            {data ? ` • ${data.totalValidationRuns} lượt rà soát / ${data.totalUniqueInvoices} hóa đơn` : ''}
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
              { label: 'Đạt', count: data.greenCount, color: '#2d9a5c', pct: data.totalValidated ? Math.round(data.greenCount * 100 / data.totalValidated) : 0 },
              { label: 'Lưu ý', count: data.yellowCount, color: '#e6a817', pct: data.totalValidated ? Math.round(data.yellowCount * 100 / data.totalValidated) : 0 },
              { label: 'Không đạt', count: data.redCount, color: '#d63031', pct: data.totalValidated ? Math.round(data.redCount * 100 / data.totalValidated) : 0 },
            ].map((item, i) => (
              <Col xs={24} sm={8} key={i}>
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
                    onSearch={val => updateParams({ keyword: val || undefined, page: '1' })}
                    enterButton={<SearchOutlined />}
                    style={{ borderRadius: 10 }}
                    allowClear
                    onClear={() => { setSearchText(''); updateParams({ keyword: undefined, page: '1' }); }}
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
                      placeholder="Lọc theo lỗi / cảnh báo tại lớp"
                      style={{ width: '100%' }}
                      allowClear
                      value={layerIssue}
                      onChange={val => updateParams({ layerIssue: val, page: '1' })}
                      options={[
                        { value: 'layer1', label: 'Lớp 1: Cấu trúc (XSD)' },
                        { value: 'layer2', label: 'Lớp 2: Chữ ký số' },
                        { value: 'layer3', label: 'Lớp 3: Nghiệp vụ' },
                      ]}
                    />
                  </Col>
                  <Col xs={24} sm={12}>
                    <Select
                      placeholder="Kết quả kiểm tra"
                      style={{ width: '100%' }}
                      allowClear
                      value={validationStatus}
                      onChange={val => updateParams({ validationStatus: val, page: '1' })}
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

            {/* Table with Expandable Rows */}
            <Table
              className="validation-expandable-table"
              columns={columns}
              dataSource={data.items}
              rowKey="invoiceId"
              pagination={false}
              size="middle"
              style={{ padding: '0 8px' }}
              expandable={{
                expandedRowRender: (record) =>
                  record.children && record.children.length > 0 ? (
                    <Table
                      columns={columns}
                      dataSource={record.children}
                      rowKey="invoiceId"
                      pagination={false}
                      size="small"
                      showHeader={false}
                      style={{ margin: '-8px 0' }}
                      onRow={(childRecord) => ({
                        style: { cursor: 'pointer', background: '#f8fafc' },
                        onClick: () => navigate(`/app/invoices/${childRecord.invoiceId}`),
                      })}
                    />
                  ) : null,
                rowExpandable: (record) => !!(record.children && record.children.length > 0),
              }}
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
                onChange={(p, ps) => updateParams({ page: String(p), pageSize: String(ps) })}
              />
            </div>
          </Card>
        </>
      )}
    </div>
  );
};

export default ValidationPage;
