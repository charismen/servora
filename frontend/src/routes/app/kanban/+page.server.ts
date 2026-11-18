import { fail } from '@sveltejs/kit';
import { serverApiRequest, ServerApiError } from '$lib/server/api';
import { createTranslator } from '$lib/i18n';
import type { Actions, PageServerLoad } from './$types';
import type { KanbanColumn } from '$lib/types/service-tickets';
import type { StaffUser } from '$lib/types/shop-users';

function buildKanbanPath(searchParams: URLSearchParams) {
	const query = new URLSearchParams();

	for (const key of ['search', 'priority', 'paymentStatus', 'approvalStatus', 'technicianUserId', 'myTickets', 'from', 'to', 'terminalAge', 'activeOnly']) {
		const value = searchParams.get(key);
		if (value) query.set(key, value);
	}

	const suffix = query.toString();
	return `/service-tickets/kanban${suffix ? `?${suffix}` : ''}`;
}

export const load: PageServerLoad = async ({ parent, cookies, fetch, url }) => {
	await parent();
	const token = cookies.get('servora_session');

	try {
		const [columns, staff] = await Promise.all([
			serverApiRequest<KanbanColumn[]>(fetch, buildKanbanPath(url.searchParams), { token }),
			serverApiRequest<StaffUser[]>(fetch, '/shop-users/staff', { token }).catch(() => [])
		]);
		return {
			columns,
			staff,
			filters: {
				search: url.searchParams.get('search') ?? '',
				priority: url.searchParams.get('priority') ?? '',
				paymentStatus: url.searchParams.get('paymentStatus') ?? '',
				approvalStatus: url.searchParams.get('approvalStatus') ?? '',
				technicianUserId: url.searchParams.get('technicianUserId') ?? '',
				myTickets: url.searchParams.get('myTickets') === 'true',
				from: url.searchParams.get('from') ?? '',
				to: url.searchParams.get('to') ?? '',
				terminalAge: url.searchParams.get('terminalAge') ?? '30',
				activeOnly: url.searchParams.get('activeOnly') === 'true'
			}
		};
	} catch (error) {
		console.error('Failed to load Kanban board:', error);
		return {
			columns: [],
			staff: [],
			filters: {
				search: url.searchParams.get('search') ?? '',
				priority: url.searchParams.get('priority') ?? '',
				paymentStatus: url.searchParams.get('paymentStatus') ?? '',
				approvalStatus: url.searchParams.get('approvalStatus') ?? '',
				technicianUserId: url.searchParams.get('technicianUserId') ?? '',
				myTickets: url.searchParams.get('myTickets') === 'true',
				from: url.searchParams.get('from') ?? '',
				to: url.searchParams.get('to') ?? '',
				terminalAge: url.searchParams.get('terminalAge') ?? '30',
				activeOnly: url.searchParams.get('activeOnly') === 'true'
			}
		};
	}
};

export const actions: Actions = {
	moveTicket: async ({ request, cookies, fetch, locals }) => {
		const t = createTranslator(locals.locale);
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const ticketId = Number(form.get('ticketId'));
		const statusId = Number(form.get('statusId'));

		if (!ticketId || !statusId) {
			return fail(400, { message: t('kanban.ticketAndStatusRequired') });
		}

		try {
			await serverApiRequest(fetch, `/service-tickets/${ticketId}/move`, {
				method: 'POST',
				token,
				body: { statusId, reason: form.get('reason')?.toString() || null, overridePaymentBlock: false }
			});
			return { success: true };
		} catch (error) {
			if (error instanceof ServerApiError) return fail(error.status, { message: error.message });
			throw error;
		}
	}
};
