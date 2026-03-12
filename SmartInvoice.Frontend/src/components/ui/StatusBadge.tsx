import React from 'react';
import './StatusBadge.css';

type BadgeType = 'status' | 'risk';

interface StatusBadgeProps {
    type: BadgeType;
    value: string;
    /**
     * Indicates if validation is pending (for Draft status invoices)
     * Shows pulsing animation when true
     */
    isPending?: boolean;
}

const statusMap: Record<string, { label: string; bgClass: string; textClass: string }> = {
    Approved: { label: 'Đã duyệt', bgClass: 'bg-[#00B69B]/10', textClass: 'text-dash-success' },
    Pending: { label: 'Chờ duyệt', bgClass: 'bg-[#FF9500]/10', textClass: 'text-dash-warning' },
    Draft: { label: 'Nháp', bgClass: 'bg-[#E2E8F0]/30', textClass: 'text-dash-textMuted' },
    Rejected: { label: 'Từ chối', bgClass: 'bg-[#FC2A46]/10', textClass: 'text-dash-danger' },
};

const riskMap: Record<string, { label: string; bgClass: string; textClass: string }> = {
    Green: { label: 'Đạt (Green)', bgClass: 'bg-[#00B69B]/10', textClass: 'text-dash-success' },
    Yellow: { label: 'Lưu ý (Yellow)', bgClass: 'bg-[#FF9500]/10', textClass: 'text-dash-warning' },
    Red: { label: 'Không đạt (Red)', bgClass: 'bg-[#FC2A46]/10', textClass: 'text-dash-danger' },
};

const StatusBadge: React.FC<StatusBadgeProps> = ({ type, value, isPending = false }) => {
    let mapping;

    if (type === 'status') {
        mapping = statusMap[value] || { label: value, bgClass: 'bg-gray-100', textClass: 'text-dash-textMuted' };
    } else {
        mapping = riskMap[value] || { label: value, bgClass: 'bg-gray-100', textClass: 'text-dash-textMuted' };
    }

    const shouldPulse = isPending && type === 'status' && value === 'Draft';

    return (
        <span className={`inline-block px-2.5 py-1 rounded-full text-xs font-bold whitespace-nowrap ${mapping.bgClass} ${mapping.textClass} ${shouldPulse ? 'pulse-badge' : ''}`}>
            {type === 'risk' ? value : mapping.label}
            {shouldPulse && ' 🔄'}
        </span>
    );
};

export default StatusBadge;
