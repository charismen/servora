import { serverApiRequest } from '$lib/server/api';
import type { WorkerPayrollSlip } from '$lib/types/payroll';
import type { PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ parent, params, cookies, fetch, url }) => {
	await parent();
	const token = cookies.get('servora_session');

	const from = url.searchParams.get('from') || '';
	const to = url.searchParams.get('to') || '';
	const cycle = url.searchParams.get('cycle') || 'monthly';

	const query = new URLSearchParams({ cycle });
	if (from) query.set('from', from);
	if (to) query.set('to', to);

	const slip = await serverApiRequest<WorkerPayrollSlip>(
		fetch,
		`/payroll/slip/${params.userId}?${query.toString()}`,
		{ token }
	);

	return {
		slip
	};
};

