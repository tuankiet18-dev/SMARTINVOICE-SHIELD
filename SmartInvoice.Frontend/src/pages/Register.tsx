import React, { useState } from 'react';
import { Card, Form, Input, Button, Typography, Steps, Select, Row, Col, Divider, Checkbox } from 'antd';
import {
  SafetyCertificateOutlined,
  BankOutlined,
  UserOutlined,
  LockOutlined,
  MailOutlined,
  PhoneOutlined,
  IdcardOutlined,
  EnvironmentOutlined,
  SolutionOutlined,
  CheckCircleOutlined,
  ArrowLeftOutlined,
} from '@ant-design/icons';
import { useNavigate } from 'react-router-dom';

import { message } from 'antd';
import { authService, RegisterCompanyRequest } from '@/services/auth';

const { Title, Text, Paragraph } = Typography;

const Register: React.FC = () => {
  const navigate = useNavigate();
  const [current, setCurrent] = useState(0);
  const [companyForm] = Form.useForm();
  const [adminForm] = Form.useForm();
  const [verifyForm] = Form.useForm();
  const [loading, setLoading] = useState(false);
  const [formData, setFormData] = useState<any>({});

  const onFinishCompany = async () => {
    try {
      setLoading(true);
      const values = await companyForm.validateFields();
      const checkResult = await authService.checkTaxCode(values.taxCode);
      if (!checkResult.isValid) {
        message.error('Mã số thuế không hợp lệ');
        return;
      }
      if (checkResult.isRegistered) {
        message.error('Doanh nghiệp đã được đăng ký trên hệ thống');
        return;
      }

      setFormData((prev: any) => ({ ...prev, ...values }));
      setCurrent(1);
    } catch (error: any) {
      if (error.response) {
        message.error(error.response.data?.message || 'Lỗi kiểm tra mã số thuế');
      }
    } finally {
      setLoading(false);
    }
  };

  const onFinishAdmin = async () => {
    try {
      setLoading(true);
      const adminValues = await adminForm.validateFields();

      const payload: RegisterCompanyRequest = {
        ...formData,
        ...adminValues,
        adminFullName: adminValues.fullName,
        phoneNumber: formData.companyPhone, // Map companyPhone to API expected phoneNumber
        legalRepresentative: formData.representative, // Map representative to API expected legalRepresentative
      };

      await authService.register(payload);

      // Update persistent state for verification step
      // Ensure adminEmail is available for the next step
      setFormData((prev: any) => ({
        ...prev,
        ...adminValues,
        adminEmail: adminValues.adminEmail // Explicitly set adminEmail
      }));

      message.success('Đăng ký thành công. Vui lòng kiểm tra email để lấy mã xác thực.');
      setCurrent(2);
    } catch (error: any) {
      if (error.response) {
        message.error(error.response.data?.message || 'Đăng ký thất bại');
        console.error("Register Error:", error.response.data);
      } else {
        message.error('Đăng ký thất bại. Vui lòng thử lại.');
        console.error("Register Error:", error);
      }
    } finally {
      setLoading(false);
    }
  };

  const onFinishVerify = async () => {
    try {
      setLoading(true);
      const values = await verifyForm.validateFields();
      // Use adminEmail from formData. 
      // Note: formData update from onFinishAdmin should be reflected here in the next render.
      if (!formData.adminEmail) {
        message.error('Không tìm thấy email cần xác thực. Vui lòng đăng ký lại.');
        return;
      }
      await authService.verifyEmail(formData.adminEmail, values.token);
      message.success('Xác thực tài khoản thành công!');
      setCurrent(3);
    } catch (error: any) {
      message.error(error.response?.data?.message || 'Mã xác thực không hợp lệ');
    } finally {
      setLoading(false);
    }
  };

  const stepItems = [
    { title: 'Thông tin công ty', icon: <BankOutlined /> },
    { title: 'Tài khoản Admin', icon: <UserOutlined /> },
    { title: 'Xác thực', icon: <SafetyCertificateOutlined /> },
    { title: 'Hoàn tất', icon: <CheckCircleOutlined /> },
  ];

  return (
    <div style={{
      minHeight: '100vh',
      background: 'linear-gradient(135deg, hsl(220 20% 96%) 0%, hsl(220 25% 94%) 100%)',
      padding: '32px 16px',
    }}>
      <div style={{ maxWidth: 720, margin: '0 auto 24px', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10, cursor: 'pointer' }} onClick={() => navigate('/')}>
          <div style={{
            width: 36, height: 36, borderRadius: 10,
            background: 'linear-gradient(135deg, #2db791, #1a8a6a)',
            display: 'flex', alignItems: 'center', justifyContent: 'center',
          }}>
            <SafetyCertificateOutlined style={{ color: '#fff', fontSize: 18 }} />
          </div>
          <Text strong style={{ fontSize: 16, color: '#1a4b8c' }}>SmartInvoice Shield</Text>
        </div>
        <Button type="link" onClick={() => navigate('/login')} style={{ color: '#1a4b8c' }}>
          Đã có tài khoản? Đăng nhập
        </Button>
      </div>

      <Card bordered={false} style={{
        maxWidth: 720, margin: '0 auto', borderRadius: 16,
        boxShadow: '0 8px 30px rgba(0,0,0,0.08)',
      }} bodyStyle={{ padding: '36px 40px' }}>
        <Title level={3} style={{ marginBottom: 4 }}>Đăng ký sử dụng dịch vụ</Title>
        <Text type="secondary" style={{ display: 'block', marginBottom: 28 }}>
          Tạo tài khoản công ty và quản trị viên để bắt đầu
        </Text>

        <Steps current={current} items={stepItems} style={{ marginBottom: 36 }} />

        {current === 0 && (
          <Form form={companyForm} layout="vertical" onFinish={onFinishCompany} requiredMark="optional">
            <Title level={5} style={{ marginBottom: 16 }}>
              <BankOutlined style={{ marginRight: 8 }} />Thông tin doanh nghiệp
            </Title>
            <Form.Item name="companyName" label="Tên công ty / doanh nghiệp" rules={[{ required: true, message: 'Vui lòng nhập tên công ty' }]}>
              <Input prefix={<BankOutlined style={{ color: '#bfbfbf' }} />} placeholder="VD: Công ty TNHH ABC" style={{ borderRadius: 10, height: 44 }} />
            </Form.Item>
            <Row gutter={16}>
              <Col xs={24} sm={12}>
                <Form.Item name="taxCode" label="Mã số thuế (MST)" rules={[{ required: true, message: 'Vui lòng nhập MST' }]}>
                  <Input prefix={<IdcardOutlined style={{ color: '#bfbfbf' }} />} placeholder="VD: 0123456789" style={{ borderRadius: 10, height: 44 }} />
                </Form.Item>
              </Col>
              <Col xs={24} sm={12}>
                <Form.Item name="businessType" label="Loại hình doanh nghiệp" rules={[{ required: true, message: 'Vui lòng chọn' }]}>
                  <Select placeholder="Chọn loại hình" style={{ borderRadius: 10 }}
                    options={[
                      { value: 'tnhh', label: 'Công ty TNHH' },
                      { value: 'cp', label: 'Công ty Cổ phần' },
                      { value: 'tn', label: 'Doanh nghiệp tư nhân' },
                      { value: 'hkd', label: 'Hộ kinh doanh' },
                      { value: 'other', label: 'Khác' },
                    ]}
                  />
                </Form.Item>
              </Col>
            </Row>
            <Form.Item name="address" label="Địa chỉ trụ sở" rules={[{ required: true, message: 'Vui lòng nhập địa chỉ' }]}>
              <Input prefix={<EnvironmentOutlined style={{ color: '#bfbfbf' }} />} placeholder="Số nhà, đường, phường/xã, quận/huyện, tỉnh/TP" style={{ borderRadius: 10, height: 44 }} />
            </Form.Item>
            <Row gutter={16}>
              <Col xs={24} sm={12}>
                <Form.Item name="companyPhone" label="Số điện thoại công ty" rules={[{ required: true, message: 'Vui lòng nhập SĐT' }]}>
                  <Input prefix={<PhoneOutlined style={{ color: '#bfbfbf' }} />} placeholder="VD: 028 1234 5678" style={{ borderRadius: 10, height: 44 }} />
                </Form.Item>
              </Col>
              <Col xs={24} sm={12}>
                <Form.Item name="companyEmail" label="Email công ty" rules={[{ required: true, type: 'email', message: 'Email không hợp lệ' }]}>
                  <Input prefix={<MailOutlined style={{ color: '#bfbfbf' }} />} placeholder="info@company.vn" style={{ borderRadius: 10, height: 44 }} />
                </Form.Item>
              </Col>
            </Row>
            <Row gutter={16}>
              <Col xs={24} sm={12}>
                <Form.Item name="industry" label="Ngành nghề chính">
                  <Select placeholder="Chọn ngành nghề"
                    options={[
                      { value: 'manufacturing', label: 'Sản xuất' },
                      { value: 'trading', label: 'Thương mại' },
                      { value: 'service', label: 'Dịch vụ' },
                      { value: 'construction', label: 'Xây dựng' },
                      { value: 'tech', label: 'Công nghệ' },
                      { value: 'finance', label: 'Tài chính - Ngân hàng' },
                      { value: 'other', label: 'Khác' },
                    ]}
                  />
                </Form.Item>
              </Col>
              <Col xs={24} sm={12}>
                <Form.Item name="companySize" label="Quy mô công ty">
                  <Select placeholder="Chọn quy mô"
                    options={[
                      { value: 'micro', label: '< 10 nhân viên' },
                      { value: 'small', label: '10 - 50 nhân viên' },
                      { value: 'medium', label: '50 - 200 nhân viên' },
                      { value: 'large', label: '200 - 500 nhân viên' },
                      { value: 'enterprise', label: '> 500 nhân viên' },
                    ]}
                  />
                </Form.Item>
              </Col>
            </Row>
            <Form.Item name="representative" label="Người đại diện pháp luật" rules={[{ required: true, message: 'Vui lòng nhập' }]}>
              <Input prefix={<SolutionOutlined style={{ color: '#bfbfbf' }} />} placeholder="Họ và tên người đại diện" style={{ borderRadius: 10, height: 44 }} />
            </Form.Item>
            <Button type="primary" htmlType="submit" block loading={loading}
              style={{
                height: 48, borderRadius: 10, fontWeight: 600, fontSize: 15, marginTop: 8,
                background: 'linear-gradient(135deg, #1a4b8c, #15396d)',
              }}>
              Tiếp tục →
            </Button>
          </Form>
        )}

        {current === 1 && (
          <Form form={adminForm} layout="vertical" onFinish={onFinishAdmin} requiredMark="optional">
            <Title level={5} style={{ marginBottom: 16 }}>
              <UserOutlined style={{ marginRight: 8 }} />Tài khoản quản trị viên
            </Title>
            <Row gutter={16}>
              <Col xs={24} sm={12}>
                <Form.Item name="fullName" label="Họ và tên" rules={[{ required: true, message: 'Vui lòng nhập họ tên' }]}>
                  <Input prefix={<UserOutlined style={{ color: '#bfbfbf' }} />} placeholder="Nguyễn Văn A" style={{ borderRadius: 10, height: 44 }} />
                </Form.Item>
              </Col>
              <Col xs={24} sm={12}>
                <Form.Item name="position" label="Chức vụ">
                  <Select placeholder="Chọn chức vụ"
                    options={[
                      { value: 'director', label: 'Giám đốc' },
                      { value: 'cfo', label: 'Giám đốc tài chính' },
                      { value: 'accountant_lead', label: 'Kế toán trưởng' },
                      { value: 'it_manager', label: 'Quản lý IT' },
                      { value: 'other', label: 'Khác' },
                    ]}
                  />
                </Form.Item>
              </Col>
            </Row>
            <Form.Item name="adminEmail" label="Email đăng nhập"
              rules={[
                { required: true, message: 'Vui lòng nhập email' },
                { type: 'email', message: 'Email không hợp lệ' },
              ]}>
              <Input prefix={<MailOutlined style={{ color: '#bfbfbf' }} />} placeholder="admin@company.vn" style={{ borderRadius: 10, height: 44 }} />
            </Form.Item>
            <Form.Item name="adminPhone" label="Số điện thoại" rules={[{ required: true, message: 'Vui lòng nhập SĐT' }]}>
              <Input prefix={<PhoneOutlined style={{ color: '#bfbfbf' }} />} placeholder="0912 345 678" style={{ borderRadius: 10, height: 44 }} />
            </Form.Item>
            <Row gutter={16}>
              <Col xs={24} sm={12}>
                <Form.Item name="password" label="Mật khẩu"
                  rules={[
                    { required: true, message: 'Vui lòng nhập mật khẩu' },
                    { min: 8, message: 'Tối thiểu 8 ký tự' },
                  ]}>
                  <Input.Password prefix={<LockOutlined style={{ color: '#bfbfbf' }} />} placeholder="Tối thiểu 8 ký tự" style={{ borderRadius: 10, height: 44 }} />
                </Form.Item>
              </Col>
              <Col xs={24} sm={12}>
                <Form.Item name="confirmPassword" label="Xác nhận mật khẩu"
                  dependencies={['password']}
                  rules={[
                    { required: true, message: 'Vui lòng xác nhận mật khẩu' },
                    ({ getFieldValue }) => ({
                      validator(_, value) {
                        if (!value || getFieldValue('password') === value) return Promise.resolve();
                        return Promise.reject(new Error('Mật khẩu không khớp'));
                      },
                    }),
                  ]}>
                  <Input.Password prefix={<LockOutlined style={{ color: '#bfbfbf' }} />} placeholder="Nhập lại mật khẩu" style={{ borderRadius: 10, height: 44 }} />
                </Form.Item>
              </Col>
            </Row>
            <Form.Item name="agree" valuePropName="checked"
              rules={[{ validator: (_, v) => v ? Promise.resolve() : Promise.reject('Vui lòng đồng ý điều khoản') }]}>
              <Checkbox>
                Tôi đồng ý với <a style={{ color: '#1a4b8c' }}>Điều khoản sử dụng</a> và{' '}
                <a style={{ color: '#1a4b8c' }}>Chính sách bảo mật</a>
              </Checkbox>
            </Form.Item>
            <div style={{ display: 'flex', gap: 12, marginTop: 8 }}>
              <Button style={{ height: 48, borderRadius: 10, flex: '0 0 auto', paddingInline: 24 }}
                icon={<ArrowLeftOutlined />} onClick={() => setCurrent(0)}>
                Quay lại
              </Button>
              <Button type="primary" htmlType="submit" block loading={loading}
                style={{
                  height: 48, borderRadius: 10, fontWeight: 600, fontSize: 15,
                  background: 'linear-gradient(135deg, #2db791, #1a8a6a)', border: 'none',
                }}>
                Hoàn tất đăng ký
              </Button>
            </div>
          </Form>
        )}

        {current === 2 && (
          <Form form={verifyForm} layout="vertical" onFinish={onFinishVerify} requiredMark="optional">
            <div style={{ textAlign: 'center', marginBottom: 24 }}>
              <Title level={4}>Xác thực Email</Title>
              <Text type="secondary">
                Vui lòng nhập mã xác thực gồm 6 chữ số đã được gửi đến email <strong>{formData.adminEmail}</strong>
              </Text>
            </div>
            <Form.Item name="token" rules={[
              { required: true, message: 'Vui lòng nhập mã xác thực' },
              { len: 6, message: 'Mã xác thực phải có 6 chữ số' }
            ]}>
              <Input placeholder="Nhập mã xác thực (6 số)" style={{ borderRadius: 10, height: 44, fontSize: 18, textAlign: 'center', letterSpacing: 4 }} maxLength={6} />
            </Form.Item>
            <Button type="primary" htmlType="submit" block loading={loading}
              style={{
                height: 48, borderRadius: 10, fontWeight: 600, fontSize: 15,
                background: 'linear-gradient(135deg, #1a4b8c, #15396d)',
              }}>
              Xác thực
            </Button>
            <div style={{ textAlign: 'center', marginTop: 16 }}>
              <Button type="link" onClick={() => { message.info('Chức năng gửi lại đang được phát triển'); }}>
                Gửi lại mã
              </Button>
            </div>
          </Form>
        )}

        {current === 3 && (
          <div style={{ textAlign: 'center', padding: '40px 0 20px' }}>
            <div style={{
              width: 72, height: 72, borderRadius: '50%',
              background: 'rgba(45,183,145,0.1)', display: 'inline-flex',
              alignItems: 'center', justifyContent: 'center', marginBottom: 20,
            }}>
              <CheckCircleOutlined style={{ fontSize: 36, color: '#2db791' }} />
            </div>
            <Title level={3} style={{ marginBottom: 8 }}>Đăng ký thành công!</Title>
            <Paragraph type="secondary" style={{ fontSize: 15, maxWidth: 420, margin: '0 auto 28px' }}>
              Tài khoản công ty và quản trị viên đã được tạo. Bạn có thể đăng nhập ngay để bắt đầu sử dụng hệ thống.
            </Paragraph>
            <Button type="primary" size="large" onClick={() => navigate('/login')}
              style={{
                height: 48, borderRadius: 10, fontWeight: 600, paddingInline: 36,
                background: 'linear-gradient(135deg, #1a4b8c, #15396d)',
              }}>
              Đăng nhập ngay
            </Button>
          </div>
        )}

        <Divider plain style={{ margin: '28px 0 12px', color: '#bfbfbf', fontSize: 12 }}>
          Tuân thủ NĐ 123/2020/NĐ-CP & TT 78/2021/TT-BTC
        </Divider>
        <Text type="secondary" style={{ display: 'block', textAlign: 'center', fontSize: 12 }}>
          © 2026 SmartInvoice Shield. AWS Cloud AI Journey.
        </Text>
      </Card>
    </div>
  );
};

export default Register;
