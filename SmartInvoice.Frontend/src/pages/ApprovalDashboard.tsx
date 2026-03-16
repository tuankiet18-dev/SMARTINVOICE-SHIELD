import React, { useState } from "react";
import {
  Card,
  Table,
  Tag,
  Input,
  Typography,
  Row,
  Col,
  Tabs,
  Button,
  Space,
  message,
  Badge,
} from "antd";
import {
  SearchOutlined,
  CheckCircleOutlined,
  SyncOutlined,
  FilterOutlined,
  ExclamationCircleOutlined,
} from "@ant-design/icons";
import type { TabsProps } from "antd";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { invoiceService } from "../services/invoice";
import ApprovalDrawer from "../components/dashboard/ApprovalDrawer";

const { Title, Text } = Typography;

const riskColors: Record<string, string> = {
  Green: "#2d9a5c",
  Yellow: "#e6a817",
  Red: "#d63031",
};

const ApprovalDashboard: React.FC = () => {
  const [selectedTab, setSelectedTab] = useState("Pending");
  const [selectedInvoice, setSelectedInvoice] = useState<any>(null);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [selectedRowKeys, setSelectedRowKeys] = useState<React.Key[]>([]);
  const [lastRefreshTime, setLastRefreshTime] = useState<Date>(new Date());

  const queryClient = useQueryClient();

  const {
    data: apiDataResponse,
    isLoading,
    isFetching,
  } = useQuery({
    queryKey: ["invoices", selectedTab],
    queryFn: () =>
      invoiceService.getInvoices(
        1,
        50,
        undefined,
        selectedTab === "All" ? undefined : selectedTab,
      ),
    refetchInterval: 10_000, // auto-refresh every 10s for admin approvals
    meta: {
      onSuccess: () => setLastRefreshTime(new Date()),
    },
  });

  const { data: pendingStatsData } = useQuery({
    queryKey: ["invoices-stats", "Pending"],
    // Fetch a large page size to count risks on the frontend for now
    queryFn: () => invoiceService.getInvoices(1, 100, undefined, "Pending"),
    refetchInterval: 10_000,
  });

  const pendingItems = pendingStatsData?.items || [];
  const totalPending = pendingStatsData?.totalCount || 0;
  const pendingGreen = pendingItems.filter(
    (i: any) => (i.riskLevel || i.risk) === "Green",
  ).length;
  const pendingYellow = pendingItems.filter(
    (i: any) => (i.riskLevel || i.risk) === "Yellow",
  ).length;
  const pendingRed = pendingItems.filter(
    (i: any) => (i.riskLevel || i.risk) === "Red",
  ).length;

  const approveBulkMutation = useMutation({
    mutationFn: async (ids: string[]) => {
      // Using a loop for bulk approve, ideally we should have a bulk endpoint in backend
      for (let id of ids) {
        await invoiceService.approveInvoice(id);
      }
    },
    onSuccess: () => {
      message.success(`Đã tự động phê duyệt ${selectedRowKeys.length} hóa đơn`);
      setSelectedRowKeys([]);
      queryClient.invalidateQueries({ queryKey: ["invoices"] });
    },
    onError: (err: any) => {
      message.error(`Lỗi duyệt hàng loạt: ${err.message}`);
    },
  });

  // Map API data directly
  const dataToDisplay =
    apiDataResponse?.items?.map((i: any) => ({
      ...i,
      key: i.invoiceId,
      id: i.invoiceId,
    })) || [];

  const openDrawer = (record: any) => {
    setSelectedInvoice(record);
    setDrawerOpen(true);
  };

  const columns = [
    {
      title: "Số hóa đơn",
      dataIndex: "invoiceNumber",
      key: "invoiceNumber",
      render: (text: string, record: any) => (
        <a onClick={() => openDrawer(record)}>
          <Text strong style={{ color: "#1a4b8c" }}>
            {text || record.invoiceNo}
          </Text>
        </a>
      ),
    },
    {
      title: "Người bán",
      dataIndex: "sellerName",
      key: "sellerName",
      render: (text: string, record: any) => text || record.seller,
    },
    {
      title: "Tổng tiền",
      dataIndex: "totalAmount",
      key: "totalAmount",
      align: "right" as const,
      render: (text: number, record: any) => (
        <Text strong>{(text || record.amount)?.toLocaleString()} ₫</Text>
      ),
    },
    {
      title: "Cảnh báo rủi ro",
      dataIndex: "riskLevel",
      key: "riskLevel",
      width: 140,
      render: (riskLevel: string, record: any) => {
        const risk = riskLevel || record.risk || "Green";
        return (
          <Tag
            style={{
              background: `${riskColors[risk]}14`,
              color: riskColors[risk],
              border: `1px solid ${riskColors[risk]}30`,
              borderRadius: 6,
              fontWeight: 600,
              fontSize: 12,
            }}
          >
            {risk} Risk
          </Tag>
        );
      },
    },
    {
      title: "Trạng thái",
      dataIndex: "status",
      key: "status",
      render: (status: string) => {
        if (status === "Pending")
          return <Badge status="processing" text="Chờ duyệt" />;
        if (status === "Approved")
          return <Badge status="success" text="Đã duyệt" />;
        if (status === "Rejected")
          return <Badge status="error" text="Từ chối" />;
        return <Badge status="default" text={status} />;
      },
    },
    {
      title: "Hành động",
      key: "action",
      width: 120,
      render: (_: any, record: any) => (
        <Button
          size="small"
          type="primary"
          ghost
          onClick={() => openDrawer(record)}
        >
          Chi tiết
        </Button>
      ),
    },
  ];

  const handleBulkApprove = () => {
    if (selectedRowKeys.length === 0) return;
    approveBulkMutation.mutate(selectedRowKeys.map((k) => k.toString()));
  };

  const rowSelection = {
    selectedRowKeys,
    onChange: (newSelectedRowKeys: React.Key[]) => {
      setSelectedRowKeys(newSelectedRowKeys);
    },
    getCheckboxProps: (record: any) => ({
      disabled: record.status !== "Pending",
    }),
  };

  const tabItems: TabsProps["items"] = [
    { key: "Pending", label: "Chờ duyệt (Pending)" },
    { key: "Approved", label: "Đã duyệt (Approved)" },
    { key: "Rejected", label: "Từ chối (Rejected)" },
    { key: "All", label: "Tất cả hóa đơn" },
  ];

  return (
    <div className="animate-fade-in-up">
      <div
        style={{
          marginBottom: 24,
          display: "flex",
          justifyContent: "space-between",
          alignItems: "flex-start",
        }}
      >
        <div>
          <Title level={4} style={{ margin: 0 }}>
            Duyệt Ngoại Lệ (Approval Dashboard)
          </Title>
          <Text type="secondary">
            {isFetching
              ? "🔄 Kiểm tra mục duyệt..."
              : `✓ Cập nhật cách đây ${Math.floor((new Date().getTime() - lastRefreshTime.getTime()) / 1000)}s`}
          </Text>
        </div>
        <Space>
          <Button
            icon={<SyncOutlined />}
            onClick={() =>
              queryClient.invalidateQueries({ queryKey: ["invoices"] })
            }
          >
            Làm mới ngay
          </Button>
        </Space>
      </div>

      {/* Thống kê nhanh (Dựa trên dữ liệu trang hiện tại) */}
      <Row gutter={16} style={{ marginBottom: 24 }}>
        <Col span={6}>
          <Card
            style={{ borderRadius: 12, borderLeft: "4px solid #1677ff" }}
            bodyStyle={{ padding: "16px 20px" }}
          >
            <Text type="secondary">Tổng số chờ duyệt</Text>
            <Title level={2} style={{ margin: "4px 0 0" }}>
              {totalPending}
            </Title>
          </Card>
        </Col>
        <Col span={6}>
          <Card
            style={{
              borderRadius: 12,
              borderLeft: `4px solid ${riskColors.Green}`,
            }}
            bodyStyle={{ padding: "16px 20px" }}
          >
            <Text type="secondary">An toàn (Green)</Text>
            <Title
              level={2}
              style={{ margin: "4px 0 0", color: riskColors.Green }}
            >
              {pendingGreen}
            </Title>
          </Card>
        </Col>
        <Col span={6}>
          <Card
            style={{
              borderRadius: 12,
              borderLeft: `4px solid ${riskColors.Yellow}`,
            }}
            bodyStyle={{ padding: "16px 20px" }}
          >
            <Text type="secondary">Cần lưu ý (Yellow)</Text>
            <Title
              level={2}
              style={{ margin: "4px 0 0", color: riskColors.Yellow }}
            >
              {pendingYellow}
            </Title>
          </Card>
        </Col>
        <Col span={6}>
          <Card
            style={{
              borderRadius: 12,
              borderLeft: `4px solid ${riskColors.Red}`,
            }}
            bodyStyle={{ padding: "16px 20px" }}
          >
            <Text type="secondary">Rủi ro (Red)</Text>
            <Title
              level={2}
              style={{ margin: "4px 0 0", color: riskColors.Red }}
            >
              {pendingRed}
            </Title>
          </Card>
        </Col>
      </Row>

      <Card bordered={false} style={{ borderRadius: 12 }}>
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            marginBottom: 16,
          }}
        >
          <Tabs
            activeKey={selectedTab}
            onChange={(key) => {
              setSelectedTab(key);
              setSelectedRowKeys([]);
            }}
            items={tabItems}
            style={{ marginBottom: 0 }}
          />
          <Space>
            <Input
              prefix={<SearchOutlined />}
              placeholder="Tìm kiếm hóa đơn..."
              style={{ width: 250 }}
            />
            <Button icon={<FilterOutlined />}>Bộ lọc</Button>
          </Space>
        </div>

        {selectedTab === "Pending" && selectedRowKeys.length > 0 && (
          <div
            style={{
              marginBottom: 16,
              padding: "10px 16px",
              background: "#e6f4ff",
              borderRadius: 8,
              display: "flex",
              justifyContent: "space-between",
              alignItems: "center",
            }}
          >
            <Text strong style={{ color: "#1677ff" }}>
              Đã chọn {selectedRowKeys.length} hóa đơn
            </Text>
            <Button
              type="primary"
              icon={<CheckCircleOutlined />}
              onClick={handleBulkApprove}
              loading={approveBulkMutation.isPending}
            >
              Duyệt tất cả đã chọn
            </Button>
          </div>
        )}

        <Table
          rowSelection={selectedTab === "Pending" ? rowSelection : undefined}
          loading={isLoading}
          columns={columns}
          dataSource={dataToDisplay}
          pagination={{ pageSize: 15 }}
        />
      </Card>

      <ApprovalDrawer
        open={drawerOpen}
        onClose={() => setDrawerOpen(false)}
        invoice={selectedInvoice}
      />
    </div>
  );
};

export default ApprovalDashboard;
