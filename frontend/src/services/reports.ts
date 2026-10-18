import { apiClient } from '@/services/api';

export interface RevenueQuery { fromDate: string; toDate: string; groupBy: string; top: string; sortBy: string; cashierId?: number }
export interface RevenueReport {
  filter: RevenueQuery;
  generatedAt: string;
  summary: { grossRevenue: number; totalRefunded: number; netRevenue: number; totalRevenue: number; totalInvoiced: number; totalDiscount: number; totalOutstanding: number; temporaryCollections: number; cancelledInvoiceCount: number };
  dailyRevenue: { date: string; revenue: number; transactionCount: number; grossRevenue: number; totalRefunded: number }[];
  revenueTimeline: { label: string; revenue: number; transactionCount: number }[];
  byPaymentMethod: { method: string; amount: number; grossRevenue: number; totalRefunded: number; percentage: number }[];
  byCashier: { cashierId: number; name: string; amount: number; transactionCount: number }[];
  topServices: { serviceCode: string; serviceName: string; usageCount: number; revenue: number }[];
}
export const reportsService = {
  async revenue(params: RevenueQuery): Promise<RevenueReport> {
    return (await apiClient.get<RevenueReport>('/reports/revenue', { params })).data;
  },
  async exportRevenue(params: RevenueQuery): Promise<{ blob: Blob; filename: string }> {
    const response = await apiClient.get<Blob>('/reports/revenue/export', { params, responseType: 'blob' });
    if (!String(response.headers['content-type']).toLowerCase().startsWith('text/csv')) throw new Error('Invalid report export');
    const disposition = String(response.headers['content-disposition'] ?? '');
    const filename = disposition.match(/BaoCao_DoanhThu_\d{8}_\d{8}_\d{6}\.csv/)?.[0];
    if (!filename) throw new Error('Invalid report filename');
    return { blob: response.data, filename };
  }
};
