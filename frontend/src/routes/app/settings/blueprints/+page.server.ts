import { fail, redirect } from '@sveltejs/kit';
import { serverApiRequest, ServerApiError } from '$lib/server/api';
import type { IndustryBlueprintDefinition, WorkspaceBlueprintConfig } from '$lib/types/blueprints';
import type { Actions, PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ cookies, fetch }) => {
	const token = cookies.get('servora_session');
	if (!token) throw redirect(303, '/login');

	const [blueprints, config] = await Promise.all([
		serverApiRequest<IndustryBlueprintDefinition[]>(fetch, '/blueprints', { token }).catch(() => []),
		serverApiRequest<WorkspaceBlueprintConfig>(fetch, '/blueprints/workspace-config', { token }).catch(() => null)
	]);

	return { blueprints, config };
};

export const actions: Actions = {
	applyBlueprint: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		if (!token) throw redirect(303, '/login');
		const formData = await request.formData();
		const blueprintKey = String(formData.get('blueprintKey') ?? 'it_computer');
		const replaceStatuses = formData.get('replaceStatuses') === 'on';
		const replaceFieldsAndChecklists = formData.get('replaceFieldsAndChecklists') === 'on';

		try {
			await serverApiRequest(fetch, '/blueprints/apply', {
				method: 'POST',
				token,
				body: { blueprintKey, replaceStatuses, replaceFieldsAndChecklists }
			});
			return { success: true, messageKey: 'blueprints.applied' };
		} catch (error) {
			if (error instanceof ServerApiError) return fail(error.status, { success: false, message: error.message });
			return fail(500, { success: false, message: 'Failed to apply blueprint.' });
		}
	},

	saveTerminology: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		if (!token) throw redirect(303, '/login');
		const formData = await request.formData();
		const operatingMode = String(formData.get('operatingMode') ?? 'Commercial');
		const terminologyJson = String(formData.get('terminologyJson') ?? '{}');

		try {
			const terminology = JSON.parse(terminologyJson);
			await serverApiRequest(fetch, '/blueprints/terminology', {
				method: 'PUT',
				token,
				body: { operatingMode, terminology }
			});
			return { success: true, messageKey: 'blueprints.terminologySaved' };
		} catch (error) {
			if (error instanceof ServerApiError) return fail(error.status, { success: false, message: error.message });
			return fail(400, { success: false, message: 'Invalid terminology payload.' });
		}
	},

	createField: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		if (!token) throw redirect(303, '/login');
		const formData = await request.formData();
		const optionsRaw = String(formData.get('options') ?? '');
		const options = optionsRaw
			.split(',')
			.map((s) => s.trim())
			.filter(Boolean);

		try {
			await serverApiRequest(fetch, '/custom-fields', {
				method: 'POST',
				token,
				body: {
					scope: String(formData.get('scope') ?? 'Asset'),
					labelEn: String(formData.get('labelEn') ?? '').trim(),
					labelId: String(formData.get('labelId') ?? '').trim(),
					fieldType: String(formData.get('fieldType') ?? 'Text'),
					options,
					isRequired: formData.get('isRequired') === 'on',
					isSearchable: formData.get('isSearchable') === 'on',
					showOnIntake: formData.get('showOnIntake') === 'on',
					showOnDetail: formData.get('showOnDetail') === 'on',
					showOnPrint: formData.get('showOnPrint') === 'on',
					showOnPublicTracking: formData.get('showOnPublicTracking') === 'on',
					sortOrder: Number(formData.get('sortOrder') ?? 10) || 10,
					isActive: true
				}
			});
			return { success: true, messageKey: 'blueprints.fieldCreated' };
		} catch (error) {
			if (error instanceof ServerApiError) return fail(error.status, { success: false, message: error.message });
			return fail(500, { success: false, message: 'Failed to create custom field.' });
		}
	},

	deleteField: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		if (!token) throw redirect(303, '/login');
		const id = Number((await request.formData()).get('id'));
		try {
			await serverApiRequest(fetch, `/custom-fields/${id}`, { method: 'DELETE', token });
			return { success: true, messageKey: 'blueprints.fieldDeleted' };
		} catch (error) {
			if (error instanceof ServerApiError) return fail(error.status, { success: false, message: error.message });
			return fail(500, { success: false, message: 'Failed to delete field.' });
		}
	},

	createChecklist: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		if (!token) throw redirect(303, '/login');
		const formData = await request.formData();
		try {
			await serverApiRequest(fetch, '/inspection-checklists', {
				method: 'POST',
				token,
				body: {
					labelEn: String(formData.get('labelEn') ?? '').trim(),
					labelId: String(formData.get('labelId') ?? '').trim(),
					category: String(formData.get('category') ?? 'General').trim() || 'General',
					sortOrder: Number(formData.get('sortOrder') ?? 10) || 10,
					isRequired: formData.get('isRequired') === 'on',
					isActive: true
				}
			});
			return { success: true, messageKey: 'blueprints.checklistCreated' };
		} catch (error) {
			if (error instanceof ServerApiError) return fail(error.status, { success: false, message: error.message });
			return fail(500, { success: false, message: 'Failed to add checklist item.' });
		}
	},

	deleteChecklist: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		if (!token) throw redirect(303, '/login');
		const id = Number((await request.formData()).get('id'));
		try {
			await serverApiRequest(fetch, `/inspection-checklists/${id}`, { method: 'DELETE', token });
			return { success: true, messageKey: 'blueprints.checklistDeleted' };
		} catch (error) {
			if (error instanceof ServerApiError) return fail(error.status, { success: false, message: error.message });
			return fail(500, { success: false, message: 'Failed to delete checklist item.' });
		}
	}
};
