import { serverApiRequest } from '$lib/server/api';

export const INVENTORY_ITEM_TYPES = [
	'SparePart',
	'Accessory',
	'ServiceConsumable',
	'Equipment',
	'ToolOrSupply',
	'RefurbishedUnit',
	'SecondhandPart',
	'SecondhandPcBuild',
	'SecondhandLaptop'
] as const;

export const INVENTORY_CONDITIONS = ['New', 'Used', 'Refurbished', 'ForParts'] as const;

export function nullableFormText(value: FormDataEntryValue | null) {
	const text = String(value ?? '').trim();
	return text.length > 0 ? text : null;
}

export function formNumber(value: FormDataEntryValue | null, fallback = 0) {
	const raw = nullableFormText(value);
	if (!raw) return fallback;
	const clean = String(raw).replace(/[^\d.-]/g, '');
	const parsed = Number(clean);
	return Number.isFinite(parsed) ? parsed : fallback;
}

export function optionalFormNumber(value: FormDataEntryValue | null) {
	const raw = nullableFormText(value);
	if (!raw) return null;
	const clean = String(raw).replace(/[^\d.-]/g, '');
	const parsed = Number(clean);
	return Number.isFinite(parsed) ? parsed : null;
}

function formText(value: FormDataEntryValue | null, fallback = '') {
	return nullableFormText(value) ?? fallback;
}

function formEnumText(value: FormDataEntryValue | null, allowed: readonly string[], fallback: string) {
	const raw = formText(value, fallback);
	return allowed.includes(raw) ? raw : fallback;
}

export async function saveInventoryItem(
	form: FormData,
	fetch: typeof globalThis.fetch,
	token?: string,
	explicitId?: number
) {
	const id = explicitId ?? formNumber(form.get('id'));
	const purchasePrice = formNumber(form.get('purchasePrice'));
	const costPrice = formNumber(form.get('costPrice'), purchasePrice);
	const body = {
		categoryId: optionalFormNumber(form.get('categoryId')),
		sku: formText(form.get('sku')),
		name: formText(form.get('name')),
		description: nullableFormText(form.get('description')),
		unit: formText(form.get('unit'), 'pcs'),
		costPrice,
		salePrice: formNumber(form.get('salePrice')),
		itemType: formEnumText(form.get('itemType'), INVENTORY_ITEM_TYPES, 'SparePart'),
		condition: formEnumText(form.get('condition'), INVENTORY_CONDITIONS, 'New'),
		purchasePrice,
		shippingCost: formNumber(form.get('shippingCost')),
		serviceFee: formNumber(form.get('serviceFee')),
		marginPercent: formNumber(form.get('marginPercent')),
		taxPercent: formNumber(form.get('taxPercent'), 11),
		fixedSalePrice: optionalFormNumber(form.get('fixedSalePrice')),
		quality: nullableFormText(form.get('quality')),
		warrantyDays: optionalFormNumber(form.get('warrantyDays')),
		supplierName: nullableFormText(form.get('supplierName')),
		barcodeValue: nullableFormText(form.get('barcodeValue')),
		locationNote: nullableFormText(form.get('locationNote')),
		updatedAt: nullableFormText(form.get('updatedAt')),
		reorderLevel: formNumber(form.get('reorderLevel')),
		isActive: form.get('isActive') === 'on'
	};

	const result = await serverApiRequest<{ id?: number }>(fetch, id ? `/inventory/items/${id}` : '/inventory/items', {
		method: id ? 'PUT' : 'POST',
		token,
		body
	});
	return id || result.id;
}

export async function deleteInventoryItem(id: number, fetch: typeof globalThis.fetch, token?: string) {
	return serverApiRequest<{ action: 'deleted' | 'deactivated' }>(fetch, `/inventory/items/${id}`, {
		method: 'DELETE',
		token
	});
}
