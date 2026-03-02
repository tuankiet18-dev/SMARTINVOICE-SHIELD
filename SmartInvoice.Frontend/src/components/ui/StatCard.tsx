import React from 'react';
import { Card } from 'antd';
import { ArrowUpOutlined, ArrowDownOutlined } from '@ant-design/icons';

interface StatCardProps {
    title: string;
    value: string | number;
    icon: React.ReactNode;
    iconBgClass: string;
    iconColorClass: string;
    changeValue: number;
    isUp: boolean;
    changeText?: string;
}

const StatCard: React.FC<StatCardProps> = ({
    title,
    value,
    icon,
    iconBgClass,
    iconColorClass,
    changeValue,
    isUp,
    changeText = 'so với hôm qua',
}) => {
    return (
        <Card
            bordered={false}
            className="bg-dash-card rounded-[14px] shadow-dash h-full"
            bodyStyle={{ padding: '24px' }}
        >
            <div className="flex justify-between items-start">
                <div>
                    <p className="text-dash-textMuted font-medium text-base mb-4">{title}</p>
                    <h2 className="text-dash-textMain font-bold text-3xl mb-0">
                        {typeof value === 'number' ? value.toLocaleString() : value}
                    </h2>
                </div>
                <div className={`w-14 h-14 rounded-2xl flex items-center justify-center text-2xl ${iconBgClass} ${iconColorClass}`}>
                    {icon}
                </div>
            </div>
            <div className="mt-6 flex items-center gap-2">
                <span className={`flex items-center gap-1 font-bold text-sm ${isUp ? 'text-dash-success' : 'text-dash-danger'}`}>
                    {isUp ? <ArrowUpOutlined /> : <ArrowDownOutlined />}
                    {changeValue}%
                </span>
                <span className="text-dash-textMuted font-medium text-sm">{changeText}</span>
            </div>
        </Card>
    );
};

export default StatCard;
