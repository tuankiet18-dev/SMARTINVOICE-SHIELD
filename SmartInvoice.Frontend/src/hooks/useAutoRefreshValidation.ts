import { useEffect, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';

/**
 * Custom hook that auto-refreshes invoice validation data while validation is in progress
 * 
 * @param invoiceId - The invoice ID to refresh
 * @param isValidationPending - Whether validation is still pending (Draft status + no results)
 * @param pollIntervalMs - Polling interval in milliseconds (default 3000ms)
 * @returns Object with isRefreshing state and lastRefreshTime
 * 
 * @example
 * ```tsx
 * const isValidationPending = invoiceData?.status === 'Draft' && 
 *   !invoiceData?.validationResults?.layerResults?.length;
 * const { isRefreshing, lastRefreshTime } = useAutoRefreshValidation(
 *   invoiceId,
 *   isValidationPending,
 *   3000
 * );
 * ```
 */
export function useAutoRefreshValidation(
  invoiceId: string,
  isValidationPending: boolean,
  pollIntervalMs: number = 3000
) {
  const queryClient = useQueryClient();
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [lastRefreshTime, setLastRefreshTime] = useState<Date | null>(null);

  useEffect(() => {
    if (!isValidationPending || !invoiceId) {
      return;
    }

    // Initial refresh
    const refresh = async () => {
      setIsRefreshing(true);
      try {
        await queryClient.invalidateQueries({
          queryKey: ['invoice-detail', invoiceId],
          refetchType: 'active',
        });
        setLastRefreshTime(new Date());
      } finally {
        setIsRefreshing(false);
      }
    };

    // Perform first refresh immediately
    refresh();

    // Set up interval for subsequent refreshes
    const intervalId = setInterval(refresh, pollIntervalMs);

    // Cleanup on unmount or when validation completes
    return () => {
      clearInterval(intervalId);
    };
  }, [invoiceId, isValidationPending, pollIntervalMs, queryClient]);

  return {
    isRefreshing,
    lastRefreshTime,
  };
}
