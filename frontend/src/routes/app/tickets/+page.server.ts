import { fail } from '@sveltejs/kit';
import { serverApiRequest, ServerApiError } from '$lib/server/api';
import type { Actions, PageServerLoad } from './$types';
import type { TicketSummaryPage } from '$lib/types/service-tickets';
import type { ServiceStatus } from '$lib/types/service-statuses';

export const load: PageServerLoad = async ({ parent, cookies, fetch, url }) => {
	await parent();
	const token = cookies.get('servora_session');
	const query = new URLSearchParams();
	for (const key of ['page', 'pageSize', 'search', 'status', 'priority', 'sort', 'myTickets']) {
		const value = url.searchParams.get(key);
		if (value) query.set(key, value);
	}
	if (!query.has('page')) query.set('page', '1');
	if (!query.has('pageSize')) query.set('pageSize', '20');

	try {
		const [tickets, statuses] = await Promise.all([
			serverApiRequest<TicketSummaryPage>(fetch, `/service-tickets?${query}`, { token }),
			serverApiRequest<ServiceStatus[]>(fetch, '/service-statuses', { token }).catch(() => [])
		]);
		return {
			tickets,
			statuses,
			filters: {
				search: url.searchParams.get('search') ?? '',
				status: url.searchParams.get('status') ?? '',
				priority: url.searchParams.get('priority') ?? '',
				sort: url.searchParams.get('sort') ?? 'newest',
				myTickets: url.searchParams.get('myTickets') === 'true',
				pageSize: Number(url.searchParams.get('pageSize') ?? 20)
			}
		};
	} catch (error) {
		console.error('Failed to load service tickets:', error);
		return {
			tickets: { items: [], totalCount: 0, page: 1, pageSize: 20 },
			statuses: [],
			filters: { search: '', status: '', priority: '', sort: 'newest', myTickets: false, pageSize: 20 }
		};
	}
};

export const actions: Actions = {
	deleteTicket: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const id = Number(form.get('id'));

		try {
			await serverApiRequest(fetch, `/service-tickets/${id}`, { method: 'DELETE', token });
			return { success: true };
		} catch (error) {
			if (error instanceof ServerApiError) return fail(error.status, { message: error.message });
			throw error;
		}
	}
};
