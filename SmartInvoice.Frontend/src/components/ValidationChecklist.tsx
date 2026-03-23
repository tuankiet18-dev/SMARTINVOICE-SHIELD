import React, { useMemo } from "react";
import { Collapse, Tag, Space, Empty, Typography } from "antd";
import { CheckCircleOutlined, CloseCircleOutlined, WarningOutlined } from "@ant-design/icons";
import type { ValidationResult } from "../services/invoice";

const { Text, Title } = Typography;

interface ExtractedData {
  payment_terms?: string;
  delivery_address?: string;
  seller_name?: string;
  seller_tax_code?: string;
  invoice_date?: string;
  invoice_number?: string;
  invoice_symbol?: string;
  invoice_template_code?: string;
  total_pre_tax?: number;
  total_tax_amount?: number;
  total_amount?: number;
  line_items: Array<{
    stt: number;
    product_name: string;
    unit: string;
    quantity: number;
    unit_price: number;
    total_amount: number;
    vat_rate: number;
    vat_amount: number;
  }>;
}

interface ValidationResultExtended extends Omit<ValidationResult, "extractedData"> {
  extractedData?: ExtractedData;
}

interface ValidationChecklistProps {
  result: ValidationResultExtended;
}

// Interface dùng nội bộ để chuẩn hóa mọi loại lỗi/cảnh báo
interface NormalizedIssue {
  code: string;
  message: string;
  isError: boolean;
  suggestion?: string;
  isMapped: boolean;
}

const ValidationChecklist: React.FC<ValidationChecklistProps> = ({ result }) => {
  if (!result) {
    return <Empty description="Không có kết quả kiểm tra" />;
  }

  // 1. CHUẨN HÓA TOÀN BỘ LỖI VÀ CẢNH BÁO TỪ BACKEND (Tránh trùng lặp)
  const allIssues = useMemo(() => {
    const issues: NormalizedIssue[] = [];
    const messageSet = new Set<string>(); // Để track và tránh duplicate messages

    // Lấy từ errorDetails (có mã lỗi rõ ràng)
    result.errorDetails?.forEach((e) => {
      if (e.errorMessage && !messageSet.has(e.errorMessage)) {
        messageSet.add(e.errorMessage);
        issues.push({ code: e.errorCode || "UNKNOWN_ERR", message: e.errorMessage, isError: true, suggestion: e.suggestion, isMapped: false });
      }
    });

    // Lấy từ warningDetails
    result.warningDetails?.forEach((w) => {
      if (w.errorMessage && !messageSet.has(w.errorMessage)) {
        messageSet.add(w.errorMessage);
        issues.push({ code: w.errorCode || "UNKNOWN_WARN", message: w.errorMessage, isError: false, suggestion: w.suggestion, isMapped: false });
      }
    });

    // Chỉ lấy từ mảng errors cũ nếu không có errorDetails (legacy format)
    if (!result.errorDetails || result.errorDetails.length === 0) {
      result.errors?.forEach((e, i) => {
        if (e && !messageSet.has(e)) {
          messageSet.add(e);
          issues.push({ code: `LEGACY_ERR_${i}`, message: e, isError: true, isMapped: false });
        }
      });
    }

    // Chỉ lấy từ mảng warnings cũ nếu không có warningDetails (legacy format)
    if (!result.warningDetails || result.warningDetails.length === 0) {
      result.warnings?.forEach((w, i) => {
        if (w && !messageSet.has(w)) {
          messageSet.add(w);
          issues.push({ code: `LEGACY_WARN_${i}`, message: w, isError: false, isMapped: false });
        }
      });
    }

    return issues;
  }, [result]);

  // 2. ĐỊNH NGHĨA CÁC LỚP KIỂM TRA (GIỮ NGUYÊN NHƯ CŨ)
  const validationLayers = [
    {
      key: "xml-structure",
      title: "Kiểm tra Cấu trúc XML",
      icon: "🗂️",
      checks: [
        { name: "Xác thực schema XSD", errorCode: "ERR_XML_STRUCT", description: "Kiểm tra tính hợp lệ của cấu trúc XML" },
        { name: "Kiểm tra DTD", errorCode: "ERR_XML_SYS", description: "Đảm bảo tệp XML không chứa DTD độc hại" },
        { name: "Kiểm tra trường bắt buộc", errorCode: "ERR_XML_MISSING_FIELD", description: "Xác minh tất cả các trường bắt buộc" },
        { name: "Kiểm tra định dạng dữ liệu", errorCode: "ERR_DATA_NOT_NUMBER", description: "Kiểm tra định dạng các trường số" },
      ],
    },
    {
      key: "digital-signature",
      title: "Kiểm tra Chữ ký số",
      icon: "🔐",
      checks: [
        { name: "Kiểm tra chữ ký có tồn tại", errorCode: "ERR_SIG_MISSING", description: "Xác nhận hóa đơn có chứa chữ ký số" },
        { name: "Kiểm tra chứng chỉ hợp lệ", errorCode: "ERR_SIG_INVALID", description: "Xác minh chứng chỉ số là hợp lệ" },
        { name: "Kiểm tra chứng chỉ chưa hết hạn", errorCode: "ERR_SIG_EXPIRED", description: "Đảm bảo chứng chỉ số chưa hết hạn" },
        { name: "Kiểm tra tính toàn vẹn chữ ký", errorCode: "ERR_SIG_SYS", description: "Xác nhận chữ ký số chưa bị giả mạo" },
      ],
    },
    {
      key: "business-logic",
      title: "Kiểm tra Logic Kinh doanh",
      icon: "💼",
      checks: [
        { name: "Kiểm tra phiên bản hóa đơn", errorCode: "ERR_LOGIC_VERSION" },
        { name: "Kiểm tra ký hiệu hóa đơn", errorCode: "ERR_LOGIC_INV_SYMBOL" },
        { name: "Kiểm tra số hóa đơn", errorCode: "ERR_LOGIC_INV_NUM" },
        { name: "Kiểm tra loại hóa đơn", errorCode: "ERR_LOGIC_INV_TYPE" },
        { name: "Kiểm tra tiền tệ", errorCode: "ERR_LOGIC_CURRENCY" },
        { name: "Kiểm tra tỷ giá hối đoái", errorCode: "ERR_LOGIC_EX_RATE" },
        { name: "Kiểm tra mã số thuế đúng định dạng", errorCode: "ERR_LOGIC_TAX_FORMAT" },
        { name: "Kiểm tra thuế suất hợp lệ", errorCode: "ERR_LOGIC_TAX_RATE" },
        { name: "Kiểm tra tính toán tổng tiền", errorCode: "ERR_LOGIC_TOTAL_MISMATCH" },
        { name: "Kiểm tra tính toán doanh số", errorCode: "ERR_LOGIC_SALES_TOTAL_MISMATCH" },
        { name: "Kiểm tra hóa đơn có chi tiết", errorCode: "ERR_LOGIC_NO_ITEMS" },
        { name: "Kiểm tra trường tính chất công việc", errorCode: "ERR_LOGIC_NO_PROPERTY" },
        { name: "Kiểm tra loại chứng từ", errorCode: "ERR_LOGIC_REL" },
        { name: "Kiểm tra hóa đơn bị trùng lặp", errorCode: "ERR_LOGIC_DUP" },
        { name: "Kiểm tra hóa đơn bị từ chối", errorCode: "ERR_LOGIC_DUP_REJECTED" },
        { name: "Kiểm tra danh sách đen công ty", errorCode: "ERR_LOGIC_BLACKLIST" },
        { name: "Kiểm tra người ký phù hợp", errorCode: "ERR_LOGIC_SIGNER_MISMATCH" },
        { name: "Kiểm tra mã MCCQT", errorCode: "ERR_LOGIC_MCCQT" },
        { name: "Kiểm tra quyền sở hữu", errorCode: "ERR_LOGIC_OWNER" },
        { name: "Kiểm tra thông tin người mua", errorCode: "WARN_LOGIC_NO_BUYER_TAX" },
        { name: "Kiểm tra dữ liệu bắt buộc (OCR)", errorCode: "ERR_LOGIC_MISSING_FIELD" },
        { name: "Kiểm tra sai lệch tính toán dòng hàng (OCR)", errorCode: "WARN_LOGIC_CALC_DEV_OCR" },
        { name: "Kiểm tra thuế suất hợp lệ (OCR)", errorCode: "WARN_LOGIC_TAX_MISMATCH_OCR" },
      ],
    },
  ];

  // 3. XÂY DỰNG GIAO DIỆN COLLAPSE CHO CÁC LỚP CỐ ĐỊNH
  const collapseItems = validationLayers.map((layer) => {
    const failedChecks: any[] = [];
    const passedChecks: any[] = [];

    layer.checks.forEach((check) => {
      // Tìm xem có issue nào khớp mã không
      const matchedIssue = allIssues.find((issue) => issue.code === check.errorCode);
      
      if (matchedIssue) {
        matchedIssue.isMapped = true; // Đánh dấu là đã được xử lý hiển thị
        failedChecks.push({ ...check, issue: matchedIssue });
      } else {
        passedChecks.push(check);
      }
    });

    return {
      key: layer.key,
      label: (
        <div style={{ display: "flex", alignItems: "center", gap: 12, width: "100%" }}>
          <span style={{ fontSize: 16 }}>{layer.icon}</span>
          <Text strong style={{ fontSize: 14 }}>{layer.title}</Text>
          <Space size="small" style={{ marginLeft: "auto" }}>
            {passedChecks.length > 0 && (
              <Tag icon={<CheckCircleOutlined />} color="success" style={{ marginRight: 0 }}>
                {passedChecks.length} ✓
              </Tag>
            )}
            {failedChecks.length > 0 && (
              <Tag icon={<CloseCircleOutlined />} color="error" style={{ marginRight: 0 }}>
                {failedChecks.length} ✕
              </Tag>
            )}
          </Space>
        </div>
      ),
      children: (
        <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
          {/* HIỂN THỊ CÁC TIÊU CHÍ BỊ LỖI / CẢNH BÁO (TO RÕ RÀNG) */}
          {failedChecks.map((check) => {
            const { issue } = check;
            return (
              <div
                key={check.errorCode}
                style={{
                  padding: "12px",
                  borderRadius: "6px",
                  background: issue.isError ? "#fff2f0" : "#fffbe6",
                  border: issue.isError ? "1px solid #ffccc7" : "1px solid #ffe58f",
                }}
              >
                <div style={{ display: "flex", alignItems: "flex-start", gap: 12 }}>
                  <div style={{ marginTop: "2px", fontSize: "16px" }}>
                    {issue.isError ? <CloseCircleOutlined style={{ color: "#ff4d4f" }} /> : <WarningOutlined style={{ color: "#faad14" }} />}
                  </div>
                  <div style={{ flex: 1 }}>
                    <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: "4px" }}>
                      <Text strong>{check.name}</Text>
                      <Tag color={issue.isError ? "red" : "orange"}>{check.errorCode}</Tag>
                    </div>
                    <div style={{ marginTop: "4px" }}>
                      <Text style={{ color: issue.isError ? "#ff4d4f" : "#d48806", fontSize: "13px", display: "block", marginBottom: "4px" }}>
                        <strong>Chi tiết:</strong> {issue.message}
                      </Text>
                      {issue.suggestion && (
                        <Text type="secondary" style={{ fontSize: "12px" }}>
                          <strong>💡 Gợi ý:</strong> {issue.suggestion}
                        </Text>
                      )}
                    </div>
                  </div>
                </div>
              </div>
            );
          })}

          {/* HIỂN THỊ CÁC TIÊU CHÍ ĐÃ PASS (DƯỚI DẠNG TAG NHỎ ĐỂ TIẾT KIỆM DIỆN TÍCH) */}
          {passedChecks.length > 0 && (
            <div style={{ marginTop: failedChecks.length > 0 ? 8 : 0, padding: "8px", background: "#f6ffed", borderRadius: "6px", border: "1px dashed #b7eb8f" }}>
              <Text type="secondary" style={{ fontSize: 13, display: "block", marginBottom: 8 }}>
                Đã vượt qua ({passedChecks.length} tiêu chí):
              </Text>
              <div style={{ display: "flex", flexWrap: "wrap", gap: "6px" }}>
                {passedChecks.map((check) => (
                  <Tag key={check.errorCode} variant="filled" color="success" style={{ margin: 0 }}>
                    {check.name}
                  </Tag>
                ))}
              </div>
            </div>
          )}
        </div>
      ),
    };
  });

  // 4. KIỂM TRA XEM CÒN LỖI/CẢNH BÁO NÀO CHƯA ĐƯỢC MAPPING KHÔNG
  const unmappedIssues = allIssues.filter((issue) => !issue.isMapped);
  
  if (unmappedIssues.length > 0) {
    collapseItems.push({
      key: "other-issues",
      label: (
        <div style={{ display: "flex", alignItems: "center", gap: 12, width: "100%" }}>
          <span style={{ fontSize: 16 }}>⚠️</span>
          <Text strong style={{ fontSize: 14 }}>Cảnh báo / Lỗi khác (Hệ thống)</Text>
          <Space style={{ marginLeft: "auto" }}>
            <Tag color="warning" style={{ marginRight: 0 }}>{unmappedIssues.length} vấn đề</Tag>
          </Space>
        </div>
      ),
      children: (
        <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
          {unmappedIssues.map((issue, index) => (
            <div
              key={index}
              style={{
                padding: "12px",
                borderRadius: "6px",
                background: issue.isError ? "#fff2f0" : "#fffbe6",
                border: issue.isError ? "1px solid #ffccc7" : "1px solid #ffe58f",
              }}
            >
              <div style={{ display: "flex", alignItems: "flex-start", gap: 12 }}>
                <div style={{ marginTop: "2px", fontSize: "16px" }}>
                  {issue.isError ? <CloseCircleOutlined style={{ color: "#ff4d4f" }} /> : <WarningOutlined style={{ color: "#faad14" }} />}
                </div>
                <div style={{ flex: 1 }}>
                  <Text style={{ color: issue.isError ? "#ff4d4f" : "#d48806", fontSize: "13px" }}>
                    {issue.message}
                  </Text>
                  {issue.suggestion && (
                    <Text type="secondary" style={{ fontSize: "12px", display: "block", marginTop: 4 }}>
                      <strong>💡 Gợi ý:</strong> {issue.suggestion}
                    </Text>
                  )}
                </div>
              </div>
            </div>
          ))}
        </div>
      ),
    });
  }

  // Nếu có lỗi "khác", mặc định mở luôn tab đó ra cho User thấy
  const activeKeys = ["xml-structure", "digital-signature", "business-logic"];
  if (unmappedIssues.length > 0) activeKeys.push("other-issues");

  return (
    <div style={{ padding: "12px 0" }}>
      <Collapse items={collapseItems} defaultActiveKey={activeKeys} />
    </div>
  );
};

export default ValidationChecklist;