import React, { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  ShieldCheck,
  CheckCircle2,
  Zap,
  Layers,
  Clock,
  Lock,
  BarChart3,
  Database,
  ArrowRight,
  Upload,
  Shield,
  AlertTriangle,
  FileText,
  ChevronRight,
} from "lucide-react";

const features = [
  {
    icon: <Zap className="w-5 h-5 text-blue-600" />,
    title: "Xử lý bằng AI",
    desc: "Tự động trích xuất và xác thực dữ liệu hóa đơn từ XML, PDF và hình ảnh với độ chính xác trên 85%.",
  },
  {
    icon: <Layers className="w-5 h-5 text-slate-700" />,
    title: "Xác thực 3 lớp",
    desc: "Kiểm tra cấu trúc, xác thực chữ ký số và đối chiếu tính tuân thủ logic nghiệp vụ.",
  },
  {
    icon: <ShieldCheck className="w-5 h-5 text-slate-700" />,
    title: "Đảm bảo tuân thủ",
    desc: "Tuân thủ 100% các yêu cầu của Nghị định 123/2020/NĐ-CP và Quyết định 1550/QĐ-TCT.",
  },
  {
    icon: <Database className="w-5 h-5 text-slate-700" />,
    title: "Phát hiện rủi ro theo thời gian thực",
    desc: "Cảnh báo tức thì đối với hóa đơn giả mạo, mã số thuế không hợp lệ và lỗi tính toán.",
  },
  {
    icon: <Clock className="w-5 h-5 text-purple-600" />,
    title: "Giảm 90% thời gian",
    desc: "Xử lý hóa đơn tính bằng giây thay vì phút. Rút ngắn từ 5-10 phút xuống dưới 30 giây cho mỗi tài liệu.",
  },
  {
    icon: <Lock className="w-5 h-5 text-indigo-600" />,
    title: "Công nghệ chống giả mạo",
    desc: "Xác minh độ chuẩn xác của MST và phát hiện chữ ký số gian lận bằng mật mã nâng cao.",
  },
  {
    icon: <BarChart3 className="w-5 h-5 text-teal-600" />,
    title: "Phân tích chuyên sâu",
    desc: "Dashboard với các chỉ số KPI theo thời gian thực, xu hướng rủi ro và báo cáo kiểm toán toàn diện.",
  },
  {
    icon: <Database className="w-5 h-5 text-emerald-600" />,
    title: "Nhật ký kiểm toán bất biến",
    desc: "Lưu trữ toàn bộ lịch sử thay đổi với thông tin AI, CÁI GÌ, KHI NÀO để phục vụ tuân thủ.",
  },
];

const LandingPage: React.FC = () => {
  const navigate = useNavigate();
  const [isScrolled, setIsScrolled] = useState(false);

  useEffect(() => {
    document.documentElement.style.scrollBehavior = "smooth";

    const handleScroll = () => {
      setIsScrolled(window.scrollY > 20);
    };

    window.addEventListener("scroll", handleScroll);
    return () => {
      window.removeEventListener("scroll", handleScroll);
      document.documentElement.style.scrollBehavior = "auto";
    };
  }, []);

  const scrollToSection = (
    e: React.MouseEvent<HTMLAnchorElement>,
    id: string,
  ) => {
    e.preventDefault();
    const element = document.getElementById(id);
    if (element) {
      const navHeight = 90;
      const elementPosition =
        element.getBoundingClientRect().top + window.scrollY;
      window.scrollTo({
        top: elementPosition - navHeight,
        behavior: "smooth",
      });
    }
  };

  return (
    <div className="min-h-screen bg-white font-sans text-slate-900 selection:bg-blue-100/50">
      {/* Navbar Minimalist & Sticky */}
      <nav
        className={`fixed top-0 left-0 right-0 z-50 transition-all duration-300 ${isScrolled ? "bg-white/90 backdrop-blur-md border-b border-slate-100 shadow-sm py-4" : "bg-transparent py-6"}`}
      >
        <div className="max-w-7xl mx-auto px-6 lg:px-12 flex items-center justify-between">
          <div
            className="flex items-center gap-3 flex-shrink-0 cursor-pointer"
            onClick={() => window.scrollTo({ top: 0, behavior: "smooth" })}
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

          <div className="hidden md:flex items-center gap-10 text-sm font-semibold text-slate-600">
            <a
              href="#features"
              onClick={(e) => scrollToSection(e, "features")}
              className="hover:text-blue-600 transition-colors"
            >
              Tính năng
            </a>
            <a
              href="#how-it-works"
              onClick={(e) => scrollToSection(e, "how-it-works")}
              className="hover:text-blue-600 transition-colors"
            >
              Cách hoạt động
            </a>
            <a
              href="#pricing"
              onClick={(e) => scrollToSection(e, "pricing")}
              className="hover:text-blue-600 transition-colors"
            >
              Bảng giá
            </a>
          </div>

          <div className="flex items-center gap-4">
            <button
              onClick={() => navigate("/login")}
              className="hidden sm:block px-5 py-2.5 text-sm font-semibold text-slate-600 transition-all hover:text-slate-900"
            >
              Đăng nhập
            </button>
            <button
              onClick={() => navigate("/register")}
              className="px-6 py-2.5 text-sm font-bold text-white bg-blue-600 rounded-xl hover:bg-blue-700 hover:shadow-lg hover:shadow-blue-600/30 transition-all"
            >
              Bắt đầu ngay
            </button>
          </div>
        </div>
      </nav>

      {/* Hero Section */}
      <section className="pt-36 pb-20 px-4 text-center overflow-hidden relative bg-white">
        {/* Background gradient blur elements */}
        <div className="absolute top-1/4 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[800px] h-[600px] bg-blue-50/50 rounded-full blur-[100px] pointer-events-none"></div>

        <div className="max-w-4xl mx-auto relative z-10">
          <div className="inline-flex items-center px-4 py-1.5 mb-10 text-xs font-bold uppercase tracking-wider border border-blue-200/60 rounded-full bg-blue-50/50 text-blue-600 shadow-sm">
            <span className="relative flex h-2 w-2 mr-2">
              <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-blue-400 opacity-75"></span>
              <span className="relative inline-flex rounded-full h-2 w-2 bg-blue-500"></span>
            </span>
            Hệ thống Quản trị Rủi ro Doanh nghiệp đã sẵn sàng
          </div>

          <h1 className="text-5xl md:text-[5.5rem] font-black tracking-tighter text-slate-900 mb-8 leading-[1.05]">
            Bảo mật Hóa đơn. <br className="hidden md:block" />
            <span className="text-transparent bg-clip-text bg-gradient-to-r from-blue-600 to-indigo-600">
              Không còn Gian lận.
            </span>
          </h1>

          <p className="max-w-2xl mx-auto text-lg md:text-xl text-slate-500 mb-10 leading-relaxed font-medium">
            Loại bỏ 90% thời gian xử lý hóa đơn và phát hiện gian lận trước khi
            chúng ảnh hưởng đến tài chính của bạn. Tự động xác minh tuân thủ với
            quy trình kiểm tra 3 lớp, vận hành trên nền tảng AWS Cloud.
          </p>

          <div className="flex flex-col sm:flex-row items-center justify-center gap-4 mb-20 relative z-20">
            <button
              onClick={() => navigate("/register")}
              className="group flex justify-center items-center gap-2 w-full sm:w-auto px-8 py-4 text-[15px] font-bold text-white transition-all bg-slate-900 shadow-[0_8px_30px_rgba(15,23,42,0.2)] rounded-2xl hover:bg-slate-800 hover:shadow-[0_8px_30px_rgba(15,23,42,0.3)] hover:-translate-y-0.5"
            >
              Dùng thử Miễn phí
              <ArrowRight className="w-4 h-4 group-hover:translate-x-1 transition-transform" />
            </button>
            <button className="flex justify-center items-center w-full sm:w-auto px-8 py-4 text-[15px] font-bold text-slate-700 transition-all bg-white border-2 border-slate-200 rounded-2xl hover:bg-slate-50 hover:border-slate-300">
              Liên hệ Kinh doanh
            </button>
          </div>
        </div>
      </section>

      {/* Image Grid Overlap Placeholder Section */}
      <div className="max-w-[1200px] mx-auto px-6 mb-40 relative z-20">
        <div className="relative h-[300px] sm:h-[450px] md:h-[600px] w-full perspective-[1200px] flex items-end justify-center">
          {/* Back Left - Alerts Modal Panel */}
          <div className="absolute top-[5%] left-[5%] md:left-[10%] w-[55%] md:w-[40%] h-[60%] md:h-[70%] bg-white rounded-2xl shadow-2xl shadow-slate-300/60 border border-slate-200 overflow-hidden transform -rotate-3 origin-bottom-left transition-all hover:rotate-0 hover:z-30 duration-500 hidden sm:flex flex-col">
            <div className="h-10 bg-slate-50 border-b border-slate-100 flex items-center px-4 gap-2 shrink-0">
              <div className="w-2.5 h-2.5 rounded-full bg-slate-300"></div>
              <div className="w-2.5 h-2.5 rounded-full bg-slate-300"></div>
              <div className="w-2.5 h-2.5 rounded-full bg-slate-300"></div>
              <span className="ml-2 text-[10px] font-semibold text-slate-400">
                Cảnh báo Rủi ro
              </span>
            </div>
            <div className="flex-1 relative bg-slate-100 overflow-hidden p-[2px]">
              <img
                src="/risk-check.png"
                alt="Cảnh báo Rủi ro Dashboard"
                className="w-full h-full object-cover object-top rounded-b-[14px]"
                onError={(e) => {
                  e.currentTarget.style.display = "none";
                }}
              />
              <div className="absolute inset-0 flex items-center justify-center -z-10 bg-white">
                <div className="text-center">
                  <AlertTriangle className="w-8 h-8 text-slate-300 mx-auto mb-2" />
                  <span className="text-xs text-slate-400 font-bold">
                    Cảnh báo Rủi ro
                  </span>
                </div>
              </div>
            </div>
          </div>

          {/* Back Right - Risky Invoice List Panel */}
          <div className="absolute top-0 right-[5%] md:right-[8%] w-[60%] md:w-[45%] h-[75%] bg-white rounded-2xl shadow-[0_20px_60px_rgba(0,0,0,0.08)] border border-slate-200 overflow-hidden transform rotate-2 origin-bottom-right transition-all hover:rotate-0 hover:z-30 duration-500 hidden sm:flex flex-col">
            <div className="h-10 bg-slate-50 border-b border-slate-100 flex items-center px-4 gap-2 shrink-0">
              <div className="w-2.5 h-2.5 rounded-full bg-slate-300"></div>
              <div className="w-2.5 h-2.5 rounded-full bg-slate-300"></div>
              <div className="w-2.5 h-2.5 rounded-full bg-slate-300"></div>
              <span className="ml-2 text-[10px] font-semibold text-slate-400">
                Chờ Phê duyệt
              </span>
            </div>
            <div className="flex-1 relative bg-slate-100 overflow-hidden p-[2px]">
              <img
                src="/pending-approval.png"
                alt="Chờ Phê duyệt Dashboard"
                className="w-full h-full object-cover object-top rounded-b-[14px]"
                onError={(e) => {
                  e.currentTarget.style.display = "none";
                }}
              />
              <div className="absolute inset-0 flex items-center justify-center -z-10 bg-white">
                <div className="text-center">
                  <FileText className="w-8 h-8 text-slate-300 mx-auto mb-2" />
                  <span className="text-xs text-slate-400 font-bold">
                    Chờ Phê duyệt
                  </span>
                </div>
              </div>
            </div>
          </div>

          {/* Front Center - Main Dashboard Panel */}
          <div className="absolute bottom-0 w-[94%] md:w-[75%] h-[85%] bg-slate-50 rounded-t-3xl shadow-[0_-10px_40px_rgba(0,0,0,0.08)] border-t border-l border-r border-slate-200/80 overflow-hidden z-20 flex flex-col">
            <div className="h-12 bg-white border-b border-slate-200 flex items-center px-4 md:px-6 gap-2 shrink-0">
              <div className="w-3 h-3 rounded-full bg-red-400"></div>
              <div className="w-3 h-3 rounded-full bg-amber-400"></div>
              <div className="w-3 h-3 rounded-full bg-green-400"></div>
              <div className="ml-4 h-6 w-32 sm:w-64 bg-slate-100 border border-slate-200 rounded-md"></div>
            </div>

            <div className="flex-1 relative bg-slate-100 overflow-hidden p-[2px]">
              <img
                src="/dashboard.png"
                alt="SmartInvoice Main Dashboard"
                className="w-full h-full object-cover object-top rounded-b-[22px]"
                onError={(e) => {
                  e.currentTarget.style.display = "none";
                }}
              />
              <div className="absolute inset-0 m-4 sm:m-8 border-2 border-slate-200 border-dashed rounded-2xl flex items-center justify-center -z-10 bg-slate-50">
                <div className="text-center">
                  <BarChart3 className="w-10 h-10 text-slate-300 mx-auto mb-3" />
                  <span className="text-slate-400 font-bold tracking-tight">
                    Tổng quan Bảng điều khiển (Dashboard)
                  </span>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <div className="max-w-7xl mx-auto px-6 mb-32 opacity-60 text-center">
        <p className="text-xs font-bold text-slate-400 uppercase tracking-widest mb-8">
          Được tin dùng bởi các Đội ngũ Kế toán tại Việt Nam
        </p>
        <div className="flex flex-wrap justify-center items-center gap-10 md:gap-20 grayscale">
          <div className="h-8 font-black text-xl text-slate-400">Công ty A</div>
          <div className="h-8 font-black text-xl text-slate-400">
            Doanh nghiệp B
          </div>
          <div className="h-8 font-black text-xl text-slate-400">
            Tập đoàn Toàn cầu
          </div>
          <div className="h-8 font-black text-xl text-slate-400">
            Giải pháp Công nghệ
          </div>
        </div>
      </div>

      {/* Features Section */}
      <section id="features" className="py-24 bg-white scroll-mt-24">
        <div className="max-w-7xl mx-auto px-6">
          <div className="text-center mb-20 font-sans">
            <h2 className="text-4xl md:text-5xl font-black text-slate-900 mb-6 tracking-tight">
              Mọi thứ bạn cần để Làm chủ Hóa đơn
            </h2>
            <p className="text-xl text-slate-500 max-w-3xl mx-auto font-medium leading-relaxed">
              Bộ công cụ toàn diện được thiết kế để loại bỏ những đau đầu trong
              quá trình xử lý hóa đơn và đảm bảo tuân thủ quy định
            </p>
          </div>

          <div className="grid md:grid-cols-2 lg:grid-cols-4 gap-6">
            {features.map((feature, i) => (
              <div
                key={i}
                className="p-8 pb-10 rounded-3xl border border-slate-100 bg-white hover:shadow-xl hover:shadow-slate-200/50 hover:border-blue-100 transition-all duration-300 group"
              >
                <div
                  className={`w-12 h-12 rounded-xl flex items-center justify-center mb-6 bg-slate-50 border border-slate-100 group-hover:scale-110 group-hover:bg-white transition-all`}
                >
                  {feature.icon || (
                    <Layers className="w-5 h-5 text-slate-400" />
                  )}
                </div>
                <h3 className="text-lg font-bold text-slate-900 mb-3 tracking-tight">
                  {feature.title}
                </h3>
                <p className="text-slate-500 leading-relaxed font-medium text-[15px]">
                  {feature.desc}
                </p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* Target Audience Section */}
      <section className="py-24 bg-slate-50/50">
        <div className="max-w-7xl mx-auto px-6">
          <div className="text-center mb-16">
            <h2 className="text-4xl md:text-5xl font-black text-slate-900 mb-6 tracking-tight">
              Hoàn hảo cho Mọi Tổ chức
            </h2>
            <p className="text-lg text-slate-500 max-w-2xl mx-auto font-medium leading-relaxed">
              Dù bạn xử lý hàng chục hay hàng nghìn hóa đơn mỗi ngày,
              SmartInvoice Shield đều có thể mở rộng theo nhu cầu của bạn
            </p>
          </div>

          <div className="grid lg:grid-cols-3 gap-6">
            <div className="p-10 bg-white border border-slate-100 rounded-3xl shadow-sm hover:shadow-xl hover:shadow-slate-200/50 hover:border-transparent transition-all group cursor-default">
              <h3 className="text-xl font-extrabold text-slate-900 flex items-center justify-between border-b border-slate-100 pb-6 mb-8 tracking-tight">
                CFO & Đội ngũ Tài chính{" "}
                <ChevronRight className="w-5 h-5 text-slate-300 group-hover:text-blue-600 transition-colors" />
              </h3>
              <ul className="space-y-5 text-slate-500 font-medium text-[15px]">
                <li className="flex items-start gap-3">
                  <CheckCircle2 className="w-4 h-4 text-green-500 shrink-0 mt-0.5" />{" "}
                  Nắm bắt mức độ rủi ro hóa đơn theo thời gian thực
                </li>
                <li className="flex items-start gap-3">
                  <CheckCircle2 className="w-4 h-4 text-green-500 shrink-0 mt-0.5" />{" "}
                  Báo cáo tuân thủ & nhật ký kiểm toán tự động
                </li>
                <li className="flex items-start gap-3">
                  <CheckCircle2 className="w-4 h-4 text-green-500 shrink-0 mt-0.5" />{" "}
                  Giảm thiểu rủi ro tài chính từ hóa đơn giả mạo
                </li>
              </ul>
            </div>

            <div className="p-10 bg-white border border-slate-100 rounded-3xl shadow-sm hover:shadow-xl hover:shadow-slate-200/50 hover:border-transparent transition-all group cursor-default">
              <h3 className="text-xl font-extrabold text-slate-900 flex items-center justify-between border-b border-slate-100 pb-6 mb-8 tracking-tight">
                Phòng Kế toán{" "}
                <ChevronRight className="w-5 h-5 text-slate-300 group-hover:text-blue-600 transition-colors" />
              </h3>
              <ul className="space-y-5 text-slate-500 font-medium text-[15px]">
                <li className="flex items-start gap-3">
                  <CheckCircle2 className="w-4 h-4 text-green-500 shrink-0 mt-0.5" />{" "}
                  Loại bỏ sai sót khi nhập liệu thủ công
                </li>
                <li className="flex items-start gap-3">
                  <CheckCircle2 className="w-4 h-4 text-green-500 shrink-0 mt-0.5" />{" "}
                  Đánh dấu ngay lập tức các hóa đơn có vấn đề
                </li>
                <li className="flex items-start gap-3">
                  <CheckCircle2 className="w-4 h-4 text-green-500 shrink-0 mt-0.5" />{" "}
                  Rút ngắn chu kỳ từ xử lý hóa đơn đến thanh toán
                </li>
              </ul>
            </div>

            <div className="p-10 bg-white border border-slate-100 rounded-3xl shadow-sm hover:shadow-xl hover:shadow-slate-200/50 hover:border-transparent transition-all group cursor-default">
              <h3 className="text-xl font-extrabold text-slate-900 flex items-center justify-between border-b border-slate-100 pb-6 mb-8 tracking-tight">
                Tuân thủ & Kiểm toán{" "}
                <ChevronRight className="w-5 h-5 text-slate-300 group-hover:text-blue-600 transition-colors" />
              </h3>
              <ul className="space-y-5 text-slate-500 font-medium text-[15px]">
                <li className="flex items-start gap-3">
                  <CheckCircle2 className="w-4 h-4 text-green-500 shrink-0 mt-0.5" />{" "}
                  Nhật ký kiểm toán bất biến và đầy đủ
                </li>
                <li className="flex items-start gap-3">
                  <CheckCircle2 className="w-4 h-4 text-green-500 shrink-0 mt-0.5" />{" "}
                  Tài liệu tuân thủ quy định pháp luật
                </li>
                <li className="flex items-start gap-3">
                  <CheckCircle2 className="w-4 h-4 text-green-500 shrink-0 mt-0.5" />{" "}
                  Dễ dàng xuất dữ liệu cho cơ quan thuế
                </li>
              </ul>
            </div>
          </div>
        </div>
      </section>

      {/* Workflow Section */}
      <section
        id="how-it-works"
        className="py-32 bg-slate-900 relative overflow-hidden scroll-mt-20"
      >
        <div className="absolute top-0 w-full h-px bg-gradient-to-r from-transparent via-slate-700 to-transparent"></div>
        <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[800px] h-[800px] bg-blue-600/10 rounded-full blur-[120px] pointer-events-none"></div>

        <div className="max-w-7xl mx-auto px-6 relative z-10">
          <div className="text-center mb-20">
            <p className="text-xs font-bold text-blue-400 uppercase tracking-widest mb-4">
              Cách Hoạt động
            </p>
            <h2 className="text-4xl md:text-5xl font-black text-white mb-6 tracking-tight">
              Quy trình Đơn giản nhưng Mạnh mẽ
            </h2>
            <p className="text-lg text-slate-400 max-w-2xl mx-auto font-medium leading-relaxed">
              Từ tải lên đến phê duyệt chỉ trong vài phút. 4 bước đơn giản để
              hoàn tất xác minh hóa đơn và tuân thủ
            </p>
          </div>

          <div className="grid lg:grid-cols-4 gap-6 mb-16 relative">
            {[
              {
                step: "01",
                icon: <Upload className="w-6 h-6 text-white" />,
                title: "Tải Hóa đơn lên",
                desc: "Tải lên file XML, PDF hoặc hình ảnh. Hỗ trợ tải lên hàng loạt nhiều tài liệu cùng lúc.",
              },
              {
                step: "02",
                icon: <Zap className="w-6 h-6 text-white" />,
                title: "AI Phân tích",
                desc: "Tự động trích xuất và xác thực 3 lớp. Chấm điểm rủi ro và phát hiện gian lận theo thời gian thực.",
              },
              {
                step: "03",
                icon: <ShieldCheck className="w-6 h-6 text-white" />,
                title: "Đánh giá Tức thì",
                desc: "Chỉ báo rủi ro mã hóa bằng màu sắc. Phân tích chi tiết mọi vấn đề hoặc mối lo ngại được phát hiện.",
              },
              {
                step: "04",
                icon: <BarChart3 className="w-6 h-6 text-white" />,
                title: "Hoàn tất Kiểm toán",
                desc: "Lưu vết toàn bộ thay đổi. Bộ tài liệu tuân thủ hoàn chỉnh sẵn sàng cung cấp cho cơ quan chức năng.",
              },
            ].map((item, i) => (
              <div
                key={i}
                className="relative p-10 bg-slate-800/80 backdrop-blur-sm rounded-3xl border border-slate-700 shadow-xl mt-6"
              >
                <div className="absolute -top-4 right-8 w-10 h-10 bg-blue-600 text-white rounded-full flex items-center justify-center font-bold shadow-lg shadow-blue-600/30 text-sm">
                  {item.step}
                </div>
                <div className="w-14 h-14 bg-slate-700/50 rounded-2xl flex items-center justify-center border border-slate-600 mb-8">
                  {item.icon}
                </div>
                <h3 className="text-xl font-extrabold text-white mb-4 tracking-tight">
                  {item.title}
                </h3>
                <p className="text-slate-400 font-medium leading-relaxed text-[15px]">
                  {item.desc}
                </p>
              </div>
            ))}
          </div>

          <div className="max-w-4xl mx-auto bg-slate-800/60 backdrop-blur-md border border-slate-700/50 rounded-3xl p-8 text-center text-white shadow-xl shadow-black/10">
            <p className="text-lg font-extrabold flex items-center justify-center gap-2 mb-2">
              <span className="text-orange-400">⚡</span> Thời gian xử lý trung
              bình: Dưới 30 giây mỗi hóa đơn
            </p>
            <p className="text-slate-400 font-medium text-[15px]">
              Kể từ thời điểm bạn tải lên cho đến khi hoàn tất đánh giá rủi ro
              và xác thực
            </p>
          </div>
        </div>
      </section>

      {/* Pricing Section */}
      <section id="pricing" className="py-32 bg-slate-50/50 scroll-mt-20">
        <div className="max-w-7xl mx-auto px-6">
          <div className="text-center mb-24">
            <h2 className="text-4xl md:text-5xl font-black text-slate-900 mb-6 tracking-tight">
              Gói cước Linh hoạt cho Mọi Quy mô
            </h2>
            <p className="text-lg text-slate-500 max-w-2xl mx-auto font-medium leading-relaxed">
              Bảng giá đơn giản, minh bạch và không có phí ẩn. Tất cả các gói
              đều bao gồm tính năng cốt lõi và mở rộng theo nhu cầu sử dụng.
            </p>
          </div>

          <div className="grid lg:grid-cols-3 gap-8 items-center max-w-6xl mx-auto">
            {/* Starter */}
            <div className="p-10 bg-white rounded-3xl border border-slate-200 shadow-sm">
              <h3 className="text-3xl font-black text-slate-900 mb-3 tracking-tight">
                Khởi đầu
              </h3>
              <p className="text-slate-500 font-medium text-[15px] mb-10">
                Hoàn hảo cho doanh nghiệp nhỏ
              </p>
              <div className="mb-10 flex items-baseline">
                <span className="text-[3.5rem] font-black tracking-tighter text-slate-900 leading-none">
                  $299
                </span>
                <span className="text-base font-bold text-slate-500 ml-1">
                  /tháng
                </span>
              </div>
              <button
                onClick={() => navigate("/register")}
                className="w-full py-4 text-center text-sm font-bold rounded-xl border border-slate-200 text-slate-700 hover:bg-slate-50 hover:border-slate-300 transition-all mb-10"
              >
                Bắt đầu ngay
              </button>
              <div className="text-[11px] font-bold tracking-widest text-slate-400 uppercase mb-6">
                Bao gồm
              </div>
              <ul className="space-y-5 text-slate-600 font-medium text-[14px]">
                <li className="flex items-start gap-3">
                  <CheckCircle2 className="w-5 h-5 text-slate-300 shrink-0" />{" "}
                  Tối đa 500 hóa đơn/tháng
                </li>
                <li className="flex items-start gap-3">
                  <CheckCircle2 className="w-5 h-5 text-slate-300 shrink-0" />{" "}
                  AI phân tích tài liệu
                </li>
                <li className="flex items-start gap-3">
                  <CheckCircle2 className="w-5 h-5 text-slate-300 shrink-0" />{" "}
                  Tích hợp xác thực 3 lớp
                </li>
                <li className="flex items-start gap-3">
                  <CheckCircle2 className="w-5 h-5 text-slate-300 shrink-0" />{" "}
                  Dashboard & thống kê cơ bản
                </li>
                <li className="flex items-start gap-3">
                  <CheckCircle2 className="w-5 h-5 text-slate-300 shrink-0" />{" "}
                  Hỗ trợ qua Email
                </li>
              </ul>
            </div>

            {/* Professional */}
            <div className="relative p-12 bg-[#0A0F1C] rounded-[2.5rem] border border-slate-800 shadow-[0_20px_80px_rgba(15,23,42,0.4)] z-10 scale-100 lg:scale-[1.05] text-white overflow-hidden">
              <div className="absolute top-0 inset-x-0 h-2 bg-blue-600"></div>
              <div className="absolute top-0 right-0 w-64 h-64 bg-blue-500/10 blur-[60px] pointer-events-none"></div>

              <div className="relative z-10">
                <div className="flex justify-between items-start mb-4 gap-3">
                  <h3 className="text-3xl font-black text-white tracking-tight leading-tight">
                    Chuyên nghiệp
                  </h3>
                  <div className="px-3 py-1.5 bg-blue-600/20 text-blue-400 text-[11px] font-bold rounded-full border border-blue-600/30 shrink-0 whitespace-nowrap mt-1.5">
                    PHỔ BIẾN
                  </div>
                </div>
                <p className="text-slate-400 font-medium text-[15px] mb-10">
                  Dành cho doanh nghiệp đang phát triển
                </p>
                <div className="mb-10 flex items-baseline">
                  <span className="text-[3.5rem] font-black tracking-tighter text-white leading-none">
                    $899
                  </span>
                  <span className="text-base font-bold text-slate-400 ml-1">
                    /tháng
                  </span>
                </div>
                <button
                  onClick={() => navigate("/register")}
                  className="w-full py-4 text-center text-[15px] font-bold bg-blue-600 text-white rounded-xl hover:bg-blue-500 shadow-lg shadow-blue-600/20 hover:shadow-blue-600/40 hover:-translate-y-0.5 transition-all mb-10"
                >
                  Bắt đầu 14 Ngày Dùng thử
                </button>
                <div className="text-[11px] font-bold tracking-widest text-slate-500 uppercase mb-6">
                  Bao gồm
                </div>
                <ul className="space-y-5 text-slate-300 font-semibold text-[14px]">
                  <li className="flex items-start gap-3">
                    <CheckCircle2 className="w-5 h-5 text-blue-500 shrink-0" />{" "}
                    Không giới hạn hóa đơn/tháng
                  </li>
                  <li className="flex items-start gap-3">
                    <CheckCircle2 className="w-5 h-5 text-blue-500 shrink-0" />{" "}
                    Ưu tiên xử lý bằng AI
                  </li>
                  <li className="flex items-start gap-3">
                    <CheckCircle2 className="w-5 h-5 text-blue-500 shrink-0" />{" "}
                    Phát hiện gian lận chuyên sâu
                  </li>
                  <li className="flex items-start gap-3">
                    <CheckCircle2 className="w-5 h-5 text-blue-500 shrink-0" />{" "}
                    Cảnh báo rủi ro thời gian thực
                  </li>
                  <li className="flex items-start gap-3">
                    <CheckCircle2 className="w-5 h-5 text-blue-500 shrink-0" />{" "}
                    Lưu trữ nhật ký kiểm toán 1 năm
                  </li>
                  <li className="flex items-start gap-3">
                    <CheckCircle2 className="w-5 h-5 text-blue-500 shrink-0" />{" "}
                    Báo cáo tuân thủ tùy chỉnh
                  </li>
                </ul>
              </div>
            </div>

            {/* Enterprise */}
            <div className="p-10 bg-white rounded-3xl border border-slate-200 shadow-sm">
              <h3 className="text-3xl font-black text-slate-900 mb-3 tracking-tight">
                Doanh nghiệp
              </h3>
              <p className="text-slate-500 font-medium text-[15px] mb-10">
                Dành cho quy mô vận hành lớn
              </p>
              <div className="mb-10 flex items-baseline">
                <span className="text-[3.5rem] font-black tracking-tighter text-slate-900 leading-none">
                  Tùy chỉnh
                </span>
              </div>
              <button className="w-full py-4 text-center text-sm font-bold rounded-xl border border-slate-200 text-slate-700 hover:bg-slate-50 hover:border-slate-300 transition-all mb-10">
                Liên hệ Kinh doanh
              </button>
              <div className="text-[11px] font-bold tracking-widest text-slate-400 uppercase mb-6">
                Bao gồm
              </div>
              <ul className="space-y-5 text-slate-600 font-medium text-[14px]">
                <li className="flex items-start gap-3">
                  <CheckCircle2 className="w-5 h-5 text-slate-300 shrink-0" />{" "}
                  Mọi tính năng của gói Chuyên nghiệp
                </li>
                <li className="flex items-start gap-3">
                  <CheckCircle2 className="w-5 h-5 text-slate-300 shrink-0" />{" "}
                  Quản lý tài khoản chuyên trách
                </li>
                <li className="flex items-start gap-3">
                  <CheckCircle2 className="w-5 h-5 text-slate-300 shrink-0" />{" "}
                  Tích hợp On-premise tùy chỉnh
                </li>
                <li className="flex items-start gap-3">
                  <CheckCircle2 className="w-5 h-5 text-slate-300 shrink-0" />{" "}
                  Đảm bảo SLA (Uptime 99.9%)
                </li>
                <li className="flex items-start gap-3">
                  <CheckCircle2 className="w-5 h-5 text-slate-300 shrink-0" />{" "}
                  Tùy chọn White-label (Gắn thương hiệu riêng)
                </li>
              </ul>
            </div>
          </div>
        </div>
      </section>

      {/* Modern Footer CTA */}
      <footer className="bg-white py-24 border-t border-slate-100">
        <div className="max-w-4xl mx-auto px-6 text-center">
          <h2 className="text-4xl font-black text-slate-900 mb-6 tracking-tight">
            Sẵn sàng Làm chủ Quản lý Hóa đơn?
          </h2>
          <p className="text-lg text-slate-500 mb-10 font-medium">
            Tham gia cùng các đội ngũ kế toán hàng đầu Việt Nam xử lý hàng nghìn
            hóa đơn mỗi ngày với rủi ro bằng không.
          </p>
          <button
            onClick={() => navigate("/register")}
            className="px-8 py-4 text-[15px] font-bold text-white transition-all bg-slate-900 rounded-2xl hover:bg-slate-800 shadow-[0_8px_30px_rgba(15,23,42,0.2)] hover:-translate-y-0.5"
          >
            Bắt đầu Dùng thử Miễn phí Ngay
          </button>
          <p className="text-sm text-slate-400 font-semibold mt-16 pt-8 border-t border-slate-100">
            © 2026 SmartInvoice Shield. Được xây dựng trên nền tảng AWS Cloud.
            Tuân thủ NĐ 123/2020/NĐ-CP.
          </p>
        </div>
      </footer>
    </div>
  );
};

export default LandingPage;
