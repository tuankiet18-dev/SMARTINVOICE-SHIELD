import React from "react";
import { Modal, Button, Space, Typography, Result } from "antd";
import { ExclamationCircleOutlined } from "@ant-design/icons";

const { Paragraph, Text } = Typography;

interface LeaveUploadModalProps {
  open: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}

const LeaveUploadModal: React.FC<LeaveUploadModalProps> = ({
  open,
  onConfirm,
  onCancel,
}) => {
  return (
    <Modal
      title={
        <Space>
          <ExclamationCircleOutlined style={{ color: "#faad14", fontSize: 20 }} />
          <span>Cảnh báo: Danh sách sẽ bị mất</span>
        </Space>
      }
      open={open}
      footer={null}
      onCancel={onCancel}
      width={500}
      centered
      closable={true}
    >
      <Result
        status="warning"
        title="Bạn có chắc muốn rời khỏi trang này?"
        subTitle="Danh sách hóa đơn đã xử lý sẽ bị mất nếu bạn không lưu. Bạn có thể gửi duyệt các hóa đơn trước khi rời đi."
        extra={
          <Space>
            <Button type="default" size="large" onClick={onCancel}>
              Ở lại trang upload
            </Button>
            <Button type="primary" danger size="large" onClick={onConfirm}>
              Rời khỏi trang
            </Button>
          </Space>
        }
      />
    </Modal>
  );
};

export default LeaveUploadModal;
