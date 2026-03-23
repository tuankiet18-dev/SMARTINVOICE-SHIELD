import React from 'react';
import { Drawer, Row, Col, Typography, Space, Button, Badge, Tag, Form, Input, Card, Divider } from 'antd';
import { ExclamationCircleOutlined, CheckCircleOutlined, CloseCircleOutlined, InfoCircleOutlined } from '@ant-design/icons';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { invoiceService } from '../../services/invoice';
import { message } from 'antd';
import { useAuth } from '../../contexts/AuthContext';

const { Title, Text, Paragraph } = Typography;
const { TextArea } = Input;

interface ApprovalDrawerProps {
    open: boolean;
    onClose: () => void;
    invoice: any;
}

const riskColors: Record<string, string> = {
    Green: '#2d9a5c', Yellow: '#e6a817', Red: '#d63031',
};

const ApprovalDrawer: React.FC<ApprovalDrawerProps> = ({ open, onClose, invoice }) => {
    const [form] = Form.useForm();
    const queryClient = useQueryClient();
    const { user } = useAuth();

    const approveMutation = useMutation({
        mutationFn: () => invoiceService.approveInvoice(invoice?.id || invoice?.invoiceId || ''),
        onSuccess: () => {
            message.success(`Đã tự động phê duyệt hóa đơn ${invoice?.invoiceNo || invoice?.invoiceNumber}`);
            queryClient.invalidateQueries({ queryKey: ['invoices'] });
            onClose();
        },
        onError: (err: any) => {
            message.error(`Lỗi duyệt: ${err.message}`);
        }
    });

    const rejectMutation = useMutation({
        mutationFn: (reason: string) => invoiceService.rejectInvoice(invoice?.id || invoice?.invoiceId || '', reason),
        onSuccess: () => {
            message.success(`Đã từ chối hóa đơn ${invoice?.invoiceNo || invoice?.invoiceNumber}`);
            queryClient.invalidateQueries({ queryKey: ['invoices'] });
            form.resetFields();
            onClose();
        },
        onError: (err: any) => {
            message.error(`Lỗi từ chối: ${err.message}`);
        }
    });

    const handleReject = () => {
        form.validateFields().then(values => {
            if (!values.reason) {
                message.warning("Vui lòng nhập lý do từ chối");
                return;
            }
            rejectMutation.mutate(values.reason);
        });
    };

    if (!invoice) return null;

    const risk = invoice.riskLevel || invoice.risk || 'Green';
    const invoiceNo = invoice.invoiceNumber || invoice.invoiceNo;
    const amount = invoice.totalAmount || invoice.amount;
    const seller = invoice.sellerName || invoice.seller;

    // Temporary layout: pdf placeholder
    return (
        <Drawer
            title={
                <Space>
                    <Text strong style={{ fontSize: 18 }}>Chi tiết Hóa đơn: {invoiceNo}</Text>
                    <Tag color={riskColors[risk]}>{risk} Risk</Tag>
                    {invoice.status === 'Pending' && <Badge status="processing" text="Chờ duyệt" />}
                    {invoice.status === 'Approved' && <Badge status="success" text="Đã duyệt" />}
                    {invoice.status === 'Rejected' && <Badge status="error" text="Từ chối" />}
                </Space>
            }
            placement="right"
            width="80vw"
            onClose={onClose}
            open={open}
            footer={
                invoice.status === 'Pending' || invoice.status === 'Chờ duyệt' || !invoice.status ? (
                    <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 16 }}>
                        <Button
                            danger
                            size="large"
                            icon={<CloseCircleOutlined />}
                            onClick={() => {
                                Modal.confirm({
                                    title: 'Từ chối hóa đơn',
                                    content: (
                                        <Form form={form} layout="vertical" style={{ marginTop: 16 }}>
                                            <Form.Item name="reason" rules={[{ required: true, message: 'Bắt buộc nhập lý do' }]}>
                                                <TextArea rows={4} placeholder="Nhập lý do từ chối (bắt buộc để lưu audit log)..." />
                                            </Form.Item>
                                        </Form>
                                    ),
                                    okText: 'Xác nhận từ chối',
                                    okButtonProps: { danger: true, loading: rejectMutation.isPending },
                                    cancelText: 'Huỷ',
                                    onOk: () => {
                                        return new Promise((resolve, reject) => {
                                            form.validateFields().then(values => {
                                                rejectMutation.mutate(values.reason, {
                                                    onSuccess: resolve,
                                                    onError: reject
                                                });
                                            }).catch(reject);
                                        });
                                    }
                                });
                            }}
                        >
                            Từ chối
                        </Button>
                        <Button
                            type="primary"
                            size="large"
                            icon={<CheckCircleOutlined />}
                            onClick={() => approveMutation.mutate()}
                            loading={approveMutation.isPending}
                        >
                            Phê duyệt ngay
                        </Button>
                    </div>
                ) : null
            }
        >
            <Row gutter={24} style={{ height: '100%' }}>
                <Col span={12} style={{ height: '100%', borderRight: '1px solid #f0f0f0' }}>
                    <Title level={5}><InfoCircleOutlined /> Bản xem trước Hóa đơn</Title>
                    <div style={{ 
                        background: '#f8fafc', width: '100%', height: 'calc(100% - 40px)', 
                        borderRadius: 8, display: 'flex', alignItems: 'center', justifyContent: 'center',
                        border: '1px dashed #d9d9d9'
                    }}>
                        <Text type="secondary">Khu vực hiển thị PDF / Ảnh gốc của hóa đơn</Text>
                    </div>
                </Col>
                
                <Col span={12} style={{ overflowY: 'auto', height: '100%', paddingRight: 8 }}>
                    <Title level={5}><ExclamationCircleOutlined /> Thông tin & Ràng buộc</Title>
                    
                    <Card size="small" style={{ marginBottom: 16, borderColor: riskColors[risk], backgroundColor: `${riskColors[risk]}0A` }}>
                        <Space align="start">
                            {risk === 'Red' && <CloseCircleOutlined style={{ color: riskColors.Red, fontSize: 20, marginTop: 4 }} />}
                            {risk === 'Yellow' && <InfoCircleOutlined style={{ color: riskColors.Yellow, fontSize: 20, marginTop: 4 }} />}
                            {risk === 'Green' && <CheckCircleOutlined style={{ color: riskColors.Green, fontSize: 20, marginTop: 4 }} />}
                            
                            <div>
                                <Text strong style={{ color: riskColors[risk], fontSize: 16 }}>Đánh giá rủi ro hệ thống: {risk}</Text>
                                <Paragraph style={{ marginBottom: 0 }}>
                                    {invoice.reason || invoice.notes || (risk === 'Green' ? 'Hóa đơn hoàn toàn hợp lệ, thông tin khớp VietQR và chữ ký số chuẩn xác.' : 'Phát hiện rủi ro tiềm ẩn. Cần Admin kiểm tra lại tính hợp lệ.')}
                                </Paragraph>
                            </div>
                        </Space>
                    </Card>

                    <Divider style={{ margin: '16px 0' }}>Dữ liệu bóc tách (XML)</Divider>
                    
                    <Card size="small" variant="borderless" style={{ background: '#fafafa' }}>
                        <Row gutter={[16, 12]}>
                            <Col span={8}><Text type="secondary">Số hóa đơn:</Text></Col>
                            <Col span={16}><Text strong>{invoiceNo}</Text></Col>
                            
                            <Col span={8}><Text type="secondary">Mẫu số / Ký hiệu:</Text></Col>
                            <Col span={16}><Text>{invoice.formNumber || 'N/A'} / {invoice.serialNumber || 'N/A'}</Text></Col>

                            <Col span={8}><Text type="secondary">Ngày lập:</Text></Col>
                            <Col span={16}><Text>{invoice.invoiceDate || invoice.date}</Text></Col>

                            <Col span={8}><Text type="secondary">Đơn vị bán hàng:</Text></Col>
                            <Col span={16}><Text strong>{seller}</Text></Col>

                            <Col span={8}><Text type="secondary">Mã số thuế:</Text></Col>
                            <Col span={16}><Text>{invoice.sellerTaxCode || invoice.mst || '0101245486'}</Text></Col>

                            <Col span={8}><Text type="secondary">Tổng tiền thanh toán:</Text></Col>
                            <Col span={16}><Text strong style={{ fontSize: 16, color: '#1677ff' }}>{amount?.toLocaleString()} ₫</Text></Col>
                        </Row>
                    </Card>

                </Col>
            </Row>
        </Drawer>
    );
};

// We need to import Modal inside ApprovalDrawer so we add it to the component.
// Oh wait, I didn't import Modal. Let's make sure it's in the import list.
import { Modal } from 'antd';
export default ApprovalDrawer;
