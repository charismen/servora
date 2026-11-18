import { redirect } from '@sveltejs/kit';
import { serverApiRequest } from '$lib/server/api';
import type { InventoryItemDetail, InventoryMovement } from '$lib/types/inventory';
import type { PageServerLoad } from './$types';

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
		const [detail, movements] = await Promise.all([
			serverApiRequest<InventoryItemDetail>(fetch, `/inventory/items/${id}`, { token }),
			serverApiRequest<InventoryMovement[]>(fetch, `/inventory/movements?itemId=${id}&${query.toString()}`, { token }).catch(() => [])
		]);
		const exportUrl = `/app/inventory/${id}/movements/export?${query.toString()}`;
		return { detail, movements, filters: Object.fromEntries(query), exportUrl };
	} catch {
		throw redirect(303, '/app/inventory');
	}
};
