import React, { useEffect, useState } from "react";
import {
  Card,
  Form,
  Input,
  Switch,
  Button,
  notification,
  Typography,
  Divider,
  Spin,
  InputNumber,
} from "antd";
import { useAuth } from "@/contexts/AuthContext";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { settingsService } from "@/services/settingsService";

const { Title, Text } = Typography;

const Settings: React.FC = () => {
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const [profileForm] = Form.useForm();
  const [companyForm] = Form.useForm();
  const [activeTab, setActiveTab] = useState<
    "profile" | "company" | "notifications"
  >("profile");

  const isCompanyAdmin = user?.role === "CompanyAdmin";

  // --- Profile Data ---
  const { data: profile, isLoading: isLoadingProfile } = useQuery({
    queryKey: ["settings", "profile"],
    queryFn: settingsService.getUserProfile,
    staleTime: 5 * 60 * 1000,
  });

  const updateProfileMutation = useMutation({
    mutationFn: settingsService.updateUserProfile,
    onSuccess: () => {
      notification.success({
        message: "Lưu cài đặt thành công",
        description: "Hồ sơ cá nhân và thông báo đã được cập nhật.",
      });
      queryClient.invalidateQueries({ queryKey: ["settings", "profile"] });
    },
    onError: (err: any) => {
      notification.error({
        message: "Lỗi",
        description: err?.response?.data?.message || "Không thể lưu cài đặt.",
      });
    },
  });

  // --- Company Data ---
  const { data: company, isLoading: isLoadingCompany } = useQuery({
    queryKey: ["settings", "company"],
    queryFn: settingsService.getCompanySettings,
    enabled: isCompanyAdmin,
    staleTime: 5 * 60 * 1000,
  });

  const updateCompanyMutation = useMutation({
    mutationFn: settingsService.updateCompanySettings,
    onSuccess: () => {
      notification.success({
        message: "Lưu cài đặt thành công",
        description: "Cấu hình công ty đã được cập nhật.",
      });
      queryClient.invalidateQueries({ queryKey: ["settings", "company"] });
    },
    onError: (err: any) => {
      notification.error({
        message: "Lỗi",
        description: err?.response?.data?.message || "Không thể lưu cài đặt.",
      });
    },
  });

  // Sync forms
  useEffect(() => {
    if (profile) {
      profileForm.setFieldsValue({
        fullName: profile.fullName,
        employeeId: profile.employeeId,
        receiveEmailNotifications: profile.receiveEmailNotifications,
        receiveInAppNotifications: profile.receiveInAppNotifications,
      });
    }
  }, [profile, profileForm]);

  useEffect(() => {
    if (company) {
      companyForm.setFieldsValue({
      });
    }
  }, [company, companyForm]);

  const onProfileFinish = (values: any) => {
    const allValues = { ...profileForm.getFieldsValue(), ...values };
    updateProfileMutation.mutate({
      fullName: allValues.fullName || profile?.fullName || "",
      receiveEmailNotifications:
        allValues.receiveEmailNotifications ??
        profile?.receiveEmailNotifications ??
        true,
      receiveInAppNotifications:
        allValues.receiveInAppNotifications ??
        profile?.receiveInAppNotifications ??
        true,
    });
  };

  const onCompanyFinish = (values: any) => {
    updateCompanyMutation.mutate({
    });
  };

  if (isLoadingProfile || (isCompanyAdmin && isLoadingCompany)) {
    return (
      <div
        style={{
          display: "flex",
          justifyContent: "center",
          alignItems: "center",
          height: "100%",
        }}
      >
        <Spin size="large" />
      </div>
    );
  }

  return (
    <div style={{ maxWidth: 1000, margin: "0 auto" }}>
      <div style={{ marginBottom: 24 }}>
        <Title
          level={3}
          style={{ margin: 0, color: "#1E293B", fontWeight: 600 }}
        >
          Cài đặt
        </Title>
        <Text style={{ color: "#64748B" }}>
          Quản lý thông tin cá nhân và cấu hình hệ thống
        </Text>
      </div>

      <div style={{ display: "flex", gap: 24 }}>
        {/* Sidebar Tabs */}
        <div style={{ width: 250, flexShrink: 0 }}>
          <Card styles={{ body: { padding: 0 } }} className="settings-sidebar">
            <div
              style={{
                padding: "16px 20px",
                cursor: "pointer",
                borderLeft:
                  activeTab === "profile"
                    ? "3px solid #4880FF"
                    : "3px solid transparent",
                background: activeTab === "profile" ? "#F4F7FF" : "transparent",
                fontWeight: activeTab === "profile" ? 600 : 400,
              }}
              onClick={() => setActiveTab("profile")}
            >
              Hồ sơ cá nhân
            </div>

            <div
              style={{
                padding: "16px 20px",
                cursor: "pointer",
                borderLeft:
                  activeTab === "notifications"
                    ? "3px solid #4880FF"
                    : "3px solid transparent",
                background:
                  activeTab === "notifications" ? "#F4F7FF" : "transparent",
                fontWeight: activeTab === "notifications" ? 600 : 400,
              }}
              onClick={() => setActiveTab("notifications")}
            >
              Cài đặt thông báo
            </div>

            {isCompanyAdmin && (
              <>
                <Divider style={{ margin: 0 }} />
                <div
                  style={{
                    padding: "16px 20px",
                    cursor: "pointer",
                    borderLeft:
                      activeTab === "company"
                        ? "3px solid #4880FF"
                        : "3px solid transparent",
                    background:
                      activeTab === "company" ? "#F4F7FF" : "transparent",
                    fontWeight: activeTab === "company" ? 600 : 400,
                  }}
                  onClick={() => setActiveTab("company")}
                >
                  Cấu hình Công ty
                </div>
              </>
            )}
          </Card>
        </div>

        {/* Content Area */}
        <div style={{ flex: 1 }}>
          {activeTab === "profile" && (
            <Card
              title="Hồ sơ cá nhân"
              variant="borderless"
              style={{
                boxShadow: "0 1px 3px rgba(0,0,0,0.05)",
                borderRadius: 12,
              }}
            >
              <Form
                layout="vertical"
                form={profileForm}
                onFinish={onProfileFinish}
              >
                <Form.Item label="Email đăng nhập">
                  <Input value={profile?.email} disabled />
                  <Text type="secondary" style={{ fontSize: 12 }}>
                    Email không thể thay đổi.
                  </Text>
                </Form.Item>
                <Form.Item
                  label="Họ và tên"
                  name="fullName"
                  rules={[{ required: true, message: "Vui lòng nhập họ tên" }]}
                >
                  <Input placeholder="Nhập họ và tên của bạn" />
                </Form.Item>
                <Form.Item
                  label="Mã nhân viên"
                  name="employeeId"
                  style={{ marginBottom: 4 }}
                >
                  <Input disabled />
                </Form.Item>
                <Text
                  type="secondary"
                  style={{ fontSize: 12, display: "block", marginBottom: 24 }}
                >
                  Mã nhân viên do hệ thống cấp và không thể thay đổi.
                </Text>
                <Button
                  type="primary"
                  htmlType="submit"
                  loading={updateProfileMutation.isPending}
                  style={{ marginTop: 8 }}
                >
                  Lưu thay đổi
                </Button>
              </Form>
            </Card>
          )}

          {activeTab === "notifications" && (
            <Card
              title="Cài đặt thông báo"
              variant="borderless"
              style={{
                boxShadow: "0 1px 3px rgba(0,0,0,0.05)",
                borderRadius: 12,
              }}
            >
              <Form
                layout="vertical"
                form={profileForm}
                onFinish={onProfileFinish}
              >
                <div style={{ marginBottom: 24 }}>
                  <div
                    style={{
                      display: "flex",
                      justifyContent: "space-between",
                      alignItems: "center",
                      marginBottom: 8,
                    }}
                  >
                    <div>
                      <Text strong style={{ display: "block" }}>
                        Thông báo trong ứng dụng (In-app)
                      </Text>
                      <Text type="secondary" style={{ fontSize: 13 }}>
                        Nhận thông báo qua biểu tượng chuông trên góc phải màn
                        hình.
                      </Text>
                    </div>
                    <Form.Item
                      name="receiveInAppNotifications"
                      valuePropName="checked"
                      noStyle
                    >
                      <Switch />
                    </Form.Item>
                  </div>
                </div>
                <Divider />
                <div>
                  <div
                    style={{
                      display: "flex",
                      justifyContent: "space-between",
                      alignItems: "center",
                      marginBottom: 8,
                    }}
                  >
                    <div>
                      <Text strong style={{ display: "block" }}>
                        Thông báo qua Email
                      </Text>
                      <Text type="secondary" style={{ fontSize: 13 }}>
                        Gửi email cập nhật về các hóa đơn cần duyệt, kết quả xử
                        lý hóa đơn, v.v.
                      </Text>
                    </div>
                    <Form.Item
                      name="receiveEmailNotifications"
                      valuePropName="checked"
                      noStyle
                    >
                      <Switch />
                    </Form.Item>
                  </div>
                </div>
                <div style={{ marginTop: 32 }}>
                  <Button
                    type="primary"
                    htmlType="submit"
                    loading={updateProfileMutation.isPending}
                  >
                    Lưu thay đổi
                  </Button>
                </div>
              </Form>
            </Card>
          )}

          {isCompanyAdmin && activeTab === "company" && company && (
            <Card
              title="Cấu hình Công ty & Luồng duyệt"
              variant="borderless"
              style={{
                boxShadow: "0 1px 3px rgba(0,0,0,0.05)",
                borderRadius: 12,
              }}
            >
              <div style={{ marginBottom: 24 }}>
                <Text strong style={{ display: "block", fontSize: 14 }}>
                  Thông tin Công ty
                </Text>
                <div
                  style={{
                    marginTop: 12,
                    display: "grid",
                    gridTemplateColumns: "120px 1fr",
                    gap: "8px 16px",
                  }}
                >
                  <Text type="secondary">Tên công ty:</Text>
                  <Text strong>{company.companyName}</Text>
                  <Text type="secondary">Mã số thuế:</Text>
                  <Text strong>{company.taxCode}</Text>
                  <Text type="secondary">Số điện thoại:</Text>
                  <Text>{company.phoneNumber || "N/A"}</Text>
                  <Text type="secondary">Địa chỉ:</Text>
                  <Text>{company.address || "N/A"}</Text>
                </div>
              </div>
              <Divider />

              <Form
                layout="vertical"
                form={companyForm}
                onFinish={onCompanyFinish}
              >

                <div style={{ marginTop: 24 }}>
                  <Button
                    type="primary"
                    htmlType="submit"
                    loading={updateCompanyMutation.isPending}
                  >
                    Lưu cấu hình
                  </Button>
                </div>
              </Form>
            </Card>
          )}
        </div>
      </div>
    </div>
  );
};

export default Settings;
