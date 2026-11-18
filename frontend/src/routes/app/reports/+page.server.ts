import { redirect } from '@sveltejs/kit';
import { serverApiRequest } from '$lib/server/api';
import { can, permissions } from '$lib/auth/permissions';
import type { ReportSummary } from '$lib/types/reports';
import type { PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ parent, cookies, fetch, url }) => {
	const parentData = await parent();
	if (!can(parentData.user.permissions, permissions.reportsRead)) {
		throw redirect(303, '/app');
	}
	const token = cookies.get('servora_session');
	const filters = normalizeFilters(url.searchParams);
	const query = new URLSearchParams(filters).toString();
	const report = await serverApiRequest<ReportSummary>(fetch, `/reports/summary?${query}`, { token })
		.catch(() => emptyReport(filters));

	return { report: normalizeReport(report, filters), filters };
};

function normalizeFilters(searchParams: URLSearchParams) {
	const today = new Date();
	const from = searchParams.get('from') ?? toDateInput(new Date(today.getTime() - 30 * 24 * 60 * 60 * 1000));
	const to = searchParams.get('to') ?? toDateInput(today);
	const groupBy = searchParams.get('groupBy') ?? 'daily';

	return { from, to, groupBy };
}

function toDateInput(date: Date) {
	return date.toISOString().slice(0, 10);
}

function normalizeReport(report: ReportSummary, filters: { from: string; to: string; groupBy: string }): ReportSummary {
	const empty = emptyReport(filters);
	return {
		...empty,
		...report,
		totals: { ...empty.totals, ...report.totals },
		serviceRows: report.serviceRows ?? [],
		paymentRows: report.paymentRows ?? [],
		inventoryRows: report.inventoryRows ?? [],
		inventoryMovementRows: report.inventoryMovementRows ?? [],
		inventoryLowStockGroups: report.inventoryLowStockGroups ?? [],
		inventoryValuationRows: report.inventoryValuationRows ?? [],
		agingRows: report.agingRows ?? [],
		approvalRows: report.approvalRows ?? []
	};
}

function emptyReport(filters: { from: string; to: string; groupBy: string }): ReportSummary {
	return {
		from: filters.from,
		to: filters.to,
		groupBy: filters.groupBy,
		canViewCost: false,
		totals: {
			receivedTickets: 0,
			completedTickets: 0,
			cancelledTickets: 0,
			unpaidTickets: 0,
			partialPaidTickets: 0,
			totalRevenue: 0,
			totalInventoryCost: null,
			totalInventoryValue: null,
			grossProfit: null,
			lowStockItems: 0
		},
		serviceRows: [],
		paymentRows: [],
		inventoryRows: [],
		inventoryMovementRows: [],
		inventoryLowStockGroups: [],
		inventoryValuationRows: [],
		agingRows: [],
		approvalRows: []
	};
}
