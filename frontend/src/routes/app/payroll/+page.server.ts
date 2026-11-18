import { fail, redirect } from '@sveltejs/kit';
import { serverApiRequest, ServerApiError } from '$lib/server/api';
import { can, permissions } from '$lib/auth/permissions';
import type { PayrollSummary } from '$lib/types/payroll';
import type { ShopRole } from '$lib/types/shop-users';
import type { Actions, PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ parent, cookies, fetch, url }) => {
	const parentData = await parent();
	if (!can(parentData.user.permissions, permissions.payrollRead)) {
		throw redirect(303, '/app');
	}
	const token = cookies.get('servora_session');

	const cycle = (url.searchParams.get('cycle') === 'weekly' ? 'weekly' : 'monthly') as 'weekly' | 'monthly';
	const now = new Date();

	let defaultFrom = '';
	let defaultTo = '';
	if (cycle === 'weekly') {
		const dayOfWeek = now.getDay();
		const diffToMonday = (dayOfWeek + 6) % 7;
		const monday = new Date(now);
		monday.setDate(now.getDate() - diffToMonday);
		const sunday = new Date(monday);
		sunday.setDate(monday.getDate() + 6);
		defaultFrom = `${monday.getFullYear()}-${String(monday.getMonth() + 1).padStart(2, '0')}-${String(monday.getDate()).padStart(2, '0')}`;
		defaultTo = `${sunday.getFullYear()}-${String(sunday.getMonth() + 1).padStart(2, '0')}-${String(sunday.getDate()).padStart(2, '0')}`;
	} else {
		const lastDay = new Date(now.getFullYear(), now.getMonth() + 1, 0).getDate();
		defaultFrom = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-01`;
		defaultTo = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(lastDay).padStart(2, '0')}`;
	}

	const from = url.searchParams.get('from') || defaultFrom;
	const to = url.searchParams.get('to') || defaultTo;
	const roleId = url.searchParams.get('roleId') || '';
	const includeFullTime = url.searchParams.get('includeFullTime') === 'true';

	const query = new URLSearchParams({
		from,
		to,
		cycle,
		includeFullTime: String(includeFullTime)
	});
	if (roleId) query.set('roleId', roleId);

	const [summary, roles] = await Promise.all([
		serverApiRequest<PayrollSummary>(fetch, `/payroll/summary?${query.toString()}`, { token })
			.catch(() => ({
				from,
				to,
				cycle,
				totals: {
					totalFreelanceWorkers: 0,
					totalDaysWorked: 0,
					totalApprovedHours: 0,
					totalPendingShifts: 0,
					totalBaseWages: 0,
					totalBonuses: 0,
					totalAdjustments: 0,
					totalPayableAmount: 0
				},
				workers: []
			})),
		serverApiRequest<ShopRole[]>(fetch, '/shop-users/roles', { token }).catch(() => [])
	]);

	return {
		summary,
		roles,
		filters: {
			from: summary.from || from,
			to: summary.to || to,
			cycle: summary.cycle || cycle,
			roleId,
			includeFullTime
		}
	};
};

export const actions: Actions = {
	addAdjustment: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const userId = Number(form.get('userId') ?? 0);
		const periodStart = String(form.get('periodStart') ?? '');
		const periodEnd = String(form.get('periodEnd') ?? '');
		const amountRaw = String(form.get('amount') ?? '').trim();
		const amount = amountRaw && /^-?\d+(?:\.\d+)?$/.test(amountRaw) ? Number(amountRaw) : 0;
		const reason = String(form.get('reason') ?? '').trim();

		if (!Number.isFinite(amount) || amount === 0) {
			return fail(400, {
				success: false,
				messageKey: 'payroll.adjustmentAmountNonZero'
			});
		}

		try {
			await serverApiRequest(fetch, '/payroll/adjustments', {
				method: 'POST',
				token,
				body: {
					userId,
					periodStart,
					periodEnd,
					amount,
					reason
				}
			});
			return { success: true, messageKey: 'payroll.adjustmentSaved' };
		} catch (error) {
			if (error instanceof ServerApiError) {
				return fail(error.status, {
					success: false,
					message: error.message
				});
			}
			throw error;
		}
	},
	deleteAdjustment: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const id = Number(form.get('id') ?? 0);

		try {
			await serverApiRequest(fetch, `/payroll/adjustments/${id}`, {
				method: 'DELETE',
				token
			});
			return { success: true, messageKey: 'payroll.adjustmentDeleted' };
		} catch (error) {
			if (error instanceof ServerApiError) {
				return fail(error.status, {
					success: false,
					message: error.message
				});
			}
			throw error;
		}
	}
};

