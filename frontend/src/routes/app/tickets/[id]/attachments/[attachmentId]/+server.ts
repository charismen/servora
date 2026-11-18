import { env } from '$env/dynamic/public';
import { error } from '@sveltejs/kit';
import type { RequestHandler } from './$types';

export const GET: RequestHandler = async ({ cookies, fetch, params }) => {
	const token = cookies.get('servora_session');
	const apiBaseUrl = env.PUBLIC_API_BASE_URL ?? 'http://localhost:5051/api/v1';
	const response = await fetch(`${apiBaseUrl}/service-tickets/${params.id}/attachments/${params.attachmentId}`, {
		headers: token ? { Authorization: `Bearer ${token}` } : undefined
	});
	if (!response.ok) throw error(response.status);
	return new Response(response.body, { headers: response.headers });
};
