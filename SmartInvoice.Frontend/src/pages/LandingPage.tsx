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
      <div className="glass-panel" style={{
        position: 'sticky', top: 0, zIndex: 100,
        padding: '0 40px', height: 72,
        display: 'flex', alignItems: 'center', justifyContent: 'space-between',
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
          <div style={{
            width: 40, height: 40, borderRadius: 12,
            background: 'linear-gradient(135deg, #10b981, #059669)',
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            boxShadow: '0 4px 12px rgba(16, 185, 129, 0.2)',
          }}>
            <SafetyCertificateOutlined style={{ color: '#fff', fontSize: 20 }} />
          </div>
          <Text strong style={{ fontSize: 18, color: '#0f172a', letterSpacing: '-0.02em' }}>SmartInvoice Shield</Text>
        </div>
        <Space size={16}>
          <Button type="text" onClick={() => navigate('/login')} style={{ fontWeight: 600, color: '#475569' }}>
            Đăng nhập
          </Button>
          <Button type="primary" onClick={() => navigate('/register')}
            style={{
              borderRadius: 12, height: 42, fontWeight: 600, paddingInline: 28,
              background: '#0f172a', border: 'none', boxShadow: '0 4px 12px rgba(15, 23, 42, 0.15)'
            }}>
            Đăng ký dùng thử
          </Button>
        </Space>
      </div>

      {/* Hero */}
      <div style={{
        background: 'linear-gradient(135deg, #0f172a 0%, #1e293b 100%)',
        padding: '120px 40px 100px',
        textAlign: 'center',
        position: 'relative',
        overflow: 'hidden',
      }}>
        <div style={{
          position: 'absolute', top: '-20%', right: '-10%',
          width: 600, height: 600, borderRadius: '50%',
          background: 'radial-gradient(circle, rgba(16, 185, 129, 0.15), transparent 70%)',
        }} />
        <div style={{ position: 'relative', zIndex: 1, maxWidth: 760, margin: '0 auto' }}>
          <div style={{
            display: 'inline-flex', alignItems: 'center', gap: 8,
            background: 'rgba(16, 185, 129, 0.15)', border: '1px solid rgba(16, 185, 129, 0.3)',
            borderRadius: 20, padding: '6px 16px', marginBottom: 32,
          }}>
            <RocketOutlined style={{ color: '#10b981' }} />
            <Text style={{ color: '#10b981', fontSize: 13, fontWeight: 700, letterSpacing: '0.05em', textTransform: 'uppercase' }}>AWS Cloud AI Journey 2026</Text>
          </div>

          <Title style={{ color: '#fff', fontSize: 56, fontWeight: 800, lineHeight: 1.15, margin: '0 0 24px', letterSpacing: '-0.02em' }}>
            Quản trị & Rà soát Rủi ro<br />
            <span style={{ color: '#10b981' }}>Hóa đơn Điện tử</span>
          </Title>
          <Paragraph style={{ color: '#94a3b8', fontSize: 18, lineHeight: 1.7, maxWidth: 640, margin: '0 auto 40px' }}>
            Nền tảng AI tự động hóa quy trình kiểm tra hóa đơn, phát hiện rủi ro gian lận,
            đảm bảo tuân thủ pháp luật Việt Nam.
          </Paragraph>
          <Space size={16}>
            <Button type="primary" size="large" onClick={() => navigate('/register')}
              style={{
                height: 56, borderRadius: 14, fontWeight: 600, fontSize: 16, paddingInline: 40,
                background: '#10b981', color: '#fff', border: 'none',
                boxShadow: '0 8px 20px rgba(16, 185, 129, 0.25)',
              }}
              icon={<ArrowRightOutlined />}
            >
              Bắt đầu miễn phí
            </Button>
            <Button size="large" ghost onClick={() => navigate('/login')}
              style={{
                height: 56, borderRadius: 14, fontWeight: 600, fontSize: 16, paddingInline: 40,
                color: '#fff', borderColor: 'rgba(255,255,255,0.2)',
                background: 'rgba(255,255,255,0.05)',
              }}
            >
              Đăng nhập
            </Button>
          </Space>
        </div>
      </div>

      {/* Stats */}
      <div style={{
        maxWidth: 1000, margin: '-50px auto 0', padding: '0 20px', position: 'relative', zIndex: 2,
      }}>
        <Card bordered={false} style={{
          borderRadius: 24, background: 'rgba(255, 255, 255, 0.95)', backdropFilter: 'blur(16px)',
          boxShadow: '0 20px 40px -10px rgba(0,0,0,0.1)', border: '1px solid rgba(255,255,255,0.5)',
        }} bodyStyle={{ padding: '36px 40px' }}>
          <Row gutter={[24, 24]} justify="center">
            {stats.map((s, i) => (
              <Col xs={12} sm={6} key={i} style={{ textAlign: 'center' }}>
                <Text style={{ fontSize: 36, fontWeight: 800, color: '#0f172a', display: 'block', letterSpacing: '-0.02em', marginBottom: 4 }}>{s.value}</Text>
                <Text style={{ fontSize: 14, color: '#64748b', fontWeight: 500 }}>{s.label}</Text>
              </Col>
            ))}
          </Row>
        </Card>
      </div>

      {/* Features */}
      <div style={{ maxWidth: 1200, margin: '0 auto', padding: '100px 20px 80px' }}>
        <div style={{ textAlign: 'center', marginBottom: 56 }}>
          <Title level={2} style={{ marginBottom: 12, color: '#0f172a', fontWeight: 800, letterSpacing: '-0.02em' }}>Tính năng nổi bật</Title>
          <Text style={{ fontSize: 18, color: '#64748b' }}>
            Giải pháp toàn diện cho doanh nghiệp Việt Nam
          </Text>
        </div>
        <Row gutter={[32, 32]}>
          {features.map((f, i) => (
            <Col xs={24} sm={12} lg={8} key={i}>
              <Card bordered={false} hoverable style={{
                borderRadius: 20, height: '100%',
                border: '1px solid #f1f5f9', boxShadow: '0 4px 20px -2px rgba(0, 0, 0, 0.03)',
                transition: 'all 0.3s ease',
              }} bodyStyle={{ padding: 32 }} className="hover:shadow-lg hover:-translate-y-1">
                <div style={{
                  width: 56, height: 56, borderRadius: 16,
                  background: '#f8fafc', border: '1px solid #f1f5f9',
                  display: 'flex', alignItems: 'center', justifyContent: 'center',
                  marginBottom: 20,
                }}>
                  {f.icon}
                </div>
                <Title level={4} style={{ marginBottom: 12, color: '#0f172a', fontWeight: 700 }}>{f.title}</Title>
                <Text style={{ fontSize: 15, lineHeight: 1.6, color: '#64748b' }}>{f.desc}</Text>
              </Card>
            </Col>
          ))}
        </Row>
      </div>

      {/* CTA */}
      <div style={{
        background: 'linear-gradient(135deg, #0f172a 0%, #1e293b 100%)',
        padding: '80px 40px', textAlign: 'center',
      }}>
        <Title level={2} style={{ color: '#fff', marginBottom: 16, fontWeight: 800, letterSpacing: '-0.02em' }}>
          Sẵn sàng bảo vệ doanh nghiệp bạn?
        </Title>
        <Paragraph style={{ color: '#94a3b8', fontSize: 18, marginBottom: 40, maxWidth: 600, margin: '0 auto 40px' }}>
          Đăng ký ngay để trải nghiệm hệ thống quản trị hóa đơn thông minh nhất Việt Nam.
        </Paragraph>
        <Button type="primary" size="large" onClick={() => navigate('/register')}
          style={{
            height: 56, borderRadius: 14, fontWeight: 600, paddingInline: 40, fontSize: 16,
            background: '#10b981', color: '#fff', border: 'none',
            boxShadow: '0 8px 20px rgba(16, 185, 129, 0.25)',
          }}
        >
          <CheckCircleOutlined /> Đăng ký dùng thử miễn phí
        </Button>
      </div>

      {/* Footer */}
      <div style={{ background: '#090e17', padding: '40px 40px', textAlign: 'center' }}>
        <Text style={{ color: '#475569', fontSize: 14 }}>
          © 2026 SmartInvoice Shield — AWS Cloud AI Journey. Tuân thủ NĐ 123/2020/NĐ-CP & TT 78/2021/TT-BTC.
        </Text>
      </div>
    </div>
  );
};

export default LandingPage;
