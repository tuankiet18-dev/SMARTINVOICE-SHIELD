import { useQuery } from '@tanstack/react-query';
import { paymentService, type CurrentSubscriptionDto } from '@/services/payment';

export type FeatureFlag =
  | 'hasAiProcessing'
  | 'hasAdvancedWorkflow'
  | 'hasRiskWarning'
  | 'hasAuditLog'
  | 'hasErpIntegration';

export function useFeatureFlags() {
  const { data, isLoading } = useQuery<CurrentSubscriptionDto>({
    queryKey: ['current-subscription'],
    queryFn: paymentService.getCurrentSubscription,
    staleTime: 5 * 60 * 1000, // cache 5 min
  });

  const check = (flag: FeatureFlag): boolean => data?.[flag] ?? false;

  return {
    subscription: data,
    isLoading,
    check,
    hasAiProcessing: data?.hasAiProcessing ?? false,
    hasAdvancedWorkflow: data?.hasAdvancedWorkflow ?? false,
    hasRiskWarning: data?.hasRiskWarning ?? false,
    hasAuditLog: data?.hasAuditLog ?? false,
    hasErpIntegration: data?.hasErpIntegration ?? false,
    packageLevel: data?.packageLevel ?? 0,
    usedInvoicesThisMonth: data?.usedInvoicesThisMonth ?? 0,
    extraInvoicesBalance: data?.extraInvoicesBalance ?? 0,
    maxInvoicesPerMonth: data?.maxInvoicesPerMonth ?? 0,
  };
}
