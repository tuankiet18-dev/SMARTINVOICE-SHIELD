import React from 'react';

type BadgeType = 'status' | 'risk';

interface StatusBadgeProps {
    type: BadgeType;
    value: string;
}

const statusMap: Record<string, { label: string; bgClass: string; textClass: string }> = {
    Approved: { label: 'Đã duyệt', bgClass: 'bg-[#00B69B]/10', textClass: 'text-dash-success' },
    Pending: { label: 'Chờ duyệt', bgClass: 'bg-[#FF9500]/10', textClass: 'text-dash-warning' },
    Draft: { label: 'Nháp', bgClass: 'bg-[#E2E8F0]/30', textClass: 'text-dash-textMuted' },
    Rejected: { label: 'Từ chối', bgClass: 'bg-[#FC2A46]/10', textClass: 'text-dash-danger' },
};

const riskMap: Record<string, { label: string; bgClass: string; textClass: string }> = {
    Green: { label: 'An toàn (Green)', bgClass: 'bg-[#00B69B]/10', textClass: 'text-dash-success' },
    Yellow: { label: 'Lưu ý (Yellow)', bgClass: 'bg-[#FF9500]/10', textClass: 'text-dash-warning' },
    Orange: { label: 'Cảnh báo (Orange)', bgClass: 'bg-[#FD7E14]/10', textClass: 'text-[#FD7E14]' },
    Red: { label: 'Nguy hiểm (Red)', bgClass: 'bg-[#FC2A46]/10', textClass: 'text-dash-danger' },
};

const StatusBadge: React.FC<StatusBadgeProps> = ({ type, value }) => {
    let mapping;

    if (type === 'status') {
        mapping = statusMap[value] || { label: value, bgClass: 'bg-gray-100', textClass: 'text-dash-textMuted' };
    } else {
        mapping = riskMap[value] || { label: value, bgClass: 'bg-gray-100', textClass: 'text-dash-textMuted' };
    }

    return (
        <span className={`px-4 py-1.5 rounded-full text-xs font-bold ${mapping.bgClass} ${mapping.textClass}`}>
            {type === 'risk' ? value : mapping.label}
        </span>
    );
};

export default StatusBadge;
