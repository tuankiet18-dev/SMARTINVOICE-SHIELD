import React, { useState, useEffect } from "react";
import {
  Card,
  Upload,
  Typography,
  Row,
  Col,
  Steps,
  Button,
  Space,
  Tag,
  Alert,
  message,
  Result,
  Table,
  Input,
  InputNumber,
  Tooltip,
  Modal,
  Checkbox,
  Dropdown,
  Tabs,
} from "antd";
import type { TableRowSelection } from "antd/es/table/interface";
import {
  FileTextOutlined,
  SafetyCertificateOutlined,
  CheckCircleOutlined,
  CloudUploadOutlined,
  FilePdfOutlined,
  FileImageOutlined,
  LoadingOutlined,
  WarningOutlined,
  CloseCircleOutlined,
  EditOutlined,
  DeleteOutlined,
  ClockCircleOutlined,
  SendOutlined,
  CheckSquareOutlined,
  EyeOutlined,
  MoreOutlined,
} from "@ant-design/icons";
import { invoiceService, ValidationResult } from "../services/invoice";
import { useNavigate } from "react-router-dom";
import ValidationChecklist from "../components/ValidationChecklist";
import BusinessValidationSummary from "../components/BusinessValidationSummary";
import LeaveUploadModal from "../components/LeaveUploadModal";

const { Title, Text, Paragraph } = Typography;
const { Dragger } = Upload;

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

interface ValidationResultExtended extends Omit<
  ValidationResult,
  "extractedData"
> {
  extractedData?: ExtractedData;
}

type SubmitStatus = "idle" | "submitting" | "submitted" | "failed";

interface ProcessResult {
  fileName: string;
  fileSize: number;
  status: "pending" | "processing" | "success" | "error" | "warning";
  result?: ValidationResultExtended;
  errorMessage?: string;
  invoiceId?: string;
  submitStatus: SubmitStatus;
  submitError?: string;
}

const UploadInvoice: React.FC = () => {
  const navigate = useNavigate();
  const [currentStep, setCurrentStep] = useState(0);
  const [fileList, setFileList] = useState<any[]>([]);
  const [isProcessing, setIsProcessing] = useState(false);
  const [results, setResults] = useState<ProcessResult[]>([]);
  const [isBatchSubmitting, setIsBatchSubmitting] = useState(false);
  const [commentModalVisible, setCommentModalVisible] = useState(false);
  const [pendingSubmitId, setPendingSubmitId] = useState<string | null>(null);
  const [submitComment, setSubmitComment] = useState("");
  const [activeTab, setActiveTab] = useState<"xml" | "ocr">("xml");
  const [expandedRows, setExpandedRows] = useState<Set<string>>(new Set());
  const [leaveModalVisible, setLeaveModalVisible] = useState(false);
  const [pendingNavigation, setPendingNavigation] = useState<string | null>(
    null,
  );

  const getDefaultSelected = (res: ProcessResult[]) =>
    res
      .filter(
        (r) =>
          r.invoiceId && r.status === "success" && r.submitStatus === "idle",
      )
      .map((r) => r.fileName);

  const [selectedRowKeys, setSelectedRowKeys] = useState<React.Key[]>([]);

  // Handle page reload/close/back button - warn when there are results that will be lost
  useEffect(() => {
    const handleBeforeUnload = (e: BeforeUnloadEvent) => {
      // Warn if there are any results (danh sách sẽ mất)
      if (results.length > 0) {
        e.preventDefault();
        e.returnValue = "";
      }
    };

    window.addEventListener("beforeunload", handleBeforeUnload);
    return () => window.removeEventListener("beforeunload", handleBeforeUnload);
  }, [results]);

  // Handle proceed with navigation
  const handleProceedNavigation = () => {
    setLeaveModalVisible(false);
    if (pendingNavigation) {
      navigate(pendingNavigation);
    }
  };

  // Override router navigation
  useEffect(() => {
    const unlistenPrompt = navigate.toString(); // This is a workaround; proper way depends on router version
    // For now, we'll handle it through button clicks
  }, [navigate]);

  const handleSafeNavigation = (path: string) => {
    // Check if there are any results (danh sách sẽ mất)
    if (results.length > 0) {
      setPendingNavigation(path);
      setLeaveModalVisible(true);
    } else {
      navigate(path);
    }
  };

  const handleOpenInvoiceDetail = (invoiceId: string) => {
    // Navigate to invoice detail page with validation tab active
    handleSafeNavigation(`/app/invoices/${invoiceId}?tab=validation`);
  };

  const uploadProps = {
    name: "file",
    multiple: true,
    accept: activeTab === "xml" ? ".xml" : ".pdf,.jpg,.jpeg,.png",
    fileList,
    onChange(info: any) {
      setFileList(info.fileList);
    },
    beforeUpload: () => false,
    showUploadList: false,
  };

  const handleTabChange = (key: string) => {
    setActiveTab(key as "xml" | "ocr");
    handleReset();
  };

  const handleReset = () => {
    setFileList([]);
    setResults([]);
    setCurrentStep(0);
    setSelectedRowKeys([]);
  };

  const handleProcessFiles = async () => {
    if (fileList.length === 0) return;

    setIsProcessing(true);
    setCurrentStep(1);

    const initialResults: ProcessResult[] = fileList.map((f) => ({
      fileName: f.name,
      fileSize: f.size,
      status: "pending",
      submitStatus: "idle",
    }));
    setResults(initialResults);
    setSelectedRowKeys([]);

    try {
      for (let i = 0; i < fileList.length; i++) {
        const fileObj = fileList[i].originFileObj as File;
        if (!fileObj) continue;

        setResults((prev) =>
          prev.map((item, idx) =>
            idx === i ? { ...item, status: "processing" } : item,
          ),
        );

        // Check if file type matches the current tab
        const isXmlFile = fileObj.name.toLowerCase().endsWith(".xml");
        const isPdfOrImage = [".pdf", ".jpg", ".jpeg", ".png"].some((ext) =>
          fileObj.name.toLowerCase().endsWith(ext),
        );

        // If XML tab and file is not XML, skip
        if (activeTab === "xml" && !isXmlFile) {
          setResults((prev) =>
            prev.map((item, idx) =>
              idx === i
                ? {
                    ...item,
                    status: "error",
                    errorMessage:
                      "Chỉ chấp nhận file XML trong tab này. Vui lòng chọn file XML.",
                  }
                : item,
            ),
          );
          continue;
        }

        // If OCR tab and file is not PDF/Image, skip
        if (activeTab === "ocr" && !isPdfOrImage) {
          setResults((prev) =>
            prev.map((item, idx) =>
              idx === i
                ? {
                    ...item,
                    status: "error",
                    errorMessage:
                      "Chỉ chấp nhận file PDF, JPG, PNG trong tab này.",
                  }
                : item,
            ),
          );
          continue;
        }

        try {
          if (activeTab === "xml") {
            // ══════ XML TAB: Existing sync flow (unchanged) ══════
            const { uploadUrl, s3Key } = await invoiceService.getUploadUrl(
              fileObj.name,
              fileObj.type || "application/xml",
            );
            await invoiceService.uploadToS3(uploadUrl, fileObj);
            if (currentStep < 2) setCurrentStep(2);

            const validation = await invoiceService.processXml(s3Key);
            const hasErrors = !!validation.errors?.length;
            const hasWarnings = !!validation.warnings?.length;

            setResults((prev) => {
              const next = prev.map((item, idx) => {
                if (idx !== i) return item;

                let finalErrorMessage = undefined;
                if (
                  validation.errorDetails &&
                  validation.errorDetails.length > 0
                ) {
                  finalErrorMessage = validation.errorDetails
                    .map((e) => e.errorMessage)
                    .join(" | ");
                } else if (hasErrors) {
                  finalErrorMessage = validation.errors.join(" | ");
                } else if (
                  validation.warningDetails &&
                  validation.warningDetails.length > 0
                ) {
                  finalErrorMessage =
                    validation.warningDetails[0].errorMessage || undefined;
                } else if (hasWarnings) {
                  finalErrorMessage = validation.warnings[0];
                }

                return {
                  ...item,
                  status: hasErrors
                    ? "error"
                    : hasWarnings
                      ? "warning"
                      : "success",
                  result: validation as ValidationResultExtended,
                  invoiceId: validation.invoiceId,
                  errorMessage: finalErrorMessage,
                  submitStatus: "idle",
                } as ProcessResult;
              });
              setSelectedRowKeys(getDefaultSelected(next));
              return next;
            });
          } else {
            // ══════ OCR TAB: Async upload + polling flow ══════
            // Step 1: Upload image → backend uploads to S3 + publishes SQS
            const uploadResult = await invoiceService.uploadImage(fileObj);
            if (currentStep < 2) setCurrentStep(2);

            // Step 2: Poll until OCR worker finishes
            setResults((prev) =>
              prev.map((item, idx) =>
                idx === i
                  ? {
                      ...item,
                      status: "processing",
                      invoiceId: uploadResult.invoiceId,
                      errorMessage: "Đang chờ OCR xử lý...",
                    }
                  : item,
              ),
            );

            const detail = await invoiceService.pollInvoiceUntilDone(
              uploadResult.invoiceId,
              (status) => {
                // Update status text while polling
                setResults((prev) =>
                  prev.map((item, idx) =>
                    idx === i && item.status === "processing"
                      ? {
                          ...item,
                          errorMessage:
                            status === "Processing"
                              ? "Đang chờ OCR xử lý..."
                              : `Trạng thái: ${status}`,
                        }
                      : item,
                  ),
                );
              },
            );

            // Step 3: Map InvoiceDetailDto → ProcessResult
            const isFailed = detail.status === "Failed" || detail.status === "Rejected";
            const hasWarnings = detail.riskLevel === "Yellow" || detail.riskLevel === "Orange";

            // Build validation-like result from detail
            const warningDetails: Array<{ errorCode: string | null; errorMessage: string | null; suggestion: string | null }> = [];
            const errorDetails: Array<{ errorCode: string | null; errorMessage: string | null; suggestion: string | null }> = [];

            // Extract from validationLayers
            if (detail.validationLayers) {
              for (const layer of detail.validationLayers) {
                if (layer.validationStatus === "Fail" || !layer.isValid) {
                  errorDetails.push({
                    errorCode: layer.errorCode,
                    errorMessage: layer.errorMessage,
                    suggestion: null,
                  });
                } else if (layer.validationStatus === "Warning" || layer.validationStatus === "WARNING") {
                  warningDetails.push({
                    errorCode: layer.errorCode,
                    errorMessage: layer.errorMessage,
                    suggestion: null,
                  });
                }
              }
            }

            // If OCR-only, add a default warning if nothing else
            if (!isFailed && warningDetails.length === 0 && errorDetails.length === 0) {
              warningDetails.push({
                errorCode: "WARN_MISSING_XML_EVIDENCE",
                errorMessage: "Hóa đơn từ OCR, cần bổ sung XML để xác thực pháp lý.",
                suggestion: "Tải lên file XML gốc để xác thực chữ ký số.",
              });
            }

            const ocrValidation: ValidationResultExtended = {
              isValid: !isFailed,
              errors: errorDetails.map(e => e.errorMessage || ""),
              warnings: warningDetails.map(w => w.errorMessage || ""),
              errorDetails,
              warningDetails,
              signerSubject: null,
              extractedData: null,
              invoiceId: detail.invoiceId,
            };

            let finalErrorMessage = undefined;
            if (isFailed) {
              finalErrorMessage = detail.notes || errorDetails[0]?.errorMessage || "OCR thất bại";
            } else if (warningDetails.length > 0) {
              finalErrorMessage = warningDetails[0]?.errorMessage || undefined;
            }

            const finalIdx = i;
            setResults((prev) => {
              const next = prev.map((item, idx) => {
                if (idx !== finalIdx) return item;
                return {
                  ...item,
                  status: isFailed
                    ? "error"
                    : hasWarnings
                      ? "warning"
                      : "success",
                  result: ocrValidation,
                  invoiceId: detail.invoiceId,
                  errorMessage: finalErrorMessage,
                  submitStatus: "idle",
                } as ProcessResult;
              });
              setSelectedRowKeys(getDefaultSelected(next));
              return next;
            });
          }
        } catch (error: any) {
          const resData = error.response?.data;
          const errMsg =
            resData?.errors?.join(", ") ||
            resData?.message ||
            error.message ||
            "Lỗi hệ thống";
          setResults((prev) =>
            prev.map((item, idx) =>
              idx === i
                ? {
                    ...item,
                    status: "error",
                    result: resData,
                    errorMessage: errMsg,
                    submitStatus: "idle",
                  }
                : item,
            ),
          );
        }
      }
      setCurrentStep(3);
    } catch (err) {
      message.error("Quá trình tổng thể gặp lỗi. Vui lòng thử lại.");
    } finally {
      setIsProcessing(false);
    }
  };

  const handleSingleSubmit = async (
    record: ProcessResult,
    comment?: string,
  ) => {
    if (!record.invoiceId) {
      message.error(
        "Hóa đơn này chưa được lưu vào hệ thống (lỗi fatal), không thể gửi duyệt.",
      );
      return;
    }
    setResults((prev) =>
      prev.map((r) =>
        r.fileName === record.fileName
          ? { ...r, submitStatus: "submitting" }
          : r,
      ),
    );
    try {
      await invoiceService.submitInvoice(record.invoiceId, comment);
      setResults((prev) => {
        const next = prev.map((r) =>
          r.fileName === record.fileName
            ? { ...r, submitStatus: "submitted" as SubmitStatus }
            : r,
        );
        setSelectedRowKeys(getDefaultSelected(next));
        return next;
      });
      message.success(`Đã gửi duyệt: ${record.fileName}`);
    } catch (err: any) {
      const errMsg =
        err.response?.data?.message || err.message || "Gửi duyệt thất bại";
      setResults((prev) =>
        prev.map((r) =>
          r.fileName === record.fileName
            ? { ...r, submitStatus: "failed", submitError: errMsg }
            : r,
        ),
      );
      message.error(`Lỗi gửi duyệt ${record.fileName}: ${errMsg}`);
    }
  };

  const openSubmitWithComment = (record: ProcessResult) => {
    setSubmitComment("");
    setPendingSubmitId(record.fileName);
    setCommentModalVisible(true);
  };

  const confirmSubmitWithComment = () => {
    const record = results.find((r) => r.fileName === pendingSubmitId);
    if (record) handleSingleSubmit(record, submitComment);
    setCommentModalVisible(false);
    setPendingSubmitId(null);
  };

  const handleBatchSubmit = async () => {
    const selectedGreens = results.filter(
      (r) =>
        selectedRowKeys.includes(r.fileName) &&
        r.invoiceId &&
        r.status === "success" &&
        r.submitStatus === "idle",
    );
    if (selectedGreens.length === 0) {
      message.info("Không có hóa đơn Green nào được chọn để gửi duyệt.");
      return;
    }
    await executeBatchSubmit(selectedGreens, undefined);
  };

  const executeBatchSubmit = async (
    submittable: ProcessResult[],
    comment: string | undefined,
  ) => {
    const ids = submittable.map((r) => r.invoiceId!);
    setIsBatchSubmitting(true);
    setResults((prev) =>
      prev.map((r) =>
        ids.includes(r.invoiceId ?? "")
          ? { ...r, submitStatus: "submitting" }
          : r,
      ),
    );
    try {
      const batchResult = await invoiceService.submitBatch(ids, comment);
      setResults((prev) => {
        const next = prev.map((r) => {
          const found = batchResult.results.find(
            (res) => res.invoiceId === r.invoiceId,
          );
          if (!found) return r;
          return {
            ...r,
            submitStatus: (found.success
              ? "submitted"
              : "failed") as SubmitStatus,
            submitError: found.errorMessage,
          };
        });
        setSelectedRowKeys(getDefaultSelected(next));
        return next;
      });
      message.success(
        `Gửi duyệt thành công ${batchResult.successCount} hóa đơn` +
          (batchResult.failCount > 0 ? `, ${batchResult.failCount} lỗi` : ""),
      );
    } catch (err: any) {
      message.error(
        "Lỗi khi gửi batch: " + (err.response?.data?.message || err.message),
      );
      setResults((prev) =>
        prev.map((r) =>
          ids.includes(r.invoiceId ?? "") && r.submitStatus === "submitting"
            ? { ...r, submitStatus: "failed" }
            : r,
        ),
      );
    } finally {
      setIsBatchSubmitting(false);
    }
  };

  const handleDismiss = (record: ProcessResult) => {
    setResults((prev) => prev.filter((r) => r.fileName !== record.fileName));
    setSelectedRowKeys((prev) => prev.filter((k) => k !== record.fileName));
  };

  const renderStatusTag = (
    status: string,
    result?: ValidationResultExtended,
  ) => {
    if (status === "pending")
      return (
        <Tag icon={<ClockCircleOutlined />} color="default">
          Chờ xử lý
        </Tag>
      );
    if (status === "processing")
      return (
        <Tag icon={<LoadingOutlined />} color="processing">
          Đang xử lý
        </Tag>
      );
    const hasWarnings =
      (result?.warningDetails && result.warningDetails.length > 0) ||
      (result?.warnings && result.warnings.length > 0);
    if (status === "error") {
      const isFatal = !result?.invoiceId;
      return (
        <Tag icon={<CloseCircleOutlined />} color={isFatal ? "error" : "red"}>
          {isFatal ? "Lỗi (Không lưu)" : "Lỗi"}
        </Tag>
      );
    }
    if (hasWarnings)
      return (
        <Tag icon={<WarningOutlined />} color="warning">
          Cảnh báo
        </Tag>
      );
    return (
      <Tag icon={<CheckCircleOutlined />} color="success">
        Hợp lệ
      </Tag>
    );
  };

  const renderActionCell = (record: ProcessResult) => {
    const isSubmittable =
      record.invoiceId &&
      (record.status === "success" || record.status === "warning");
    const isYellow = record.status === "warning" && record.invoiceId;
    const { submitStatus } = record;

    // Build dropdown menu items
    const menuItems = [];

    // View details option
    if (
      record.status !== "pending" &&
      record.status !== "processing" &&
      record.invoiceId
    ) {
      menuItems.push({
        key: "view",
        icon: <EyeOutlined />,
        label: "Xem chi tiết",
        onClick: () => handleOpenInvoiceDetail(record.invoiceId!),
      });
    }

    // Submit options
    if (isSubmittable && submitStatus === "idle") {
      menuItems.push({
        key: "submit",
        icon: <SendOutlined />,
        label: isYellow ? "Gửi duyệt (có cảnh báo)" : "Gửi duyệt",
        onClick: () => {
          if (isYellow) openSubmitWithComment(record);
          else handleSingleSubmit(record);
        },
      });
    }

    // Resubmit option
    if (submitStatus === "failed") {
      menuItems.push({
        key: "resubmit",
        icon: <SendOutlined />,
        label: isYellow ? "Gửi lại (có cảnh báo)" : "Gửi lại",
        onClick: () => {
          if (isYellow) openSubmitWithComment(record);
          else handleSingleSubmit(record);
        },
      });
    }

    // Dismiss option
    if (record.status === "error" && submitStatus !== "submitted") {
      menuItems.push({
        key: "dismiss",
        icon: <DeleteOutlined />,
        label: "Ẩn khỏi danh sách",
        danger: true,
        onClick: () => handleDismiss(record),
      });
    }

    // Render status or actions
    if (submitStatus === "submitted") {
      return (
        <Tag icon={<CheckCircleOutlined />} color="blue">
          Đã gửi duyệt
        </Tag>
      );
    }

    if (submitStatus === "submitting") {
      return (
        <Tag icon={<LoadingOutlined />} color="processing">
          Đang gửi
        </Tag>
      );
    }

    if (submitStatus === "failed") {
      return (
        <Tooltip title={record.submitError || "Gửi duyệt thất bại"}>
          <Tag icon={<WarningOutlined />} color="error">
            Gửi thất bại
          </Tag>
        </Tooltip>
      );
    }

    if (menuItems.length === 0) {
      return (
        <Text type="secondary" style={{ fontSize: 12 }}>
          -
        </Text>
      );
    }

    return (
      <Dropdown
        menu={{ items: menuItems }}
        placement="bottomRight"
        trigger={["click"]}
      >
        <Button
          size="small"
          type="text"
          icon={<MoreOutlined />}
          style={{ color: isYellow ? "#d48806" : undefined }}
        />
      </Dropdown>
    );
  };

  const formatFileSize = (bytes: number) => {
    if (bytes === 0) return "0 B";
    const k = 1024;
    const sizes = ["B", "KB", "MB"];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return Math.round((bytes / Math.pow(k, i)) * 100) / 100 + " " + sizes[i];
  };

  const columns = [
    {
      title: "Tên File",
      dataIndex: "fileName",
      key: "fileName",
      width: 180,
      ellipsis: true,
      render: (text: string) => (
        <Text strong style={{ fontSize: 13 }}>
          {text}
        </Text>
      ),
    },
    {
      title: "Kích thước",
      dataIndex: "fileSize",
      key: "fileSize",
      width: 80,
      render: (size: number) => (
        <Text type="secondary" style={{ fontSize: 12 }}>
          {formatFileSize(size)}
        </Text>
      ),
    },
    {
      title: "Trạng thái",
      key: "status",
      width: 120,
      render: (_: any, record: ProcessResult) =>
        renderStatusTag(record.status, record.result),
    },
    {
      title: "Thông điệp / Cảnh báo",
      key: "message",
      ellipsis: true,
      render: (_: any, record: ProcessResult) => {
        if (record.status === "pending")
          return <Text type="secondary">Đang chờ xử lý...</Text>;
        if (record.status === "processing")
          return <Text type="secondary">Đang bóc tách dữ liệu...</Text>;

        if (record.status === "error") {
          const errors =
            record.result?.errorDetails && record.result.errorDetails.length > 0
              ? record.result.errorDetails
              : record.result?.errors;

          let firstMsg = record.errorMessage || "";
          let errorCode = null;
          let suggestion = null;

          if (
            record.result?.errorDetails &&
            record.result.errorDetails.length > 0
          ) {
            const firstErr = record.result.errorDetails[0];
            firstMsg = firstErr.errorMessage || firstMsg;
            errorCode = firstErr.errorCode;
            suggestion = firstErr.suggestion;
          } else if (record.result?.errors && record.result.errors.length > 0) {
            firstMsg = record.result.errors[0];
          }

          const extraLen = errors && errors.length > 1 ? errors.length - 1 : 0;
          const extra = extraLen > 0 ? ` (+${extraLen} lỗi)` : "";

          return (
            <div style={{ overflow: "hidden", maxWidth: "100%" }}>
              <Tooltip
                title={
                  <div>
                    <div>{firstMsg}</div>
                    {suggestion && (
                      <div
                        style={{
                          marginTop: 4,
                          fontStyle: "italic",
                          color: "#e2e8f0",
                        }}
                      >
                        💡 {suggestion}
                      </div>
                    )}
                  </div>
                }
              >
                <Paragraph
                  type="danger"
                  ellipsis={{ rows: 1 }}
                  style={{
                    margin: 0,
                    wordBreak: "break-word",
                    cursor: "pointer",
                  }}
                >
                  {firstMsg}
                  {extra}
                </Paragraph>
              </Tooltip>
            </div>
          );
        }

        const hasWarningsLegacy =
          record.result?.warnings && record.result.warnings.length > 0;
        const hasWarningDetails =
          record.result?.warningDetails &&
          record.result.warningDetails.length > 0;

        if (hasWarningDetails || hasWarningsLegacy) {
          const warnings = hasWarningDetails
            ? record.result!.warningDetails
            : record.result!.warnings;

          let firstMsg = "";
          let errorCode = null;
          let suggestion = null;

          if (hasWarningDetails && record.result!.warningDetails.length > 0) {
            const firstWarn = record.result!.warningDetails[0];
            firstMsg = firstWarn.errorMessage || "";
            errorCode = firstWarn.errorCode;
            suggestion = firstWarn.suggestion;
          } else if (hasWarningsLegacy) {
            firstMsg = record.result!.warnings[0];
          }

          const extraLen =
            warnings && warnings.length > 1 ? warnings.length - 1 : 0;
          const extra = extraLen > 0 ? ` (+${extraLen} cảnh báo)` : "";

          return (
            <div style={{ overflow: "hidden", maxWidth: "100%" }}>
              <Tooltip
                title={
                  <div>
                    <div>{firstMsg}</div>
                    {suggestion && (
                      <div
                        style={{
                          marginTop: 4,
                          fontStyle: "italic",
                          color: "#e2e8f0",
                        }}
                      >
                        💡 {suggestion}
                      </div>
                    )}
                  </div>
                }
              >
                <Paragraph
                  style={{
                    margin: 0,
                    color: "#d48806",
                    wordBreak: "break-word",
                    cursor: "pointer",
                  }}
                  ellipsis={{ rows: 1 }}
                >
                  {firstMsg}
                  {extra}
                </Paragraph>
              </Tooltip>
            </div>
          );
        }

        return <Text type="success">Dữ liệu chuẩn xác</Text>;
      },
    },
    {
      title: "Hành động",
      key: "action",
      width: 140,
      align: "center" as const,
      render: (_: any, record: ProcessResult) => renderActionCell(record),
    },
  ];

  const submittedCount = results.filter(
    (r) => r.submitStatus === "submitted",
  ).length;
  const selectedGreenCount = results.filter(
    (r) =>
      selectedRowKeys.includes(r.fileName) &&
      r.status === "success" &&
      r.submitStatus === "idle",
  ).length;

  const rowSelection: TableRowSelection<ProcessResult> = {
    selectedRowKeys,
    onChange: (keys: React.Key[], selectedRows: ProcessResult[]) => {
      const newKeys = keys.filter((k) => {
        const row = results.find((r) => r.fileName === k);
        return row && row.status === "success" && row.submitStatus === "idle";
      });
      setSelectedRowKeys(newKeys);
    },
    onSelect: (record: ProcessResult, selected: boolean) => {
      if (selected && record.status === "warning" && record.invoiceId) {
        openSubmitWithComment(record);
      }
    },
    getCheckboxProps: (record: ProcessResult) => ({
      disabled:
        record.status !== "success" ||
        record.submitStatus !== "idle" ||
        !record.invoiceId,
    }),
    renderCell: (checked, record, _index, originNode) => {
      // Hide checkbox completely if not a "Green" idle invoice
      if (record.status !== "success" || record.submitStatus !== "idle") {
        return null; // Trả về null để ẩn hoàn toàn ô checkbox
      }
      return originNode;
    },
  };

  return (
    <div className="animate-fade-in-up">
      <div style={{ marginBottom: 24 }}>
        <Title level={4} style={{ margin: 0 }}>
          Xử lý Hóa đơn Đầu vào
        </Title>
        {results.length === 0 && (
          <Text type="secondary">
            Tải lên file XML/PDF/Ảnh để hệ thống tự động bóc tách và rà soát rủi
            ro.
          </Text>
        )}
      </div>

      <Card
        bordered={false}
        style={{ borderRadius: 12, marginBottom: 24 }}
        bodyStyle={{ paddingBottom: results.length > 0 ? 0 : undefined }}
      >
        <Steps
          current={currentStep}
          size="small"
          items={[
            {
              title: "Tải lên",
              icon: <CloudUploadOutlined />,
              status: currentStep > 0 ? "finish" : "process",
            },
            {
              title: "Bóc tách",
              icon: <FileTextOutlined />,
              status:
                currentStep > 1
                  ? "finish"
                  : currentStep === 1
                    ? "process"
                    : "wait",
            },
            {
              title: "Rà soát",
              icon: <SafetyCertificateOutlined />,
              status:
                currentStep > 2
                  ? "finish"
                  : currentStep === 2
                    ? "process"
                    : "wait",
            },
            {
              title: "Hoàn tất",
              icon: <CheckCircleOutlined />,
              status: currentStep >= 3 ? "finish" : "wait",
            },
          ]}
          style={{ maxWidth: 800, margin: "0 auto 24px" }}
        />

        {results.length === 0 ? (
          <Row
            gutter={24}
            align="stretch"
            justify={fileList.length === 0 ? "center" : "start"}
          >
            <Col xs={24} lg={fileList.length > 0 ? 8 : 24}>
              <Tabs
                activeKey={activeTab}
                onChange={handleTabChange}
                type="card"
              >
                <Tabs.TabPane
                  key="xml"
                  tab={
                    <span>
                      <FileTextOutlined /> Tải lên Hóa đơn gốc (XML)
                    </span>
                  }
                >
                  <Dragger
                    {...uploadProps}
                    style={
                      fileList.length === 0
                        ? {
                            padding: "80px 20px",
                            borderRadius: 16,
                            background: "#f8fafc",
                            border: "2px dashed #cbd5e1",
                            width: "100%",
                            maxWidth: 800,
                            margin: "0 auto",
                          }
                        : {
                            padding: "60px 10px",
                            borderRadius: 8,
                            background: "#fafbfc",
                            height: "100%",
                            borderColor: "#1677ff40",
                          }
                    }
                  >
                    <p className="ant-upload-drag-icon">
                      <CloudUploadOutlined
                        style={
                          fileList.length === 0
                            ? { fontSize: 64, color: "#1677ff" }
                            : { fontSize: 48, color: "#1677ff" }
                        }
                      />
                    </p>
                    <p
                      className="ant-upload-text"
                      style={{
                        fontSize: fileList.length === 0 ? 18 : 16,
                        fontWeight: 500,
                        marginBottom: 8,
                        marginTop: 16,
                      }}
                    >
                      {fileList.length > 0 ? (
                        "Thêm file khác"
                      ) : (
                        <>
                          Kéo thả hoặc{" "}
                          <span style={{ color: "#1677ff" }}>
                            click vào khu vực này
                          </span>{" "}
                          để chọn file
                        </>
                      )}
                    </p>
                    <p
                      className="ant-upload-hint"
                      style={{ color: "#64748b", fontSize: 14 }}
                    >
                      Hỗ trợ định dạng: .xml.{" "}
                      <Text strong>Tối đa 10MB/file.</Text>
                    </p>

                    {fileList.length === 0 && (
                      <div style={{ marginTop: 32 }}>
                        <Tag
                          color="blue"
                          style={{
                            padding: "6px 16px",
                            borderRadius: 20,
                            fontSize: 13,
                            border: "none",
                            background: "#e6f4ff",
                            color: "#1677ff",
                          }}
                        >
                          💡 Khuyến nghị: Ưu tiên sử dụng file XML (QĐ
                          1550/QĐ-TCT) để bóc tách chính xác 100%.
                        </Tag>
                      </div>
                    )}
                  </Dragger>
                </Tabs.TabPane>

                <Tabs.TabPane
                  key="ocr"
                  tab={
                    <span>
                      <FileImageOutlined /> Tải lên PDF / Ảnh (AI Bóc tách)
                    </span>
                  }
                >
                  <Alert
                    type="warning"
                    showIcon
                    message="Lưu ý quan trọng"
                    description="Dữ liệu bóc tách bằng AI (OCR) có thể có sai sót so với bản gốc. Bắt buộc rà soát kỹ Số tiền, Mã số thuế và Ngày lập sau khi hệ thống xử lý xong."
                    style={{ marginBottom: 16 }}
                  />

                  <Dragger
                    {...uploadProps}
                    style={
                      fileList.length === 0
                        ? {
                            padding: "80px 20px",
                            borderRadius: 16,
                            background: "#f8fafc",
                            border: "2px dashed #cbd5e1",
                            width: "100%",
                            maxWidth: 800,
                            margin: "0 auto",
                          }
                        : {
                            padding: "60px 10px",
                            borderRadius: 8,
                            background: "#fafbfc",
                            height: "100%",
                            borderColor: "#1677ff40",
                          }
                    }
                  >
                    <p className="ant-upload-drag-icon">
                      <CloudUploadOutlined
                        style={
                          fileList.length === 0
                            ? { fontSize: 64, color: "#1677ff" }
                            : { fontSize: 48, color: "#1677ff" }
                        }
                      />
                    </p>
                    <p
                      className="ant-upload-text"
                      style={{
                        fontSize: fileList.length === 0 ? 18 : 16,
                        fontWeight: 500,
                        marginBottom: 8,
                        marginTop: 16,
                      }}
                    >
                      {fileList.length > 0 ? (
                        "Thêm file khác"
                      ) : (
                        <>
                          Kéo thả hoặc{" "}
                          <span style={{ color: "#1677ff" }}>
                            click vào khu vực này
                          </span>{" "}
                          để chọn file
                        </>
                      )}
                    </p>
                    <p
                      className="ant-upload-hint"
                      style={{ color: "#64748b", fontSize: 14 }}
                    >
                      Hỗ trợ định dạng: .pdf, .jpg, .jpeg, .png.{" "}
                      <Text strong>Tối đa 10MB/file.</Text>
                    </p>
                  </Dragger>
                </Tabs.TabPane>
              </Tabs>
            </Col>

            {fileList.length > 0 && (
              <Col xs={24} lg={16}>
                <Card
                  size="small"
                  title={
                    <Text strong style={{ fontSize: 16 }}>
                      Danh sách tải lên ({fileList.length} file)
                    </Text>
                  }
                  extra={
                    <Space>
                      <Button
                        type="text"
                        danger
                        onClick={() => setFileList([])}
                      >
                        Xóa tất cả
                      </Button>
                      <Button
                        type="primary"
                        onClick={handleProcessFiles}
                        loading={isProcessing}
                        icon={<CloudUploadOutlined />}
                      >
                        Bắt đầu xử lý {fileList.length} file
                      </Button>
                    </Space>
                  }
                  style={{
                    borderColor: "#e2e8f0",
                    borderRadius: 8,
                    height: "100%",
                  }}
                  bodyStyle={{ padding: 0 }}
                >
                  <div
                    style={{
                      maxHeight: 400,
                      overflowY: "auto",
                      padding: 16,
                      background: "#fafbfc",
                    }}
                  >
                    {fileList.map((f, i) => {
                      const lowerName = f.name.toLowerCase();
                      const isXml = lowerName.endsWith(".xml");
                      const isPdf = lowerName.endsWith(".pdf");
                      const isImage =
                        lowerName.endsWith(".jpg") ||
                        lowerName.endsWith(".png") ||
                        lowerName.endsWith(".jpeg");

                      let Icon = FileTextOutlined;
                      let color = "#1677ff";
                      let tagLabel = "HÓA ĐƠN";
                      if (isXml) {
                        Icon = FileTextOutlined;
                        color = "#52c41a";
                        tagLabel = "XML";
                      } else if (isPdf) {
                        Icon = FilePdfOutlined;
                        color = "#ff4d4f";
                        tagLabel = "PDF";
                      } else if (isImage) {
                        Icon = FileImageOutlined;
                        color = "#faad14";
                        tagLabel = "IMAGE";
                      }

                      const sizeKb = (f.size / 1024).toFixed(1);

                      return (
                        <div
                          key={i}
                          style={{
                            padding: "12px 16px",
                            background: "#fff",
                            border: "1px solid #f0f0f0",
                            borderRadius: 8,
                            marginBottom: 12,
                            display: "flex",
                            justifyContent: "space-between",
                            alignItems: "center",
                          }}
                        >
                          <div
                            style={{
                              display: "flex",
                              alignItems: "center",
                              gap: 16,
                              overflow: "hidden",
                            }}
                          >
                            <div
                              style={{
                                width: 44,
                                height: 44,
                                borderRadius: 8,
                                background: `${color}15`,
                                color: color,
                                display: "flex",
                                alignItems: "center",
                                justifyContent: "center",
                                fontSize: 22,
                                flexShrink: 0,
                              }}
                            >
                              <Icon />
                            </div>
                            <div style={{ minWidth: 0 }}>
                              <Text
                                strong
                                ellipsis
                                style={{
                                  display: "block",
                                  maxWidth: 300,
                                  fontSize: 14,
                                }}
                              >
                                {f.name}
                              </Text>
                              <Space size="middle" style={{ marginTop: 2 }}>
                                <Tag
                                  bordered={false}
                                  color={color}
                                  style={{ margin: 0 }}
                                >
                                  {tagLabel}
                                </Tag>
                                <Text type="secondary" style={{ fontSize: 12 }}>
                                  {sizeKb} KB
                                </Text>
                              </Space>
                            </div>
                          </div>
                          <Tooltip title="Xóa file">
                            <Button
                              type="text"
                              danger
                              icon={<DeleteOutlined />}
                              onClick={(e) => {
                                e.stopPropagation();
                                setFileList((prev) =>
                                  prev.filter((_, idx) => idx !== i),
                                );
                              }}
                            />
                          </Tooltip>
                        </div>
                      );
                    })}
                  </div>
                </Card>
              </Col>
            )}
          </Row>
        ) : (
          <div>
            <div
              style={{
                display: "flex",
                justifyContent: "space-between",
                alignItems: "center",
                marginBottom: 16,
                flexWrap: "wrap",
                gap: 8,
              }}
            >
              <div>
                <Title level={5} style={{ margin: 0 }}>
                  Kết quả xử lý ({results.length} file)
                </Title>
                {submittedCount > 0 && (
                  <Text type="secondary">
                    {submittedCount}/{results.length} đã gửi duyệt
                  </Text>
                )}
              </div>
              <Space wrap>
                {!isProcessing && results.length > 0 && (
                  <>
                    {results.length === 1 && results[0].invoiceId && (
                      <Button
                        type="primary"
                        icon={<EyeOutlined />}
                        onClick={() =>
                          handleOpenInvoiceDetail(results[0].invoiceId!)
                        }
                      >
                        Xem chi tiết hóa đơn
                      </Button>
                    )}
                    {results.length > 1 && (
                      <Button
                        icon={<EyeOutlined />}
                        onClick={() =>
                          handleSafeNavigation(
                            "/app/invoices?status=Draft&sort=newest",
                          )
                        }
                      >
                        Xem hóa đơn đã tải ({results.length})
                      </Button>
                    )}
                  </>
                )}
                {selectedGreenCount > 0 && (
                  <Button
                    type="primary"
                    icon={<CheckSquareOutlined />}
                    loading={isBatchSubmitting}
                    onClick={handleBatchSubmit}
                  >
                    Gửi duyệt (Đã chọn: {selectedGreenCount})
                  </Button>
                )}
                {submittedCount > 0 && (
                  <Button
                    type="default"
                    icon={<CheckCircleOutlined />}
                    onClick={() => handleSafeNavigation("/app/invoices")}
                  >
                    Xem danh sách hóa đơn
                  </Button>
                )}
                <Button onClick={handleReset} icon={<CloudUploadOutlined />}>
                  Tải lên file khác
                </Button>
              </Space>
            </div>

            <Table<ProcessResult>
              dataSource={results}
              columns={columns}
              rowKey="fileName"
              pagination={false}
              size="middle"
              bordered
              scroll={{ x: 800 }}
              rowSelection={rowSelection}
              expandable={{
                expandedRowKeys: Array.from(expandedRows),
                onExpandedRowsChange: (keys) => {
                  setExpandedRows(new Set(keys as string[]));
                },
                expandedRowRender: (record) => {
                  if (!record.result) {
                    return (
                      <Text type="secondary">Không có kết quả kiểm tra</Text>
                    );
                  }
                  return (
                    <div style={{ background: "#fafbfc", padding: "16px" }}>
                      <BusinessValidationSummary result={record.result} />
                    </div>
                  );
                },
                columnWidth: 50,
              }}
              rowClassName={(record) => {
                if (record.submitStatus === "submitted") return "row-submitted";
                if (record.status === "error") return "row-error";
                return "";
              }}
            />
          </div>
        )}
      </Card>

      <Modal
        title={
          <Space>
            <WarningOutlined style={{ color: "#faad14" }} />
            <span>Gửi duyệt hóa đơn cảnh báo</span>
          </Space>
        }
        open={commentModalVisible}
        onOk={confirmSubmitWithComment}
        onCancel={() => setCommentModalVisible(false)}
        okText="Xác nhận gửi duyệt"
        cancelText="Hủy"
      >
        <Paragraph type="secondary">
          Hóa đơn này có cảnh báo rủi ro (ví dụ: không có MST người mua). Vui
          lòng nhập lý do và giải trình để Admin xét duyệt.
        </Paragraph>
        <Input.TextArea
          rows={3}
          placeholder="Ví dụ: Hóa đơn đổ xăng công tác tháng 3"
          value={submitComment}
          onChange={(e) => setSubmitComment(e.target.value)}
        />
      </Modal>

      <LeaveUploadModal
        open={leaveModalVisible}
        onConfirm={handleProceedNavigation}
        onCancel={() => {
          setLeaveModalVisible(false);
          setPendingNavigation(null);
        }}
      />

      <style>{`
        .ant-table .row-submitted {
          background-color: #f6ffed !important;
        }
        .ant-table .row-submitted:hover > td {
          background-color: #edfbf5 !important;
        }
        .ant-table .row-error {
          background-color: #fff2f0 !important;
        }
        .ant-table .row-error:hover > td {
          background-color: #ffe7e6 !important;
        }
      `}</style>
    </div>
  );
};

export default UploadInvoice;
