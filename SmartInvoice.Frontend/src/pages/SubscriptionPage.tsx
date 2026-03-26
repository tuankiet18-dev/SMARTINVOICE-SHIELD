import React, { useState } from 'react';
import {
  Card, Row, Col, Button, Tag, Spin, Typography, Segmented, Badge,
  Modal, List, Divider, message, Alert, Table, Space, Tooltip, Descriptions,
} from 'antd';
import {
  CrownOutlined, RocketOutlined, ThunderboltOutlined, StarOutlined,
  CheckCircleFilled, CloseCircleFilled, ShoppingCartOutlined,
  HistoryOutlined, SafetyCertificateOutlined, CalendarOutlined,
  TeamOutlined, FileTextOutlined, CloudOutlined, PlusCircleOutlined,
} from '@ant-design/icons';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  paymentService,
  type SubscriptionPackageDto,
  type CreatePaymentRequest,
  type PaymentHistoryDto,
  type AddonInfoDto,
} from '@/services/payment';
import { useAuth } from '@/contexts/AuthContext';
import dayjs from 'dayjs';

const { Title, Text, Paragraph } = Typography;

type BillingCycle = 'Monthly' | 'SemiAnnual' | 'Annual';

const billingLabels: Record<BillingCycle, string> = {
  Monthly: '1 tháng',
  SemiAnnual: '6 tháng',
  Annual: '1 năm',
};

const packageIcons: Record<string, React.ReactNode> = {
  FREE: <StarOutlined style={{ fontSize: 28, color: '#8c8c8c' }} />,
  STARTER: <RocketOutlined style={{ fontSize: 28, color: '#1890ff' }} />,
  PRO: <ThunderboltOutlined style={{ fontSize: 28, color: '#722ed1' }} />,
  ENTERPRISE: <CrownOutlined style={{ fontSize: 28, color: '#fa8c16' }} />,
};

const packageColors: Record<string, string> = {
  FREE: '#8c8c8c',
  STARTER: '#1890ff',
  PRO: '#722ed1',
  ENTERPRISE: '#fa8c16',
};

const formatVnd = (amount: number) =>
  new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(amount);

const getPrice = (pkg: SubscriptionPackageDto, cycle: BillingCycle) => {
  switch (cycle) {
    case 'SemiAnnual': return pkg.pricePerSixMonths;
    case 'Annual': return pkg.pricePerYear;
    default: return pkg.pricePerMonth;
  }
};

const getMonthlyEquivalent = (pkg: SubscriptionPackageDto, cycle: BillingCycle) => {
  switch (cycle) {
    case 'SemiAnnual': return pkg.pricePerSixMonths / 6;
    case 'Annual': return pkg.pricePerYear / 12;
    default: return pkg.pricePerMonth;
  }
};

const getSavingsPercent = (pkg: SubscriptionPackageDto, cycle: BillingCycle) => {
  if (pkg.pricePerMonth === 0) return 0;
  const monthly = pkg.pricePerMonth;
  switch (cycle) {
    case 'SemiAnnual': return Math.round((1 - pkg.pricePerSixMonths / (monthly * 6)) * 100);
    case 'Annual': return Math.round((1 - pkg.pricePerYear / (monthly * 12)) * 100);
    default: return 0;
  }
};

const statusColors: Record<string, string> = {
  Pending: 'processing',
  Success: 'success',
  Failed: 'error',
  Cancelled: 'default',
};

const SubscriptionPage: React.FC = () => {
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const [billingCycle, setBillingCycle] = useState<BillingCycle>('Monthly');
  const [showHistory, setShowHistory] = useState(false);

  // Queries
  const { data: packages, isLoading: loadingPkgs } = useQuery({
    queryKey: ['subscription-packages'],
    queryFn: paymentService.getPackages,
  });

  const { data: currentSub, isLoading: loadingSub } = useQuery({
    queryKey: ['current-subscription'],
    queryFn: paymentService.getCurrentSubscription,
  });

  const { data: history, isLoading: loadingHistory } = useQuery({
    queryKey: ['payment-history'],
    queryFn: paymentService.getPaymentHistory,
    enabled: showHistory,
  });

  const { data: addons } = useQuery({
    queryKey: ['addons'],
    queryFn: paymentService.getAddons,
  });

  // Mutation
  const createPayment = useMutation({
    mutationFn: (data: CreatePaymentRequest) => paymentService.createPayment(data),
    onSuccess: (res) => {
      // Redirect to VNPay
      window.location.href = res.paymentUrl;
    },
    onError: (err: any) => {
      message.error(err?.response?.data?.message ?? 'Có lỗi xảy ra khi tạo thanh toán.');
    },
  });

  const createAddonPayment = useMutation({
    mutationFn: (addonCode: string) => paymentService.createAddonPayment({ addonCode }),
    onSuccess: (res) => {
      window.location.href = res.paymentUrl;
    },
    onError: (err: any) => {
      message.error(err?.response?.data?.message ?? 'Có lỗi xảy ra khi mua Add-on.');
    },
  });

  const handleAddonPurchase = (addon: AddonInfoDto) => {
    Modal.confirm({
      title: 'Xác nhận mua Add-on',
      icon: <PlusCircleOutlined style={{ color: '#52c41a' }} />,
      content: (
        <div>
          <Descriptions column={1} size="small" className="mt-3">
            <Descriptions.Item label="Gói">{addon.addonName}</Descriptions.Item>
            <Descriptions.Item label="Số lượng">+{addon.invoiceCount} hóa đơn</Descriptions.Item>
            <Descriptions.Item label="Thành tiền">
              <Text strong style={{ color: '#cf1322', fontSize: 16 }}>{formatVnd(addon.price)}</Text>
            </Descriptions.Item>
          </Descriptions>
          <Alert
            className="mt-3"
            type="info"
            showIcon
            message="Hóa đơn mua thêm không có thời hạn sử dụng."
          />
        </div>
      ),
      okText: 'Thanh toán ngay',
      cancelText: 'Hủy',
      onOk: () => {
        createAddonPayment.mutate(addon.addonCode);
      },
    });
  };

  const handlePurchase = (pkg: SubscriptionPackageDto) => {
    const price = getPrice(pkg, billingCycle);
    if (price <= 0) {
      message.info('Gói Free không cần thanh toán. Hãy liên hệ admin để được kích hoạt.');
      return;
    }

    Modal.confirm({
      title: 'Xác nhận mua gói',
      icon: <ShoppingCartOutlined style={{ color: packageColors[pkg.packageCode] }} />,
      content: (
        <div>
          <Descriptions column={1} size="small" className="mt-3">
            <Descriptions.Item label="Gói">{pkg.packageName}</Descriptions.Item>
            <Descriptions.Item label="Chu kỳ">{billingLabels[billingCycle]}</Descriptions.Item>
            <Descriptions.Item label="Thành tiền">
              <Text strong style={{ color: '#cf1322', fontSize: 16 }}>{formatVnd(price)}</Text>
            </Descriptions.Item>
          </Descriptions>
          <Alert
            className="mt-3"
            type="info"
            showIcon
            message="Bạn sẽ được chuyển đến cổng thanh toán VNPay để hoàn tất giao dịch."
          />
        </div>
      ),
      okText: 'Thanh toán ngay',
      cancelText: 'Hủy',
      onOk: () => {
        createPayment.mutate({ packageId: pkg.packageId, billingCycle });
      },
    });
  };

  const isCurrentPackage = (pkgCode: string) =>
    currentSub?.packageCode === pkgCode || currentSub?.subscriptionTier === pkgCode;

  const historyColumns = [
    {
      title: 'Thời gian',
      dataIndex: 'createdAt',
      key: 'createdAt',
      render: (v: string) => dayjs(v).format('DD/MM/YYYY HH:mm'),
    },
    { title: 'Gói', dataIndex: 'packageName', key: 'packageName' },
    {
      title: 'Chu kỳ',
      dataIndex: 'billingCycle',
      key: 'billingCycle',
      render: (v: string) => billingLabels[v as BillingCycle] ?? v,
    },
    {
      title: 'Số tiền',
      dataIndex: 'amount',
      key: 'amount',
      render: (v: number) => formatVnd(v),
    },
    {
      title: 'Trạng thái',
      dataIndex: 'status',
      key: 'status',
      render: (v: string) => <Tag color={statusColors[v] ?? 'default'}>{v}</Tag>,
    },
    {
      title: 'Mã GD VNPay',
      dataIndex: 'vnpTransactionNo',
      key: 'vnpTransactionNo',
      render: (v: string | null) => v ?? '—',
    },
  ];

  if (loadingPkgs || loadingSub) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <Spin size="large" description="Đang tải thông tin gói..." />
      </div>
    );
  }

  return (
    <div className="bg-dash-bg p-6 md:p-8 min-h-screen">
      {/* Header */}
      <div className="mb-6 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div>
          <Title level={3} className="!mb-1">Quản lý gói dịch vụ</Title>
          <Text type="secondary">Chọn gói phù hợp nhất với nhu cầu doanh nghiệp của bạn</Text>
        </div>
        <Button
          icon={<HistoryOutlined />}
          onClick={() => setShowHistory(!showHistory)}
        >
          {showHistory ? 'Ẩn lịch sử' : 'Lịch sử thanh toán'}
        </Button>
      </div>

      {/* Current Subscription Info */}
      {currentSub && currentSub.packageCode && (
        <Alert
          className="mb-6"
          type="success"
          showIcon
          icon={<SafetyCertificateOutlined />}
          message={
            <span>
              Gói hiện tại: <strong>{currentSub.packageName ?? currentSub.subscriptionTier}</strong>
              {currentSub.subscriptionExpiredAt && (
                <span> — Hết hạn: <strong>{dayjs(currentSub.subscriptionExpiredAt).format('DD/MM/YYYY')}</strong></span>
              )}
            </span>
          }
        />
      )}

      {/* Payment History */}
      {showHistory && (
        <Card className="mb-6" title="Lịch sử thanh toán" size="small">
          <Table
            dataSource={history ?? []}
            columns={historyColumns}
            rowKey="transactionId"
            loading={loadingHistory}
            pagination={{ pageSize: 5 }}
            size="small"
            locale={{ emptyText: 'Chưa có giao dịch nào' }}
          />
        </Card>
      )}

      {/* Billing Cycle Selector */}
      <div className="flex justify-center mb-8">
        <Segmented
          size="large"
          value={billingCycle}
          onChange={(val) => setBillingCycle(val as BillingCycle)}
          options={[
            { label: '1 tháng', value: 'Monthly' },
            { label: '6 tháng (Tiết kiệm ~17%)', value: 'SemiAnnual' },
            { label: '1 năm (Tiết kiệm ~17%)', value: 'Annual' },
          ]}
        />
      </div>

      {/* Package Cards */}
      <Row gutter={[24, 24]} justify="center">
        {packages?.map((pkg) => {
          const isCurrent = isCurrentPackage(pkg.packageCode);
          const price = getPrice(pkg, billingCycle);
          const savings = getSavingsPercent(pkg, billingCycle);
          const monthlyEq = getMonthlyEquivalent(pkg, billingCycle);
          const color = packageColors[pkg.packageCode] ?? '#1890ff';
          const isPro = pkg.packageCode === 'PRO';

          // Downgrade check: block if current subscription is active and target package level is lower or equal
          const isSubscriptionActive = currentSub?.subscriptionExpiredAt
            ? new Date(currentSub.subscriptionExpiredAt) > new Date()
            : false;
          const isDowngrade = isSubscriptionActive
            && (currentSub?.packageLevel ?? 0) >= pkg.packageLevel
            && !isCurrent;
          const isDisabled = isCurrent || price <= 0 || isDowngrade;

          return (
            <Col xs={24} sm={12} lg={6} key={pkg.packageId}>
              <Badge.Ribbon
                text={isPro ? 'Phổ biến nhất' : savings > 0 ? `Tiết kiệm ${savings}%` : ''}
                color={isPro ? 'purple' : savings > 0 ? 'green' : 'transparent'}
                style={{ display: (isPro || savings > 0) ? undefined : 'none' }}
              >
                <Card
                  hoverable
                  className={`h-full transition-all ${isPro ? 'ring-2 ring-purple-400 shadow-lg' : ''} ${isCurrent ? 'border-green-400 border-2' : ''}`}
                  styles={{ body: { padding: '24px 20px', display: 'flex', flexDirection: 'column', height: '100%' } }}
                >
                  {/* Icon & Title */}
                  <div className="text-center mb-4">
                    {packageIcons[pkg.packageCode]}
                    <Title level={5} className="!mt-2 !mb-0">{pkg.packageName}</Title>
                    {isCurrent && <Tag color="green" className="mt-1">Gói hiện tại</Tag>}
                  </div>

                  {/* Price */}
                  <div className="text-center mb-4">
                    {price === 0 ? (
                      <Title level={2} className="!mb-0" style={{ color }}>Miễn phí</Title>
                    ) : (
                      <>
                        <Title level={3} className="!mb-0" style={{ color }}>{formatVnd(price)}</Title>
                        <Text type="secondary">
                          /{billingLabels[billingCycle].toLowerCase()}
                        </Text>
                        {billingCycle !== 'Monthly' && (
                          <div>
                            <Text type="secondary" className="text-xs">
                              ~ {formatVnd(monthlyEq)}/tháng
                            </Text>
                          </div>
                        )}
                      </>
                    )}
                  </div>

                  <Divider className="!my-3" />

                  {/* Description */}
                  <Paragraph type="secondary" className="text-center text-sm !mb-4 flex-none min-h-[60px]">
                    {pkg.description}
                  </Paragraph>

                  {/* Quotas */}
                  <div className="space-y-2 mb-4 flex-1">
                    <div className="flex items-center gap-2">
                      <TeamOutlined style={{ color }} />
                      <Text>{pkg.maxUsers >= 999 ? 'Không giới hạn' : pkg.maxUsers} người dùng</Text>
                    </div>
                    <div className="flex items-center gap-2">
                      <FileTextOutlined style={{ color }} />
                      <Text>Xử lý {pkg.maxInvoicesPerMonth >= 99999 ? 'không giới hạn' : pkg.maxInvoicesPerMonth.toLocaleString()} hóa đơn / tháng</Text>
                    </div>
                    <div className="flex items-center gap-2">
                      <CloudOutlined style={{ color }} />
                      <Text>{pkg.storageQuotaGB} GB lưu trữ</Text>
                    </div>
                  </div>

                  {/* Features */}
                  <div className="space-y-1.5 mb-5">
                    <FeatureItem enabled={pkg.hasAiProcessing} label="AI xử lý hóa đơn" />
                    <FeatureItem enabled={pkg.hasAdvancedWorkflow} label="Quy trình duyệt nhiều cấp" />
                    <FeatureItem enabled={pkg.hasRiskWarning} label="Cảnh báo rủi ro thuế" />
                    <FeatureItem enabled={pkg.hasAuditLog} label="Nhật ký kiểm toán" />
                    <FeatureItem enabled={pkg.hasErpIntegration} label="Tích hợp ERP/API" />
                  </div>

                  {/* CTA Button */}
                  <Tooltip
                    title={isDowngrade ? 'Không thể hạ cấp khi gói hiện tại vẫn còn hiệu lực.' : undefined}
                  >
                    <div>
                      <Button
                        type={isCurrent ? 'default' : 'primary'}
                        block
                        size="large"
                        disabled={isDisabled}
                        loading={createPayment.isPending}
                        style={!isCurrent && price > 0 && !isDowngrade ? { background: color, borderColor: color } : undefined}
                        icon={<ShoppingCartOutlined />}
                        onClick={() => handlePurchase(pkg)}
                      >
                        {isCurrent ? 'Đang sử dụng' : isDowngrade ? 'Không khả dụng' : price <= 0 ? 'Miễn phí' : 'Mua ngay'}
                      </Button>
                    </div>
                  </Tooltip>
                </Card>
              </Badge.Ribbon>
            </Col>
          );
        })}
      </Row>

      {/* Quota Usage Info */}
      {currentSub && currentSub.packageCode && (
        <Alert
          className="mt-6"
          type="info"
          showIcon
          icon={<FileTextOutlined />}
          message={
            <span>
              Đã dùng: <strong>{currentSub.usedInvoicesThisMonth}/{currentSub.maxInvoicesPerMonth}</strong> hóa đơn tháng này
              {currentSub.extraInvoicesBalance > 0 && (
                <span> — Hóa đơn mua thêm (Còn lại): <strong>{currentSub.extraInvoicesBalance}</strong></span>
              )}
            </span>
          }
        />
      )}

      {/* Add-on Section */}
      {addons && addons.length > 0 && (
        <div className="mt-8">
          <Divider />
          <div className="text-center mb-6">
            <Title level={4} className="!mb-1">
              <PlusCircleOutlined style={{ color: '#52c41a', marginRight: 8 }} />
              Hết dung lượng? Mua thêm hóa đơn lẻ
            </Title>
            <Text type="secondary">Hóa đơn mua thêm không có thời hạn sử dụng, dùng khi nào hết khi đó.</Text>
          </div>
          <Row gutter={[16, 16]} justify="center">
            {addons.map((addon) => (
              <Col xs={24} sm={12} md={8} key={addon.addonCode}>
                <Card
                  hoverable
                  className="text-center"
                  styles={{ body: { padding: '24px 20px' } }}
                >
                  <PlusCircleOutlined style={{ fontSize: 36, color: '#52c41a', marginBottom: 12 }} />
                  <Title level={4} className="!mb-1" style={{ color: '#52c41a' }}>
                    +{addon.invoiceCount} Hóa đơn
                  </Title>
                  <Title level={3} className="!mb-1 !mt-2" style={{ color: '#cf1322' }}>
                    {formatVnd(addon.price)}
                  </Title>
                  <Text type="secondary" className="block mb-4">{addon.description}</Text>
                  <Button
                    type="primary"
                    block
                    size="large"
                    loading={createAddonPayment.isPending}
                    style={{ background: '#52c41a', borderColor: '#52c41a' }}
                    icon={<ShoppingCartOutlined />}
                    onClick={() => handleAddonPurchase(addon)}
                  >
                    Mua ngay
                  </Button>
                </Card>
              </Col>
            ))}
          </Row>
        </div>
      )}
    </div>
  );
};

const FeatureItem: React.FC<{ enabled: boolean; label: string }> = ({ enabled, label }) => (
  <div className="flex items-center gap-2">
    {enabled ? (
      <CheckCircleFilled style={{ color: '#52c41a', fontSize: 14 }} />
    ) : (
      <CloseCircleFilled style={{ color: '#d9d9d9', fontSize: 14 }} />
    )}
    <Text className={!enabled ? 'text-gray-400' : ''}>{label}</Text>
  </div>
);

export default SubscriptionPage;
