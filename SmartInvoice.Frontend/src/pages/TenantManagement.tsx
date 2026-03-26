import React, { useState, useEffect } from 'react';
import {
    Card, Table, Tag, Input, Typography, Row, Col, Button, Space, Modal, message, Badge, Popconfirm, Descriptions
} from 'antd';
import {
    SearchOutlined, StopOutlined, CheckCircleOutlined, InfoCircleOutlined
} from '@ant-design/icons';
import { companyService } from '../services/companyService'; 

const { Title, Text } = Typography;

const formatBytes = (bytes: number) => {
    if (bytes === 0) return '0 Byte';
    const k = 1000; // Mình dùng hệ 1000 theo chuẩn hệ thập phân của bạn
    const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
};

const TenantManagement: React.FC = () => {
    const [data, setData] = useState<any[]>([]);
    const [loading, setLoading] = useState<boolean>(false);
    const [searchText, setSearchText] = useState<string>('');
    
    // State cho Modal Chi tiết
    const [isModalVisible, setIsModalVisible] = useState(false);
    const [selectedTenant, setSelectedTenant] = useState<any>(null);

    const fetchCompanies = async () => {
        setLoading(true);
        try {
            const companies = await companyService.getAllCompanies();
            
            const mappedData = companies.map((c: any) => {
                const usedStorageGB = (c.usedStorageBytes || 0) / (1000 * 1000 * 1000);
                
                return {
                    key: c.companyId,
                    name: c.companyName,
                    email: c.email,
                    plan: c.subscriptionTier || 'Free',
                    usedStorageGB: usedStorageGB,
                    quotaGB: c.storageQuotaGB,
                    isActive: c.isActive,
                    // Giữ lại toàn bộ data gốc để lát show lên Modal
                    originalData: c 
                };
            });
            setData(mappedData);
        } catch (error) {
            message.error('Không thể tải dữ liệu doanh nghiệp.');
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        fetchCompanies();
    }, []);

    // Hàm gọi API Khóa / Mở khóa
    const handleToggleStatus = async (id: string, currentStatus: boolean) => {
        try {
            const res = await companyService.toggleCompanyStatus(id);
            message.success(res.message);
            fetchCompanies(); // Load lại bảng để cập nhật màu sắc
        } catch (error: any) {
            message.error(error.response?.data?.message || 'Có lỗi xảy ra khi cập nhật trạng thái.');
        }
    };

    // Hàm mở Modal xem chi tiết
    const handleViewDetails = (record: any) => {
        setSelectedTenant(record.originalData);
        setIsModalVisible(true);
    };

    const columns = [
        {
            title: 'Tên Doanh Nghiệp',
            dataIndex: 'name',
            key: 'name',
            render: (text: string) => <Text strong>{text}</Text>,
        },
        {
            title: 'Email Liên Hệ',
            dataIndex: 'email',
            key: 'email',
        },
        {
            title: 'Gói Dịch Vụ',
            dataIndex: 'plan',
            key: 'plan',
            render: (plan: string) => <Tag color="blue">{plan}</Tag>,
        },
        {
            title: 'Trạng Thái',
            dataIndex: 'isActive',
            key: 'isActive',
            render: (isActive: boolean) => (
                <Badge 
                    status={isActive ? 'success' : 'error'} 
                    text={isActive ? 'Đang hoạt động' : 'Đã khóa'} 
                />
            ),
        },
        {
            title: 'Hành động',
            key: 'action',
            render: (_: any, record: any) => (
                <Space size="middle">
                    <Button 
                        size="small" 
                        type="primary" 
                        ghost 
                        icon={<InfoCircleOutlined />}
                        onClick={() => handleViewDetails(record)}
                    >
                        Chi tiết
                    </Button>

                    <Popconfirm
                        title={record.isActive ? "Khóa doanh nghiệp này?" : "Mở khóa doanh nghiệp này?"}
                        description={record.isActive ? "Doanh nghiệp sẽ không thể đăng nhập và sử dụng hệ thống." : "Doanh nghiệp sẽ có thể sử dụng lại hệ thống bình thường."}
                        onConfirm={() => handleToggleStatus(record.key, record.isActive)}
                        okText="Đồng ý"
                        cancelText="Hủy"
                        okButtonProps={{ danger: record.isActive }}
                    >
                        <Button 
                            size="small" 
                            danger={record.isActive}
                            style={!record.isActive ? { borderColor: '#52c41a', color: '#52c41a' } : {}}
                            icon={record.isActive ? <StopOutlined /> : <CheckCircleOutlined />}
                        >
                            {record.isActive ? 'Khóa' : 'Mở khóa'}
                        </Button>
                    </Popconfirm>
                </Space>
            ),
        },
    ];

    const filteredData = data.filter(item => 
        item.name.toLowerCase().includes(searchText.toLowerCase()) || 
        item.email.toLowerCase().includes(searchText.toLowerCase())
    );

    return (
        <div className="animate-fade-in-up">
            <div style={{ marginBottom: 24 }}>
                <Title level={4} style={{ margin: 0 }}>Quản lý Doanh Nghiệp (Tenants)</Title>
                <Text type="secondary">Giám sát tài nguyên và trạng thái đăng ký của các công ty trên nền tảng SmartInvoice Shield.</Text>
            </div>

            <Card variant="borderless" style={{ borderRadius: 12 }}>
                <Row style={{ marginBottom: 16 }}>
                    <Col span={8}>
                        <Input 
                            prefix={<SearchOutlined />} 
                            placeholder="Tìm kiếm công ty, email..." 
                            value={searchText}
                            onChange={(e) => setSearchText(e.target.value)}
                            allowClear
                        />
                    </Col>
                </Row>
                <Table 
                    columns={columns} 
                    dataSource={filteredData} 
                    loading={loading}
                    pagination={{ pageSize: 10 }}
                />
            </Card>

            {/* MODAL CHI TIẾT DOANH NGHIỆP */}
            <Modal
                title={<Title level={4} style={{ margin: 0 }}>Thông tin chi tiết Doanh nghiệp</Title>}
                open={isModalVisible}
                onCancel={() => setIsModalVisible(false)}
                footer={[
                    <Button key="close" type="primary" onClick={() => setIsModalVisible(false)}>
                        Đóng
                    </Button>
                ]}
                width={850} // Tăng chiều rộng lên 850px để không bị rớt chữ
                centered
            >
                {selectedTenant && (
                    <Descriptions 
                        bordered 
                        column={2} 
                        size="middle" 
                        style={{ marginTop: 16 }}
                        labelStyle={{ width: '150px', fontWeight: 600, backgroundColor: '#f8fafc' }}
                    >
                        <Descriptions.Item label="Tên công ty" span={2}>
                            <Text strong className="text-lg text-blue-700">{selectedTenant.companyName}</Text>
                        </Descriptions.Item>
                        
                        <Descriptions.Item label="Mã số thuế">{selectedTenant.taxCode || '—'}</Descriptions.Item>
                        <Descriptions.Item label="Trạng thái">
                            {selectedTenant.isActive ? <Tag color="success">Đang hoạt động</Tag> : <Tag color="error">Đã khóa</Tag>}
                        </Descriptions.Item>
                        
                        <Descriptions.Item label="Email">{selectedTenant.email}</Descriptions.Item>
                        <Descriptions.Item label="Số điện thoại">{selectedTenant.phoneNumber || '—'}</Descriptions.Item>
                        
                        <Descriptions.Item label="Người đại diện">{selectedTenant.legalRepresentative || '—'}</Descriptions.Item>
                        <Descriptions.Item label="Gói dịch vụ">
                            <Tag color="purple" style={{ fontWeight: 'bold' }}>{selectedTenant.subscriptionTier || 'Free'}</Tag>
                        </Descriptions.Item>
                        
                        {/* HIỂN THỊ SỐ THÀNH VIÊN */}
                        <Descriptions.Item label="Thành viên">
                            <Text strong>{selectedTenant.currentActiveUsers}</Text> / {selectedTenant.maxUsers >= 999999 ? 'Không giới hạn' : selectedTenant.maxUsers}
                        </Descriptions.Item>
                        
                        <Descriptions.Item label="Hạn mức hóa đơn">
                            <Text strong>{selectedTenant.usedInvoicesThisMonth}</Text> / {selectedTenant.maxInvoicesPerMonth >= 999999 ? 'Không giới hạn' : selectedTenant.maxInvoicesPerMonth}
                        </Descriptions.Item>
                        
                        <Descriptions.Item label="Hóa đơn dự phòng">
                            <Text strong style={{ color: '#1677ff' }}>{selectedTenant.extraInvoicesBalance}</Text> khả dụng
                        </Descriptions.Item>
                        
                        <Descriptions.Item label="Dung lượng S3">
                            <Text strong>{formatBytes(selectedTenant.usedStorageBytes || 0)}</Text> / {selectedTenant.storageQuotaGB} GB
                        </Descriptions.Item>

                        <Descriptions.Item label="Địa chỉ" span={2}>{selectedTenant.address || '—'}</Descriptions.Item>
                    </Descriptions>
                )}
            </Modal>
        </div>
    );
};

export default TenantManagement;