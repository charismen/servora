import { fail } from '@sveltejs/kit';
import { serverApiRequest, ServerApiError } from '$lib/server/api';
import type { ServiceTemplatePage } from '$lib/types/service-templates';
import type { Actions, PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ parent, cookies, fetch, url }) => {
	await parent();
	const token = cookies.get('servora_session');
	const query = new URLSearchParams();
	for (const key of ['page', 'pageSize', 'search']) {
		const value = url.searchParams.get(key);
		if (value) query.set(key, value);
	}
	if (!query.has('page')) query.set('page', '1');
	if (!query.has('pageSize')) query.set('pageSize', '10');
	const templates = await serverApiRequest<ServiceTemplatePage>(fetch, `/service-templates?${query}`, { token });
	return {
		templates,
		filters: {
			search: url.searchParams.get('search') ?? '',
			pageSize: Number(url.searchParams.get('pageSize') ?? 10)
		}
	};
};

export const actions: Actions = {
	save: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const id = Number(form.get('id') || 0);
		const body = {
			name: String(form.get('name') ?? '').trim(),
			description: nullable(form.get('description')),
			isActive: form.get('isActive') === 'on',
			checklistItems: checklistItems(form)
		};

		try {
			await serverApiRequest(fetch, id ? `/service-templates/${id}` : '/service-templates', {
				method: id ? 'PUT' : 'POST',
				token,
				body
			});
			return { success: true, messageKey: 'settings.serviceTemplates.saved' };
		} catch (error) {
			if (error instanceof ServerApiError) return fail(error.status, { success: false, message: error.message });
			throw error;
		}
	},
	delete: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();

		try {
			await serverApiRequest(fetch, `/service-templates/${form.get('id')}`, { method: 'DELETE', token });
			return { success: true, messageKey: 'settings.serviceTemplates.deleted' };
		} catch (error) {
			if (error instanceof ServerApiError) return fail(error.status, { success: false, message: error.message });
			throw error;
		}
	}
};

function checklistItems(form: FormData) {
	return String(form.get('checklistLines') ?? '')
		.split('\n')
		.map((line) => line.trim())
		.filter(Boolean)
		.map((title, index) => ({ title, sortOrder: index + 1 }));
}

function nullable(value: FormDataEntryValue | null) {
	const text = String(value ?? '').trim();
	return text.length > 0 ? text : null;
}
