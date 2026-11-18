import { fail } from '@sveltejs/kit';
import { serverApiRequest, ServerApiError } from '$lib/server/api';
import type { NotificationTemplatePage } from '$lib/types/notification-templates';
import type { Actions, PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ parent, cookies, fetch, url }) => {
	await parent();
	const token = cookies.get('servora_session');
	const channel = url.searchParams.get('channel') ?? 'WhatsApp';
	const locale = url.searchParams.get('locale') ?? '';
	const sort = url.searchParams.get('sort') ?? 'key-asc';
	const page = url.searchParams.get('page') ?? '1';
	const pageSize = url.searchParams.get('pageSize') ?? '10';
	const query = new URLSearchParams({ channel, sort, page, pageSize });
	if (locale) query.set('locale', locale);

	const templates = await serverApiRequest<NotificationTemplatePage>(fetch, `/notification-templates?${query}`, { token });

	return { templates, filters: { channel, locale, sort } };
};

export const actions: Actions = {
	save: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const id = Number(form.get('id') || 0);
		const body = {
			key: String(form.get('key') ?? ''),
			channel: String(form.get('channel') ?? 'WhatsApp'),
			provider: String(form.get('provider') ?? 'manual'),
			locale: String(form.get('locale') ?? 'en'),
			name: String(form.get('name') ?? ''),
			body: String(form.get('body') ?? ''),
			isActive: form.get('isActive') === 'on'
		};

		try {
			await serverApiRequest(fetch, id ? `/notification-templates/${id}` : '/notification-templates', {
				method: id ? 'PUT' : 'POST',
				token,
				body
			});
			return { messageKey: 'settings.notificationTemplates.saved' };
		} catch (error) {
			if (error instanceof ServerApiError) return fail(error.status, { message: error.message });
			throw error;
		}
	},
	delete: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const id = Number(form.get('id'));

		try {
			await serverApiRequest(fetch, `/notification-templates/${id}`, { method: 'DELETE', token });
			return { messageKey: 'settings.notificationTemplates.deleted' };
		} catch (error) {
			if (error instanceof ServerApiError) return fail(error.status, { message: error.message });
			throw error;
		}
	}
};
