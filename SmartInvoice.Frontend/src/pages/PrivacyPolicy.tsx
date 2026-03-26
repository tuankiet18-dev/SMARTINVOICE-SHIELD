import React from 'react';
import { Layout, Typography, Divider } from 'antd';
import { useNavigate } from 'react-router-dom';
import { ArrowLeftOutlined } from '@ant-design/icons';

const { Title, Paragraph } = Typography;

const PrivacyPolicy: React.FC = () => {
    const navigate = useNavigate();

    return (
        <div className="min-h-screen bg-slate-50 font-sans py-10 px-4 sm:px-6">
            <div className="max-w-4xl mx-auto bg-white rounded-2xl shadow-xl shadow-slate-200/50 p-8 sm:p-12 relative">
                <button
                    onClick={() => {
                        if (window.history.state && window.history.state.idx > 0) {
                            navigate(-1);
                        } else {
                            window.close();
                            // Nếu trình duyệt chặn đóng tab, điều hướng về trang chủ
                            setTimeout(() => navigate('/'), 100);
                        }
                    }}
                    className="text-slate-500 hover:text-blue-600 transition-colors flex items-center gap-2 font-medium mb-6"
                >
                    <ArrowLeftOutlined /> Quay lại
                </button>
                
                <Typography>
                    <Title level={2} className="text-slate-900 border-b pb-4 mb-6">
                        Chính sách bảo mật
                    </Title>
                    <Paragraph className="text-slate-600 text-[15px] leading-relaxed">
                        Chính sách bảo mật này giải thích cách SmartInvoice Shield thu thập, sử dụng và bảo vệ thông tin 
                        của bạn khi truy cập và sử dụng hệ thống trích xuất hóa đơn của chúng tôi.
                    </Paragraph>

                    <Title level={4} className="mt-8 text-slate-800">1. Thu thập thông tin</Title>
                    <Paragraph className="text-slate-600 text-[15px] leading-relaxed">
                        Chúng tôi thu thập các thông tin sau:<br/>
                        - <strong>Thông tin doanh nghiệp:</strong> Mã số thuế, tên công ty, địa chỉ, loại hình doanh nghiệp.<br/>
                        - <strong>Thông tin quản trị viên:</strong> Họ tên, email, số điện thoại.<br/>
                        - <strong>Dữ liệu hóa đơn:</strong> File hóa đơn (PDF, XML, hình ảnh) được tải lên hệ thống để phân tích và lưu trữ.
                    </Paragraph>

                    <Title level={4} className="mt-8 text-slate-800">2. Mục đích sử dụng dữ liệu</Title>
                    <Paragraph className="text-slate-600 text-[15px] leading-relaxed">
                        - Xác thực và khởi tạo tài khoản hệ thống.<br/>
                        - Xử lý, trích xuất dữ liệu bằng công nghệ OCR/AI từ hóa đơn người dùng tải lên.<br/>
                        - Kiểm tra tính hợp lệ của hóa đơn với cơ sở dữ liệu Tổng cục Thuế.<br/>
                        - Gửi thông báo bảo mật, xác thực email, và hỗ trợ khách hàng.
                    </Paragraph>

                    <Title level={4} className="mt-8 text-slate-800">3. Bảo vệ dữ liệu</Title>
                    <Paragraph className="text-slate-600 text-[15px] leading-relaxed">
                        - SmartInvoice Shield được xây dựng trên nền tảng AWS với các tiêu chuẩn an ninh mạng quốc tế (AWS Cognito, S3 Encryption).<br/>
                        - Dữ liệu hóa đơn của bạn được mã hóa an toàn và chỉ có những tài khoản được phân quyền trong chính doanh nghiệp của bạn mới có thể truy cập.<br/>
                        - Chúng tôi không bao giờ bán, trao đổi hoặc chia sẻ thông tin cá nhân hay dữ liệu hóa đơn của bạn cho bất kỳ bên thứ ba nào vì mục đích thương mại.
                    </Paragraph>

                    <Title level={4} className="mt-8 text-slate-800">4. Liên hệ</Title>
                    <Paragraph className="text-slate-600 text-[15px] leading-relaxed">
                        Nếu bạn có bất kỳ thắc mắc nào về quyền riêng tư hoặc dữ liệu của mình, vui lòng liên hệ phòng hỗ trợ qua công cụ được cung cấp hoặc qua email đăng ký của hệ thống.
                    </Paragraph>
                </Typography>

                <div className="mt-12 pt-6 border-t border-slate-100 text-center">
                    <p className="text-xs font-semibold text-slate-400">
                        Cập nhật lần cuối: 22/03/2026. © SmartInvoice Shield. Built on AWS.
                    </p>
                </div>
            </div>
        </div>
    );
};

export default PrivacyPolicy;
