import { redirect } from '@sveltejs/kit';
import { serverApiRequest } from '$lib/server/api';
import type { InventoryItemDetail } from '$lib/types/inventory';
import type { PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ parent, cookies, fetch, params, url }) => {
	await parent();
	const token = cookies.get('servora_session');
	const backHref = url.searchParams.get('from') === 'list' ? '/app/inventory' : `/app/inventory/${params.id}`;

	try {
		const detail = await serverApiRequest<InventoryItemDetail>(fetch, `/inventory/items/${params.id}`, { token });
		return { detail, backHref };
	} catch {
		throw redirect(303, '/app/inventory');
	}
};
