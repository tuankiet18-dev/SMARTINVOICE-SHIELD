import React, { useState } from "react";
import { useNavigate } from "react-router-dom";
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

const { Title, Text } = Typography;

const riskColors: Record<string, string> = {
  Green: "#2d9a5c",
  Yellow: "#e6a817",
  Red: "#d63031",
};

const ApprovalDashboard: React.FC = () => {
  const navigate = useNavigate();
  const [selectedTab, setSelectedTab] = useState("Pending");
  const [selectedRowKeys, setSelectedRowKeys] = useState<React.Key[]>([]);
  const [lastRefreshTime, setLastRefreshTime] = useState<Date>(new Date());

  const [searchText, setSearchText] = useState(""); 
  const [keyword, setKeyword] = useState("");

  const queryClient = useQueryClient();

  const {
    data: apiDataResponse,
    isLoading,
    isFetching,
  } = useQuery({
    queryKey: ["invoices", selectedTab, keyword],
    queryFn: () =>
      invoiceService.getInvoices(
        1,
        50,
        keyword || undefined,
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
        // 1. Cố gắng lấy câu thông báo lỗi tiếng Việt từ Backend gửi về (nếu có)
        const backendMessage = err?.response?.data?.message;

        // 2. Dịch lỗi sang ngôn ngữ thân thiện cho user
        if (err?.response?.status === 400) {
            // Hiển thị lỗi do BE trả về, nếu BE không trả về thì dùng câu mặc định này
            message.error(
                backendMessage || "Không thể duyệt: Quy định hệ thống không cho phép một người duyệt cả 2 cấp trên cùng một hóa đơn."
            );
        } else if (err?.response?.status === 403) {
            message.error("Bạn không có quyền thực hiện thao tác duyệt này.");
        } else {
            // Các lỗi khác (500 Server, rớt mạng...)
            message.error(`Lỗi hệ thống: ${backendMessage || "Vui lòng thử lại sau."}`);
        }
      },
  });

  // Map API data directly
  const dataToDisplay =
    apiDataResponse?.items?.map((i: any) => ({
      ...i,
      key: i.invoiceId,
      id: i.invoiceId,
    })) || [];

  const columns = [
    {
      title: "Số hóa đơn",
      dataIndex: "invoiceNumber",
      key: "invoiceNumber",
      render: (text: string, record: any) => (
        <a onClick={(e) => {
            e.stopPropagation();
            navigate(`/app/invoices/${record.invoiceId}`);
        }}>
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
      width: 150, // Thêm độ rộng cố định cho cột
      render: (text: number, record: any) => (
        <Text strong style={{ whiteSpace: "nowrap" }}>
          {(text || record.amount)?.toLocaleString()} ₫
        </Text>
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
      render: (status: string, record: any) => {
        // MẸO DEBUG: Bật dòng này lên, mở F12 (Console) trên trình duyệt để soi xem Backend gửi chữ gì về
        // console.log("Data hóa đơn: ", record);

        let color = "processing";
        let text = "Chờ duyệt";

        // Lấy step từ ngoài hoặc từ trong object workflow
        const step = record.currentApprovalStep || record.workflow?.currentApprovalStep;

        if (status === "Approved") { 
            color = "success"; 
            text = "Đã duyệt"; 
        } else if (status === "Rejected") { 
            color = "error"; 
            text = "Từ chối"; 
        } else if (status === "Draft") { 
            color = "default"; 
            text = "Nháp"; 
        } else if (status === "Pending") {
            if (step === 2) {
                color = "orange"; 
                text = "Chờ duyệt (Cấp 2)";
            } else {
                color = "warning";
                text = "Chờ duyệt"; 
            }
        }

        return <Tag color={color} style={{ fontWeight: 500 }}>{text}</Tag>;
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
          onClick={(e) => {
              e.stopPropagation();
              navigate(`/app/invoices/${record.invoiceId}`);
          }}
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
            styles={{ body: { padding: "16px 20px" } }}
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
            styles={{ body: { padding: "16px 20px" } }}
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
            styles={{ body: { padding: "16px 20px" } }}
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
            styles={{ body: { padding: "16px 20px" } }}
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

      <Card variant="borderless" style={{ borderRadius: 12 }}>
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
            <Input.Search
              placeholder="Tìm số HĐ, tên người bán..."
              allowClear
              enterButton
              style={{ width: 300 }}
              value={searchText}
              onChange={(e) => setSearchText(e.target.value)}
              onSearch={(value) => setKeyword(value)} // Chỉ gọi API khi bấm Enter hoặc click icon Kính lúp
            />
            {/* <Button icon={<FilterOutlined />}>Bộ lọc</Button> */}
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
    </div>
  );
};

export default ApprovalDashboard;
