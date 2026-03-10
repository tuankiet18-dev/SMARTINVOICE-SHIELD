import React, { useState } from "react";
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
  Drawer,
  Descriptions,
  Table,
  Input,
  InputNumber,
  Tooltip,
  Modal,
  Checkbox,
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
} from "@ant-design/icons";
import { invoiceService, ValidationResult } from "../services/invoice";
import { useNavigate } from "react-router-dom";

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
  const [drawerVisible, setDrawerVisible] = useState(false);
  const [selectedInvoice, setSelectedInvoice] = useState<ProcessResult | null>(
    null,
  );
  const [isBatchSubmitting, setIsBatchSubmitting] = useState(false);
  const [commentModalVisible, setCommentModalVisible] = useState(false);
  const [pendingSubmitId, setPendingSubmitId] = useState<string | null>(null);
  const [submitComment, setSubmitComment] = useState("");

  const getDefaultSelected = (res: ProcessResult[]) =>
    res
      .filter(
        (r) =>
          r.invoiceId && r.status === "success" && r.submitStatus === "idle",
      )
      .map((r) => r.fileName);

  const [selectedRowKeys, setSelectedRowKeys] = useState<React.Key[]>([]);

  const handleViewDetails = (item: ProcessResult) => {
    setSelectedInvoice(item);
    setDrawerVisible(true);
  };

  const uploadProps = {
    name: "file",
    multiple: true,
    accept: ".xml,.pdf,.jpg,.jpeg,.png",
    fileList,
    onChange(info: any) {
      setFileList(info.fileList);
    },
    beforeUpload: () => false,
    showUploadList: false,
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

        if (!fileObj.name.toLowerCase().endsWith(".xml")) {
          setResults((prev) =>
            prev.map((item, idx) =>
              idx === i
                ? {
                    ...item,
                    status: "warning",
                    errorMessage:
                      "File PDF/Ảnh đang trong quá trình thử nghiệm OCR, cần kiểm tra thủ công.",
                  }
                : item,
            ),
          );
          continue;
        }

        try {
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

    return (
      <Space size={8} wrap>
        {record.status !== "pending" && record.status !== "processing" && (
          <Tooltip title="Xem chi tiết hóa đơn">
            <Button
              size="small"
              type="text"
              icon={<EyeOutlined style={{ color: "#1677ff" }} />}
              onClick={() => handleViewDetails(record)}
            />
          </Tooltip>
        )}

        {submitStatus === "submitted" && (
          <Text type="success" style={{ fontSize: 13 }}>
            <CheckCircleOutlined /> Đã gửi duyệt
          </Text>
        )}

        {isSubmittable && !isYellow && submitStatus === "idle" && (
          <Tooltip title="Gửi hóa đơn chờ Admin duyệt">
            <Button
              size="small"
              type="primary"
              ghost
              icon={<SendOutlined />}
              onClick={() => handleSingleSubmit(record)}
            >
              Gửi duyệt
            </Button>
          </Tooltip>
        )}

        {isYellow && submitStatus === "idle" && (
          <Tooltip title="Hóa đơn có cảnh báo — cần nhập giải trình trước khi gửi duyệt">
            <Button
              size="small"
              icon={<SendOutlined />}
              style={{
                borderColor: "#faad14",
                color: "#d48806",
                background: "#fffbe6",
                fontSize: 13,
              }}
              onClick={() => openSubmitWithComment(record)}
            >
              Giải trình &amp; Gửi
            </Button>
          </Tooltip>
        )}

        {submitStatus === "submitting" && (
          <Text type="secondary" style={{ fontSize: 13 }}>
            <LoadingOutlined /> Đang gửi...
          </Text>
        )}

        {submitStatus === "failed" && (
          <Tooltip title={`Lỗi gửi duyệt: ${record.submitError}`}>
            <Button
              size="small"
              danger
              type="dashed"
              icon={<SendOutlined />}
              onClick={() =>
                isYellow
                  ? openSubmitWithComment(record)
                  : handleSingleSubmit(record)
              }
            >
              Gửi lại
            </Button>
          </Tooltip>
        )}

        {record.status === "error" && submitStatus !== "submitted" && (
          <Tooltip title="Ẩn hóa đơn lỗi này khỏi danh sách">
            <Button
              size="small"
              type="text"
              danger
              icon={<DeleteOutlined />}
              onClick={() => handleDismiss(record)}
            />
          </Tooltip>
        )}
      </Space>
    );
  };

  const columns = [
    {
      title: "Tên File",
      dataIndex: "fileName",
      key: "fileName",
      width: 150,
      ellipsis: true,
      render: (text: string) => (
        <Text strong style={{ fontSize: 13 }}>
          {text}
        </Text>
      ),
    },
    {
      title: "Trạng thái",
      key: "status",
      width: 160,
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
                    {errorCode && (
                      <div style={{ marginBottom: 4 }}>
                        <Tag color="error">{errorCode}</Tag>
                      </div>
                    )}
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
                  {errorCode && (
                    <Text type="danger" strong style={{ marginRight: 4 }}>
                      [{errorCode}]
                    </Text>
                  )}
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
                    {errorCode && (
                      <div style={{ marginBottom: 4 }}>
                        <Tag color="warning">{errorCode}</Tag>
                      </div>
                    )}
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
                  {errorCode && (
                    <Text strong style={{ marginRight: 4, color: "#d48806" }}>
                      [{errorCode}]
                    </Text>
                  )}
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
      width: 240,
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
                  Hỗ trợ định dạng: XML, PDF, JPG, PNG.{" "}
                  <Text strong>Tối đa 10MB/file.</Text>
                </p>

                {fileList.length === 0 && (
                  <>
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
                  </>
                )}
              </Dragger>
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
                    onClick={() => navigate("/app/invoices")}
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

      <Drawer
        title={
          <Space>
            {renderStatusTag(
              selectedInvoice?.status || "",
              selectedInvoice?.result,
            )}
            <Text strong>{selectedInvoice?.fileName}</Text>
          </Space>
        }
        width="95%"
        onClose={() => setDrawerVisible(false)}
        open={drawerVisible}
        bodyStyle={{ padding: "16px 24px", background: "#f5f5f5" }}
        extra={
          <Space>
            <Button onClick={() => setDrawerVisible(false)}>Đóng</Button>
            {selectedInvoice?.invoiceId &&
              (selectedInvoice.status === "success" ||
                selectedInvoice.status === "warning") &&
              selectedInvoice.submitStatus === "idle" && (
                <Button
                  type="primary"
                  icon={<SendOutlined />}
                  onClick={() => {
                    setDrawerVisible(false);
                    if (selectedInvoice.status === "warning")
                      openSubmitWithComment(selectedInvoice);
                    else handleSingleSubmit(selectedInvoice);
                  }}
                >
                  Gửi duyệt
                </Button>
              )}
          </Space>
        }
      >
        <Row gutter={24} style={{ height: "100%" }}>
          <Col
            span={12}
            style={{ height: "100%", display: "flex", flexDirection: "column" }}
          >
            <div
              style={{
                background: "#fff",
                padding: 16,
                borderRadius: 8,
                height: "100%",
                border: "1px solid #d9d9d9",
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
              }}
            >
              <Space direction="vertical" align="center">
                <FilePdfOutlined style={{ fontSize: 64, color: "#bfbfbf" }} />
                <Text type="secondary">
                  Khu vực View bản thể hiện PDF / Ảnh gốc
                </Text>
                <Text type="secondary" style={{ fontSize: 12 }}>
                  (Tích hợp thư viện react-pdf ở đây)
                </Text>
              </Space>
            </div>
          </Col>

          <Col span={12}>
            <Card
              size="small"
              title="Thông tin chung"
              style={{ marginBottom: 16, borderRadius: 8 }}
            >
              {selectedInvoice?.result?.errorDetails?.length ? (
                <Alert
                  message="Lỗi hệ thống / Nghiệp vụ"
                  description={
                    <div
                      style={{
                        display: "flex",
                        flexDirection: "column",
                        gap: 8,
                        marginTop: 4,
                      }}
                    >
                      {selectedInvoice.result.errorDetails.map((e, i) => (
                        <div
                          key={i}
                          style={{
                            padding: 8,
                            background: "#fff",
                            borderRadius: 4,
                            border: "1px solid #ffa39e",
                          }}
                        >
                          {e.errorCode && (
                            <Tag color="error" style={{ marginBottom: 4 }}>
                              {e.errorCode}
                            </Tag>
                          )}
                          <div>
                            <Text type="danger">{e.errorMessage}</Text>
                          </div>
                          {e.suggestion && (
                            <div>
                              <Text
                                type="secondary"
                                italic
                                style={{ fontSize: 13 }}
                              >
                                💡 {e.suggestion}
                              </Text>
                            </div>
                          )}
                        </div>
                      ))}
                    </div>
                  }
                  type="error"
                  showIcon
                  style={{ marginBottom: 8 }}
                />
              ) : selectedInvoice?.result?.errors?.length ? (
                <Alert
                  message="Lỗi hệ thống / Nghiệp vụ"
                  description={
                    <ul style={{ paddingLeft: 16, margin: 0 }}>
                      {selectedInvoice.result.errors.map((e, i) => (
                        <li key={i}>{e}</li>
                      ))}
                    </ul>
                  }
                  type="error"
                  showIcon
                  style={{ marginBottom: 8 }}
                />
              ) : null}

              {selectedInvoice?.result?.warningDetails?.length ? (
                <Alert
                  message="Cảnh báo rủi ro"
                  description={
                    <div
                      style={{
                        display: "flex",
                        flexDirection: "column",
                        gap: 8,
                        marginTop: 4,
                      }}
                    >
                      {selectedInvoice.result.warningDetails.map((w, i) => (
                        <div
                          key={i}
                          style={{
                            padding: 8,
                            background: "#fff",
                            borderRadius: 4,
                            border: "1px solid #ffe58f",
                          }}
                        >
                          {w.errorCode && (
                            <Tag color="warning" style={{ marginBottom: 4 }}>
                              {w.errorCode}
                            </Tag>
                          )}
                          <div>
                            <Text style={{ color: "#d48806" }}>
                              {w.errorMessage}
                            </Text>
                          </div>
                          {w.suggestion && (
                            <div>
                              <Text
                                type="secondary"
                                italic
                                style={{ fontSize: 13 }}
                              >
                                💡 {w.suggestion}
                              </Text>
                            </div>
                          )}
                        </div>
                      ))}
                    </div>
                  }
                  type="warning"
                  showIcon
                  style={{ marginBottom: 16 }}
                />
              ) : selectedInvoice?.result?.warnings?.length ? (
                <Alert
                  message="Cảnh báo rủi ro"
                  description={
                    <ul style={{ paddingLeft: 16, margin: 0 }}>
                      {selectedInvoice.result.warnings.map((w, i) => (
                        <li key={i}>{w}</li>
                      ))}
                    </ul>
                  }
                  type="warning"
                  showIcon
                  style={{ marginBottom: 16 }}
                />
              ) : null}
              <Descriptions column={2} size="small" bordered>
                <Descriptions.Item label="Người bán" span={2}>
                  <Text strong>
                    {selectedInvoice?.result?.extractedData?.seller_name ||
                      selectedInvoice?.result?.signerSubject
                        ?.split("CN=")[1]
                        ?.split(",")[0] ||
                      "Chưa trích xuất được"}
                  </Text>
                </Descriptions.Item>
                <Descriptions.Item label="Mã Số Thuế">
                  {selectedInvoice?.result?.extractedData?.seller_tax_code ||
                    "Chưa có"}
                </Descriptions.Item>
                <Descriptions.Item label="Ngày lập">
                  {selectedInvoice?.result?.extractedData?.invoice_date
                    ? new Date(
                        selectedInvoice.result.extractedData.invoice_date,
                      ).toLocaleDateString("vi-VN")
                    : "Chưa có"}
                </Descriptions.Item>
                <Descriptions.Item label="Mẫu số">
                  {selectedInvoice?.result?.extractedData
                    ?.invoice_template_code || "Chưa có"}
                </Descriptions.Item>
                <Descriptions.Item label="Ký hiệu">
                  {selectedInvoice?.result?.extractedData?.invoice_symbol ||
                    "Chưa có"}
                </Descriptions.Item>
                <Descriptions.Item label="Số hóa đơn">
                  {selectedInvoice?.result?.extractedData?.invoice_number ||
                    "Chưa có"}
                </Descriptions.Item>
              </Descriptions>
            </Card>

            <Card
              size="small"
              title="Chi tiết hàng hóa"
              style={{ borderRadius: 8 }}
            >
              {selectedInvoice?.result?.extractedData?.line_items ? (
                <Table
                  dataSource={selectedInvoice.result.extractedData.line_items}
                  rowKey={(record) =>
                    `${selectedInvoice.fileName}_${record.stt}`
                  }
                  pagination={false}
                  size="small"
                  scroll={{ y: 300 }}
                  columns={[
                    {
                      title: "Tên hàng",
                      dataIndex: "product_name",
                      width: "35%",
                      render: (val) =>
                        (
                          <Input defaultValue={val} style={{ width: "100%" }} />
                        ) as any,
                    },
                    {
                      title: "ĐVT",
                      dataIndex: "unit",
                      width: "10%",
                      render: (val) =>
                        (
                          <Input
                            defaultValue={val || ""}
                            size="small"
                            style={{ width: "100%" }}
                          />
                        ) as any,
                    },
                    {
                      title: "SL",
                      dataIndex: "quantity",
                      width: "10%",
                      render: (val) => (
                        <InputNumber
                          defaultValue={val}
                          size="small"
                          style={{ width: "100%" }}
                        />
                      ),
                    },
                    {
                      title: "Đơn giá",
                      dataIndex: "unit_price",
                      width: "15%",
                      render: (val) => (
                        <InputNumber
                          defaultValue={val}
                          size="small"
                          style={{ width: "100%" }}
                          formatter={(value) =>
                            `${value}`.replace(/\B(?=(\d{3})+(?!\d))/g, ",")
                          }
                        />
                      ),
                    },
                    {
                      title: "Thuế",
                      dataIndex: "vat_rate",
                      width: "10%",
                      align: "center",
                      render: (val) => <Text>{val}%</Text>,
                    },
                    {
                      title: "Thành tiền",
                      dataIndex: "total_amount",
                      width: "20%",
                      align: "right",
                      render: (val) => (
                        <Text strong>{val?.toLocaleString()}</Text>
                      ),
                    },
                  ]}
                  summary={(pageData) => {
                    let total = 0;
                    pageData.forEach(({ total_amount }) => {
                      total += total_amount || 0;
                    });
                    return (
                      <Table.Summary.Row style={{ background: "#fafafa" }}>
                        <Table.Summary.Cell index={0} colSpan={5}>
                          <Text
                            strong
                            style={{ float: "right", paddingRight: 16 }}
                          >
                            Tổng cộng:
                          </Text>
                        </Table.Summary.Cell>
                        <Table.Summary.Cell index={1}>
                          <Space
                            direction="vertical"
                            size={2}
                            style={{ width: "100%", textAlign: "right" }}
                          >
                            {selectedInvoice?.result?.extractedData
                              ?.total_pre_tax !== undefined && (
                              <Text type="secondary">
                                Cộng tiền hàng:{" "}
                                {selectedInvoice.result.extractedData.total_pre_tax.toLocaleString()}{" "}
                                ₫
                              </Text>
                            )}
                            {selectedInvoice?.result?.extractedData
                              ?.total_tax_amount !== undefined && (
                              <Text type="secondary">
                                Tiền thuế:{" "}
                                {selectedInvoice.result.extractedData.total_tax_amount.toLocaleString()}{" "}
                                ₫
                              </Text>
                            )}
                            <Text strong>
                              Tổng cộng:{" "}
                              {selectedInvoice?.result?.extractedData?.total_amount?.toLocaleString() ||
                                total.toLocaleString()}{" "}
                              ₫
                            </Text>
                          </Space>
                        </Table.Summary.Cell>
                      </Table.Summary.Row>
                    );
                  }}
                />
              ) : (
                <Result status="info" title="Chưa có dữ liệu hàng hóa" />
              )}
            </Card>
          </Col>
        </Row>
      </Drawer>
    </div>
  );
};

export default UploadInvoice;
