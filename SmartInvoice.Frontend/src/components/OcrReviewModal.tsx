import React, { useState, useEffect } from "react";
import {
  SaveOutlined,
  EyeOutlined,
  EditOutlined,
  DeleteOutlined,
  PlusOutlined,
  AlertOutlined,
} from "@ant-design/icons";
import {
  Modal,
  Row,
  Col,
  Form,
  Input,
  InputNumber,
  DatePicker,
  Table,
  Button,
  Space,
  message,
  Divider,
  Spin,
  Alert,
} from "antd";
import dayjs from "dayjs";
import {
  invoiceService,
  InvoiceDetailDto,
  UpdateInvoiceDto,
  LineItemDto,
} from "../services/invoice";

interface OcrReviewModalProps {
  visible: boolean;
  invoiceId: string;
  onClose: () => void;
  onSaveSuccess: () => void;
}

const OcrReviewModal: React.FC<OcrReviewModalProps> = ({
  visible,
  invoiceId,
  onClose,
  onSaveSuccess,
}) => {
  const [form] = Form.useForm();
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [visualUrl, setVisualUrl] = useState<string | null>(null);
  const [lineItems, setLineItems] = useState<LineItemDto[]>([]);

  const [validationErrors, setValidationErrors] = useState<
    { message: string; type: "error" | "warning" }[]
  >([]);
  const [activeTab, setActiveTab] = useState("data");

  useEffect(() => {
    if (visible && invoiceId) {
      fetchData();
    }
  }, [visible, invoiceId]);

  const fetchData = async () => {
    setLoading(true);
    try {
      // Fetch main invoice detail first (required)
      const detail = await invoiceService.getInvoiceDetail(invoiceId);

      // Try fetching visual URL separately so a 404 doesn't crash the modal
      let url = null;
      try {
        const urlRes = await invoiceService.getVisualFileUrl(invoiceId);
        url = urlRes.url;
      } catch (urlErr) {
        console.warn(
          "Could not fetch visual URL, it might not exist or OCR failed to save it:",
          urlErr,
        );
      }

      setVisualUrl(url);
      setLineItems(detail.lineItems || []);

      // Handle validation errors from layers and risk checks
      const errors: { message: string; type: "error" | "warning" }[] = [];
      detail.validationLayers?.forEach((layer) => {
        if (!layer.isValid && layer.errorMessage) {
          errors.push({ message: layer.errorMessage, type: "error" });
        }
      });
      detail.riskChecks?.forEach((risk) => {
        if (risk.riskLevel === "Red" && risk.errorMessage) {
          errors.push({ message: risk.errorMessage, type: "error" });
        } else if (risk.riskLevel === "Yellow" && risk.errorMessage) {
          errors.push({ message: risk.errorMessage, type: "error" });
        }
      });
      setValidationErrors(errors);

      // Fill form
      form.setFieldsValue({
        invoiceNumber: detail.invoiceNumber,
        serialNumber: detail.serialNumber,
        formNumber: detail.formNumber,
        invoiceDate: detail.invoiceDate ? dayjs(detail.invoiceDate) : null,
        sellerName: detail.sellerName,
        sellerTaxCode: detail.sellerTaxCode,
        sellerAddress: detail.sellerAddress,
        buyerName: detail.buyerName,
        buyerTaxCode: detail.buyerTaxCode,
        buyerAddress: detail.buyerAddress,
        totalAmountBeforeTax: detail.totalAmountBeforeTax,
        totalTaxAmount: detail.totalTaxAmount,
        totalAmount: detail.totalAmount,
        notes: detail.notes,
      });
    } catch (error) {
      console.error("Error fetching invoice detail:", error);
      message.error("Không thể lấy thông tin chi tiết hóa đơn.");
    } finally {
      setLoading(false);
    }
  };

  const handleSave = async (values: any) => {
    setSaving(true);
    try {
      const updateData: UpdateInvoiceDto = {
        ...values,
        invoiceDate: values.invoiceDate?.toISOString(),
        lineItems: lineItems,
      };

      await invoiceService.updateInvoice(invoiceId, updateData);
      message.success("Cập nhật hóa đơn thành công!");
      onSaveSuccess();
      onClose();
    } catch (error) {
      console.error("Error updating invoice:", error);
      message.error("Cập nhật hóa đơn thất bại.");
    } finally {
      setSaving(false);
    }
  };

  const columns = [
    {
      title: "STT",
      dataIndex: "lineNumber",
      width: 60,
    },
    {
      title: "Tên hàng hóa/dịch vụ",
      dataIndex: "itemName",
      render: (text: string, _record: LineItemDto, index: number) => (
        <Input
          value={text}
          onChange={(e) => updateItem(index, "itemName", e.target.value)}
        />
      ),
    },
    {
      title: "ĐVT",
      dataIndex: "unit",
      width: 80,
      render: (text: string, _record: LineItemDto, index: number) => (
        <Input
          value={text}
          onChange={(e) => updateItem(index, "unit", e.target.value)}
        />
      ),
    },
    {
      title: "Số lượng",
      dataIndex: "quantity",
      width: 100,
      render: (val: number, _record: LineItemDto, index: number) => (
        <InputNumber
          value={val}
          onChange={(v) => updateItem(index, "quantity", v)}
          style={{ width: "100%" }}
        />
      ),
    },
    {
      title: "Đơn giá",
      dataIndex: "unitPrice",
      width: 120,
      render: (val: number, _record: LineItemDto, index: number) => (
        <InputNumber
          value={val}
          onChange={(v) => updateItem(index, "unitPrice", v)}
          style={{ width: "100%" }}
        />
      ),
    },
    {
      title: "Thành tiền",
      dataIndex: "totalAmount",
      width: 130,
      render: (val: number, _record: LineItemDto, index: number) => (
        <InputNumber
          value={val}
          onChange={(v) => updateItem(index, "totalAmount", v)}
          style={{ width: "100%" }}
        />
      ),
    },
    {
      title: "Thuế (%)",
      dataIndex: "vatRate",
      width: 80,
      render: (val: number, _record: LineItemDto, index: number) => (
        <InputNumber
          value={val}
          onChange={(v) => updateItem(index, "vatRate", v || 0)}
          style={{ width: "100%" }}
        />
      ),
    },
    {
      title: "Hành động",
      key: "action",
      width: 80,
      render: (_: any, __: any, index: number) => (
        <Button
          type="text"
          danger
          icon={<DeleteOutlined />}
          onClick={() => deleteItem(index)}
        />
      ),
    },
  ];

  const updateItem = (index: number, field: keyof LineItemDto, value: any) => {
    const newData = [...lineItems];
    const item = { ...newData[index], [field]: value };

    // Auto-calculate line total if quantity or price changed
    if (
      field === "quantity" ||
      field === "unitPrice" ||
      field === "totalAmount" ||
      field === "vatRate"
    ) {
      if (field !== "totalAmount") {
        item.totalAmount = (item.quantity || 0) * (item.unitPrice || 0);
      }
      // Calculate VAT Amount for this line
      item.vatAmount = Math.round(
        ((item.totalAmount || 0) * (item.vatRate || 0)) / 100,
      );
    }

    newData[index] = item;
    setLineItems(newData);

    // Auto-calculate form totals
    const sumBeforeTax = newData.reduce(
      (sum, it) => sum + (it.totalAmount || 0),
      0,
    );
    const sumTax = newData.reduce((sum, it) => sum + (it.vatAmount || 0), 0);

    form.setFieldsValue({
      totalAmountBeforeTax: sumBeforeTax,
      totalTaxAmount: sumTax,
      totalAmount: sumBeforeTax + sumTax,
    });
  };

  const addItem = () => {
    const newLine: LineItemDto = {
      lineNumber: lineItems.length + 1,
      itemName: "",
      unit: "",
      quantity: 0,
      unitPrice: 0,
      totalAmount: 0,
      vatRate: 10,
      vatAmount: 0,
    };
    setLineItems([...lineItems, newLine]);
  };

  const deleteItem = (index: number) => {
    const newData = lineItems.filter((_, i) => i !== index);
    // Re-number
    const renumbered = newData.map((it, i) => ({ ...it, lineNumber: i + 1 }));
    setLineItems(renumbered);

    // Recalculate totals
    const sumBeforeTax = renumbered.reduce(
      (sum, it) => sum + (it.totalAmount || 0),
      0,
    );
    const sumTax = renumbered.reduce((sum, it) => sum + (it.vatAmount || 0), 0);

    form.setFieldsValue({
      totalAmountBeforeTax: sumBeforeTax,
      totalTaxAmount: sumTax,
      totalAmount: sumBeforeTax + sumTax,
    });
  };

  return (
    <Modal
      title={
        <Space>
          <EditOutlined /> Soát lỗi & Chỉnh sửa kết quả OCR
        </Space>
      }
      open={visible}
      onCancel={onClose}
      width={1200}
      style={{ top: 20 }}
      footer={[
        <Button key="cancel" onClick={onClose}>
          Hủy bỏ
        </Button>,
        <Button
          key="save"
          type="primary"
          icon={<SaveOutlined />}
          loading={saving}
          onClick={() => form.submit()}
        >
          Lưu thay đổi
        </Button>,
      ]}
      mask={{ closable: false }}
      destroyOnHidden
    >
      <Spin spinning={loading}>
        <Row gutter={24}>
          {/* View File Side */}
          <Col span={11}>
            <div
              style={{
                height: "calc(100vh - 250px)",
                border: "1px solid #d9d9d9",
                borderRadius: 4,
                overflow: "hidden",
                background: "#f5f5f5",
              }}
            >
              {visualUrl ? (
                visualUrl.toLowerCase().includes(".pdf") ? (
                  <iframe
                    src={`${visualUrl}#toolbar=0`}
                    width="100%"
                    height="100%"
                    title="Invoice Viewer"
                    style={{ border: "none" }}
                  />
                ) : (
                  <img
                    src={visualUrl}
                    alt="Invoice Viewer"
                    style={{
                      width: "100%",
                      height: "100%",
                      objectFit: "contain",
                    }}
                  />
                )
              ) : (
                <div
                  style={{
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                    height: "100%",
                  }}
                >
                  Không tìm thấy dữ liệu trực quan.
                </div>
              )}
            </div>
            <div style={{ marginTop: 8, textAlign: "center" }}>
              <Button
                type="link"
                icon={<EyeOutlined />}
                onClick={() => window.open(visualUrl || "", "_blank")}
              >
                Xem toàn màn hình
              </Button>
            </div>
          </Col>

          {/* Edit Form Side */}
          <Col span={13}>
            {validationErrors.length > 0 && (
              <div style={{ marginBottom: 16 }}>
                <Alert
                  message="Phát hiện sai sót từ OCR / Hệ thống"
                  description={
                    <ul style={{ paddingLeft: 20, margin: 0 }}>
                      {validationErrors.map((err, i) => (
                        <li key={i}>{err.message}</li>
                      ))}
                    </ul>
                  }
                  type="error"
                  showIcon
                  icon={<AlertOutlined />}
                />
              </div>
            )}
            <div
              style={{
                height: "calc(100vh - 350px)",
                overflowY: "auto",
                paddingRight: 12,
              }}
            >
              <Form form={form} layout="vertical" onFinish={handleSave}>
                <Divider style={{ margin: "0 0 16px", fontWeight: "bold" }}>
                  Thông tin chung
                </Divider>
                <Row gutter={12}>
                  <Col span={8}>
                    <Form.Item
                      label="Số hóa đơn"
                      name="invoiceNumber"
                      rules={[{ required: true }]}
                    >
                      <Input />
                    </Form.Item>
                  </Col>
                  <Col span={8}>
                    <Form.Item label="Ký hiệu" name="serialNumber">
                      <Input />
                    </Form.Item>
                  </Col>
                  <Col span={8}>
                    <Form.Item
                      label="Ngày hóa đơn"
                      name="invoiceDate"
                      rules={[{ required: true }]}
                    >
                      <DatePicker
                        style={{ width: "100%" }}
                        format="DD/MM/YYYY"
                      />
                    </Form.Item>
                  </Col>
                </Row>

                <Divider style={{ margin: "16px 0", fontWeight: "bold" }}>
                  Bên bán (Seller)
                </Divider>
                <Form.Item label="Tên đơn vị bán" name="sellerName">
                  <Input />
                </Form.Item>
                <Row gutter={12}>
                  <Col span={8}>
                    <Form.Item label="Mã số thuế" name="sellerTaxCode">
                      <Input />
                    </Form.Item>
                  </Col>
                  <Col span={16}>
                    <Form.Item label="Địa chỉ" name="sellerAddress">
                      <Input />
                    </Form.Item>
                  </Col>
                </Row>

                <Divider style={{ margin: "16px 0", fontWeight: "bold" }}>
                  Bên mua (Buyer)
                </Divider>
                <Form.Item label="Tên đơn vị mua" name="buyerName">
                  <Input />
                </Form.Item>
                <Row gutter={12}>
                  <Col span={8}>
                    <Form.Item label="Mã số thuế" name="buyerTaxCode">
                      <Input />
                    </Form.Item>
                  </Col>
                  <Col span={16}>
                    <Form.Item label="Địa chỉ" name="buyerAddress">
                      <Input />
                    </Form.Item>
                  </Col>
                </Row>

                <Divider style={{ margin: "16px 0", fontWeight: "bold" }}>
                  Chi tiết hàng hóa
                </Divider>
                <Table
                  dataSource={lineItems}
                  columns={columns}
                  pagination={false}
                  size="small"
                  rowKey="lineNumber"
                  scroll={{ x: 600 }}
                />
                <Button
                  type="dashed"
                  onClick={addItem}
                  block
                  icon={<PlusOutlined />}
                  style={{ marginTop: 8 }}
                >
                  Thêm dòng mới
                </Button>

                <Divider style={{ margin: "24px 0 16px", fontWeight: "bold" }}>
                  Tổng cộng
                </Divider>
                <Row gutter={12}>
                  <Col span={8}>
                    <Form.Item label="Chưa thuế" name="totalAmountBeforeTax">
                      <InputNumber
                        style={{ width: "100%" }}
                        formatter={(value) =>
                          `${value}`.replace(/\B(?=(\d{3})+(?!\d))/g, ",")
                        }
                      />
                    </Form.Item>
                  </Col>
                  <Col span={8}>
                    <Form.Item label="Tiền thuế" name="totalTaxAmount">
                      <InputNumber
                        style={{ width: "100%" }}
                        formatter={(value) =>
                          `${value}`.replace(/\B(?=(\d{3})+(?!\d))/g, ",")
                        }
                      />
                    </Form.Item>
                  </Col>
                  <Col span={8}>
                    <Form.Item
                      label="Tổng thanh toán"
                      name="totalAmount"
                      rules={[{ required: true }]}
                    >
                      <InputNumber
                        style={{ width: "100%" }}
                        formatter={(value) =>
                          `${value}`.replace(/\B(?=(\d{3})+(?!\d))/g, ",")
                        }
                      />
                    </Form.Item>
                  </Col>
                </Row>

                <Form.Item label="Ghi chú" name="notes">
                  <Input.TextArea rows={2} />
                </Form.Item>
              </Form>
            </div>
          </Col>
        </Row>
      </Spin>
    </Modal>
  );
};

export default OcrReviewModal;
