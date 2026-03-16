import React, { useState } from "react";
import {
  Card,
  Tabs,
  Table,
  Button,
  Typography,
  Space,
  Tag,
  Modal,
  Form,
  Input,
  InputNumber,
  Switch,
  message,
  Popconfirm,
  Descriptions,
  Tooltip,
  Divider,
} from "antd";
import {
  PlusOutlined,
  EditOutlined,
  DeleteOutlined,
  SaveOutlined,
  ReloadOutlined,
  SettingOutlined,
  CrownOutlined,
  ToolOutlined,
  InfoCircleOutlined,
} from "@ant-design/icons";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  subscriptionPackageService,
  SubscriptionPackage,
  CreateSubscriptionPackage,
} from "../services/subscriptionPackageService";
import {
  systemConfigService,
  SystemConfig as SystemConfigModel,
} from "../services/systemConfigService";

const { Title, Text } = Typography;

const SystemConfig: React.FC = () => {
  const [activeTab, setActiveTab] = useState("packages");
  const queryClient = useQueryClient();

  // --- Subscription Packages Logic ---
  const [isPackageModalVisible, setIsPackageModalVisible] = useState(false);
  const [editingPackage, setEditingPackage] =
    useState<SubscriptionPackage | null>(null);
  const [packageForm] = Form.useForm();

  const { data: packages, isLoading: isPackagesLoading } = useQuery({
    queryKey: ["subscription-packages"],
    queryFn: () => subscriptionPackageService.getAll(),
  });

  const createPackageMutation = useMutation({
    mutationFn: (data: CreateSubscriptionPackage) =>
      subscriptionPackageService.create(data),
    onSuccess: () => {
      message.success("Đã tạo gói cước mới thành công");
      queryClient.invalidateQueries({ queryKey: ["subscription-packages"] });
      setIsPackageModalVisible(false);
      packageForm.resetFields();
    },
    onError: (error: any) => {
      message.error(
        error.response?.data?.message || "Có lỗi xảy ra khi tạo gói cước",
      );
    },
  });

  const updatePackageMutation = useMutation({
    mutationFn: ({ id, data }: { id: string; data: any }) =>
      subscriptionPackageService.update(id, data),
    onSuccess: () => {
      message.success("Đã cập nhật gói cước thành công");
      queryClient.invalidateQueries({ queryKey: ["subscription-packages"] });
      setIsPackageModalVisible(false);
      setEditingPackage(null);
      packageForm.resetFields();
    },
    onError: (error: any) => {
      message.error(
        error.response?.data?.message || "Có lỗi xảy ra khi cập nhật gói cước",
      );
    },
  });

  const deletePackageMutation = useMutation({
    mutationFn: (id: string) => subscriptionPackageService.delete(id),
    onSuccess: () => {
      message.success("Đã vô hiệu hóa gói cước thành công");
      queryClient.invalidateQueries({ queryKey: ["subscription-packages"] });
    },
    onError: (error: any) => {
      message.error(error.response?.data?.message || "Có lỗi xảy ra");
    },
  });

  const handlePackageSubmit = (values: any) => {
    if (editingPackage) {
      updatePackageMutation.mutate({
        id: editingPackage.packageId,
        data: values,
      });
    } else {
      createPackageMutation.mutate(values);
    }
  };

  const openEditModal = (pkg: SubscriptionPackage) => {
    setEditingPackage(pkg);
    packageForm.setFieldsValue(pkg);
    setIsPackageModalVisible(true);
  };

  // --- System Config Logic ---
  const { data: configs, isLoading: isConfigsLoading } = useQuery({
    queryKey: ["system-configs"],
    queryFn: () => systemConfigService.getAll(),
  });

  const updateConfigMutation = useMutation({
    mutationFn: ({ key, value }: { key: string; value: string }) =>
      systemConfigService.update(key, { configValue: value }),
    onSuccess: (res) => {
      message.success(res.message);
      if (res.requiresRestart) {
        Modal.warning({
          title: "Yêu cầu khởi chạy lại",
          content:
            "Thay đổi này yêu cầu dịch vụ cần được khởi động lại để có hiệu lực.",
        });
      }
      queryClient.invalidateQueries({ queryKey: ["system-configs"] });
    },
    onError: (error: any) => {
      message.error(error.response?.data?.message || "Lỗi cập nhật cấu hình");
    },
  });

  const handleConfigUpdate = (key: string, value: string) => {
    updateConfigMutation.mutate({ key, value });
  };

  // --- Renderers ---
  const packageColumns = [
    {
      title: "Mã gói",
      dataIndex: "packageCode",
      key: "packageCode",
      render: (code: string) => <Tag color="blue">{code}</Tag>,
    },
    {
      title: "Tên gói",
      dataIndex: "packageName",
      key: "packageName",
      render: (name: string) => <Text strong>{name}</Text>,
    },
    {
      title: "Giá / Tháng",
      dataIndex: "pricePerMonth",
      key: "pricePerMonth",
      render: (price: number) => (
        <Text type="danger">
          {new Intl.NumberFormat("vi-VN", {
            style: "currency",
            currency: "VND",
          }).format(price)}
        </Text>
      ),
    },
    {
      title: "Invoices/Tháng",
      dataIndex: "maxInvoicesPerMonth",
      key: "maxInvoicesPerMonth",
      align: "center" as const,
      render: (count: number) => (count >= 99999 ? "∞" : count),
    },
    {
      title: "Trạng thái",
      dataIndex: "isActive",
      key: "isActive",
      render: (active: boolean) => (
        <Tag color={active ? "success" : "error"}>
          {active ? "Đang hoạt động" : "Vô hiệu hóa"}
        </Tag>
      ),
    },
    {
      title: "Hành động",
      key: "action",
      align: "center" as const,
      render: (_: any, record: SubscriptionPackage) => (
        <Space>
          <Button
            icon={<EditOutlined />}
            size="small"
            onClick={() => openEditModal(record)}
          />
          <Popconfirm
            title="Vô hiệu hóa gói cước này?"
            onConfirm={() => deletePackageMutation.mutate(record.packageId)}
            okText="Đồng ý"
            cancelText="Hủy"
            disabled={!record.isActive}
          >
            <Button
              icon={<DeleteOutlined />}
              size="small"
              danger
              disabled={!record.isActive}
            />
          </Popconfirm>
        </Space>
      ),
    },
  ];

  return (
    <div className="animate-fade-in-up">
      <div style={{ marginBottom: 24 }}>
        <Title level={4} style={{ margin: 0, color: "#1a4b8c" }}>
          Quản trị Hệ thống
        </Title>
        <Text type="secondary">
          Cấu hình tham số lõi và quản lý các gói cước subscription cho toàn bộ
          hệ thống.
        </Text>
      </div>

      <Card
        bordered={false}
        style={{ borderRadius: 12 }}
        bodyStyle={{ padding: "0 24px 24px 24px" }}
      >
        <Tabs
          activeKey={activeTab}
          onChange={setActiveTab}
          items={[
            {
              key: "packages",
              label: (
                <span>
                  <CrownOutlined /> Quản lý Gói cước
                </span>
              ),
              children: (
                <div style={{ paddingTop: 16 }}>
                  <div
                    style={{
                      marginBottom: 16,
                      display: "flex",
                      justifyContent: "flex-end",
                    }}
                  >
                    <Button
                      type="primary"
                      icon={<PlusOutlined />}
                      onClick={() => {
                        setEditingPackage(null);
                        packageForm.resetFields();
                        setIsPackageModalVisible(true);
                      }}
                    >
                      Thêm gói cước mới
                    </Button>
                  </div>
                  <Table
                    columns={packageColumns}
                    dataSource={packages}
                    rowKey="packageId"
                    loading={isPackagesLoading}
                    pagination={false}
                    size="middle"
                  />
                </div>
              ),
            },
            {
              key: "system",
              label: (
                <span>
                  <ToolOutlined /> Cấu hình hệ thống
                </span>
              ),
              children: (
                <div style={{ paddingTop: 24 }}>
                  <div
                    style={{
                      display: "grid",
                      gridTemplateColumns:
                        "repeat(auto-fit, minmax(400px, 1fr))",
                      gap: 24,
                    }}
                  >
                    {/* Render configs by category */}
                    {Array.from(
                      new Set(configs?.map((c) => c.category) || []),
                    ).map((category) => (
                      <Card
                        key={category}
                        title={
                          <Space>
                            <SettingOutlined style={{ color: "#1a4b8c" }} />
                            <Text strong>{category || "Cấu hình chung"}</Text>
                          </Space>
                        }
                        size="small"
                        bordered
                        style={{ borderRadius: 8 }}
                      >
                        <Descriptions column={1} size="small" bordered>
                          {configs
                            ?.filter((c) => c.category === category)
                            .map((config) => (
                              <Descriptions.Item
                                key={config.configKey}
                                label={
                                  <Space direction="vertical" size={0}>
                                    <Text strong style={{ fontSize: 13 }}>
                                      {config.description || config.configKey}
                                    </Text>
                                    <Text
                                      type="secondary"
                                      style={{ fontSize: 11 }}
                                    >
                                      {config.configKey}
                                    </Text>
                                  </Space>
                                }
                              >
                                <div
                                  style={{
                                    display: "flex",
                                    alignItems: "center",
                                    gap: 8,
                                  }}
                                >
                                  {config.configType === "Boolean" ? (
                                    <Switch
                                      checked={
                                        config.configValue.toLowerCase() ===
                                        "true"
                                      }
                                      disabled={config.isReadOnly}
                                      onChange={(checked) =>
                                        handleConfigUpdate(
                                          config.configKey,
                                          checked.toString(),
                                        )
                                      }
                                    />
                                  ) : config.configType === "Integer" ? (
                                    <InputNumber
                                      value={parseInt(config.configValue)}
                                      disabled={config.isReadOnly}
                                      onBlur={(e) => {
                                        if (
                                          e.target.value !== config.configValue
                                        ) {
                                          handleConfigUpdate(
                                            config.configKey,
                                            e.target.value,
                                          );
                                        }
                                      }}
                                      style={{ width: "100%" }}
                                    />
                                  ) : (
                                    <Input
                                      defaultValue={config.configValue}
                                      disabled={config.isReadOnly}
                                      onPressEnter={(e: any) =>
                                        handleConfigUpdate(
                                          config.configKey,
                                          e.target.value,
                                        )
                                      }
                                      onBlur={(e: any) => {
                                        if (
                                          e.target.value !== config.configValue
                                        ) {
                                          handleConfigUpdate(
                                            config.configKey,
                                            e.target.value,
                                          );
                                        }
                                      }}
                                    />
                                  )}
                                  {config.requiresRestart && (
                                    <Tooltip title="Yêu cầu khởi động lại">
                                      <InfoCircleOutlined
                                        style={{ color: "#faad14" }}
                                      />
                                    </Tooltip>
                                  )}
                                </div>
                              </Descriptions.Item>
                            ))}
                        </Descriptions>
                      </Card>
                    ))}
                  </div>
                </div>
              ),
            },
          ]}
        />
      </Card>

      {/* Package Edit/Add Modal */}
      <Modal
        title={editingPackage ? "Chỉnh sửa gói cước" : "Thêm gói cước mới"}
        open={isPackageModalVisible}
        onCancel={() => setIsPackageModalVisible(false)}
        onOk={() => packageForm.submit()}
        width={800}
        confirmLoading={
          createPackageMutation.isPending || updatePackageMutation.isPending
        }
        destroyOnClose
      >
        <Form
          form={packageForm}
          layout="vertical"
          onFinish={handlePackageSubmit}
          initialValues={{
            pricePerMonth: 0,
            pricePerSixMonths: 0,
            pricePerYear: 0,
            maxUsers: 1,
            maxInvoicesPerMonth: 30,
            storageQuotaGB: 1,
            packageLevel: 1,
            isActive: true,
            hasAiProcessing: true,
          }}
        >
          <div
            style={{
              display: "grid",
              gridTemplateColumns: "1fr 1fr",
              gap: "0 24px",
            }}
          >
            <Form.Item
              name="packageCode"
              label="Mã gói"
              rules={[{ required: true, message: "Vui lòng nhập mã gói" }]}
            >
              <Input
                placeholder="Ví dụ: STARTER, PRO, ENTERPRISE"
                disabled={!!editingPackage}
              />
            </Form.Item>
            <Form.Item
              name="packageName"
              label="Tên gói"
              rules={[{ required: true, message: "Vui lòng nhập tên gói" }]}
            >
              <Input placeholder="Ví dụ: Gói Khởi nghiệp" />
            </Form.Item>
            <Form.Item
              name="packageLevel"
              label="Cấp độ (Sắp xếp)"
              rules={[{ required: true }]}
            >
              <InputNumber min={1} max={10} style={{ width: "100%" }} />
            </Form.Item>
            <Form.Item
              name="isActive"
              label="Hoạt động"
              valuePropName="checked"
            >
              <Switch />
            </Form.Item>
          </div>

          <Form.Item name="description" label="Mô tả">
            <Input.TextArea
              rows={3}
              placeholder="Nhập mô tả ngắn về gói cước..."
            />
          </Form.Item>

          <Divider orientation="left" style={{ margin: "12px 0" }}>
            Giá cước (VND)
          </Divider>
          <div
            style={{
              display: "grid",
              gridTemplateColumns: "1fr 1fr 1fr",
              gap: "0 16px",
            }}
          >
            <Form.Item name="pricePerMonth" label="Giá / Tháng">
              <InputNumber
                style={{ width: "100%" }}
                formatter={(value) =>
                  `${value}`.replace(/\B(?=(\d{3})+(?!\d))/g, ",")
                }
              />
            </Form.Item>
            <Form.Item name="pricePerSixMonths" label="Giá / 6 Tháng">
              <InputNumber
                style={{ width: "100%" }}
                formatter={(value) =>
                  `${value}`.replace(/\B(?=(\d{3})+(?!\d))/g, ",")
                }
              />
            </Form.Item>
            <Form.Item name="pricePerYear" label="Giá / Năm">
              <InputNumber
                style={{ width: "100%" }}
                formatter={(value) =>
                  `${value}`.replace(/\B(?=(\d{3})+(?!\d))/g, ",")
                }
              />
            </Form.Item>
          </div>

          <Divider orientation="left" style={{ margin: "12px 0" }}>
            Hạn mức (Quotas)
          </Divider>
          <div
            style={{
              display: "grid",
              gridTemplateColumns: "1fr 1fr 1fr",
              gap: "0 16px",
            }}
          >
            <Form.Item name="maxUsers" label="Số User tối đa">
              <InputNumber min={1} style={{ width: "100%" }} />
            </Form.Item>
            <Form.Item
              name="maxInvoicesPerMonth"
              label="HĐ tối đa / Tháng (99999 = ∞)"
            >
              <InputNumber min={1} style={{ width: "100%" }} />
            </Form.Item>
            <Form.Item name="storageQuotaGB" label="Dung lượng (GB)">
              <InputNumber min={1} style={{ width: "100%" }} />
            </Form.Item>
          </div>

          <Divider orientation="left" style={{ margin: "12px 0" }}>
            Tính năng (Features)
          </Divider>
          <div
            style={{
              display: "grid",
              gridTemplateColumns: "1fr 1fr 1fr",
              gap: "12px 16px",
            }}
          >
            <Form.Item
              name="hasAiProcessing"
              label="Phân tích AI"
              valuePropName="checked"
            >
              <Switch />
            </Form.Item>
            <Form.Item
              name="hasRiskWarning"
              label="Cảnh báo rủi ro"
              valuePropName="checked"
            >
              <Switch />
            </Form.Item>
            <Form.Item
              name="hasAdvancedWorkflow"
              label="Workflow nâng cao"
              valuePropName="checked"
            >
              <Switch />
            </Form.Item>
            <Form.Item
              name="hasAuditLog"
              label="Truy vết Audit Log"
              valuePropName="checked"
            >
              <Switch />
            </Form.Item>
            <Form.Item
              name="hasErpIntegration"
              label="Tích hợp ERP"
              valuePropName="checked"
            >
              <Switch />
            </Form.Item>
          </div>
        </Form>
      </Modal>
    </div>
  );
};

export default SystemConfig;
