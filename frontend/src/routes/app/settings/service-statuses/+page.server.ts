import { fail } from '@sveltejs/kit';
import { serverApiRequest, ServerApiError } from '$lib/server/api';
import type { ServiceStatus } from '$lib/types/service-statuses';
import type { Actions, PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ parent, cookies, fetch }) => {
	await parent();
	const token = cookies.get('servora_session');
	const statuses = await serverApiRequest<ServiceStatus[]>(fetch, '/service-statuses', { token });

	return { statuses };
};

const checkbox = (form: FormData, key: string) => form.get(key) === 'on';

export const actions: Actions = {
	save: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const id = Number(form.get('id') || 0);
		const body = {
			name: String(form.get('name') ?? ''),
			color: String(form.get('color') ?? '#64748b'),
			sortOrder: Number(form.get('sortOrder') || 0),
			isInitial: checkbox(form, 'isInitial'),
			isFinal: checkbox(form, 'isFinal'),
			isCancelled: checkbox(form, 'isCancelled'),
			isBlocked: checkbox(form, 'isBlocked')
		};

		try {
			await serverApiRequest(fetch, id ? `/service-statuses/${id}` : '/service-statuses', {
				method: id ? 'PUT' : 'POST',
				token,
				body
			});
			return { messageKey: 'settings.serviceStatuses.saved' };
		} catch (error) {
			if (error instanceof ServerApiError) return fail(400, { message: error.message });
			throw error;
		}
	},
	delete: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const id = Number(form.get('id'));

		try {
			await serverApiRequest(fetch, `/service-statuses/${id}`, { method: 'DELETE', token });
			return { messageKey: 'settings.serviceStatuses.deleted' };
		} catch (error) {
			if (error instanceof ServerApiError) return fail(400, { message: error.message });
			throw error;
		}
	}
};
