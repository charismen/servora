import { env } from '$env/dynamic/public';
import { error } from '@sveltejs/kit';
import { createTranslator } from '$lib/i18n';
import type { RequestHandler } from './$types';

export const GET: RequestHandler = async ({ cookies, fetch, locals, url }) => {
	const t = createTranslator(locals.locale);
	const token = cookies.get('servora_session');
	if (!token) throw error(401, t('auth.unauthorized'));

	const format = url.searchParams.get('format') ?? 'csv';
	const apiBaseUrl = env.PUBLIC_API_BASE_URL ?? 'http://localhost:5051/api/v1';
	const response = await fetch(`${apiBaseUrl}/inventory/imports/template?format=${encodeURIComponent(format)}`, {
		headers: { Authorization: `Bearer ${token}` }
	});

	if (!response.ok) throw error(response.status, await response.text());

	return new Response(await response.arrayBuffer(), {
		headers: {
			'content-type': response.headers.get('content-type') ?? 'text/csv',
			'content-disposition': response.headers.get('content-disposition') ?? `attachment; filename="inventory-import-template.${format}"`
		}
	});
};
