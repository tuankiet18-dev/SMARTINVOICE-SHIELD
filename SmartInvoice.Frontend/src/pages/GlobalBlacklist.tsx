import React, { useState, useEffect, useMemo } from 'react';
import {
    Card, Table, Tag, Input, Typography, Row, Col, Button, Space, Modal, Form, message, Spin, Tooltip
} from 'antd';
import {
    SearchOutlined, DeleteOutlined, PlusOutlined, SafetyCertificateOutlined, ReloadOutlined, EditOutlined
} from '@ant-design/icons';
import { blacklistService, BlacklistDto, CreateBlacklistDto, UpdateBlacklistDto } from '@/services/blacklist';

const { Title, Text } = Typography;

const GlobalBlacklist: React.FC = () => {
    const [data, setData] = useState<BlacklistDto[]>([]);
    const [loading, setLoading] = useState(false);
    const [searchText, setSearchText] = useState('');

    // Create Modal
    const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
    const [createForm] = Form.useForm();
    const [submitting, setSubmitting] = useState(false);

    // Edit Modal
    const [isEditModalOpen, setIsEditModalOpen] = useState(false);
    const [editForm] = Form.useForm();
    const [editingRecord, setEditingRecord] = useState<BlacklistDto | null>(null);

    // --- Fetch data ---
    useEffect(() => {
        fetchBlacklist();
    }, []);

    const fetchBlacklist = async () => {
        setLoading(true);
        try {
            const items = await blacklistService.getAll();
            setData(items);
        } catch (error: any) {
            message.error(error.response?.data?.message || 'Lỗi khi tải danh sách Blacklist');
        } finally {
            setLoading(false);
        }
    };

    // --- Filtered data by search ---
    const filteredData = useMemo(() => {
        if (!searchText.trim()) return data;
        const keyword = searchText.toLowerCase();
        return data.filter(
            item =>
                item.taxCode.toLowerCase().includes(keyword) ||
                (item.companyName && item.companyName.toLowerCase().includes(keyword))
        );
    }, [data, searchText]);

    // --- Create ---
    const handleCreate = async () => {
        try {
            const values = await createForm.validateFields();
            setSubmitting(true);

            const dto: CreateBlacklistDto = {
                taxCode: values.taxCode.trim(),
                companyName: values.companyName?.trim(),
                reason: values.reason?.trim(),
            };

            await blacklistService.create(dto);
            message.success(`Đã thêm MST ${dto.taxCode} vào Blacklist.`);
            setIsCreateModalOpen(false);
            createForm.resetFields();
            fetchBlacklist();
        } catch (error: any) {
            if (error.response?.status === 409) {
                message.warning(error.response.data?.message || 'MST đã tồn tại trong Blacklist.');
            } else if (error.errorFields) {
                // Form validation error - do nothing, antd shows inline
            } else {
                message.error(error.response?.data?.message || 'Lỗi khi thêm vào Blacklist.');
            }
        } finally {
            setSubmitting(false);
        }
    };

    // --- Edit ---
    const openEditModal = (record: BlacklistDto) => {
        setEditingRecord(record);
        editForm.setFieldsValue({
            companyName: record.companyName,
            reason: record.reason,
        });
        setIsEditModalOpen(true);
    };

    const handleEdit = async () => {
        if (!editingRecord) return;
        try {
            const values = await editForm.validateFields();
            setSubmitting(true);

            const dto: UpdateBlacklistDto = {
                companyName: values.companyName?.trim(),
                reason: values.reason?.trim(),
            };

            await blacklistService.update(editingRecord.blacklistId, dto);
            message.success(`Đã cập nhật thông tin MST ${editingRecord.taxCode}.`);
            setIsEditModalOpen(false);
            editForm.resetFields();
            setEditingRecord(null);
            fetchBlacklist();
        } catch (error: any) {
            if (!error.errorFields) {
                message.error(error.response?.data?.message || 'Lỗi khi cập nhật.');
            }
        } finally {
            setSubmitting(false);
        }
    };

    // --- Delete (soft) ---
    const handleRemove = (record: BlacklistDto) => {
        Modal.confirm({
            title: 'Xóa khỏi Blacklist?',
            content: `Bạn có chắc chắn muốn gỡ MST ${record.taxCode} (${record.companyName || 'N/A'}) khỏi danh sách đen? Các Tenant sẽ có thể nhận hóa đơn từ doanh nghiệp này trở lại.`,
            okText: 'Xác nhận xóa',
            okType: 'danger',
            cancelText: 'Hủy',
            async onOk() {
                try {
                    await blacklistService.remove(record.blacklistId);
                    message.success(`Đã xóa MST ${record.taxCode} khỏi Blacklist.`);
                    fetchBlacklist();
                } catch (error: any) {
                    message.error(error.response?.data?.message || 'Lỗi khi xóa khỏi Blacklist.');
                }
            },
        });
    };

    // --- Format date ---
    const formatDate = (dateStr: string) => {
        try {
            return new Date(dateStr).toLocaleDateString('vi-VN', {
                day: '2-digit',
                month: '2-digit',
                year: 'numeric',
            });
        } catch {
            return dateStr;
        }
    };

    // --- Table columns ---
    const columns = [
        {
            title: 'Mã Số Thuế (MST)',
            dataIndex: 'taxCode',
            key: 'taxCode',
            render: (text: string) => (
                <Tag color="error" style={{ fontSize: 13, padding: '4px 8px' }}>
                    {text}
                </Tag>
            ),
        },
        {
            title: 'Tên Doanh nghiệp',
            dataIndex: 'companyName',
            key: 'companyName',
            render: (text: string | null) => <Text strong>{text || '—'}</Text>,
        },
        {
            title: 'Lý do đưa vào Blacklist',
            dataIndex: 'reason',
            key: 'reason',
            render: (text: string | null) => <Text type="secondary">{text || '—'}</Text>,
        },
        {
            title: 'Trạng thái',
            dataIndex: 'isActive',
            key: 'isActive',
            width: 120,
            render: (isActive: boolean) => (
                <Tag color={isActive ? 'red' : 'default'}>
                    {isActive ? 'Đang chặn' : 'Đã gỡ'}
                </Tag>
            ),
        },
        {
            title: 'Ngày thêm',
            dataIndex: 'addedDate',
            key: 'addedDate',
            width: 130,
            render: (date: string) => (
                <span style={{ fontSize: 12 }}>{formatDate(date)}</span>
            ),
        },
        {
            title: 'Hành động',
            key: 'action',
            width: 160,
            render: (_: any, record: BlacklistDto) => (
                <Space>
                    <Tooltip title="Chỉnh sửa">
                        <Button
                            icon={<EditOutlined />}
                            size="small"
                            onClick={() => openEditModal(record)}
                        />
                    </Tooltip>
                    {record.isActive && (
                        <Button
                            danger
                            icon={<DeleteOutlined />}
                            size="small"
                            onClick={() => handleRemove(record)}
                        >
                            Gỡ bỏ
                        </Button>
                    )}
                </Space>
            ),
        },
    ];

    return (
        <div className="animate-fade-in-up">
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 24 }}>
                <div>
                    <Title level={4} style={{ margin: 0, color: '#d63031' }}>
                        Quản Trị Rủi Ro Tập Trung (Global Blacklist)
                    </Title>
                    <Text type="secondary">
                        Cập nhật danh sách đen các MST rủi ro. Mọi hóa đơn chứa MST này lập tức bị chặn (Red Risk) trên toàn bộ hệ thống các Tenant.
                    </Text>
                </div>
                <Space>
                    <Button icon={<ReloadOutlined />} onClick={fetchBlacklist} loading={loading}>
                        Tải lại
                    </Button>
                    <Button type="primary" danger icon={<PlusOutlined />} onClick={() => setIsCreateModalOpen(true)}>
                        Thêm MST Rủi Ro
                    </Button>
                </Space>
            </div>

            <Card variant="borderless" style={{ borderRadius: 12, borderTop: '3px solid #d63031' }}>
                <Row style={{ marginBottom: 16 }}>
                    <Col span={8}>
                        <Input
                            prefix={<SearchOutlined />}
                            placeholder="Tra cứu MST, tên công ty trong Blacklist..."
                            value={searchText}
                            onChange={(e) => setSearchText(e.target.value)}
                            allowClear
                        />
                    </Col>
                </Row>
                <Table
                    columns={columns}
                    dataSource={filteredData}
                    rowKey="blacklistId"
                    loading={loading}
                    pagination={{
                        pageSize: 10,
                        showSizeChanger: true,
                        showTotal: (total) => `Tổng cộng ${total} mục`,
                    }}
                />
            </Card>

            {/* Modal: Thêm mới */}
            <Modal
                title={
                    <Space>
                        <SafetyCertificateOutlined style={{ color: '#d63031', fontSize: 20 }} />
                        <Text strong>Thêm Mã Số Thuế vào Blacklist</Text>
                    </Space>
                }
                open={isCreateModalOpen}
                onOk={handleCreate}
                onCancel={() => {
                    setIsCreateModalOpen(false);
                    createForm.resetFields();
                }}
                okText="Thêm vào Blacklist"
                okButtonProps={{ danger: true, loading: submitting }}
                cancelButtonProps={{ disabled: submitting }}
            >
                <Form form={createForm} layout="vertical" style={{ marginTop: 24 }}>
                    <Form.Item
                        name="taxCode"
                        label="Mã Số Thuế (Bắt buộc)"
                        rules={[
                            { required: true, message: 'Vui lòng nhập mã số thuế' },
                            { max: 14, message: 'MST không được vượt quá 14 ký tự' },
                        ]}
                    >
                        <Input placeholder="Nhập mã số thuế (Vd: 0101234567)" />
                    </Form.Item>
                    <Form.Item
                        name="companyName"
                        label="Tên Doanh Nghiệp"
                        rules={[{ max: 200, message: 'Tên không được vượt quá 200 ký tự' }]}
                    >
                        <Input placeholder="Tên đơn vị rủi ro" />
                    </Form.Item>
                    <Form.Item name="reason" label="Lý do Vi phạm / Cảnh báo (Hiển thị cho Tenant)">
                        <Input.TextArea rows={3} placeholder="Mô tả lý do: VD: Nợ thuế quá hạn, doanh nghiệp ma..." />
                    </Form.Item>
                </Form>
            </Modal>

            {/* Modal: Chỉnh sửa */}
            <Modal
                title={
                    <Space>
                        <EditOutlined style={{ color: '#1890ff', fontSize: 20 }} />
                        <Text strong>Chỉnh sửa thông tin Blacklist</Text>
                    </Space>
                }
                open={isEditModalOpen}
                onOk={handleEdit}
                onCancel={() => {
                    setIsEditModalOpen(false);
                    editForm.resetFields();
                    setEditingRecord(null);
                }}
                okText="Lưu thay đổi"
                okButtonProps={{ loading: submitting }}
                cancelButtonProps={{ disabled: submitting }}
            >
                <Form form={editForm} layout="vertical" style={{ marginTop: 24 }}>
                    <Form.Item label="Mã Số Thuế">
                        <Input value={editingRecord?.taxCode} disabled />
                    </Form.Item>
                    <Form.Item
                        name="companyName"
                        label="Tên Doanh Nghiệp"
                        rules={[{ max: 200, message: 'Tên không được vượt quá 200 ký tự' }]}
                    >
                        <Input placeholder="Tên đơn vị rủi ro" />
                    </Form.Item>
                    <Form.Item name="reason" label="Lý do Vi phạm / Cảnh báo">
                        <Input.TextArea rows={3} placeholder="Mô tả lý do..." />
                    </Form.Item>
                </Form>
            </Modal>
        </div>
    );
};

export default GlobalBlacklist;
