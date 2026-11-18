import { redirect } from '@sveltejs/kit';
import { serverApiRequest } from '$lib/server/api';
import type { OrganizationSummary } from '$lib/types/maintenance';
import type { PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ cookies, fetch }) => {
	const token = cookies.get('servora_session');
	if (!token) throw redirect(303, '/login');

	const summary = await serverApiRequest<OrganizationSummary>(fetch, '/organizations/summary', { token }).catch(() => null);
	return { summary };
};
