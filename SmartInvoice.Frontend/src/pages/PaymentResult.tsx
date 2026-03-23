import React, { useEffect } from 'react';
import { Result, Card, Spin, Descriptions, Button, Typography, Tag } from 'antd';
import {
  CheckCircleOutlined,
  CloseCircleOutlined,
  ArrowLeftOutlined,
  HomeOutlined,
} from '@ant-design/icons';
import { useSearchParams, useNavigate } from 'react-router-dom';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { paymentService } from '@/services/payment';
import dayjs from 'dayjs';

const { Text } = Typography;

const formatVnd = (amount: number) =>
  new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(amount);

const billingLabels: Record<string, string> = {
  Monthly: 'Hàng tháng',
  SemiAnnual: '6 tháng',
  Annual: '1 năm',
};

const PaymentResult: React.FC = () => {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  // Build the query string from URL params (VNPay redirects back with these)
  const queryString = searchParams.toString();

  const { data: result, isLoading, isError, error } = useQuery({
    queryKey: ['payment-result', queryString],
    queryFn: () => paymentService.getVnPayResult(queryString),
    enabled: !!queryString,
    retry: false,
  });

  // Invalidate subscription cache on success
  useEffect(() => {
    if (result?.status === 'Success') {
      queryClient.invalidateQueries({ queryKey: ['current-subscription'] });
      queryClient.invalidateQueries({ queryKey: ['payment-history'] });
    }
  }, [result, queryClient]);

  if (!queryString) {
    return (
      <div className="flex items-center justify-center min-h-screen bg-dash-bg">
        <Result
          status="warning"
          title="Không tìm thấy thông tin thanh toán"
          subTitle="Vui lòng thực hiện thanh toán từ trang quản lý gói dịch vụ."
          extra={
            <Button type="primary" icon={<ArrowLeftOutlined />} onClick={() => navigate('/app/subscription')}>
              Quay lại trang gói dịch vụ
            </Button>
          }
        />
      </div>
    );
  }

  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-screen bg-dash-bg">
        <Spin size="large" description="Đang xử lý kết quả thanh toán..." />
      </div>
    );
  }

  if (isError) {
    return (
      <div className="flex items-center justify-center min-h-screen bg-dash-bg p-6">
        <Result
          status="error"
          title="Lỗi xử lý thanh toán"
          subTitle={(error as any)?.response?.data?.message ?? 'Có lỗi xảy ra khi xác minh giao dịch.'}
          extra={
            <Button type="primary" icon={<ArrowLeftOutlined />} onClick={() => navigate('/app/subscription')}>
              Quay lại
            </Button>
          }
        />
      </div>
    );
  }

  const isSuccess = result?.status === 'Success';

  return (
    <div className="flex items-center justify-center min-h-screen bg-dash-bg p-6">
      <Card className="w-full max-w-lg shadow-lg">
        <Result
          icon={isSuccess
            ? <CheckCircleOutlined style={{ color: '#52c41a' }} />
            : <CloseCircleOutlined style={{ color: '#ff4d4f' }} />
          }
          status={isSuccess ? 'success' : 'error'}
          title={isSuccess ? 'Thanh toán thành công!' : 'Thanh toán thất bại'}
          subTitle={result?.message}
        />

        <Descriptions bordered column={1} size="small" className="mt-4">
          <Descriptions.Item label="Gói dịch vụ">
            <Text strong>{result?.packageName ?? '—'}</Text>
          </Descriptions.Item>
          <Descriptions.Item label="Chu kỳ">
            {billingLabels[result?.billingCycle ?? ''] ?? result?.billingCycle}
          </Descriptions.Item>
          <Descriptions.Item label="Số tiền">
            <Text strong style={{ color: '#cf1322' }}>
              {formatVnd(result?.amount ?? 0)}
            </Text>
          </Descriptions.Item>
          <Descriptions.Item label="Trạng thái">
            <Tag color={isSuccess ? 'success' : 'error'}>{result?.status}</Tag>
          </Descriptions.Item>
          {result?.vnpTransactionNo && (
            <Descriptions.Item label="Mã GD VNPay">
              {result.vnpTransactionNo}
            </Descriptions.Item>
          )}
          {result?.bankCode && (
            <Descriptions.Item label="Ngân hàng">{result.bankCode}</Descriptions.Item>
          )}
          {result?.payDate && (
            <Descriptions.Item label="Thời gian thanh toán">
              {result.payDate.length === 14
                ? dayjs(result.payDate, 'YYYYMMDDHHmmss').format('DD/MM/YYYY HH:mm:ss')
                : result.payDate}
            </Descriptions.Item>
          )}
        </Descriptions>

        <div className="flex gap-3 mt-6 justify-center">
          <Button icon={<ArrowLeftOutlined />} onClick={() => navigate('/app/subscription')}>
            Xem gói dịch vụ
          </Button>
          <Button type="primary" icon={<HomeOutlined />} onClick={() => navigate('/app/dashboard')}>
            Về trang chủ
          </Button>
        </div>
      </Card>
    </div>
  );
};

export default PaymentResult;
