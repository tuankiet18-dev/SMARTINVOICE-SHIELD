import React, { useState, useEffect } from "react";
import { Form, Input, Checkbox, message, Modal } from "antd";
import {
  UserOutlined,
  LockOutlined,
  ArrowLeftOutlined,
} from "@ant-design/icons";
import { useNavigate } from "react-router-dom";
import { authService, LoginRequest } from "@/services/auth";
import { useAuth } from "@/contexts/AuthContext";

const getPostLoginRedirect = (role?: string) =>
  role === "SuperAdmin" ? "/admin" : "/app/dashboard";

const Login: React.FC = () => {
  const navigate = useNavigate();
  const [loading, setLoading] = useState(false);
  const [form] = Form.useForm();
  const [passwordForm] = Form.useForm();
  const [forgotPasswordForm] = Form.useForm();
  const { login } = useAuth();

  // New Password Challenge State
  const [showNewPasswordModal, setShowNewPasswordModal] = useState(false);
  const [challengeSession, setChallengeSession] = useState("");
  const [loginEmail, setLoginEmail] = useState("");

  // Forgot password state
  const [showForgotPasswordModal, setShowForgotPasswordModal] = useState(false);
  const [forgotPasswordStep, setForgotPasswordStep] = useState<1 | 2>(1);
  const [forgotPasswordEmail, setForgotPasswordEmail] = useState("");

  useEffect(() => {
    const rememberedEmail = localStorage.getItem("rememberedEmail");
    
    if (rememberedEmail) {
      form.setFieldsValue({
        email: rememberedEmail,
        remember: true,
      });
    }
  }, [form]);

  const onFinish = async (values: LoginRequest & { remember?: boolean }) => {
    try {
      setLoading(true);
      
      // Chúng ta chỉ nên lưu email (Username) còn việc lưu giữ phiên đăng nhập sẽ dựa trên Refresh Token.
      if (values.remember) {
        localStorage.setItem("rememberedEmail", values.email);
      } else {
        localStorage.removeItem("rememberedEmail");
      }

      const data = await login(values);

      if (data.challengeName === "NEW_PASSWORD_REQUIRED" && data.session) {
        setChallengeSession(data.session);
        setLoginEmail(values.email);
        setShowNewPasswordModal(true);
        return;
      }

      message.success("Đăng nhập thành công!");
      navigate(getPostLoginRedirect(data.user?.role));
    } catch (error: any) {
      message.error(error.response?.data?.message || "Đăng nhập thất bại");
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
        session: challengeSession,
      });

      localStorage.setItem("token", data.accessToken);
      localStorage.setItem("idToken", data.idToken);
      localStorage.setItem("user", JSON.stringify(data.user));
      window.dispatchEvent(new Event("storage"));

      message.success("Đổi mật khẩu và đăng nhập thành công!");
      setShowNewPasswordModal(false);
      const savedUser = JSON.parse(localStorage.getItem("user") || "{}");
      navigate(getPostLoginRedirect(savedUser?.role));
    } catch (error: any) {
      message.error(error.response?.data?.message || "Đổi mật khẩu thất bại");
    } finally {
      setLoading(false);
    }
  };

  const handleForgotPasswordRequest = async (values: { email: string }) => {
    try {
      setLoading(true);
      await authService.forgotPassword(values.email);
      setForgotPasswordEmail(values.email);
      setForgotPasswordStep(2);
      message.success("Mã xác nhận đã được gửi đến email của bạn.");
    } catch (error: any) {
      message.error(error.response?.data?.message || "Lỗi khi gửi mã quên mật khẩu");
    } finally {
      setLoading(false);
    }
  };

  const handleForgotPasswordConfirm = async (values: any) => {
    try {
      setLoading(true);
      await authService.confirmForgotPassword({
        email: forgotPasswordEmail,
        confirmationCode: values.confirmationCode,
        newPassword: values.newPassword,
      });
      message.success("Đặt lại mật khẩu thành công! Vui lòng đăng nhập bằng mật khẩu mới.");
      setShowForgotPasswordModal(false);
    } catch (error: any) {
      message.error(error.response?.data?.message || "Mã xác nhận không đúng hoặc đặt lại mật khẩu thất bại");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen flex bg-slate-50 font-sans selection:bg-blue-100/50">
      {/* Left Panel - Branding (Hidden on Mobile) */}
      <div className="hidden lg:flex lg:w-1/2 bg-slate-900 p-12 flex-col justify-between relative overflow-hidden">
        {/* Background gradient blur - Đồng bộ với Landing Page */}
        <div className="absolute top-1/4 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[600px] h-[600px] bg-blue-600/20 rounded-full blur-[100px] pointer-events-none z-0"></div>

        <div
          className="relative z-10 flex items-center gap-3 cursor-pointer"
          onClick={() => navigate("/")}
        >
          <img
            src="/logo-transparent.png"
            alt="SmartInvoice Logo"
            className="h-12 w-auto object-contain bg-white rounded-xl p-2 shadow-lg"
          />
          <div className="flex flex-col">
            <span className="text-xl font-bold leading-none tracking-tight text-white">
              SmartInvoice
            </span>
            <span className="text-xs font-bold leading-none tracking-widest text-slate-400 mt-1">
              SHIELD
            </span>
          </div>
        </div>

        <div className="relative z-10 max-w-md">
          <h1 className="text-4xl font-black text-white mb-6 leading-tight tracking-tight">
            Quản trị rủi ro hóa đơn <br />
            <span className="text-transparent bg-clip-text bg-gradient-to-r from-blue-400 to-indigo-400">
              cấp độ Doanh nghiệp.
            </span>
          </h1>
          <p className="text-slate-400 text-lg leading-relaxed mb-12 font-medium">
            Hệ thống tự động hóa quy trình rà soát, loại bỏ 90% thời gian xử lý
            và đảm bảo tuân thủ tuyệt đối pháp luật Việt Nam.
          </p>

          <div className="grid grid-cols-2 gap-8">
            <div>
              <span className="block text-3xl font-black text-white mb-1">
                90%
              </span>
              <span className="text-sm font-semibold text-slate-500">
                Thời gian xử lý giảm
              </span>
            </div>
            <div>
              <span className="block text-3xl font-black text-white mb-1">
                100%
              </span>
              <span className="text-sm font-semibold text-slate-500">
                Tuân thủ pháp lý
              </span>
            </div>
            <div>
              <span className="block text-3xl font-black text-white mb-1">
                85%+
              </span>
              <span className="text-sm font-semibold text-slate-500">
                Độ chính xác AI
              </span>
            </div>
            <div>
              <span className="block text-3xl font-black text-white mb-1">
                24/7
              </span>
              <span className="text-sm font-semibold text-slate-500">
                Kiểm toán liên tục
              </span>
            </div>
          </div>
        </div>

        <div className="relative z-10">
          <p className="text-sm font-semibold text-slate-500">
            © 2026 SmartInvoice Shield. Built on AWS.
          </p>
        </div>
      </div>

      {/* Right Panel - Login Form */}
      <div className="flex-1 flex flex-col items-center justify-center p-6 sm:p-12 relative">
        {/* Back to Home Button for Mobile/Tablet */}
        <button
          onClick={() => navigate("/")}
          className="lg:hidden absolute top-6 left-6 flex items-center gap-2 text-sm font-semibold text-slate-500 hover:text-slate-900 transition-colors"
        >
          <ArrowLeftOutlined /> Trang chủ
        </button>

        <div className="w-full max-w-[440px] bg-white rounded-[2rem] p-8 sm:p-10 shadow-2xl shadow-slate-200/50 border border-slate-100">
          {/* Mobile Logo */}
          <div className="lg:hidden flex flex-col items-center mb-10">
            <img
              src="/logo-transparent.png"
              alt="SmartInvoice Logo"
              className="h-14 mb-3 object-contain"
            />
            <h2 className="text-xl font-black text-slate-900 tracking-tight">
              SmartInvoice Shield
            </h2>
          </div>

          <div className="mb-8">
            <h2 className="text-3xl font-black text-slate-900 tracking-tight mb-2">
              Đăng nhập
            </h2>
            <p className="text-slate-500 font-medium text-[15px]">
              Chào mừng bạn quay lại hệ thống quản trị
            </p>
          </div>

          {/* Thay đổi size của Antd Form thành large để các ô input bự ra */}
          <Form
            form={form}
            layout="vertical"
            onFinish={onFinish}
            size="large"
            className="w-full"
          >
            <Form.Item
              name="email"
              rules={[{ required: true, message: "Vui lòng nhập email" }]}
            >
              <Input
                prefix={<UserOutlined className="text-slate-400 mr-1" />}
                placeholder="Email doanh nghiệp"
                className="h-12 rounded-xl bg-slate-50 border-slate-200 hover:border-blue-400 focus:border-blue-500 focus:shadow-[0_0_0_2px_rgba(37,99,235,0.2)] text-[15px]"
              />
            </Form.Item>

            <Form.Item
              name="password"
              rules={[{ required: true, message: "Vui lòng nhập mật khẩu" }]}
              className="mb-4"
            >
              <Input.Password
                prefix={<LockOutlined className="text-slate-400 mr-1" />}
                placeholder="Mật khẩu"
                className="h-12 rounded-xl bg-slate-50 border-slate-200 hover:border-blue-400 focus:border-blue-500 focus:shadow-[0_0_0_2px_rgba(37,99,235,0.2)] text-[15px]"
              />
            </Form.Item>

            <div className="flex items-center justify-between mb-8">
              <Form.Item name="remember" valuePropName="checked" noStyle>
                <Checkbox className="text-slate-500 font-medium">
                  Ghi nhớ tôi
                </Checkbox>
              </Form.Item>
              <a
                href="#"
                onClick={(e) => {
                  e.preventDefault();
                  setShowForgotPasswordModal(true);
                  setForgotPasswordStep(1);
                  forgotPasswordForm.resetFields();
                }}
                className="text-blue-600 text-[14px] font-bold hover:text-blue-700 transition-colors"
              >
                Quên mật khẩu?
              </a>
            </div>

            <button
              type="submit"
              disabled={loading}
              className="w-full h-12 flex items-center justify-center text-[15px] font-bold text-white transition-all bg-blue-600 rounded-xl hover:bg-blue-700 shadow-lg shadow-blue-600/20 hover:shadow-blue-600/40 hover:-translate-y-0.5 disabled:opacity-70 disabled:hover:translate-y-0"
            >
              {loading ? "Đang xử lý..." : "Đăng nhập vào hệ thống"}
            </button>
          </Form>

          <div className="relative flex items-center py-8">
            <div className="flex-grow border-t border-slate-200"></div>
            <span className="flex-shrink-0 mx-4 text-slate-400 text-sm font-semibold">
              hoặc
            </span>
            <div className="flex-grow border-t border-slate-200"></div>
          </div>

          <button
            onClick={() => navigate("/register")}
            className="w-full h-12 flex items-center justify-center text-[15px] font-bold text-slate-700 transition-all bg-white border-2 border-slate-200 rounded-xl hover:bg-slate-50 hover:border-slate-300"
          >
            Đăng ký tài khoản mới
          </button>
        </div>
      </div>

      {/* New Password Required Modal */}
      <Modal
        title={
          <span className="text-xl font-bold text-slate-900">
            Đổi mật khẩu lần đầu
          </span>
        }
        open={showNewPasswordModal}
        onCancel={() => setShowNewPasswordModal(false)}
        footer={null}
        destroyOnHidden
        mask={{ closable: false }}
        className="rounded-2xl overflow-hidden"
      >
        <div className="mb-6 mt-2">
          <p className="text-slate-500 font-medium text-[15px]">
            Tài khoản của bạn được tạo bởi Quản trị viên. Theo yêu cầu bảo mật,
            vui lòng tạo mật khẩu mới trong lần đăng nhập đầu tiên.
          </p>
        </div>

        <Form
          form={passwordForm}
          layout="vertical"
          onFinish={onFinishNewPassword}
          size="large"
        >
          <Form.Item
            name="newPassword"
            label={
              <span className="font-semibold text-slate-700">Mật khẩu mới</span>
            }
            rules={[
              { required: true, message: "Vui lòng nhập mật khẩu mới" },
              { min: 8, message: "Mật khẩu phải dài ít nhất 8 ký tự" },
              {
                pattern:
                  /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$/,
                message:
                  "Mật khẩu cần ít nhất 1 chữ hoa, 1 chữ thường, 1 số và 1 ký tự đặc biệt",
              },
            ]}
          >
            <Input.Password
              placeholder="Nhập mật khẩu mới"
              className="h-11 rounded-lg border-slate-200"
            />
          </Form.Item>

          <Form.Item
            name="confirmPassword"
            label={
              <span className="font-semibold text-slate-700">
                Xác nhận mật khẩu mới
              </span>
            }
            dependencies={["newPassword"]}
            rules={[
              { required: true, message: "Vui lòng xác nhận mật khẩu mới" },
              ({ getFieldValue }) => ({
                validator(_, value) {
                  if (!value || getFieldValue("newPassword") === value) {
                    return Promise.resolve();
                  }
                  return Promise.reject(
                    new Error("Mật khẩu xác nhận không khớp!"),
                  );
                },
              }),
            ]}
          >
            <Input.Password
              placeholder="Nhập lại mật khẩu mới"
              className="h-11 rounded-lg border-slate-200"
            />
          </Form.Item>

          <button
            type="submit"
            disabled={loading}
            className="w-full h-11 mt-2 flex items-center justify-center text-[15px] font-bold text-white transition-all bg-slate-900 rounded-xl hover:bg-slate-800 shadow-md"
          >
            {loading ? "Đang cập nhật..." : "Xác nhận Đổi mật khẩu"}
          </button>
        </Form>
      </Modal>

      {/* Forgot Password Modal */}
      <Modal
        title={
          <span className="text-xl font-bold text-slate-900">
            {forgotPasswordStep === 1 ? "Quên mật khẩu" : "Đặt lại mật khẩu"}
          </span>
        }
        open={showForgotPasswordModal}
        onCancel={() => {
          setShowForgotPasswordModal(false);
          setForgotPasswordStep(1);
          forgotPasswordForm.resetFields();
        }}
        footer={null}
        destroyOnClose
        className="rounded-2xl overflow-hidden"
      >
        <div className="mb-6 mt-2">
          <p className="text-slate-500 font-medium text-[15px]">
            {forgotPasswordStep === 1
              ? "Vui lòng nhập địa chỉ email đã đăng ký. Chúng tôi sẽ gửi một mã xác nhận để đặt lại mật khẩu của bạn."
              : `Vui lòng kiểm tra email ${forgotPasswordEmail} và nhập mã xác nhận cùng mật khẩu mới bên dưới.`}
          </p>
        </div>

        <Form
          form={forgotPasswordForm}
          layout="vertical"
          onFinish={forgotPasswordStep === 1 ? handleForgotPasswordRequest : handleForgotPasswordConfirm}
          size="large"
        >
          {forgotPasswordStep === 1 && (
            <Form.Item
              name="email"
              rules={[
                { required: true, message: "Vui lòng nhập email" },
                { type: "email", message: "Email không hợp lệ" },
              ]}
            >
              <Input
                prefix={<UserOutlined className="text-slate-400 mr-1" />}
                placeholder="Nhập email của bạn"
                className="h-11 rounded-lg border-slate-200"
              />
            </Form.Item>
          )}

          {forgotPasswordStep === 2 && (
            <>
              <Form.Item
                name="confirmationCode"
                label={<span className="font-semibold text-slate-700">Mã xác nhận</span>}
                rules={[{ required: true, message: "Vui lòng nhập mã xác nhận" }]}
              >
                <Input
                  placeholder="Nhập 6 số"
                  className="h-11 rounded-lg border-slate-200 tracking-widest text-center font-bold"
                />
              </Form.Item>

              <Form.Item
                name="newPassword"
                label={<span className="font-semibold text-slate-700">Mật khẩu mới</span>}
                rules={[
                  { required: true, message: "Vui lòng nhập mật khẩu mới" },
                  { min: 8, message: "Mật khẩu phải dài ít nhất 8 ký tự" },
                  {
                    pattern: /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$/,
                    message: "Mật khẩu cần ít nhất 1 chữ hoa, 1 chữ thường, 1 số và 1 ký tự đặc biệt",
                  },
                ]}
              >
                <Input.Password
                  placeholder="Nhập mật khẩu mới"
                  className="h-11 rounded-lg border-slate-200"
                />
              </Form.Item>

              <Form.Item
                name="confirmPassword"
                label={<span className="font-semibold text-slate-700">Xác nhận mật khẩu</span>}
                dependencies={["newPassword"]}
                rules={[
                  { required: true, message: "Vui lòng xác nhận lại mật khẩu" },
                  ({ getFieldValue }) => ({
                    validator(_, value) {
                      if (!value || getFieldValue("newPassword") === value) {
                        return Promise.resolve();
                      }
                      return Promise.reject(new Error("Mật khẩu xác nhận không khớp!"));
                    },
                  }),
                ]}
              >
                <Input.Password
                  placeholder="Nhập lại mật khẩu mới"
                  className="h-11 rounded-lg border-slate-200"
                />
              </Form.Item>
            </>
          )}

          <button
            type="submit"
            disabled={loading}
            className="w-full h-11 mt-2 flex items-center justify-center text-[15px] font-bold text-white transition-all bg-blue-600 rounded-xl hover:bg-blue-700 shadow-md disabled:opacity-70"
          >
            {loading ? "Đang xử lý..." : forgotPasswordStep === 1 ? "Gửi mã xác nhận" : "Đặt lại mật khẩu"}
          </button>
        </Form>
      </Modal>
    </div>
  );
};

export default Login;
