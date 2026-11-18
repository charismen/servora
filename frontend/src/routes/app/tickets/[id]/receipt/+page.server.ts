import { serverApiRequest } from '$lib/server/api';
import type { TicketReceipt } from '$lib/types/service-tickets';
import type { Actions, PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ cookies, fetch, params, url }) => {
	const token = cookies.get('servora_session');
	const receipt = await serverApiRequest<TicketReceipt>(fetch, `/service-tickets/${params.id}/receipt`, { token });
	const publicTrackingUrl = `${url.origin}/track/${receipt.publicTrackingToken}`;

	return { receipt, publicTrackingUrl };
};

export const actions: Actions = {
	printLog: async ({ cookies, fetch, params }) => {
		await serverApiRequest<unknown>(fetch, `/service-tickets/${params.id}/receipt/print-log`, {
			method: 'POST',
			token: cookies.get('servora_session'),
			body: {}
		});

		return { ok: true };
	}
};
