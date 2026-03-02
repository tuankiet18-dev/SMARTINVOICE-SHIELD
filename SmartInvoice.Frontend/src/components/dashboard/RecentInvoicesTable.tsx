import React from 'react';
import { Card, Table, Typography } from 'antd';
import StatusBadge from '../ui/StatusBadge';

const { Text } = Typography;

interface Invoice {
    key?: string;
    invoiceNo: string;
    seller: string;
    amount: string | number;
    date: string;
    status: string;
    risk: string;
    type?: string;
    method?: string;
    mst?: string;
}

interface RecentInvoicesTableProps {
    invoices: Invoice[];
    isLoading: boolean;
    onViewAll?: () => void;
}

const columns = [
    {
        title: 'Số hóa đơn',
        dataIndex: 'invoiceNo',
        key: 'invoiceNo',
        render: (text: string, record: any) => (
            <div>
                <Text className="text-dash-textMain font-bold block">{text}</Text>
                {record.type && (
                    <Text className="text-dash-textMuted text-xs">{record.type} • {record.method}</Text>
                )}
            </div>
        ),
    },
    {
        title: 'Người bán',
        dataIndex: 'seller',
        key: 'seller',
        render: (text: string, record: any) => (
            <div>
                <Text className="text-dash-textMain font-medium text-sm block">{text}</Text>
                {record.mst && (
                    <Text className="text-dash-textMuted text-xs">MST: {record.mst}</Text>
                )}
            </div>
        ),
    },
    {
        title: 'Tổng tiền',
        dataIndex: 'amount',
        key: 'amount',
        render: (text: string | number) => <Text className="text-dash-textMain font-bold">{text}</Text>,
    },
    {
        title: 'Ngày lập',
        dataIndex: 'date',
        key: 'date',
        render: (text: string) => <Text className="text-dash-textMuted font-medium text-sm">{text}</Text>,
    },
    {
        title: 'Trạng thái',
        dataIndex: 'status',
        key: 'status',
        render: (status: string) => <StatusBadge type="status" value={status} />,
    },
    {
        title: 'Rủi ro',
        dataIndex: 'risk',
        key: 'risk',
        render: (risk: string) => <StatusBadge type="risk" value={risk} />,
    },
];

const RecentInvoicesTable: React.FC<RecentInvoicesTableProps> = ({ invoices, isLoading, onViewAll }) => {
    return (
        <Card
            bordered={false}
            className="bg-dash-card rounded-[14px] shadow-dash h-full overflow-hidden"
            bodyStyle={{ padding: '24px 0 0 0' }}
        >
            <div className="flex justify-between items-center mb-6 px-6">
                <h3 className="text-dash-textMain font-bold text-lg m-0">Hóa đơn hệ thống</h3>
                {onViewAll && (
                    <a onClick={onViewAll} className="text-dash-primary font-bold text-sm cursor-pointer hover:opacity-80">
                        Xem tất cả
                    </a>
                )}
            </div>

            <div className="overflow-x-auto">
                <Table
                    columns={columns}
                    dataSource={invoices}
                    loading={isLoading}
                    pagination={false}
                    rowClassName={() => 'hover:bg-dash-bg/50 transition-colors'}
                    components={{
                        header: {
                            cell: (props: any) => (
                                <th {...props} className="bg-[#F9F9FB] text-dash-textMain font-semibold border-y border-dash-border py-4 px-6 text-left" />
                            )
                        },
                        body: {
                            cell: (props: any) => (
                                <td {...props} className="py-5 px-6 border-b border-dash-border" />
                            )
                        }
                    }}
                />
            </div>
        </Card>
    );
};

export default RecentInvoicesTable;
