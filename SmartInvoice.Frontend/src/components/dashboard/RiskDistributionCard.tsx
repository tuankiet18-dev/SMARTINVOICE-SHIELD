import React from 'react';
import { Card, Space, Progress } from 'antd';
import { CheckCircleOutlined } from '@ant-design/icons';

interface RiskData {
    label: string;
    percent: number;
    color: string;
}

interface RiskDistributionCardProps {
    data: RiskData[];
}

const RiskDistributionCard: React.FC<RiskDistributionCardProps> = ({ data }) => {
    const safePercent = data.find(d => d.label.includes('Green') || d.label.includes('An toàn'))?.percent || 0;

    return (
        <Card
            bordered={false}
            className="bg-dash-card rounded-[14px] shadow-dash h-full"
            styles={{ body: { padding: '24px' } }}
        >
            <h3 className="text-dash-textMain font-bold text-lg mb-6">Phân bổ rủi ro</h3>
            <Space orientation="vertical" className="w-full" size={24}>
                {data.map((item, i) => (
                    <div key={i}>
                        <div className="flex justify-between mb-2">
                            <span className="text-dash-textMuted font-medium text-sm">{item.label}</span>
                            <span className="text-dash-textMain font-bold text-sm">{item.percent}%</span>
                        </div>
                        <Progress
                            percent={item.percent}
                            showInfo={false}
                            strokeColor={item.color}
                            railColor="#E2E8F0"
                            size="small"
                        />
                    </div>
                ))}
            </Space>

            <div className="mt-8 p-4 rounded-xl bg-dash-success/10 border border-dash-success/20">
                <p className="text-dash-success font-bold text-sm m-0 flex items-center gap-2">
                    <CheckCircleOutlined />
                    {safePercent}% hóa đơn đạt chuẩn an toàn
                </p>
            </div>
        </Card>
    );
};

export default RiskDistributionCard;
