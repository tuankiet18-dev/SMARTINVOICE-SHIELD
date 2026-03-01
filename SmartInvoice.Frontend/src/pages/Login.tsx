import React from 'react';
import { Card, Form, Input, Button, Typography, Checkbox, Divider } from 'antd';
import { UserOutlined, LockOutlined, SafetyCertificateOutlined } from '@ant-design/icons';
import { useNavigate } from 'react-router-dom';

const { Title, Text, Paragraph } = Typography;

import { message, Modal } from 'antd';
import { authService, LoginRequest } from '@/services/auth';

const Login: React.FC = () => {
  const navigate = useNavigate();
  const [loading, setLoading] = React.useState(false);
  const [passwordForm] = Form.useForm();

  // New Password Challenge State
  const [showNewPasswordModal, setShowNewPasswordModal] = React.useState(false);
  const [challengeSession, setChallengeSession] = React.useState('');
  const [loginEmail, setLoginEmail] = React.useState('');

  const onFinish = async (values: LoginRequest) => {
    try {
      setLoading(true);
      const data = await authService.login(values);

      if (data.challengeName === 'NEW_PASSWORD_REQUIRED' && data.session) {
        setChallengeSession(data.session);
        setLoginEmail(values.email);
        setShowNewPasswordModal(true);
        return; // Stop here, wait for new password
      }

      localStorage.setItem('token', data.accessToken);
      localStorage.setItem('idToken', data.idToken);
      localStorage.setItem('user', JSON.stringify(data.user));
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
      background: 'linear-gradient(135deg, hsl(215 80% 18%) 0%, hsl(215 70% 28%) 50%, hsl(174 50% 30%) 100%)',
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
          width: 56, height: 56, borderRadius: 14,
          background: 'linear-gradient(135deg, #2db791, #1a8a6a)',
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          marginBottom: 32,
        }}>
          <SafetyCertificateOutlined style={{ fontSize: 28, color: '#fff' }} />
        </div>

        <Title level={1} style={{ color: '#fff', marginBottom: 16, fontWeight: 700 }}>
          SmartInvoice<br />Shield
        </Title>
        <Paragraph style={{ color: 'rgba(255,255,255,0.75)', fontSize: 16, lineHeight: 1.7, maxWidth: 420 }}>
          Hệ thống quản trị và rà soát rủi ro hóa đơn điện tử.
          Tự động hóa quy trình, đảm bảo tuân thủ pháp luật Việt Nam.
        </Paragraph>

        <div style={{ marginTop: 40, display: 'flex', gap: 32 }}>
          {[
            { value: '90%', label: 'Tiết kiệm thời gian' },
            { value: '100%', label: 'Tuân thủ pháp lý' },
            { value: '85%+', label: 'AI Accuracy' },
          ].map((stat, i) => (
            <div key={i}>
              <Text style={{ color: '#2db791', fontSize: 28, fontWeight: 700, display: 'block' }}>{stat.value}</Text>
              <Text style={{ color: 'rgba(255,255,255,0.6)', fontSize: 12 }}>{stat.label}</Text>
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
            maxWidth: 420,
            borderRadius: 16,
            boxShadow: '0 20px 60px rgba(0,0,0,0.15)',
          }}
          bodyStyle={{ padding: '40px 36px' }}
        >
          <div className="lg:hidden" style={{ textAlign: 'center', marginBottom: 24 }}>
            <div style={{
              width: 44, height: 44, borderRadius: 12,
              background: 'linear-gradient(135deg, #2db791, #1a8a6a)',
              display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
              marginBottom: 12,
            }}>
              <SafetyCertificateOutlined style={{ fontSize: 22, color: '#fff' }} />
            </div>
            <Title level={4} style={{ margin: 0 }}>SmartInvoice Shield</Title>
          </div>

          <Title level={3} style={{ marginBottom: 4 }}>Đăng nhập</Title>
          <Text type="secondary" style={{ display: 'block', marginBottom: 28 }}>
            Chào mừng bạn quay lại hệ thống
          </Text>

          <Form layout="vertical" onFinish={onFinish} size="large">
            <Form.Item
              name="email"
              rules={[{ required: true, message: 'Vui lòng nhập email' }]}
            >
              <Input
                prefix={<UserOutlined style={{ color: '#bfbfbf' }} />}
                placeholder="Email đăng nhập"
                style={{ borderRadius: 10, height: 46 }}
              />
            </Form.Item>

            <Form.Item
              name="password"
              rules={[{ required: true, message: 'Vui lòng nhập mật khẩu' }]}
            >
              <Input.Password
                prefix={<LockOutlined style={{ color: '#bfbfbf' }} />}
                placeholder="Mật khẩu"
                style={{ borderRadius: 10, height: 46 }}
              />
            </Form.Item>

            <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 24 }}>
              <Checkbox>Ghi nhớ đăng nhập</Checkbox>
              <a style={{ color: '#1a4b8c', fontSize: 13 }}>Quên mật khẩu?</a>
            </div>

            <Button type="primary" htmlType="submit" block loading={loading}
              style={{
                height: 48, borderRadius: 10, fontWeight: 600, fontSize: 15,
                background: 'linear-gradient(135deg, #1a4b8c, #15396d)',
              }}
            >
              Đăng nhập
            </Button>
          </Form>

          <Divider plain style={{ margin: '28px 0 16px', color: '#bfbfbf', fontSize: 12 }}>
            hoặc
          </Divider>

          <Button block onClick={() => navigate('/register')}
            style={{ height: 44, borderRadius: 10, fontWeight: 500, marginBottom: 16 }}>
            Đăng ký tài khoản mới
          </Button>

          <Text type="secondary" style={{ display: 'block', textAlign: 'center', fontSize: 12 }}>
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
