import { serverApiRequest } from '$lib/server/api';
import type { CustomersResponse } from '$lib/types/customers';
import type { PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ parent, cookies, fetch, url }) => {
	await parent();
	const token = cookies.get('servora_session');
	const search = url.searchParams.get('search') || '';
	const status = (url.searchParams.get('status') || 'all') as 'all' | 'active' | 'banned' | 'star';
	const startDate = url.searchParams.get('startDate') || '';
	const endDate = url.searchParams.get('endDate') || '';
	const page = Number(url.searchParams.get('page') || '1');
	const sort = url.searchParams.get('sort') || 'default';
	const pageSize = Number(url.searchParams.get('pageSize') || '20');

	const query = new URLSearchParams({
		search,
		sort,
		page: String(page),
		pageSize: String(pageSize)
	});
	if (status === 'active') query.set('isBanned', 'false');
	if (status === 'banned') query.set('isBanned', 'true');
	if (status === 'star') query.set('isStarred', 'true');
	if (startDate) query.set('from', new Date(`${startDate}T00:00:00`).toISOString());
	if (endDate) query.set('to', new Date(`${endDate}T23:59:59.999`).toISOString());

	try {
		const customers = await serverApiRequest<CustomersResponse>(
			fetch,
			`/customers?${query.toString()}`,
			{ token }
		);
		return { customers, search, status, startDate, endDate, sort, pageSize };
	} catch (error) {
		console.error('Failed to load customers:', error);
		return {
			customers: { items: [], totalCount: 0, page: 1, pageSize: 20 },
			search,
			status,
			startDate,
			endDate,
			sort,
			pageSize
		};
	}
};
