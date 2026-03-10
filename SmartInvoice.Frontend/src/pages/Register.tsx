import React, { useState } from "react";
import { Form, Input, Select, Steps, Checkbox, message } from "antd";
import {
  BankOutlined,
  UserOutlined,
  SafetyCertificateOutlined,
  LockOutlined,
  MailOutlined,
  PhoneOutlined,
  IdcardOutlined,
  EnvironmentOutlined,
  CheckCircleOutlined,
  ArrowLeftOutlined,
  SearchOutlined,
} from "@ant-design/icons";
import { useNavigate } from "react-router-dom";
import { authService, RegisterCompanyRequest } from "@/services/auth";

const Register: React.FC = () => {
  const navigate = useNavigate();
  const [current, setCurrent] = useState(0);
  const [companyForm] = Form.useForm();
  const [adminForm] = Form.useForm();
  const [verifyForm] = Form.useForm();
  const [loading, setLoading] = useState(false);
  const [checkingTaxCode, setCheckingTaxCode] = useState(false);
  const [formData, setFormData] = useState<any>({});

  const handleCheckTaxCode = async () => {
    const taxCode = companyForm.getFieldValue("taxCode");
    if (!taxCode) {
      message.warning("Vui lòng nhập Mã số thuế trước khi kiểm tra.");
      return;
    }

    try {
      setCheckingTaxCode(true);
      const checkResult = await authService.checkTaxCode(taxCode);

      if (!checkResult.isValid) {
        message.error(checkResult.errorMessage || "Mã số thuế không hợp lệ");
        companyForm.setFieldsValue({
          companyName: undefined,
          address: undefined,
        });
        return;
      }
      if (checkResult.isRegistered) {
        message.error("Doanh nghiệp đã được đăng ký trên hệ thống");
        return;
      }

      message.success("Tra cứu thông tin thành công!");
      companyForm.setFieldsValue({
        companyName: checkResult.companyName,
        address: checkResult.address,
      });
    } catch (error: any) {
      if (error.response) {
        message.error(
          error.response.data?.message || "Lỗi kiểm tra mã số thuế",
        );
      } else {
        message.error("Lỗi kết nối tới máy chủ.");
      }
    } finally {
      setCheckingTaxCode(false);
    }
  };

  const onFinishCompany = async () => {
    try {
      setLoading(true);
      const values = await companyForm.validateFields();

      if (!values.companyName || !values.address) {
        message.warning("Vui lòng kiểm tra Mã số thuế trước khi tiếp tục.");
        return;
      }

      setFormData((prev: any) => ({ ...prev, ...values }));
      setCurrent(1);
    } catch (error: any) {
      console.error(error);
    } finally {
      setLoading(false);
    }
  };

  const onFinishAdmin = async () => {
    try {
      setLoading(true);
      const adminValues = await adminForm.validateFields();
      const payload: RegisterCompanyRequest = {
        taxCode: formData.taxCode,
        companyName: formData.companyName,
        address: formData.address,
        businessType: formData.businessType || "",
        adminFullName: adminValues.adminFullName,
        adminEmail: adminValues.adminEmail,
        adminPhone: adminValues.adminPhone,
        password: adminValues.password,
      };

      await authService.register(payload);

      setFormData((prev: any) => ({
        ...prev,
        ...adminValues,
        adminEmail: adminValues.adminEmail,
      }));

      message.success(
        "Đăng ký thành công. Vui lòng kiểm tra email để lấy mã xác thực.",
      );
      setCurrent(2);
    } catch (error: any) {
      if (error.response) {
        message.error(error.response.data?.message || "Đăng ký thất bại");
      } else {
        message.error("Đăng ký thất bại. Vui lòng thử lại.");
      }
    } finally {
      setLoading(false);
    }
  };

  const onFinishVerify = async () => {
    try {
      setLoading(true);
      const values = await verifyForm.validateFields();

      if (!formData.adminEmail) {
        message.error(
          "Không tìm thấy email cần xác thực. Vui lòng đăng ký lại.",
        );
        return;
      }
      await authService.verifyEmail(formData.adminEmail, values.token);
      message.success("Xác thực tài khoản thành công!");
      setCurrent(3);
    } catch (error: any) {
      message.error(
        error.response?.data?.message || "Mã xác thực không hợp lệ",
      );
    } finally {
      setLoading(false);
    }
  };

  const stepItems = [
    { title: "Công ty", icon: <BankOutlined /> },
    { title: "Quản trị viên", icon: <UserOutlined /> },
    { title: "Xác thực", icon: <SafetyCertificateOutlined /> },
    { title: "Hoàn tất", icon: <CheckCircleOutlined /> },
  ];

  return (
    <div className="min-h-screen bg-slate-50 font-sans flex flex-col items-center py-10 px-4 sm:px-6 relative overflow-hidden selection:bg-blue-100/50">
      {/* Background blobs matching Landing Page */}
      <div className="absolute top-0 left-1/2 -translate-x-1/2 w-[800px] h-[600px] bg-blue-600/10 rounded-full blur-[100px] pointer-events-none z-0"></div>

      {/* Header */}
      <div className="w-full max-w-3xl flex items-center justify-between mb-8 relative z-10">
        <div
          className="flex items-center gap-3 cursor-pointer"
          onClick={() => navigate("/")}
        >
          <img
            src="/logo-transparent.png"
            alt="SmartInvoice Logo"
            className="h-10 w-auto object-contain"
          />
          <div className="flex flex-col hidden sm:flex">
            <span className="text-[17px] font-bold leading-none tracking-tight text-slate-900">
              SmartInvoice
            </span>
            <span className="text-[10px] font-bold leading-none tracking-widest text-slate-500 mt-0.5">
              SHIELD
            </span>
          </div>
        </div>
        <button
          onClick={() => navigate("/login")}
          className="text-sm font-semibold text-blue-600 hover:text-blue-700 transition-colors"
        >
          Đã có tài khoản? Đăng nhập
        </button>
      </div>

      {/* Main Card */}
      <div className="w-full max-w-3xl bg-white rounded-[2rem] shadow-2xl shadow-slate-200/50 border border-slate-100 p-8 sm:p-12 relative z-10">
        <div className="mb-10 text-center sm:text-left">
          <h2 className="text-3xl font-black text-slate-900 tracking-tight mb-2">
            Đăng ký sử dụng dịch vụ
          </h2>
          <p className="text-slate-500 font-medium text-[15px]">
            Tạo tài khoản doanh nghiệp và quản trị viên để bắt đầu
          </p>
        </div>

        <div className="mb-12 overflow-x-auto pb-2">
          <Steps
            current={current}
            items={stepItems}
            className="min-w-[400px]"
          />
        </div>

        {/* Step 0: Company Info */}
        {current === 0 && (
          <Form
            form={companyForm}
            layout="vertical"
            onFinish={onFinishCompany}
            size="large"
            requiredMark="optional"
          >
            <h3 className="text-lg font-bold text-slate-900 flex items-center gap-2 mb-6">
              <BankOutlined className="text-blue-600" /> Thông tin doanh nghiệp
            </h3>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-x-6">
              <Form.Item
                label={
                  <span className="font-semibold text-slate-700">
                    Mã số thuế (MST)
                  </span>
                }
                required
                className="mb-6"
              >
                <div className="flex w-full">
                  <Form.Item
                    name="taxCode"
                    noStyle
                    rules={[{ required: true, message: "Vui lòng nhập MST" }]}
                  >
                    <Input
                      prefix={
                        <IdcardOutlined className="text-slate-400 mr-1" />
                      }
                      placeholder="Nhập 10-13 số"
                      className="flex-1 h-12 rounded-l-xl rounded-r-none bg-slate-50 border-slate-200 hover:border-blue-400 focus:border-blue-500 text-[15px]"
                    />
                  </Form.Item>
                  <button
                    type="button"
                    onClick={handleCheckTaxCode}
                    disabled={checkingTaxCode}
                    className="h-12 px-6 bg-slate-900 text-white font-bold rounded-r-xl hover:bg-slate-800 transition-colors flex items-center gap-2 shadow-sm disabled:opacity-70"
                  >
                    <SearchOutlined />{" "}
                    {checkingTaxCode ? "Đang tra..." : "Kiểm tra"}
                  </button>
                </div>
                <p className="text-xs text-slate-500 mt-2 font-medium">
                  Nhập MST và ấn Kiểm tra để điền tự động thông tin bên dưới.
                </p>
              </Form.Item>

              <Form.Item
                name="businessType"
                label={
                  <span className="font-semibold text-slate-700">
                    Loại hình doanh nghiệp
                  </span>
                }
                rules={[{ required: true, message: "Vui lòng chọn loại hình" }]}
                className="mb-6"
              >
                <Select
                  placeholder="Chọn loại hình"
                  className="h-12 [&>.ant-select-selector]:rounded-xl [&>.ant-select-selector]:bg-slate-50 [&>.ant-select-selector]:border-slate-200"
                  options={[
                    { value: "tnhh", label: "Công ty TNHH" },
                    { value: "cp", label: "Công ty Cổ phần" },
                    { value: "tn", label: "Doanh nghiệp tư nhân" },
                    { value: "hkd", label: "Hộ kinh doanh" },
                    { value: "other", label: "Khác" },
                  ]}
                />
              </Form.Item>
            </div>

            <Form.Item
              name="companyName"
              label={
                <span className="font-semibold text-slate-700">
                  Tên công ty / doanh nghiệp
                </span>
              }
              rules={[
                {
                  required: true,
                  message: "Vui lòng kiểm tra MST để lấy tên công ty",
                },
              ]}
              className="mb-6"
            >
              <Input
                disabled
                prefix={<BankOutlined className="text-slate-400 mr-1" />}
                placeholder="Hệ thống tự động điền từ MST"
                className="h-12 rounded-xl bg-slate-100 border-slate-200 text-slate-900 font-medium text-[15px]"
              />
            </Form.Item>

            <Form.Item
              name="address"
              label={
                <span className="font-semibold text-slate-700">
                  Địa chỉ trụ sở
                </span>
              }
              rules={[
                {
                  required: true,
                  message: "Vui lòng kiểm tra MST để lấy địa chỉ",
                },
              ]}
              className="mb-8"
            >
              <Input
                disabled
                prefix={<EnvironmentOutlined className="text-slate-400 mr-1" />}
                placeholder="Hệ thống tự động điền từ MST"
                className="h-12 rounded-xl bg-slate-100 border-slate-200 text-slate-900 font-medium text-[15px]"
              />
            </Form.Item>

            <button
              type="submit"
              disabled={loading}
              className="w-full h-12 flex items-center justify-center text-[15px] font-bold text-white transition-all bg-blue-600 rounded-xl hover:bg-blue-700 shadow-lg shadow-blue-600/20 hover:shadow-blue-600/40 hover:-translate-y-0.5 disabled:opacity-70 disabled:hover:translate-y-0"
            >
              Tiếp tục <span className="ml-2">→</span>
            </button>
          </Form>
        )}

        {/* Step 1: Admin Info */}
        {current === 1 && (
          <Form
            form={adminForm}
            layout="vertical"
            onFinish={onFinishAdmin}
            size="large"
            requiredMark="optional"
          >
            <h3 className="text-lg font-bold text-slate-900 flex items-center gap-2 mb-6">
              <UserOutlined className="text-blue-600" /> Tài khoản Quản trị viên
            </h3>

            <Form.Item
              name="adminFullName"
              label={
                <span className="font-semibold text-slate-700">Họ và tên</span>
              }
              rules={[{ required: true, message: "Vui lòng nhập họ tên" }]}
              className="mb-6"
            >
              <Input
                prefix={<UserOutlined className="text-slate-400 mr-1" />}
                placeholder="VD: Phạm Văn A"
                className="h-12 rounded-xl bg-slate-50 border-slate-200 hover:border-blue-400 focus:border-blue-500 text-[15px]"
              />
            </Form.Item>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-x-6">
              <Form.Item
                name="adminEmail"
                label={
                  <span className="font-semibold text-slate-700">
                    Email đăng nhập
                  </span>
                }
                rules={[
                  { required: true, message: "Vui lòng nhập email" },
                  { type: "email", message: "Email không hợp lệ" },
                ]}
                className="mb-6"
              >
                <Input
                  prefix={<MailOutlined className="text-slate-400 mr-1" />}
                  placeholder="admin@company.vn"
                  className="h-12 rounded-xl bg-slate-50 border-slate-200 hover:border-blue-400 focus:border-blue-500 text-[15px]"
                />
              </Form.Item>

              <Form.Item
                name="adminPhone"
                label={
                  <span className="font-semibold text-slate-700">
                    Số điện thoại
                  </span>
                }
                rules={[{ required: true, message: "Vui lòng nhập SĐT" }]}
                className="mb-6"
              >
                <Input
                  prefix={<PhoneOutlined className="text-slate-400 mr-1" />}
                  placeholder="0912 345 678"
                  className="h-12 rounded-xl bg-slate-50 border-slate-200 hover:border-blue-400 focus:border-blue-500 text-[15px]"
                />
              </Form.Item>

              <Form.Item
                name="password"
                label={
                  <span className="font-semibold text-slate-700">Mật khẩu</span>
                }
                rules={[
                  { required: true, message: "Vui lòng nhập mật khẩu" },
                  { min: 8, message: "Tối thiểu 8 ký tự" },
                ]}
                className="mb-6"
              >
                <Input.Password
                  prefix={<LockOutlined className="text-slate-400 mr-1" />}
                  placeholder="Tối thiểu 8 ký tự"
                  className="h-12 rounded-xl bg-slate-50 border-slate-200 hover:border-blue-400 focus:border-blue-500 text-[15px]"
                />
              </Form.Item>

              <Form.Item
                name="confirmPassword"
                label={
                  <span className="font-semibold text-slate-700">
                    Xác nhận mật khẩu
                  </span>
                }
                dependencies={["password"]}
                rules={[
                  { required: true, message: "Vui lòng xác nhận mật khẩu" },
                  ({ getFieldValue }) => ({
                    validator(_, value) {
                      if (!value || getFieldValue("password") === value)
                        return Promise.resolve();
                      return Promise.reject(new Error("Mật khẩu không khớp"));
                    },
                  }),
                ]}
                className="mb-6"
              >
                <Input.Password
                  prefix={<LockOutlined className="text-slate-400 mr-1" />}
                  placeholder="Nhập lại mật khẩu"
                  className="h-12 rounded-xl bg-slate-50 border-slate-200 hover:border-blue-400 focus:border-blue-500 text-[15px]"
                />
              </Form.Item>
            </div>

            <Form.Item
              name="agree"
              valuePropName="checked"
              rules={[
                {
                  validator: (_, v) =>
                    v
                      ? Promise.resolve()
                      : Promise.reject("Vui lòng đồng ý điều khoản"),
                },
              ]}
              className="mb-8"
            >
              <Checkbox className="text-slate-500 font-medium text-[14px]">
                Tôi đồng ý với{" "}
                <a href="#" className="text-blue-600 hover:underline">
                  Điều khoản sử dụng
                </a>{" "}
                và{" "}
                <a href="#" className="text-blue-600 hover:underline">
                  Chính sách bảo mật
                </a>
              </Checkbox>
            </Form.Item>

            <div className="flex gap-4">
              <button
                type="button"
                onClick={() => setCurrent(0)}
                className="h-12 px-6 flex items-center justify-center text-[15px] font-bold text-slate-700 transition-all bg-white border-2 border-slate-200 rounded-xl hover:bg-slate-50 hover:border-slate-300"
              >
                <ArrowLeftOutlined className="mr-2" /> Quay lại
              </button>
              <button
                type="submit"
                disabled={loading}
                className="flex-1 h-12 flex items-center justify-center text-[15px] font-bold text-white transition-all bg-blue-600 rounded-xl hover:bg-blue-700 shadow-lg shadow-blue-600/20 hover:shadow-blue-600/40 hover:-translate-y-0.5 disabled:opacity-70 disabled:hover:translate-y-0"
              >
                Hoàn tất đăng ký
              </button>
            </div>
          </Form>
        )}

        {/* Step 2: Verification */}
        {current === 2 && (
          <Form
            form={verifyForm}
            layout="vertical"
            onFinish={onFinishVerify}
            size="large"
            requiredMark="optional"
          >
            <div className="text-center mb-10">
              <h3 className="text-2xl font-black text-slate-900 mb-3">
                Xác thực Email
              </h3>
              <p className="text-slate-500 font-medium">
                Vui lòng nhập mã xác thực gồm 6 chữ số đã được gửi đến email{" "}
                <br />
                <strong className="text-slate-800">
                  {formData.adminEmail}
                </strong>
              </p>
            </div>

            <Form.Item
              name="token"
              rules={[
                { required: true, message: "Vui lòng nhập mã xác thực" },
                { len: 6, message: "Mã xác thực phải có 6 chữ số" },
              ]}
              className="mb-8 max-w-sm mx-auto"
            >
              <Input
                placeholder="Nhập 6 số"
                maxLength={6}
                className="h-14 rounded-xl bg-slate-50 border-slate-200 hover:border-blue-400 focus:border-blue-500 text-center text-xl font-bold tracking-[0.5em]"
              />
            </Form.Item>

            <div className="max-w-sm mx-auto">
              <button
                type="submit"
                disabled={loading}
                className="w-full h-12 flex items-center justify-center text-[15px] font-bold text-white transition-all bg-slate-900 rounded-xl hover:bg-slate-800 shadow-lg shadow-slate-900/20 hover:-translate-y-0.5 disabled:opacity-70 disabled:hover:translate-y-0 mb-4"
              >
                Xác thực tài khoản
              </button>

              <div className="text-center">
                <button
                  type="button"
                  onClick={() =>
                    message.info("Chức năng gửi lại đang được phát triển")
                  }
                  className="text-sm font-semibold text-blue-600 hover:text-blue-700 transition-colors"
                >
                  Gửi lại mã xác thực
                </button>
              </div>
            </div>
          </Form>
        )}

        {/* Step 3: Success */}
        {current === 3 && (
          <div className="text-center py-8">
            <div className="w-24 h-24 rounded-full bg-emerald-50 border-8 border-emerald-100 flex items-center justify-center mx-auto mb-6">
              <CheckCircleOutlined className="text-4xl text-emerald-500" />
            </div>

            <h3 className="text-3xl font-black text-slate-900 mb-4 tracking-tight">
              Đăng ký thành công!
            </h3>
            <p className="text-slate-500 font-medium text-[16px] max-w-md mx-auto mb-10 leading-relaxed">
              Tài khoản doanh nghiệp và quản trị viên của bạn đã được khởi tạo.
              Bạn có thể đăng nhập ngay bây giờ để thiết lập hệ thống.
            </p>

            <button
              onClick={() => navigate("/login")}
              className="inline-flex items-center justify-center px-10 h-14 text-[16px] font-bold text-white transition-all bg-blue-600 rounded-xl hover:bg-blue-700 shadow-lg shadow-blue-600/20 hover:shadow-blue-600/40 hover:-translate-y-0.5"
            >
              Đăng nhập vào hệ thống
            </button>
          </div>
        )}

        {/* Footer Compliance Text */}
        <div className="mt-12 pt-6 border-t border-slate-100 text-center">
          <p className="text-xs font-bold text-slate-400 uppercase tracking-widest mb-2">
            Tuân thủ NĐ 123/2020/NĐ-CP & TT 78/2021/TT-BTC
          </p>
          <p className="text-xs font-semibold text-slate-400">
            © 2026 SmartInvoice Shield. Built on AWS.
          </p>
        </div>
      </div>
    </div>
  );
};

export default Register;
