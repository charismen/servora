import { fail, redirect } from '@sveltejs/kit';
import { serverApiRequest, ServerApiError } from '$lib/server/api';
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
			await saveItem(form, fetch, token);
			return { messageKey: 'inventory.itemSaved' };
		} catch (error) {
			if (error instanceof ServerApiError) return fail(error.status, { message: error.message, messageKey: error.status === 409 ? 'inventory.itemConflict' : undefined });
			throw error;
		}
	},
	saveItemAndPrint: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		try {
			const id = await saveItem(form, fetch, token);
			throw redirect(303, `/app/inventory/${id}/label?from=list`);
		} catch (error) {
			if (error instanceof ServerApiError) return fail(error.status, { message: error.message, messageKey: error.status === 409 ? 'inventory.itemConflict' : undefined });
			throw error;
		}
	},
	stockIn: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const id = number(form.get('id'));

		try {
			await serverApiRequest(fetch, `/inventory/items/${id}/stock-in`, {
				method: 'POST',
				token,
				body: {
					quantity: number(form.get('quantity')),
					unitCost: nullable(form.get('unitCost')) ? number(form.get('unitCost')) : null,
					reason: nullable(form.get('reason'))
				}
			});
			return { messageKey: 'inventory.stockAdded' };
		} catch (error) {
			if (error instanceof ServerApiError) return fail(error.status, { message: error.message });
			throw error;
		}
	},
	adjustStock: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const id = number(form.get('id'));

		try {
			await serverApiRequest(fetch, `/inventory/items/${id}/adjust-stock`, {
				method: 'POST',
				token,
				body: {
					quantityDelta: number(form.get('quantityDelta')),
					reason: String(form.get('reason') ?? '')
				}
			});
			return { messageKey: 'inventory.stockAdjusted' };
		} catch (error) {
			if (error instanceof ServerApiError) return fail(error.status, { message: error.message });
			throw error;
		}
	},
	saveCategory: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const id = number(form.get('id'));

		try {
			await serverApiRequest(fetch, id ? `/inventory/categories/${id}` : '/inventory/categories', {
				method: id ? 'PUT' : 'POST',
				token,
				body: {
					name: String(form.get('name') ?? ''),
					description: nullable(form.get('description')),
					isActive: form.get('isActive') === 'on'
				}
			});
			return { messageKey: 'inventory.categorySaved' };
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
			return { messageKey: 'inventory.categoryDeleted' };
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

function nullable(value: FormDataEntryValue | null) {
	const text = String(value ?? '').trim();
	return text.length > 0 ? text : null;
}

async function saveItem(form: FormData, fetch: typeof globalThis.fetch, token?: string) {
	const id = number(form.get('id'));
	const costPrice = number(form.get('costPrice'));
	const body = {
		categoryId: optionalNumber(form.get('categoryId')),
		sku: text(form.get('sku')),
		name: text(form.get('name')),
		description: nullable(form.get('description')),
		unit: text(form.get('unit'), 'pcs'),
		costPrice,
		salePrice: number(form.get('salePrice')),
		itemType: enumText(
			form.get('itemType'),
			['SparePart', 'Accessory', 'ServiceConsumable', 'Equipment', 'ToolOrSupply', 'RefurbishedUnit', 'SecondhandPart', 'SecondhandPcBuild', 'SecondhandLaptop'],
			'SparePart'
		),
		condition: enumText(form.get('condition'), ['New', 'Used', 'Refurbished', 'ForParts'], 'New'),
		purchasePrice: number(form.get('purchasePrice'), costPrice),
		shippingCost: number(form.get('shippingCost')),
		serviceFee: number(form.get('serviceFee')),
		marginPercent: number(form.get('marginPercent')),
		taxPercent: number(form.get('taxPercent'), 11),
		fixedSalePrice: optionalNumber(form.get('fixedSalePrice')),
		quality: nullable(form.get('quality')),
		warrantyDays: optionalNumber(form.get('warrantyDays')),
		supplierName: nullable(form.get('supplierName')),
		barcodeValue: nullable(form.get('barcodeValue')),
		locationNote: nullable(form.get('locationNote')),
		updatedAt: nullable(form.get('updatedAt')),
		reorderLevel: number(form.get('reorderLevel')),
		isActive: form.get('isActive') === 'on'
	};

	const result = await serverApiRequest<{ id?: number }>(fetch, id ? `/inventory/items/${id}` : '/inventory/items', {
		method: id ? 'PUT' : 'POST',
		token,
		body
	});
	return id || result.id;
}

function number(value: FormDataEntryValue | null, fallback = 0) {
	const raw = nullable(value);
	if (!raw) return fallback;
	const clean = String(raw).replace(/[^\d.-]/g, '');
	const parsed = Number(clean);
	return Number.isFinite(parsed) ? parsed : fallback;
}

function optionalNumber(value: FormDataEntryValue | null) {
	const raw = nullable(value);
	if (!raw) return null;
	const clean = String(raw).replace(/[^\d.-]/g, '');
	const parsed = Number(clean);
	return Number.isFinite(parsed) ? parsed : null;
}

function text(value: FormDataEntryValue | null, fallback = '') {
	return nullable(value) ?? fallback;
}

function enumText(value: FormDataEntryValue | null, allowed: string[], fallback: string) {
	const raw = text(value, fallback);
	return allowed.includes(raw) ? raw : fallback;
}
