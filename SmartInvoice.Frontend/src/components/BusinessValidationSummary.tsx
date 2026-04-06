import React from "react";
import { Card, Row, Col, Typography, Space, Divider } from "antd";
import {
  CheckCircleFilled,
  WarningFilled,
  CloseCircleFilled,
} from "@ant-design/icons";

const { Text, Title } = Typography;

interface BusinessValidationSummaryProps {
  result: any;
  processingMethod?: string;
}

const BusinessValidationSummary: React.FC<BusinessValidationSummaryProps> = ({
  result,
  processingMethod,
}) => {
  const extracted = result?.extractedData || {};
  // BE có thể trả lỗi trong errorDetails (object) hoặc errors (mảng string)
  const errorDetails = result?.errorDetails || [];
  const warningDetails = result?.warningDetails || [];
  const fallbackErrors = result?.errors || [];

  // Hàm quét và lấy toàn bộ object lỗi (Fix Lỗi 1 & Lỗi 2)
  const findErrorItem = (codes: string[]) => {
    // 1. Quét trong mảng errorDetails chuẩn
    const foundObj = errorDetails.find((e: any) =>
      codes.includes(e?.errorCode),
    );
    if (foundObj) return { ...foundObj, isWarning: false };

    // 2. Fallback: Nếu backend trả về mảng string thuần (lỗi Duplicate thường rơi vào đây)
    const foundStr = fallbackErrors.find((errStr: string) =>
      codes.some(
        (code) =>
          errStr.includes(code) ||
          // Bắt theo keyword tiếng Việt cho chắc chắn nếu BE không trả mã Code
          (code === "ERR_LOGIC_DUPLICATE" &&
            errStr.toLowerCase().includes("đã tồn tại")),
      ),
    );
    if (foundStr) return { errorMessage: foundStr, isWarning: false };

    return null;
  };

  const findWarningItem = (codes: string[]) => {
    const foundObj = warningDetails.find((w: any) =>
      codes.includes(w?.errorCode),
    );
    if (foundObj) return { ...foundObj, isWarning: true };
    return null;
  };

  const rules = [
    {
      label: "Chuẩn cấu trúc file hóa đơn (XSD)",
      errorCodes: [
        "ERR_XML_STRUCT",
        "ERR_XML_MISSING_FIELD",
        "ERR_DATA_NOT_NUMBER",
        "ERR_XML_SYS"
      ],
      warningCodes: [],
    },
    {
      label: "Hóa đơn không bị trùng lặp",
      errorCodes: [
        "ERR_LOGIC_DUP",
        "ERR_LOGIC_DUP_REJECTED",
        "ERR_LOGIC_DUPLICATE",
        "ERR_LOGIC_DUPLICATE_REJECTED",
        "LogicDuplicate",
      ],
      warningCodes: [],
    },
    {
      label: "Đúng thông tin người mua, người bán",
      errorCodes: [
        "ERR_LOGIC_OWNER",
        "ERR_LOGIC_TAX_FORMAT",
        "ERR_LOGIC_MISSING_FIELD",
        "LogicOwner",
      ],
      warningCodes: ["WARN_LOGIC_NO_BUYER_TAX"],
    },
    {
      label: "Chữ ký điện tử / Chữ ký số hợp lệ",
      errorCodes: [
        "ERR_SIG_MISSING",
        "ERR_SIG_EXPIRED",
        "ERR_SIG_INVALID",
        "SigMissing",
        "SigExpired",
        "SigInvalid",
      ],
      warningCodes: ["WARN_MISSING_XML_EVIDENCE"],
    },
    {
      label: "Ngày ký và ngày lập hợp lý",
      errorCodes: [],
      warningCodes: ["WARN_LOGIC_DATE_DISC", "WARN_LOGIC_DATE_FUTURE"],
    },
    {
      label: "Trạng thái hoạt động của doanh nghiệp",
      errorCodes: ["ERR_LOGIC_BLACKLIST", "LogicBlacklist"],
      warningCodes: [],
    },
    {
      label: "Số tiền, thuế suất tính toán khớp nhau",
      errorCodes: [
        "ERR_LOGIC_TOTAL_MISMATCH",
        "ERR_LOGIC_SALES_TOTAL_MISMATCH",
        "ERR_LOGIC_TAX_RATE",
        "ERR_OCR_INTERNAL_VALIDATION",
        "LogicTotalMismatch",
      ],
      warningCodes: [
        "WARN_LOGIC_CALC_DEV",
        "WARN_LOGIC_TAX_MISMATCH",
        "WARN_LOGIC_CALC_DEV_OCR",
        "WARN_LOGIC_TAX_MISMATCH_OCR",
      ],
    },
  ];

  // Lọc ra các rules phù hợp theo luồng xử lý
  const activeRules = rules.filter((rule) => {
    if (processingMethod === "OCR" && rule.label.includes("XSD")) {
      return false; // Bỏ qua rule XSD nếu là luồng OCR
    }
    return true;
  });

  // Tính toán trạng thái cho từng tiêu chí
  const criteria = activeRules.map((rule) => {
    const errorItem = findErrorItem(rule.errorCodes);
    const warningItem = findWarningItem(rule.warningCodes);

    const activeItem = errorItem || warningItem;
    let status = "pass";
    if (errorItem) status = "error";
    else if (warningItem) status = "warning";

    return {
      label: rule.label,
      status,
      detail: activeItem, // Truyền object lỗi xuống để render
    };
  });

  const renderIcon = (status: string) => {
    switch (status) {
      case "error":
        return (
          <CloseCircleFilled
            style={{ color: "#ff4d4f", fontSize: 18, marginTop: 2 }}
          />
        );
      case "warning":
        return (
          <WarningFilled
            style={{ color: "#faad14", fontSize: 18, marginTop: 2 }}
          />
        );
      default:
        return (
          <CheckCircleFilled
            style={{ color: "#52c41a", fontSize: 18, marginTop: 2 }}
          />
        );
    }
  };

  const formatVND = (value: number | undefined) => {
    if (value === undefined || value === null) return "0 ₫";
    return new Intl.NumberFormat("vi-VN", {
      style: "currency",
      currency: "VND",
    }).format(value);
  };

  // ─── Computed values (fallback for Mẫu 2 invoices without explicit totals) ───
  const getLineItemTotal = (item: any) =>
    item.totalAmount || item.total_amount || ((item.quantity || 0) * (item.unitPrice || item.unit_price || 0)) || 0;

  const lineItems = extracted.lineItems || extracted.line_items || [];
  
  const totalAmount = extracted.totalAmount ?? extracted.total_amount;

  const totalPreTax = extracted.totalPreTax ?? extracted.total_pre_tax ?? 
    (lineItems.length > 0 ? lineItems.reduce((sum: number, item: any) => sum + getLineItemTotal(item), 0) : totalAmount);

  const totalTaxAmount = extracted.totalTaxAmount ?? extracted.total_tax_amount ?? 
    (totalAmount ? totalAmount - (totalPreTax || 0) : 0);

  return (
    <Card variant="borderless" style={{ background: "#fafafa", margin: "16px 0" }}>
      <Row gutter={[24, 24]}>
        {/* CỘT TRÁI: THÔNG TIN THANH TOÁN */}
        <Col xs={24} lg={10}>
          <Title level={5}>Thông tin thanh toán</Title>
          <div
            style={{
              background: "#fff",
              padding: 16,
              borderRadius: 8,
              border: "1px solid #f0f0f0",
            }}
          >
            <Row justify="space-between" style={{ marginBottom: 8 }}>
              <Col>
                <Text type="secondary">Tổng tiền chưa thuế:</Text>
              </Col>
              <Col>
                <Text strong>{formatVND(totalPreTax)}</Text>
              </Col>
            </Row>
            <Row justify="space-between" style={{ marginBottom: 8 }}>
              <Col>
                <Text type="secondary">Tổng tiền thuế:</Text>
              </Col>
              <Col>
                <Text strong>{formatVND(totalTaxAmount)}</Text>
              </Col>
            </Row>
            <Divider style={{ margin: "8px 0" }} />
            <Row justify="space-between">
              <Col>
                <Text strong style={{ fontSize: 16 }}>
                  Tổng tiền thanh toán:
                </Text>
              </Col>
              <Col>
                <Text strong style={{ fontSize: 16, color: "#1677ff" }}>
                  {formatVND(totalAmount)}
                </Text>
              </Col>
            </Row>
          </div>
        </Col>

        {/* CỘT PHẢI: KẾT QUẢ KIỂM TRA */}
        <Col xs={24} lg={14}>
          <Title level={5}>Kết quả kiểm tra hóa đơn</Title>
          <div
            style={{
              background: "#fff",
              padding: 16,
              borderRadius: 8,
              border: "1px solid #f0f0f0",
            }}
          >
            <Space orientation="vertical" size="large" style={{ width: "100%" }}>
              {criteria.map((c, index) => (
                <Row key={index} justify="start" align="top" wrap={false}>
                  <Col style={{ marginRight: 12, display: "flex" }}>
                    {renderIcon(c.status)}
                  </Col>
                  <Col flex="auto">
                    <Text
                      strong={c.status !== "pass"}
                      style={{
                        fontSize: 15,
                        display: "block",
                        color:
                          c.status === "error"
                            ? "#ff4d4f"
                            : c.status === "warning"
                              ? "#d48806"
                              : "#237804",
                      }}
                    >
                      {c.label}
                    </Text>

                    {/* HIỂN THỊ CHI TIẾT LỖI TẠI ĐÂY */}
                    {c.detail && (
                      <div style={{ marginTop: 4 }}>
                        <Text
                          style={{
                            color: "#595959",
                            fontSize: 14,
                            display: "block",
                          }}
                        >
                          {c.detail.errorMessage}
                        </Text>
                        {c.detail.suggestion && (
                          <Text
                            type="secondary"
                            style={{
                              fontStyle: "italic",
                              fontSize: 13,
                              display: "block",
                              marginTop: 4,
                            }}
                          >
                            Gợi ý: {c.detail.suggestion}
                          </Text>
                        )}
                      </div>
                    )}
                  </Col>
                </Row>
              ))}
            </Space>
          </div>
        </Col>
      </Row>
    </Card>
  );
};

export default BusinessValidationSummary;
