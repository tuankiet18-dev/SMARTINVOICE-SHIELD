import React from 'react';
import { Card, Form, Input, Button, Typography, Checkbox, Divider } from 'antd';
import { UserOutlined, LockOutlined, SafetyCertificateOutlined } from '@ant-design/icons';
import { useNavigate } from 'react-router-dom';

const { Title, Text, Paragraph } = Typography;

import { message, Modal } from 'antd';
import { authService, LoginRequest } from '@/services/auth';
import { useAuth } from '@/contexts/AuthContext';

const Login: React.FC = () => {
  const navigate = useNavigate();
  const [loading, setLoading] = React.useState(false);
  const [passwordForm] = Form.useForm();
  const { login } = useAuth();

  // New Password Challenge State
  const [showNewPasswordModal, setShowNewPasswordModal] = React.useState(false);
  const [challengeSession, setChallengeSession] = React.useState('');
  const [loginEmail, setLoginEmail] = React.useState('');

  const onFinish = async (values: LoginRequest) => {
    try {
      setLoading(true);
      const data = await login(values);

      if (data.challengeName === 'NEW_PASSWORD_REQUIRED' && data.session) {
        setChallengeSession(data.session);
        setLoginEmail(values.email);
        setShowNewPasswordModal(true);
        return; // Stop here, wait for new password
      }

      message.success('Đăng nhập thành công!');
      navigate('/app/dashboard');
    } catch (error: any) {
      message.error(error.response?.data?.message || 'Đăng nhập thất bại');
    } finally {
      setLoading(false);
    }
  };

  const onFinishNewPassword = async (values: any) => {
    try {
      setLoading(true);
      const data = await authService.respondNewPassword({
        email: loginEmail,
        newPassword: values.newPassword,
        session: challengeSession
      });

      localStorage.setItem('token', data.accessToken);
      localStorage.setItem('idToken', data.idToken);
      localStorage.setItem('user', JSON.stringify(data.user));
      // Triggers checkAuth indirectly or you could expose checkAuth from useAuth
      window.dispatchEvent(new Event('storage'));

      message.success('Đổi mật khẩu và đăng nhập thành công!');
      setShowNewPasswordModal(false);
      navigate('/app/dashboard');
    } catch (error: any) {
      message.error(error.response?.data?.message || 'Đổi mật khẩu thất bại');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div style={{
      minHeight: '100vh',
      display: 'flex',
      background: 'linear-gradient(135deg, #0f172a 0%, #1e293b 100%)',
    }}>
      {/* Left Panel */}
      <div style={{
        flex: 1,
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'center',
        padding: '60px 80px',
        color: '#fff',
        maxWidth: 600,
      }}
        className="hidden lg:flex"
      >
        <div style={{
          width: 64, height: 64, borderRadius: 16,
          background: 'linear-gradient(135deg, #10b981, #059669)',
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          marginBottom: 36, boxShadow: '0 8px 24px rgba(16, 185, 129, 0.3)',
        }}>
          <SafetyCertificateOutlined style={{ fontSize: 32, color: '#fff' }} />
        </div>

        <Title level={1} style={{ color: '#fff', marginBottom: 16, fontWeight: 800, fontSize: 48, letterSpacing: '-0.02em', lineHeight: 1.1 }}>
          SmartInvoice<br />Shield
        </Title>
        <Paragraph style={{ color: 'rgba(255,255,255,0.75)', fontSize: 16, lineHeight: 1.7, maxWidth: 420 }}>
          Hệ thống quản trị và rà soát rủi ro hóa đơn điện tử.
          Tự động hóa quy trình, đảm bảo tuân thủ pháp luật Việt Nam.
        </Paragraph>

        <div style={{ marginTop: 48, display: 'flex', gap: 40 }}>
          {[
            { value: '90%', label: 'Tiết kiệm thời gian' },
            { value: '100%', label: 'Tuân thủ pháp lý' },
            { value: '85%+', label: 'AI Accuracy' },
          ].map((stat, i) => (
            <div key={i}>
              <Text style={{ color: '#10b981', fontSize: 32, fontWeight: 800, display: 'block', letterSpacing: '-0.02em' }}>{stat.value}</Text>
              <Text style={{ color: '#94a3b8', fontSize: 13, fontWeight: 500 }}>{stat.label}</Text>
            </div>
          ))}
        </div>
      </div>

      {/* Right Panel */}
      <div style={{
        flex: 1,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        padding: 24,
      }}>
        <Card
          bordered={false}
          style={{
            width: '100%',
            maxWidth: 440,
            borderRadius: 24,
            boxShadow: '0 25px 50px -12px rgba(0, 0, 0, 0.25)',
          }}
          bodyStyle={{ padding: '48px 40px' }}
        >
          <div className="lg:hidden" style={{ textAlign: 'center', marginBottom: 32 }}>
            <div style={{
              width: 48, height: 48, borderRadius: 14,
              background: 'linear-gradient(135deg, #10b981, #059669)',
              display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
              marginBottom: 16, boxShadow: '0 4px 12px rgba(16, 185, 129, 0.2)',
            }}>
              <SafetyCertificateOutlined style={{ fontSize: 24, color: '#fff' }} />
            </div>
            <Title level={4} style={{ margin: 0, fontWeight: 700, color: '#0f172a' }}>SmartInvoice Shield</Title>
          </div>

          <Title level={3} style={{ marginBottom: 8, color: '#0f172a', fontWeight: 700, letterSpacing: '-0.02em' }}>Đăng nhập</Title>
          <Text style={{ display: 'block', marginBottom: 32, color: '#64748b', fontSize: 15 }}>
            Chào mừng bạn quay lại hệ thống
          </Text>

          <Form layout="vertical" onFinish={onFinish} size="large">
            <Form.Item
              name="email"
              rules={[{ required: true, message: 'Vui lòng nhập email' }]}
            >
              <Input
                prefix={<UserOutlined style={{ color: '#94a3b8' }} />}
                placeholder="Email đăng nhập"
                style={{ borderRadius: 12, height: 48, background: '#f8fafc', border: '1px solid #e2e8f0' }}
              />
            </Form.Item>

            <Form.Item
              name="password"
              rules={[{ required: true, message: 'Vui lòng nhập mật khẩu' }]}
            >
              <Input.Password
                prefix={<LockOutlined style={{ color: '#94a3b8' }} />}
                placeholder="Mật khẩu"
                style={{ borderRadius: 12, height: 48, background: '#f8fafc', border: '1px solid #e2e8f0' }}
              />
            </Form.Item>

            <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 28 }}>
              <Checkbox style={{ color: '#64748b' }}>Ghi nhớ đăng nhập</Checkbox>
              <a style={{ color: '#10b981', fontSize: 14, fontWeight: 500 }}>Quên mật khẩu?</a>
            </div>

            <Button type="primary" htmlType="submit" block loading={loading}
              style={{
                height: 52, borderRadius: 12, fontWeight: 600, fontSize: 16,
                background: '#0f172a', border: 'none', boxShadow: '0 4px 12px rgba(15, 23, 42, 0.15)',
              }}
            >
              Đăng nhập
            </Button>
          </Form>

          <Divider plain style={{ margin: '32px 0 20px', color: '#94a3b8', fontSize: 13 }}>
            hoặc
          </Divider>

          <Button block onClick={() => navigate('/register')}
            style={{ height: 48, borderRadius: 12, fontWeight: 600, marginBottom: 24, color: '#475569', borderColor: '#cbd5e1' }}>
            Đăng ký tài khoản mới
          </Button>

          <Text style={{ display: 'block', textAlign: 'center', fontSize: 13, color: '#94a3b8' }}>
            © 2026 SmartInvoice Shield. Tuân thủ NĐ 123/2020/NĐ-CP
          </Text>
        </Card>
      </div>

      {/* New Password Required Modal */}
      <Modal
        title="Đổi mật khẩu lần đầu"
        open={showNewPasswordModal}
        onCancel={() => setShowNewPasswordModal(false)}
        footer={null}
        destroyOnClose
        maskClosable={false}
      >
        <div style={{ marginBottom: 24 }}>
          <Text type="secondary">
            Tài khoản của bạn được tạo bởi Quản trị viên. Theo yêu cầu bảo mật, vui lòng tạo mật khẩu mới trong lần đăng nhập đầu tiên.
          </Text>
        </div>

        <Form
          form={passwordForm}
          layout="vertical"
          onFinish={onFinishNewPassword}
          size="large"
        >
          <Form.Item
            name="newPassword"
            label="Mật khẩu mới"
            rules={[
              { required: true, message: 'Vui lòng nhập mật khẩu mới' },
              { min: 8, message: 'Mật khẩu phải dài ít nhất 8 ký tự' },
              { pattern: /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$/, message: 'Mật khẩu cần ít nhất 1 chữ hoa, 1 chữ thường, 1 số và 1 ký tự đặc biệt' }
            ]}
          >
            <Input.Password placeholder="Nhập mật khẩu mới" />
          </Form.Item>

          <Form.Item
            name="confirmPassword"
            label="Xác nhận mật khẩu mới"
            dependencies={['newPassword']}
            rules={[
              { required: true, message: 'Vui lòng xác nhận mật khẩu mới' },
              ({ getFieldValue }) => ({
                validator(_, value) {
                  if (!value || getFieldValue('newPassword') === value) {
                    return Promise.resolve();
                  }
                  return Promise.reject(new Error('Mật khẩu xác nhận không khớp!'));
                },
              }),
            ]}
          >
            <Input.Password placeholder="Nhập lại mật khẩu mới" />
          </Form.Item>

          <Button type="primary" htmlType="submit" block loading={loading} style={{ marginTop: 8 }}>
            Xác nhận Đổi mật khẩu
          </Button>
        </Form>
      </Modal>

    </div>
  );
};

export default Login;
