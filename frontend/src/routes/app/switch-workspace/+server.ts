import { json, type RequestHandler } from '@sveltejs/kit';
import { serverApiRequest, ServerApiError } from '$lib/server/api';
import type { LoginResponse } from '$lib/types/auth';

export const POST: RequestHandler = async ({ request, cookies, fetch }) => {
	const token = cookies.get('servora_session');
	if (!token) {
		return json({ error: 'Unauthorized' }, { status: 401 });
	}

	const body = await request.json().catch(() => null);
	const workspaceId = Number(body?.workspaceId);
	if (!Number.isInteger(workspaceId) || workspaceId <= 0) {
		return json({ error: 'Invalid workspaceId' }, { status: 400 });
	}

	try {
		const switched = await serverApiRequest<LoginResponse>(fetch, '/auth/switch-workspace', {
			method: 'POST',
			token,
			body: { workspaceId }
		});

		cookies.set('servora_session', switched.accessToken, {
			path: '/',
			httpOnly: true,
			sameSite: 'lax',
			secure: false,
			maxAge: 60 * 60 * 8
		});

		return json({ ok: true, workspace: switched.user.workspace });
	} catch (error) {
		if (error instanceof ServerApiError) {
			return json({ error: error.message }, { status: error.status });
		}
		return json({ error: 'Unable to switch workspace' }, { status: 500 });
	}
};
