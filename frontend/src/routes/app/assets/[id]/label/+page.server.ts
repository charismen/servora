import { redirect } from '@sveltejs/kit';
import { serverApiRequest } from '$lib/server/api';
import type { AssetDetail } from '$lib/types/assets';
import type { PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ parent, cookies, fetch, params, url }) => {
	await parent();
	const token = cookies.get('servora_session');
	const backHref = url.searchParams.get('from') === 'list' ? '/app/assets' : `/app/assets/${params.id}`;

	try {
		const asset = await serverApiRequest<AssetDetail>(fetch, `/assets/${params.id}`, { token });
		return { asset, backHref };
	} catch {
		throw redirect(303, '/app/assets');
	}
};
