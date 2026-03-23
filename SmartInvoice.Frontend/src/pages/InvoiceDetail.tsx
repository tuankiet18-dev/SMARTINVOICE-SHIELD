import React, { useState } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import dayjs from "dayjs";
import {
  Card,
  Typography,
  Button,
  Space,
  Tag,
  Descriptions,
  Table,
  Timeline,
  Tabs,
  Spin,
  Result,
  Modal,
  Input,
  Tooltip,
  Divider,
  Row,
  Col,
  message,
  Alert,
  Dropdown,
} from "antd";
import {
  ArrowLeftOutlined,
  SendOutlined,
  CheckCircleOutlined,
  CloseCircleOutlined,
  FileTextOutlined,
  SafetyCertificateOutlined,
  AuditOutlined,
  ShoppingCartOutlined,
  ExclamationCircleOutlined,
  WarningOutlined,
  InfoCircleOutlined,
  UserOutlined,
  CalendarOutlined,
  DollarOutlined,
  BankOutlined,
  CloudUploadOutlined,
  LinkOutlined,
  DownOutlined,
} from "@ant-design/icons";
import StatusBadge from "../components/ui/StatusBadge";
import BusinessValidationSummary from "../components/BusinessValidationSummary";
import { useAutoRefreshValidation } from "../hooks/useAutoRefreshValidation";
import { useAuth } from "../contexts/AuthContext";
import {
  invoiceService,
  type InvoiceDetailDto,
  type LineItemDto,
  type ValidationLayerDto,
} from "../services/invoice";

const { Title, Text, Paragraph } = Typography;
const { TextArea } = Input;

// ════════════════════════════════════════════
//  Helper components
// ════════════════════════════════════════════

const InfoItem: React.FC<{
  label: string;
  value: React.ReactNode;
  span?: number;
}> = ({ label, value }) => (
  <div style={{ marginBottom: 12 }}>
    <Text
      type="secondary"
      style={{ fontSize: 12, display: "block", marginBottom: 2 }}
    >
      {label}
    </Text>
    <Text strong style={{ fontSize: 14 }}>
      {value || "—"}
    </Text>
  </div>
);

const formatCurrency = (
  amount: number | null | undefined,
  currency = "VND",
) => {
  if (amount == null) return "—";
  return `${amount.toLocaleString("vi-VN")} ${currency === "VND" ? "₫" : currency}`;
};

// Removed validationStatusIcon

const actionColorMap: Record<string, string> = {
  UPLOAD: "#1677ff",
  UPLOAD_OCR: "#1677ff",
  EDIT: "#722ed1",
  SUBMIT: "#13c2c2",
  APPROVE: "#52c41a",
  REJECT: "#ff4d4f",
  OVERRIDE: "#fa8c16",
  MERGE_XML_OVERRIDE: "#13c2c2",
  ATTACH_VISUAL_FILE: "#722ed1",
};

const actionLabelMap: Record<string, string> = {
  UPLOAD: "Tải lên (XML)",
  UPLOAD_OCR: "Tải lên (OCR)",
  EDIT: "Chỉnh sửa",
  SUBMIT: "Gửi duyệt",
  APPROVE: "Phê duyệt",
  REJECT: "Từ chối",
  OVERRIDE: "Ghi đè rủi ro",
  MERGE_XML_OVERRIDE: "Ghi đè XML → OCR",
  ATTACH_VISUAL_FILE: "Đính kèm bản thể hiện",
};

const statusColorConfig: Record<string, { bg: string; text: string }> = {
  Draft: { bg: "#E2E8F014", text: "#8c8c8c" },
  Pending: { bg: "#1677ff14", text: "#1677ff" },
  Approved: { bg: "#52c41a14", text: "#52c41a" },
  Rejected: { bg: "#ff4d4f14", text: "#ff4d4f" },
};

const riskColorConfig: Record<string, { bg: string; text: string }> = {
  Green: { bg: "#52c41a14", text: "#52c41a" },
  Yellow: { bg: "#faad1414", text: "#faad14" },
  Red: { bg: "#ff4d4f14", text: "#ff4d4f" },
};

const ChangeValueBadge: React.FC<{ field: string; value: string }> = ({
  field,
  value,
}) => {
  const f = field.toLowerCase();
  let config: { bg: string; text: string } | undefined;
  let label = value;
  if (f === "status") {
    config = statusColorConfig[value];
  } else if (f.includes("risk") || f === "risklevel") {
    config = riskColorConfig[value];
  }
  if (config) {
    return (
      <span
        style={{
          display: "inline-block",
          padding: "1px 8px",
          borderRadius: 10,
          fontSize: 12,
          fontWeight: 600,
          background: config.bg,
          color: config.text,
        }}
      >
        {label}
      </span>
    );
  }
  return <span style={{ color: "#52c41a", fontWeight: 500 }}>{value}</span>;
};

// ════════════════════════════════════════════
//  Main Component
// ════════════════════════════════════════════

const InvoiceDetail: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { user } = useAuth();
  const isAdmin = user?.role === "CompanyAdmin" || user?.role === "SuperAdmin";

  const [rejectModalOpen, setRejectModalOpen] = useState(false);
  const [rejectReason, setRejectReason] = useState("");
  const [rejectComment, setRejectComment] = useState("");
  const [submitModalOpen, setSubmitModalOpen] = useState(false);
  const [submitComment, setSubmitComment] = useState("");

  // ─── Data Fetching ───
  const {
    data: invoice,
    isLoading,
    isError,
    error,
  } = useQuery({
    queryKey: ["invoice-detail", id],
    queryFn: () => invoiceService.getInvoiceDetail(id!),
    enabled: !!id,
  });

  const {
    data: versions,
    isLoading: isVersionsLoading
  } = useQuery({
    queryKey: ["invoice-versions", id],
    queryFn: () => invoiceService.getInvoiceVersions(id!),
    enabled: !!id,
  });

  // ─── Auto-refresh validation while pending ───
  // NOTE: backend stores validation results in `validationLayers` on the invoice DTO.
  // Use that field to determine whether validation is still pending.
  const isValidationPending =
    invoice?.status === "Draft" &&
    (!invoice?.validationLayers || invoice.validationLayers.length === 0);

  const { isRefreshing, lastRefreshTime } = useAutoRefreshValidation(
    id || "",
    isValidationPending,
    3000,
  );

  // Show a toast when validation completes (transition from pending -> finished)
  // Avoid notifying on initial load: only notify when count increases after mount
  const prevValidationCountRef = React.useRef<number | null>(null);
  React.useEffect(() => {
    if (!invoice) return;
    const currentCount = invoice.validationLayers ? invoice.validationLayers.length : 0;
    const prev = prevValidationCountRef.current;
    if (prev === null) {
      // First render: initialize previous count and do NOT notify
      prevValidationCountRef.current = currentCount;
      return;
    }
    // Subsequent updates: if previously 0 and now >0, show toast
    if (prev === 0 && currentCount > 0) {
      message.success('Kiểm tra VietQR hoàn tất — kết quả đã được cập nhật.');
      // Invalidate invoices list and dashboard stats so list/dashboard reflect new validation
      try {
        queryClient.invalidateQueries({ queryKey: ['invoices'] });
        queryClient.invalidateQueries({ queryKey: ['dashboard-stats'] });
      } catch (err) {
        // swallow; not critical for UX
      }
    }
    prevValidationCountRef.current = currentCount;
  }, [invoice]);

  // ─── Mutations ───
  const submitMutation = useMutation({
    mutationFn: (comment?: string) =>
      invoiceService.submitInvoice(id!, comment),
    onSuccess: () => {
      message.success("Hóa đơn đã được gửi duyệt!");
      setSubmitModalOpen(false);
      setSubmitComment("");
      queryClient.invalidateQueries({ queryKey: ["invoice-detail", id] });
      queryClient.invalidateQueries({ queryKey: ["invoices"] });
    },
    onError: (err: any) =>
      message.error(err?.response?.data?.message || "Có lỗi xảy ra"),
  });

  const approveMutation = useMutation({
    mutationFn: () => invoiceService.approveInvoice(id!),
    onSuccess: () => {
      message.success("Hóa đơn đã được phê duyệt!");
      queryClient.invalidateQueries({ queryKey: ["invoice-detail", id] });
      queryClient.invalidateQueries({ queryKey: ["invoices"] });
    },
    onError: (err: any) =>
      message.error(err?.response?.data?.message || "Có lỗi xảy ra"),
  });

  const rejectMutation = useMutation({
    mutationFn: () =>
      invoiceService.rejectInvoice(id!, rejectReason, rejectComment),
    onSuccess: () => {
      message.success("Hóa đơn đã bị từ chối.");
      setRejectModalOpen(false);
      setRejectReason("");
      setRejectComment("");
      queryClient.invalidateQueries({ queryKey: ["invoice-detail", id] });
      queryClient.invalidateQueries({ queryKey: ["invoices"] });
    },
    onError: (err: any) =>
      message.error(err?.response?.data?.message || "Có lỗi xảy ra"),
  });

  // ─── Loading / Error states ───
  if (isLoading)
    return (
      <div style={{ textAlign: "center", padding: 80 }}>
        <Spin size="large" />
      </div>
    );
  if (isError || !invoice) {
    return (
      <Result
        status="404"
        title="Không tìm thấy hóa đơn"
        subTitle={
          (error as any)?.response?.data?.message ||
          "Hóa đơn không tồn tại hoặc bạn không có quyền xem."
        }
        extra={
          <Button type="primary" onClick={() => navigate("/app/invoices")}>
            Quay lại danh sách
          </Button>
        }
      />
    );
  }

  // ─── Workflow buttons ───
  const renderWorkflowActions = () => {
    const actions: React.ReactNode[] = [];
    const isYellow = invoice.riskLevel === "Yellow";

    if (invoice.status === "Draft") {
      actions.push(
        <Button
          key="submit"
          type="primary"
          icon={<SendOutlined />}
          onClick={() => {
            if (isYellow) {
              // Yellow: show dedicated modal requiring giải trình
              setSubmitComment("");
              setSubmitModalOpen(true);
            } else {
              // Green: simple confirm, no comment needed
              Modal.confirm({
                title: "Gửi duyệt hóa đơn?",
                content:
                  'Hóa đơn sẽ chuyển sang trạng thái "Chờ duyệt" và chờ Admin phê duyệt.',
                okText: "Gửi duyệt",
                cancelText: "Hủy",
                onOk: () => submitMutation.mutate(undefined),
              });
            }
          }}
          loading={submitMutation.isPending}
          style={{
            borderRadius: 10,
            fontWeight: 600,
            height: 40,
            background: "linear-gradient(135deg, #4880FF, #6C5CE7)",
            border: "none",
            boxShadow: "0 2px 8px rgba(72,128,255,0.35)",
          }}
        >
          {isYellow ? "⚠️ Gửi duyệt + Giải trình" : "Gửi duyệt"}
        </Button>,
      );
    }

    if (invoice.status === "Pending" && isAdmin) {
      actions.push(
        <Button
          key="approve"
          type="primary"
          icon={<CheckCircleOutlined />}
          onClick={() => {
            Modal.confirm({
              title: "Phê duyệt hóa đơn?",
              content: 'Hóa đơn sẽ được chuyển sang trạng thái "Đã duyệt".',
              okText: "Phê duyệt",
              cancelText: "Hủy",
              onOk: () => approveMutation.mutate(),
            });
          }}
          loading={approveMutation.isPending}
          style={{
            borderRadius: 10,
            fontWeight: 600,
            height: 40,
            background: "#52c41a",
            borderColor: "#52c41a",
          }}
        >
          Phê duyệt
        </Button>,
        <Button
          key="reject"
          danger
          icon={<CloseCircleOutlined />}
          onClick={() => setRejectModalOpen(true)}
          style={{ borderRadius: 10, fontWeight: 600, height: 40 }}
        >
          Từ chối
        </Button>,
      );
    }

    return actions.length > 0 ? <Space>{actions}</Space> : null;
  };

  // ─── Computed values (fallback for Mẫu 2 invoices without explicit totals) ───
  const getLineItemTotal = (item: LineItemDto) =>
    item.totalAmount || item.quantity * item.unitPrice || 0;
  const computedTotalBeforeTax =
    invoice.totalAmountBeforeTax ||
    invoice.lineItems.reduce((sum, item) => sum + getLineItemTotal(item), 0) ||
    invoice.totalAmount;
  const computedTotalTax =
    invoice.totalTaxAmount ||
    invoice.totalAmount - (computedTotalBeforeTax || 0);

  // ─── Line Items Table ───
  const lineItemColumns = [
    {
      title: "STT",
      dataIndex: "lineNumber",
      key: "lineNumber",
      width: 60,
      align: "center" as const,
    },
    {
      title: "Tên hàng hóa / dịch vụ",
      dataIndex: "itemName",
      key: "itemName",
      ellipsis: true,
    },
    {
      title: "ĐVT",
      dataIndex: "unit",
      key: "unit",
      width: 80,
      align: "center" as const,
    },
    {
      title: "Số lượng",
      dataIndex: "quantity",
      key: "quantity",
      width: 100,
      align: "right" as const,
      render: (v: number) => v?.toLocaleString("vi-VN"),
    },
    {
      title: "Đơn giá",
      dataIndex: "unitPrice",
      key: "unitPrice",
      width: 130,
      align: "right" as const,
      render: (v: number) => formatCurrency(v, invoice.invoiceCurrency),
    },
    {
      title: "Thành tiền",
      dataIndex: "totalAmount",
      key: "totalAmount",
      width: 140,
      align: "right" as const,
      render: (_v: number, record: LineItemDto) => (
        <Text strong>
          {formatCurrency(getLineItemTotal(record), invoice.invoiceCurrency)}
        </Text>
      ),
    },
    {
      title: "Thuế suất",
      dataIndex: "vatRate",
      key: "vatRate",
      width: 90,
      align: "center" as const,
      render: (v: number) => `${v}%`,
    },
    {
      title: "Tiền thuế",
      dataIndex: "vatAmount",
      key: "vatAmount",
      width: 130,
      align: "right" as const,
      render: (v: number, record: LineItemDto) => {
        const lineTotal = getLineItemTotal(record);
        const computed =
          v ||
          (lineTotal && record.vatRate
            ? Math.round((lineTotal * record.vatRate) / 100)
            : 0);
        return formatCurrency(computed, invoice.invoiceCurrency);
      },
    },
  ];

  // ─── Map Data for BusinessValidationSummary ───
  // DB stores PascalCase (ErrorCode, ErrorMessage, Suggestion) but
  // BusinessValidationSummary expects camelCase (errorCode, errorMessage, suggestion).
  const normalizeItem = (item: any) => ({
    errorCode: item.ErrorCode || item.errorCode || null,
    errorMessage: item.ErrorMessage || item.errorMessage || null,
    suggestion: item.Suggestion || item.suggestion || null,
  });

  let allErrors: any[] = [];
  let allWarnings: any[] = [];
  let fallbackErrors: string[] = [];

  // Parse from RiskChecks
  invoice.riskChecks?.forEach((check: any) => {
    try {
      if (check.checkDetails) {
        const parsed = JSON.parse(check.checkDetails);
        const errs = parsed.ErrorDetails || parsed.Errors || [];
        const warns = parsed.WarningDetails || parsed.Warnings || [];
        allErrors = [...allErrors, ...errs.map(normalizeItem)];
        allWarnings = [...allWarnings, ...warns.map(normalizeItem)];
      }
    } catch (e) {
      console.error("Failed to parse checkDetails", e);
    }

    if (check.errorMessage && check.checkStatus === "ERROR") {
      fallbackErrors.push(check.errorMessage);
    }
  });

  // Parse from ValidationLayers
  invoice.validationLayers?.forEach((layer: any) => {
    try {
      if (layer.errorDetails) {
        const parsed = JSON.parse(layer.errorDetails);
        if (Array.isArray(parsed)) {
          const normalized = parsed.map(normalizeItem);
          if (layer.validationStatus === "Warning") {
             allWarnings = [...allWarnings, ...normalized];
          } else {
             allErrors = [...allErrors, ...normalized];
          }
        }
      }
    } catch (e) {
       console.error("Failed to parse errorDetails", e);
    }

    if ((layer.errorCode || layer.errorMessage) && (!layer.errorDetails || layer.errorDetails.trim() === "")) {
      const obj = {
          errorCode: layer.errorCode,
          errorMessage: layer.errorMessage,
          suggestion: layer.suggestion
      };
      if (layer.validationStatus === "Warning") allWarnings.push(obj);
      else if (layer.validationStatus !== "Pass") allErrors.push(obj);
    }
  });

  const mappedValidationResult = {
    extractedData: {
      total_pre_tax: invoice.totalAmountBeforeTax,
      total_tax_amount: invoice.totalTaxAmount,
      total_amount: invoice.totalAmount,
    },
    errorDetails: allErrors,
    warningDetails: allWarnings,
    errors: fallbackErrors
  };

  // ─── Tabs ───
  const tabItems = [
    {
      key: "info",
      label: (
        <span>
          <FileTextOutlined /> Thông tin
        </span>
      ),
      children: (
        <div>
          {/* Outdated Version Banner */}
          {invoice.isReplaced && invoice.replacedBy && (
            <Alert
              message="Hóa đơn này đã có phiên bản mới hơn"
              description={
                <span>
                  Hệ thống đã ghi nhận một tệp tải lên mới khắc phục lỗi cho hóa đơn này. Dữ liệu hiện tại chỉ mang tính chất lưu vết.{' '}
                  <a
                    onClick={() => navigate(`/app/invoices/${invoice.replacedBy}`)}
                    style={{ fontWeight: 600, textDecoration: 'underline' }}
                  >
                    Xem phiên bản mới nhất →
                  </a>
                </span>
              }
              type="error"
              showIcon
              style={{ marginBottom: 16, borderRadius: 10, border: '1px solid #ffccc7' }}
            />
          )}

          {/* ═══ Invoice Dossier Status Banner ═══ */}
          {invoice.hasOriginalFile && invoice.hasVisualFile && (
            <Alert
              message="Hồ sơ hóa đơn đầy đủ"
              description="Hóa đơn có cả bản gốc XML và bản thể hiện PDF/Ảnh."
              type="success"
              showIcon
              icon={<CheckCircleOutlined />}
              style={{ marginBottom: 16, borderRadius: 10 }}
            />
          )}
          {invoice.hasOriginalFile && !invoice.hasVisualFile && (
            <Alert
              message="Thiếu bản thể hiện (PDF/Ảnh)"
              description="Hóa đơn chỉ có bản gốc XML. Nhấn 'Tải lên OCR' để bổ sung bản thể hiện PDF/Ảnh cho hồ sơ đầy đủ."
              type="info"
              showIcon
              icon={<CloudUploadOutlined />}
              style={{ marginBottom: 16, borderRadius: 10 }}
            />
          )}
          {!invoice.hasOriginalFile && invoice.hasVisualFile && (
            <Alert
              message="Thiếu bản gốc XML — Rủi ro Yellow"
              description="Hóa đơn được trích xuất từ ảnh/PDF bằng AI. Để đảm bảo tính pháp lý khi khai thuế, hãy tải lên file XML gốc. Hệ thống sẽ tự động xác thực chữ ký số và cập nhật dữ liệu chính xác."
              type="warning"
              showIcon
              icon={<WarningOutlined />}
              style={{ marginBottom: 16, borderRadius: 10 }}
            />
          )}
          {!invoice.hasOriginalFile && !invoice.hasVisualFile && (
            <Alert
              message="Không có tệp đính kèm"
              description="Hồ sơ hóa đơn chưa có tệp nào."
              type="error"
              showIcon
              style={{ marginBottom: 16, borderRadius: 10 }}
            />
          )}

          {/* Rejection alert */}
          {invoice.status === "Rejected" && invoice.rejectionReason && (
            <Alert
              message="Hóa đơn đã bị từ chối"
              description={
                <div>
                  <Text strong>Lý do: </Text>
                  {invoice.rejectionReason}
                  {invoice.rejectedByName && (
                    <div style={{ marginTop: 4 }}>
                      <Text type="secondary">
                        Bởi: {invoice.rejectedByName} —{" "}
                        {invoice.rejectedAt
                          ? dayjs(invoice.rejectedAt).format("DD/MM/YYYY HH:mm")
                          : ""}
                      </Text>
                    </div>
                  )}
                </div>
              }
              type="error"
              showIcon
              style={{ marginBottom: 20, borderRadius: 10 }}
            />
          )}

          <Row gutter={24}>
            {/* Seller */}
            <Col xs={24} md={12}>
              <Card
                size="small"
                title={
                  <span>
                    <BankOutlined /> Người bán
                  </span>
                }
                variant="borderless"
                style={{
                  marginBottom: 16,
                  borderRadius: 12,
                  background: "#f8fafc",
                }}
              >
                <InfoItem label="Tên đơn vị" value={invoice.sellerName} />
                <InfoItem label="Mã số thuế" value={invoice.sellerTaxCode} />
                <InfoItem label="Địa chỉ" value={invoice.sellerAddress} />
                {invoice.sellerBankAccount && (
                  <InfoItem
                    label="Tài khoản ngân hàng"
                    value={`${invoice.sellerBankAccount} — ${invoice.sellerBankName}`}
                  />
                )}
              </Card>
            </Col>

            {/* Buyer */}
            <Col xs={24} md={12}>
              <Card
                size="small"
                title={
                  <span>
                    <UserOutlined /> Người mua
                  </span>
                }
                variant="borderless"
                style={{
                  marginBottom: 16,
                  borderRadius: 12,
                  background: "#f8fafc",
                }}
              >
                <InfoItem label="Tên đơn vị" value={invoice.buyerName} />
                <InfoItem label="Mã số thuế" value={invoice.buyerTaxCode} />
                <InfoItem label="Địa chỉ" value={invoice.buyerAddress} />
              </Card>
            </Col>
          </Row>

          {/* Amounts */}
          <Card
            size="small"
            title={
              <span>
                <DollarOutlined /> Số tiền
              </span>
            }
            variant="borderless"
            style={{
              marginBottom: 16,
              borderRadius: 12,
              background: "#f0f9ff",
            }}
          >
            <Row gutter={24}>
              <Col xs={8}>
                <InfoItem
                  label="Tiền trước thuế"
                  value={formatCurrency(
                    computedTotalBeforeTax,
                    invoice.invoiceCurrency,
                  )}
                />
              </Col>
              <Col xs={8}>
                <InfoItem
                  label="Tiền thuế"
                  value={formatCurrency(
                    computedTotalTax,
                    invoice.invoiceCurrency,
                  )}
                />
              </Col>
              <Col xs={8}>
                <div style={{ marginBottom: 12 }}>
                  <Text
                    type="secondary"
                    style={{ fontSize: 12, display: "block", marginBottom: 2 }}
                  >
                    Tổng tiền thanh toán
                  </Text>
                  <Text strong style={{ fontSize: 20, color: "#1677ff" }}>
                    {formatCurrency(
                      invoice.totalAmount,
                      invoice.invoiceCurrency,
                    )}
                  </Text>
                </div>
              </Col>
            </Row>
            {invoice.totalAmountInWords && (
              <Text italic type="secondary" style={{ fontSize: 13 }}>
                Bằng chữ: {invoice.totalAmountInWords}
              </Text>
            )}
          </Card>

          {/* Workflow timeline */}
          <Card
            size="small"
            title={
              <span>
                <CalendarOutlined /> Dòng thời gian
              </span>
            }
            variant="borderless"
            style={{ borderRadius: 12 }}
          >
            <Row gutter={24}>
              <Col xs={8}>
                <InfoItem
                  label="Người tải lên"
                  value={invoice.uploadedByName}
                />
                <InfoItem
                  label="Ngày tải lên"
                  value={dayjs(invoice.createdAt).format("DD/MM/YYYY HH:mm")}
                />
              </Col>
              {invoice.submittedAt && (
                <Col xs={8}>
                  <InfoItem
                    label="Người gửi duyệt"
                    value={invoice.submittedByName}
                  />
                  <InfoItem
                    label="Ngày gửi duyệt"
                    value={dayjs(invoice.submittedAt).format(
                      "DD/MM/YYYY HH:mm",
                    )}
                  />
                </Col>
              )}
              {invoice.approvedAt && (
                <Col xs={8}>
                  <InfoItem
                    label="Người duyệt"
                    value={invoice.approvedByName}
                  />
                  <InfoItem
                    label="Ngày duyệt"
                    value={dayjs(invoice.approvedAt).format("DD/MM/YYYY HH:mm")}
                  />
                </Col>
              )}
            </Row>
            {invoice.paymentMethod && (
              <InfoItem
                label="Phương thức thanh toán"
                value={invoice.paymentMethod}
              />
            )}
            {invoice.notes && (
              <div style={{ marginTop: 16 }}>
                <Typography.Text type="secondary" style={{ fontSize: 12, display: "block", marginBottom: 4 }}>Ghi chú hệ thống</Typography.Text>
                <div style={{ background: "#f6f8fa", padding: "8px 12px", borderRadius: 8, fontSize: 13, border: "1px solid #e2e8f0" }}>
                  {invoice.notes.split('---').filter(p => p.trim()).map((part: string, index: number, arr) => (
                    <div key={index} style={{ marginBottom: index !== arr.length - 1 ? 4 : 0 }}>
                      {part.trim()}
                    </div>
                  ))}
                </div>
              </div>
            )}
          </Card>
        </div>
      ),
    },
    {
      key: "items",
      label: (
        <span>
          <ShoppingCartOutlined /> Hàng hóa ({invoice.lineItems.length})
        </span>
      ),
      children: (
        <Table<LineItemDto>
          columns={lineItemColumns}
          dataSource={invoice.lineItems}
          rowKey="lineNumber"
          pagination={false}
          size="small"
          scroll={{ x: 900 }}
          summary={() => (
            <Table.Summary fixed>
              <Table.Summary.Row>
                <Table.Summary.Cell index={0} colSpan={5} align="right">
                  <Text strong>Tổng cộng:</Text>
                </Table.Summary.Cell>
                <Table.Summary.Cell index={5} align="right">
                  <Text strong>
                    {formatCurrency(
                      computedTotalBeforeTax,
                      invoice.invoiceCurrency,
                    )}
                  </Text>
                </Table.Summary.Cell>
                <Table.Summary.Cell index={6} />
                <Table.Summary.Cell index={7} align="right">
                  <Text strong>
                    {formatCurrency(computedTotalTax, invoice.invoiceCurrency)}
                  </Text>
                </Table.Summary.Cell>
              </Table.Summary.Row>
            </Table.Summary>
          )}
        />
      ),
    },
    {
      key: "validation_summary",
      label: (
        <span>
          <SafetyCertificateOutlined /> Kết quả kiểm tra
        </span>
      ),
      children: (
        <div className="animate-fade-in-up">
          <BusinessValidationSummary result={mappedValidationResult} />
        </div>
      ),
    },
    {
      key: "audit",
      label: (
        <span>
          <AuditOutlined /> Nhật ký ({invoice.auditLogs.length})
        </span>
      ),
      children: (
        <Timeline
          items={invoice.auditLogs.map((log) => ({
            color: actionColorMap[log.action] || "#8c8c8c",
            content: (
              <div key={log.auditId}>
                <Space>
                  <Tag color={actionColorMap[log.action] || "default"}>
                    {actionLabelMap[log.action] || log.action}
                  </Tag>
                  <Text type="secondary" style={{ fontSize: 12 }}>
                    {dayjs(log.createdAt).format("DD/MM/YYYY HH:mm:ss")}
                  </Text>
                </Space>
                <div style={{ marginTop: 4 }}>
                  <Text style={{ fontSize: 13 }}>
                    {log.userFullName || log.userEmail || "N/A"}
                    {log.userRole ? ` (${log.userRole})` : ""}
                  </Text>
                </div>
                {log.reason && (
                  <div style={{ marginTop: 4 }}>
                    <Text type="danger" style={{ fontSize: 13 }}>
                      Lý do: {log.reason}
                    </Text>
                  </div>
                )}
                {log.comment && (
                  <div style={{ marginTop: 2 }}>
                    <Text type="secondary" style={{ fontSize: 13 }}>
                      Ghi chú: {log.comment}
                    </Text>
                  </div>
                )}
                {log.changes && log.changes.length > 0 && (
                  <div
                    style={{
                      marginTop: 6,
                      background: "#f6f8fa",
                      borderRadius: 8,
                      padding: "8px 12px",
                    }}
                  >
                    {log.changes.map((c, idx) => {
                      const oldVal =
                        c.old_value != null && c.old_value !== ""
                          ? String(c.old_value)
                          : null;
                      const newVal =
                        c.new_value != null && c.new_value !== ""
                          ? String(c.new_value)
                          : null;
                      return (
                        <div
                          key={idx}
                          style={{
                            fontSize: 12,
                            color: "#64748b",
                            marginBottom: 4,
                          }}
                        >
                          <strong>{c.field}</strong>:{" "}
                          {oldVal && (
                            <span
                              style={{
                                textDecoration: "line-through",
                                color: "#94a3b8",
                                marginRight: 6,
                              }}
                            >
                              {oldVal}
                            </span>
                          )}
                          {oldVal && (
                            <span style={{ color: "#94a3b8", marginRight: 6 }}>
                              →
                            </span>
                          )}
                          {!oldVal && newVal && (
                            <span style={{ color: "#94a3b8", marginRight: 6 }}>
                              →
                            </span>
                          )}
                          {newVal ? (
                            <ChangeValueBadge field={c.field} value={newVal} />
                          ) : (
                            <span style={{ color: "#94a3b8" }}>Không có</span>
                          )}
                        </div>
                      );
                    })}
                  </div>
                )}
              </div>
            ),
          }))}
        />
      ),
    },
  ];

  return (
    <div className="animate-fade-in-up">
      {/* Header */}
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "flex-start",
          marginBottom: 20,
        }}
      >
        <div>
          <Button
            type="text"
            icon={<ArrowLeftOutlined />}
            onClick={() => navigate(-1)}
            style={{ marginBottom: 8, padding: 0, color: "#64748b" }}
          >
            Quay lại danh sách
          </Button>
          <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
            <Title level={3} style={{ margin: 0 }}>
              {invoice.invoiceNumber}
              {invoice.serialNumber && (
                <Text
                  type="secondary"
                  style={{ fontSize: 16, fontWeight: 400 }}
                >
                  {" "}
                  — {invoice.serialNumber}
                </Text>
              )}
            </Title>
            {versions && versions.length > 0 ? (
               <Dropdown
                 menu={{
                   items: versions.map(v => ({
                     key: v.invoiceId,
                     label: (
                       <Space>
                         <span>Phiên bản {v.version}</span>
                         <Text type="secondary" style={{ fontSize: 12 }}>
                           — {dayjs(v.createdAt).format('DD/MM/YYYY HH:mm')}
                         </Text>
                         {v.invoiceId === id && <Tag color="blue" variant="filled">Hiện tại</Tag>}
                       </Space>
                     ),
                     onClick: () => {
                       if (v.invoiceId !== id) {
                         navigate(`/app/invoices/${v.invoiceId}`);
                       }
                     }
                   }))
                 }}
                 trigger={['click']}
               >
                 <Button size="small" style={{ borderRadius: 12, padding: '2px 10px', fontSize: 13, borderColor: '#d9d9d9', color: '#1677ff' }}>
                   Phiên bản {invoice.version || 1} <DownOutlined />
                 </Button>
               </Dropdown>
            ) : (
                <Tag color="purple" style={{ borderRadius: 12, padding: '2px 10px', fontSize: 13, border: 'none' }}>
                  Phiên bản {invoice.version || 1}
                </Tag>
            )}
            <StatusBadge
              type="status"
              value={invoice.status}
              isPending={isValidationPending}
            />
            <StatusBadge type="risk" value={invoice.riskLevel} />
          </div>
          <Space style={{ marginTop: 8 }} size={16}>
            <Text type="secondary">
              <CalendarOutlined />{" "}
              {dayjs(invoice.invoiceDate).format("DD/MM/YYYY")}
            </Text>
            {invoice.formNumber && (
              <Text type="secondary">Mẫu: {invoice.formNumber}</Text>
            )}
            <Text type="secondary">
              Xử lý: {invoice.processingMethod === "API" ? "OCR" : invoice.processingMethod}
            </Text>
            {invoice.hasOriginalFile && invoice.hasVisualFile ? (
              <Tag color="success" icon={<LinkOutlined />}>Hồ sơ đầy đủ</Tag>
            ) : invoice.hasOriginalFile ? (
              <Tag color="processing" icon={<FileTextOutlined />}>Chỉ XML</Tag>
            ) : invoice.hasVisualFile ? (
              <Tag color="warning" icon={<CloudUploadOutlined />}>Chỉ OCR</Tag>
            ) : null}
            {invoice.mccqt && (
              <Text type="secondary">MCCQT: {invoice.mccqt}</Text>
            )}
          </Space>
        </div>
        {renderWorkflowActions()}
      </div>

      {/* Content Tabs */}
      <Card
        variant="borderless"
        className="bg-dash-card rounded-[14px] shadow-dash"
        styles={{ body: { padding: "16px 24px" } }}
      >
        <Tabs defaultActiveKey="info" items={tabItems} />
      </Card>

      {/* Submit with giải trình Modal - for Yellow invoices */}
      <Modal
        title={
          <Space>
            <WarningOutlined style={{ color: "#faad14" }} />
            <span>Gửi duyệt hóa đơn cảnh báo (Yellow)</span>
          </Space>
        }
        open={submitModalOpen}
        onCancel={() => setSubmitModalOpen(false)}
        onOk={() => submitMutation.mutate(submitComment || undefined)}
        okText="Xác nhận gửi duyệt"
        cancelText="Hủy"
        okButtonProps={{ loading: submitMutation.isPending }}
      >
        <Alert
          message={`Hóa đơn này có rủi ro <Yellow> — cần giải trình để Admin xét duyệt.`}
          type="warning"
          showIcon
          style={{ marginBottom: 16 }}
        />
        <Text strong style={{ display: "block", marginBottom: 8 }}>
          Lý do giải trình <Text type="secondary">(có thể bỏ trống)</Text>
        </Text>
        <TextArea
          rows={4}
          value={submitComment}
          onChange={(e) => setSubmitComment(e.target.value)}
          placeholder="Ví dụ: Hóa đơn đổ xăng công tác, cây xăng không xuất hoá đơn có MST người mua..."
          maxLength={500}
          showCount
        />
      </Modal>

      {/* Reject Modal */}
      <Modal
        title={
          <span>
            <ExclamationCircleOutlined
              style={{ color: "#ff4d4f", marginRight: 8 }}
            />
            Từ chối hóa đơn
          </span>
        }
        open={rejectModalOpen}
        onCancel={() => setRejectModalOpen(false)}
        onOk={() => rejectMutation.mutate()}
        okText="Từ chối"
        cancelText="Hủy"
        okButtonProps={{
          danger: true,
          loading: rejectMutation.isPending,
          disabled: !rejectReason.trim(),
        }}
      >
        <div style={{ marginBottom: 16 }}>
          <Text strong style={{ display: "block", marginBottom: 8 }}>
            Lý do từ chối <Text type="danger">*</Text>
          </Text>
          <TextArea
            rows={3}
            value={rejectReason}
            onChange={(e) => setRejectReason(e.target.value)}
            placeholder="Nhập lý do từ chối..."
            maxLength={1000}
            showCount
          />
        </div>
        <div>
          <Text strong style={{ display: "block", marginBottom: 8 }}>
            Ghi chú bổ sung
          </Text>
          <TextArea
            rows={2}
            value={rejectComment}
            onChange={(e) => setRejectComment(e.target.value)}
            placeholder="Ghi chú thêm (tùy chọn)..."
            maxLength={500}
          />
        </div>
      </Modal>
    </div>
  );
};

export default InvoiceDetail;
