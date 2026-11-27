import { fail } from '@sveltejs/kit';
import { serverApiRequest, ServerApiError } from '$lib/server/api';
import type { BonusTaskDefinition, CompensationTemplate } from '$lib/types/shop-users';
import type { Actions, PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ parent, cookies, fetch, url }) => {
	await parent();
	const token = cookies.get('servora_session');
	const tab = url.searchParams.get('tab') === 'templates' ? 'templates' : 'tasks';

	const [bonusTasks, templates] = await Promise.all([
		serverApiRequest<BonusTaskDefinition[]>(fetch, '/bonus-tasks?includeInactive=true', { token }).catch(() => []),
		serverApiRequest<CompensationTemplate[]>(fetch, '/compensation-templates?includeInactive=true', { token }).catch(() => [])
	]);

	return {
		tab,
		bonusTasks,
		templates
	};
};

export const actions: Actions = {
	saveBonusTask: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const id = Number(form.get('id') ?? 0);
		const isInternalOnly = form.get('defaultIsInternalOnly') === 'on' || form.get('defaultIsInternalOnly') === 'true';

		const body = {
			taskKey: String(form.get('taskKey') ?? '').trim(),
			label: String(form.get('label') ?? '').trim(),
			description: nullable(form.get('description')),
			category: nullable(form.get('category')),
			defaultStandardRate: parseMoney(form.get('defaultStandardRate')),
			defaultGamingRate: parseMoney(form.get('defaultGamingRate')),
			defaultCustomerPrice: isInternalOnly ? 0 : parseMoney(form.get('defaultCustomerPrice')),
			defaultWorkItemType: String(form.get('defaultWorkItemType') ?? 'Service'),
			defaultIsInternalOnly: isInternalOnly,
			legacyKeywords: nullable(form.get('legacyKeywords')),
			isActive: form.get('isActive') === 'on' || form.get('isActive') === 'true',
			sortOrder: Number(form.get('sortOrder') ?? 0) || 0
		};

		try {
			await serverApiRequest(fetch, id > 0 ? `/bonus-tasks/${id}` : '/bonus-tasks', {
				method: id > 0 ? 'PUT' : 'POST',
				token,
				body
			});
			return { success: true, action: 'saveBonusTask', messageKey: 'compensationCatalog.taskSaved' };
		} catch (error) {
			if (error instanceof ServerApiError) return fail(error.status, { success: false, message: error.message });
			throw error;
		}
	},

	deleteBonusTask: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const id = Number(form.get('id') ?? 0);

		try {
			await serverApiRequest(fetch, `/bonus-tasks/${id}`, { method: 'DELETE', token });
			return { success: true, action: 'deleteBonusTask', messageKey: 'compensationCatalog.taskDeleted' };
		} catch (error) {
			if (error instanceof ServerApiError) return fail(error.status, { success: false, message: error.message });
			throw error;
		}
	},

	saveTemplate: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const id = Number(form.get('id') ?? 0);

		const tieredRates = parseJson(form.get('tieredRatesJson'), []);
		const payPolicy = parseJson(form.get('payPolicyJson'), {});
		const bonusConfigs = parseJson(form.get('bonusConfigsJson'), []);

		const body = {
			name: String(form.get('name') ?? '').trim(),
			description: nullable(form.get('description')),
			basePayMode: String(form.get('basePayMode') ?? 'TieredDaily'),
			baseHourlyRate: parseMoney(form.get('baseHourlyRate')),
			tieredRates,
			payPolicy,
			bonusConfigs,
			isActive: form.get('isActive') === 'on' || form.get('isActive') === 'true',
			sortOrder: Number(form.get('sortOrder') ?? 0) || 0
		};

		try {
			await serverApiRequest(fetch, id > 0 ? `/compensation-templates/${id}` : '/compensation-templates', {
				method: id > 0 ? 'PUT' : 'POST',
				token,
				body
			});
			return { success: true, action: 'saveTemplate', messageKey: 'compensationCatalog.templateSaved' };
		} catch (error) {
			if (error instanceof ServerApiError) return fail(error.status, { success: false, message: error.message });
			throw error;
		}
	},

	deleteTemplate: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const id = Number(form.get('id') ?? 0);

		try {
			await serverApiRequest(fetch, `/compensation-templates/${id}`, { method: 'DELETE', token });
			return { success: true, action: 'deleteTemplate', messageKey: 'compensationCatalog.templateDeleted' };
		} catch (error) {
			if (error instanceof ServerApiError) return fail(error.status, { success: false, message: error.message });
			throw error;
		}
	}
};

function nullable(val: FormDataEntryValue | null) {
	const text = String(val ?? '').trim();
	return text.length > 0 ? text : null;
}

function parseMoney(val: FormDataEntryValue | null) {
	const num = Number(String(val ?? 0).replace(/[^\d.-]/g, ''));
	return Number.isFinite(num) ? Math.max(0, num) : 0;
}

function parseJson<T>(val: FormDataEntryValue | null, fallback: T): T {
	try {
		return JSON.parse(String(val ?? '')) as T;
	} catch {
		return fallback;
	}
}
