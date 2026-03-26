import React from 'react';
import { Layout, Typography, Divider } from 'antd';
import { useNavigate } from 'react-router-dom';
import { ArrowLeftOutlined } from '@ant-design/icons';

const { Content } = Layout;
const { Title, Paragraph } = Typography;

const TermsOfUse: React.FC = () => {
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
                        Điều khoản sử dụng
                    </Title>
                    <Paragraph className="text-slate-600 text-[15px] leading-relaxed">
                        Cảm ơn bạn đã sử dụng dịch vụ của SmartInvoice Shield. 
                        Bằng việc đăng ký tài khoản và sử dụng hệ thống, bạn đồng ý tuân thủ các điều khoản sau đây.
                    </Paragraph>

                    <Title level={4} className="mt-8 text-slate-800">1. Quyền và Trách nhiệm của Người dùng</Title>
                    <Paragraph className="text-slate-600 text-[15px] leading-relaxed">
                        - Cung cấp thông tin xác thực, chính xác khi đăng ký (Mã số thuế, thông tin quản trị viên).<br/>
                        - Bảo mật thông tin đăng nhập và chịu trách nhiệm về các hoạt động diễn ra dưới tài khoản của mình.<br/>
                        - Sử dụng nền tảng cho các mục đích hợp pháp, đặc biệt trong việc quản lý, trích xuất và lưu trữ hóa đơn điện tử hợp lệ theo quy định của pháp luật.
                    </Paragraph>

                    <Title level={4} className="mt-8 text-slate-800">2. Quyền và Trách nhiệm của SmartInvoice Shield</Title>
                    <Paragraph className="text-slate-600 text-[15px] leading-relaxed">
                        - Đảm bảo hệ thống hoạt động ổn định, bảo mật và an toàn dữ liệu hóa đơn của khách hàng.<br/>
                        - Tuân thủ các quy định tại Nghị định 123/2020/NĐ-CP và Thông tư 78/2021/TT-BTC về lưu trữ và xử lý hóa đơn điện tử.<br/>
                        - Có quyền tạm ngưng hoặc hủy bỏ dịch vụ nếu phát hiện người dùng có hành vi gian lận hoặc vi phạm nghiêm trọng.
                    </Paragraph>

                    <Title level={4} className="mt-8 text-slate-800">3. Quyền sở hữu trí tuệ</Title>
                    <Paragraph className="text-slate-600 text-[15px] leading-relaxed">
                        Mọi quyền sở hữu trí tuệ liên quan đến phần mềm, giao diện, logo, tính năng AI/OCR của SmartInvoice Shield đều thuộc về chúng tôi. 
                        Người dùng không được sao chép, chỉnh sửa hoặc phân phối nền tảng mà không có sự đồng ý bằng văn bản.
                    </Paragraph>

                    <Title level={4} className="mt-8 text-slate-800">4. Giới hạn trách nhiệm</Title>
                    <Paragraph className="text-slate-600 text-[15px] leading-relaxed">
                        SmartInvoice Shield không chịu trách nhiệm đối với các thiệt hại gián tiếp phát sinh từ sự cố kỹ thuật ngoài ý muốn 
                        (như sự cố hệ thống mạng quốc gia, máy chủ do nhà cung cấp AWS gặp lỗi nghiêm trọng) mặc dù chúng tôi cam kết sẽ nỗ lực khắc phục trong thời gian ngắn nhất.
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

export default TermsOfUse;
