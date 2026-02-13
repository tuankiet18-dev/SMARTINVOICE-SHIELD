import React from 'react';
import { Button, Typography, Card, Row, Col, Space } from 'antd';
import {
  SafetyCertificateOutlined,
  ThunderboltOutlined,
  CloudServerOutlined,
  CheckCircleOutlined,
  RocketOutlined,
  SecurityScanOutlined,
  FileSearchOutlined,
  BarChartOutlined,
  ArrowRightOutlined,
  TeamOutlined,
} from '@ant-design/icons';
import { useNavigate } from 'react-router-dom';

const { Title, Text, Paragraph } = Typography;

const features = [
  {
    icon: <FileSearchOutlined style={{ fontSize: 28, color: '#2db791' }} />,
    title: 'Rà soát 3 lớp',
    desc: 'Kiểm tra cấu trúc XML, xác thực chữ ký số và phân tích nghiệp vụ bằng AI.',
  },
  {
    icon: <ThunderboltOutlined style={{ fontSize: 28, color: '#e6a817' }} />,
    title: 'Xử lý tức thì',
    desc: 'Upload hàng loạt, xử lý < 5 giây/hóa đơn với hạ tầng AWS Cloud.',
  },
  {
    icon: <SecurityScanOutlined style={{ fontSize: 28, color: '#1a4b8c' }} />,
    title: 'Tuân thủ pháp luật',
    desc: 'Đáp ứng NĐ 123/2020, TT 78/2021 và các quy định mới nhất.',
  },
  {
    icon: <BarChartOutlined style={{ fontSize: 28, color: '#d63031' }} />,
    title: 'Báo cáo & Phân tích',
    desc: 'Dashboard realtime, phân bổ rủi ro, xu hướng hóa đơn theo thời gian.',
  },
  {
    icon: <CloudServerOutlined style={{ fontSize: 28, color: '#6c5ce7' }} />,
    title: 'Cloud Native',
    desc: 'Kiến trúc serverless trên AWS, auto-scaling, uptime 99.9%.',
  },
  {
    icon: <TeamOutlined style={{ fontSize: 28, color: '#00b894' }} />,
    title: 'Đa người dùng',
    desc: 'Phân quyền Admin/Kế toán/Auditor, audit trail chi tiết.',
  },
];

const stats = [
  { value: '10,000+', label: 'Hóa đơn đã xử lý' },
  { value: '99.9%', label: 'Uptime hệ thống' },
  { value: '<5s', label: 'Thời gian xử lý' },
  { value: '85%+', label: 'AI Accuracy' },
];

const LandingPage: React.FC = () => {
  const navigate = useNavigate();

  return (
    <div style={{ minHeight: '100vh', background: '#fff' }}>
      {/* Navbar */}
      <div style={{
        position: 'sticky', top: 0, zIndex: 100,
        background: 'rgba(255,255,255,0.95)', backdropFilter: 'blur(12px)',
        borderBottom: '1px solid hsl(220 15% 92%)',
        padding: '0 40px', height: 64,
        display: 'flex', alignItems: 'center', justifyContent: 'space-between',
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <div style={{
            width: 36, height: 36, borderRadius: 10,
            background: 'linear-gradient(135deg, #2db791, #1a8a6a)',
            display: 'flex', alignItems: 'center', justifyContent: 'center',
          }}>
            <SafetyCertificateOutlined style={{ color: '#fff', fontSize: 18 }} />
          </div>
          <Text strong style={{ fontSize: 17, color: '#1a4b8c' }}>SmartInvoice Shield</Text>
        </div>
        <Space size={12}>
          <Button type="text" onClick={() => navigate('/login')} style={{ fontWeight: 500 }}>
            Đăng nhập
          </Button>
          <Button type="primary" onClick={() => navigate('/register')}
            style={{
              borderRadius: 10, height: 40, fontWeight: 600, paddingInline: 24,
              background: 'linear-gradient(135deg, #1a4b8c, #15396d)',
            }}>
            Đăng ký dùng thử
          </Button>
        </Space>
      </div>

      {/* Hero */}
      <div style={{
        background: 'linear-gradient(135deg, hsl(215 80% 18%) 0%, hsl(215 70% 28%) 50%, hsl(174 50% 30%) 100%)',
        padding: '100px 40px 80px',
        textAlign: 'center',
        position: 'relative',
        overflow: 'hidden',
      }}>
        <div style={{
          position: 'absolute', top: '-30%', right: '-10%',
          width: 500, height: 500, borderRadius: '50%',
          background: 'radial-gradient(circle, rgba(45,183,145,0.15), transparent 70%)',
        }} />
        <div style={{ position: 'relative', zIndex: 1, maxWidth: 720, margin: '0 auto' }}>
          <div style={{
            display: 'inline-flex', alignItems: 'center', gap: 8,
            background: 'rgba(45,183,145,0.15)', borderRadius: 20,
            padding: '6px 16px', marginBottom: 24,
          }}>
            <RocketOutlined style={{ color: '#2db791' }} />
            <Text style={{ color: '#2db791', fontSize: 13, fontWeight: 600 }}>AWS Cloud AI Journey 2026</Text>
          </div>

          <Title style={{ color: '#fff', fontSize: 48, fontWeight: 700, lineHeight: 1.2, margin: '0 0 20px' }}>
            Quản trị & Rà soát Rủi ro<br />
            <span style={{ color: '#2db791' }}>Hóa đơn Điện tử</span>
          </Title>
          <Paragraph style={{ color: 'rgba(255,255,255,0.75)', fontSize: 18, lineHeight: 1.7, maxWidth: 580, margin: '0 auto 36px' }}>
            Nền tảng AI tự động hóa quy trình kiểm tra hóa đơn, phát hiện rủi ro gian lận,
            đảm bảo tuân thủ pháp luật Việt Nam.
          </Paragraph>
          <Space size={16}>
            <Button type="primary" size="large" onClick={() => navigate('/register')}
              style={{
                height: 52, borderRadius: 12, fontWeight: 600, fontSize: 16, paddingInline: 36,
                background: 'linear-gradient(135deg, #2db791, #1a8a6a)',
                border: 'none',
              }}
              icon={<ArrowRightOutlined />}
            >
              Bắt đầu miễn phí
            </Button>
            <Button size="large" ghost onClick={() => navigate('/login')}
              style={{
                height: 52, borderRadius: 12, fontWeight: 500, fontSize: 16, paddingInline: 36,
                color: '#fff', borderColor: 'rgba(255,255,255,0.3)',
              }}
            >
              Đăng nhập
            </Button>
          </Space>
        </div>
      </div>

      {/* Stats */}
      <div style={{
        maxWidth: 900, margin: '-40px auto 0', padding: '0 20px', position: 'relative', zIndex: 2,
      }}>
        <Card bordered={false} style={{
          borderRadius: 16,
          boxShadow: '0 8px 30px rgba(0,0,0,0.1)',
        }} bodyStyle={{ padding: '28px 40px' }}>
          <Row gutter={[24, 24]} justify="center">
            {stats.map((s, i) => (
              <Col xs={12} sm={6} key={i} style={{ textAlign: 'center' }}>
                <Text style={{ fontSize: 32, fontWeight: 700, color: '#1a4b8c', display: 'block' }}>{s.value}</Text>
                <Text type="secondary" style={{ fontSize: 13 }}>{s.label}</Text>
              </Col>
            ))}
          </Row>
        </Card>
      </div>

      {/* Features */}
      <div style={{ maxWidth: 1100, margin: '0 auto', padding: '80px 20px 60px' }}>
        <div style={{ textAlign: 'center', marginBottom: 48 }}>
          <Title level={2} style={{ marginBottom: 8 }}>Tính năng nổi bật</Title>
          <Text type="secondary" style={{ fontSize: 16 }}>
            Giải pháp toàn diện cho doanh nghiệp Việt Nam
          </Text>
        </div>
        <Row gutter={[24, 24]}>
          {features.map((f, i) => (
            <Col xs={24} sm={12} lg={8} key={i}>
              <Card bordered={false} hoverable style={{
                borderRadius: 14, height: '100%',
                border: '1px solid hsl(220 15% 92%)',
              }} bodyStyle={{ padding: 28 }}>
                <div style={{
                  width: 52, height: 52, borderRadius: 14,
                  background: 'hsl(220 20% 97%)',
                  display: 'flex', alignItems: 'center', justifyContent: 'center',
                  marginBottom: 16,
                }}>
                  {f.icon}
                </div>
                <Title level={5} style={{ marginBottom: 8 }}>{f.title}</Title>
                <Text type="secondary" style={{ fontSize: 14, lineHeight: 1.6 }}>{f.desc}</Text>
              </Card>
            </Col>
          ))}
        </Row>
      </div>

      {/* CTA */}
      <div style={{
        background: 'linear-gradient(135deg, hsl(215 80% 18%), hsl(174 50% 30%))',
        padding: '60px 40px', textAlign: 'center',
      }}>
        <Title level={3} style={{ color: '#fff', marginBottom: 12 }}>
          Sẵn sàng bảo vệ doanh nghiệp bạn?
        </Title>
        <Paragraph style={{ color: 'rgba(255,255,255,0.75)', fontSize: 16, marginBottom: 28 }}>
          Đăng ký ngay để trải nghiệm hệ thống quản trị hóa đơn thông minh nhất Việt Nam.
        </Paragraph>
        <Button type="primary" size="large" onClick={() => navigate('/register')}
          style={{
            height: 50, borderRadius: 12, fontWeight: 600, paddingInline: 36,
            background: 'linear-gradient(135deg, #2db791, #1a8a6a)', border: 'none',
          }}
        >
          <CheckCircleOutlined /> Đăng ký dùng thử miễn phí
        </Button>
      </div>

      {/* Footer */}
      <div style={{ background: 'hsl(215 80% 14%)', padding: '32px 40px', textAlign: 'center' }}>
        <Text style={{ color: 'rgba(200,210,225,0.5)', fontSize: 13 }}>
          © 2026 SmartInvoice Shield — AWS Cloud AI Journey. Tuân thủ NĐ 123/2020/NĐ-CP & TT 78/2021/TT-BTC.
        </Text>
      </div>
    </div>
  );
};

export default LandingPage;
