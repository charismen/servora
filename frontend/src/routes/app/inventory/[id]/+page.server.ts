import { fail, redirect } from '@sveltejs/kit';
import { serverApiRequest, ServerApiError } from '$lib/server/api';
import { deleteInventoryItem, saveInventoryItem } from '$lib/server/inventory';
import type { InventoryCategory, InventoryItemDetail, InventoryMovement } from '$lib/types/inventory';
import type { Actions, PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ parent, cookies, fetch, params, url }) => {
	await parent();
	const token = cookies.get('servora_session');
	const id = Number(params.id);
	const query = new URLSearchParams();
	for (const key of ['type', 'from', 'to']) {
		const value = url.searchParams.get(key);
		if (value) query.set(key, value);
	}

	try {
		const [detail, movements, categories] = await Promise.all([
			serverApiRequest<InventoryItemDetail>(fetch, `/inventory/items/${id}`, { token }),
			serverApiRequest<InventoryMovement[]>(fetch, `/inventory/movements?itemId=${id}&${query.toString()}`, { token }).catch(() => []),
			serverApiRequest<InventoryCategory[]>(fetch, '/inventory/categories', { token }).catch(() => [])
		]);
		const exportUrl = `/app/inventory/${id}/movements/export?${query.toString()}`;
		return { detail, movements, categories, filters: Object.fromEntries(query), exportUrl };
	} catch {
		throw redirect(303, '/app/inventory');
	}
};

export const actions: Actions = {
	saveItem: async ({ request, cookies, fetch, params }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const id = Number(params.id);

		try {
			await saveInventoryItem(form, fetch, token, id);
			return { success: true, messageKey: 'inventory.itemSaved' };
		} catch (error) {
			if (error instanceof ServerApiError) {
				return fail(error.status, {
					message: error.message,
					messageKey: error.status === 409 ? 'inventory.itemConflict' : undefined
				});
			}
			throw error;
		}
	},
	deleteItem: async ({ cookies, fetch, params }) => {
		const token = cookies.get('servora_session');
		const id = Number(params.id);

		let result: { action: 'deleted' | 'deactivated' };
		try {
			result = await deleteInventoryItem(id, fetch, token);
		} catch (error) {
			if (error instanceof ServerApiError) {
				return fail(error.status, { message: error.message });
			}
			throw error;
		}

		if (result.action === 'deleted') {
			throw redirect(303, '/app/inventory');
		}

		return { success: true, messageKey: 'inventory.itemDeactivated' };
	}
};
