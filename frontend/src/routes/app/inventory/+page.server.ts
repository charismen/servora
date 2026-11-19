import { fail, redirect } from '@sveltejs/kit';
import { serverApiRequest, ServerApiError } from '$lib/server/api';
import { deleteInventoryItem, formNumber, nullableFormText, saveInventoryItem } from '$lib/server/inventory';
import type { InventoryCategory, InventoryImportApplyResult, InventoryImportPreview, InventoryItemsResponse, InventoryMovement } from '$lib/types/inventory';
import type { Actions, PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ parent, cookies, fetch, url }) => {
	await parent();
	const token = cookies.get('servora_session');
	const query = new URLSearchParams();
	for (const key of ['search', 'categoryId', 'itemType', 'condition', 'isActive', 'page', 'pageSize', 'sort']) {
		const value = url.searchParams.get(key);
		if (value) query.set(key, value);
	}
	if (!query.has('page')) query.set('page', '1');
	if (!query.has('pageSize')) query.set('pageSize', '20');
	const suffix = `?${query.toString()}`;

	const [items, movements, categories] = await Promise.all([
		serverApiRequest<InventoryItemsResponse>(fetch, `/inventory/items${suffix}`, { token }).catch(() => ({ items: [], totalCount: 0, page: 1, pageSize: 20 })),
		serverApiRequest<InventoryMovement[]>(fetch, '/inventory/movements', { token }).catch(() => []),
		serverApiRequest<InventoryCategory[]>(fetch, '/inventory/categories', { token }).catch(() => [])
	]);

	return { items: items.items, inventoryPage: items, movements, categories, filters: Object.fromEntries(query) };
};

export const actions: Actions = {
	saveItem: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();

		try {
			await saveInventoryItem(form, fetch, token);
			return { success: true, messageKey: 'inventory.itemSaved' };
		} catch (error) {
			if (error instanceof ServerApiError) return fail(error.status, { message: error.message, messageKey: error.status === 409 ? 'inventory.itemConflict' : undefined });
			throw error;
		}
	},
	saveItemAndPrint: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		try {
			const id = await saveInventoryItem(form, fetch, token);
			throw redirect(303, `/app/inventory/${id}/label?from=list`);
		} catch (error) {
			if (error instanceof ServerApiError) return fail(error.status, { message: error.message, messageKey: error.status === 409 ? 'inventory.itemConflict' : undefined });
			throw error;
		}
	},
	deleteItem: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const id = formNumber(form.get('id'));
		if (!id) return fail(400, { message: 'Inventory item id is required.' });

		try {
			const result = await deleteInventoryItem(id, fetch, token);
			return {
				success: true,
				messageKey: result.action === 'deactivated' ? 'inventory.itemDeactivated' : 'inventory.itemDeleted'
			};
		} catch (error) {
			if (error instanceof ServerApiError) return fail(error.status, { message: error.message });
			throw error;
		}
	},
	stockIn: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const id = formNumber(form.get('id'));

		try {
			await serverApiRequest(fetch, `/inventory/items/${id}/stock-in`, {
				method: 'POST',
				token,
				body: {
					quantity: formNumber(form.get('quantity')),
					unitCost: nullableFormText(form.get('unitCost')) ? formNumber(form.get('unitCost')) : null,
					reason: nullableFormText(form.get('reason'))
				}
			});
			return { success: true, messageKey: 'inventory.stockAdded' };
		} catch (error) {
			if (error instanceof ServerApiError) return fail(error.status, { message: error.message });
			throw error;
		}
	},
	adjustStock: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const id = formNumber(form.get('id'));

		try {
			await serverApiRequest(fetch, `/inventory/items/${id}/adjust-stock`, {
				method: 'POST',
				token,
				body: {
					quantityDelta: formNumber(form.get('quantityDelta')),
					reason: String(form.get('reason') ?? '')
				}
			});
			return { success: true, messageKey: 'inventory.stockAdjusted' };
		} catch (error) {
			if (error instanceof ServerApiError) return fail(error.status, { message: error.message });
			throw error;
		}
	},
	saveCategory: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const id = formNumber(form.get('id'));

		try {
			await serverApiRequest(fetch, id ? `/inventory/categories/${id}` : '/inventory/categories', {
				method: id ? 'PUT' : 'POST',
				token,
				body: {
					name: String(form.get('name') ?? ''),
					description: nullableFormText(form.get('description')),
					isActive: form.get('isActive') === 'on'
				}
			});
			return { success: true, messageKey: 'inventory.categorySaved' };
		} catch (error) {
			if (error instanceof ServerApiError) return fail(error.status, { message: error.message });
			throw error;
		}
	},
	deleteCategory: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		try {
			await serverApiRequest(fetch, `/inventory/categories/${form.get('id')}`, { method: 'DELETE', token });
			return { success: true, messageKey: 'inventory.categoryDeleted' };
		} catch (error) {
			if (error instanceof ServerApiError) return fail(error.status, { message: error.message });
			throw error;
		}
	},
	importDryRun: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		try {
			const preview = await serverApiRequest<InventoryImportPreview>(fetch, '/inventory/imports/preview', {
				method: 'POST',
				token,
				body: form
			});
			return { messageKey: 'inventory.import.previewReady', importPreview: preview };
		} catch (error) {
			if (error instanceof ServerApiError) return fail(error.status, { message: error.message });
			throw error;
		}
	},
	applyImport: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		try {
			const result = await serverApiRequest<InventoryImportApplyResult>(fetch, `/inventory/imports/${form.get('batchId')}/apply`, {
				method: 'POST',
				token,
				body: { mode: String(form.get('mode') ?? 'upsert') }
			});
			return { messageKey: 'inventory.import.applied', result };
		} catch (error) {
			if (error instanceof ServerApiError) return fail(error.status, { message: error.message });
			throw error;
		}
	}
};
