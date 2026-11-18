import { serverApiRequest } from '$lib/server/api';
import type { ActivityLog } from '$lib/types/activity';
import type { ServiceStatus } from '$lib/types/service-statuses';
import type { DashboardSummary, TicketSummary } from '$lib/types/service-tickets';
import type { PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ parent, cookies, fetch }) => {
	await parent();
	const token = cookies.get('servora_session');

	const [activities, summary, tickets, statuses] = await Promise.all([
		serverApiRequest<ActivityLog[]>(fetch, '/activity?limit=8', { token }).catch(() => []),
		serverApiRequest<DashboardSummary>(fetch, '/dashboard/summary', { token }).catch(() => ({
			openTickets: 0,
			waitingApproval: 0,
			unpaidTickets: 0,
			blockedTickets: 0,
			overdueTickets: 0,
			completedToday: 0
		})),
		serverApiRequest<TicketSummary[]>(fetch, '/service-tickets', { token }).catch(() => []),
		serverApiRequest<ServiceStatus[]>(fetch, '/service-statuses', { token }).catch(() => [])
	]);

	return { activities, summary, tickets, statuses };
};
