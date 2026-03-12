import { Spin } from 'antd';
import { LoadingOutlined } from '@ant-design/icons';

interface ValidationProgressIndicatorProps {
  isValidating: boolean;
  lastUpdateTime: Date | null;
}

/**
 * Displays a live validation progress indicator with timestamp
 * 
 * Shows "Validating... (checked Xs ago)" while validation is in progress
 * Automatically hides when validation completes
 * 
 * @example
 * ```tsx
 * <ValidationProgressIndicator 
 *   isValidating={isRefreshing}
 *   lastUpdateTime={lastRefreshTime}
 * />
 * ```
 */
export function ValidationProgressIndicator({
  isValidating,
  lastUpdateTime,
}: ValidationProgressIndicatorProps) {
  const getTimeAgoText = (): string => {
    if (!lastUpdateTime) return '';

    const now = new Date();
    const diffSeconds = Math.floor((now.getTime() - lastUpdateTime.getTime()) / 1000);

    if (diffSeconds < 2) return 'just now';
    if (diffSeconds < 60) return `${diffSeconds}s ago`;
    if (diffSeconds < 3600) return `${Math.floor(diffSeconds / 60)}m ago`;
    return `${Math.floor(diffSeconds / 3600)}h ago`;
  };

  if (!isValidating && !lastUpdateTime) {
    return null;
  }

  return (
    <div className="flex items-center gap-2 py-2 px-3 bg-blue-50 rounded border border-blue-200">
      {isValidating && (
        <Spin
          indicator={<LoadingOutlined style={{ fontSize: 14 }} spin />}
          size="small"
        />
      )}
      <span className="text-sm text-gray-600">
        {isValidating && 'Validating'}
        {!isValidating && 'Validation in progress'}
        {lastUpdateTime && ` (checked ${getTimeAgoText()})`}
      </span>
    </div>
  );
}
