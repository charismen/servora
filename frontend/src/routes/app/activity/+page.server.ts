import { redirect } from '@sveltejs/kit';
import { serverApiRequest } from '$lib/server/api';
import { can, permissions } from '$lib/auth/permissions';
import type { ActivityPage } from '$lib/types/activity';
import type { PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ parent, cookies, fetch, url }) => {
	const parentData = await parent();
	if (!can(parentData.user.permissions, permissions.reportsRead)) {
		throw redirect(303, '/app');
	}
	const token = cookies.get('servora_session');
	const query = new URLSearchParams();
	for (const key of ['page', 'pageSize', 'search', 'entityType', 'actionType', 'from', 'to']) {
		const value = url.searchParams.get(key);
		if (value) query.set(key, value);
	}
	if (!query.has('page')) query.set('page', '1');
	if (!query.has('pageSize')) query.set('pageSize', '25');

	const activity = await serverApiRequest<ActivityPage>(fetch, `/activity?${query}`, { token }).catch(() => ({
		items: [],
		page: Number(query.get('page') ?? 1),
		pageSize: Number(query.get('pageSize') ?? 25),
		totalItems: 0,
		totalPages: 0
	}));

	return {
		activity,
		filters: {
			search: url.searchParams.get('search') ?? '',
			entityType: url.searchParams.get('entityType') ?? '',
			actionType: url.searchParams.get('actionType') ?? '',
			from: url.searchParams.get('from') ?? '',
			to: url.searchParams.get('to') ?? ''
		}
	};
};
