import { json } from '@sveltejs/kit';
import { serverApiRequest, ServerApiError } from '$lib/server/api';
import type { RequestHandler } from './$types';

export const GET: RequestHandler = async ({ url, cookies, fetch }) => {
	const token = cookies.get('servora_session');
	if (!token) {
		return json({ message: 'Unauthorized' }, { status: 401 });
	}

	const categoryId = url.searchParams.get('categoryId');
	const query = categoryId ? `?categoryId=${encodeURIComponent(categoryId)}` : '';

	try {
		const data = await serverApiRequest<{ sku: string; prefix: string; sequence: number }>(
			fetch,
			`/inventory/items/next-sku${query}`,
			{ token }
		);
		return json(data);
	} catch (error) {
		if (error instanceof ServerApiError) {
			return json({ message: error.message }, { status: error.status });
		}
		return json({ message: 'Failed to generate SKU.' }, { status: 500 });
	}
};
