import { fail, redirect } from '@sveltejs/kit';
import { dev } from '$app/environment';
import { createTranslator } from '$lib/i18n';
import { serverApiRequest, ServerApiError } from '$lib/server/api';
import type { LoginResponse } from '$lib/types/auth';
import type { Actions, PageServerLoad } from './$types';

export interface DemoAccount {
	userId: number;
	name: string;
	email: string;
	role: string;
	employmentType: string;
	defaultPassword?: string;
}

function safeRedirectPath(raw: string | null | undefined, fallback = '/app'): string {
	if (!raw) return fallback;
	const trimmed = raw.trim();
	return trimmed.startsWith('/') && !trimmed.startsWith('//') ? trimmed : fallback;
}

export const load: PageServerLoad = async ({ cookies, url, fetch }) => {
	const isSwitching = url.searchParams.get('switch') === 'true';
	if (isSwitching) {
		cookies.delete('servora_session', { path: '/' });
	}

	const redirectTo = safeRedirectPath(url.searchParams.get('redirectTo'));
	if (!isSwitching && cookies.get('servora_session')) {
		throw redirect(303, redirectTo);
	}

	let demoAccounts: DemoAccount[] = [];
	// Only query and expose demo accounts in local development mode
	if (dev) {
		try {
			demoAccounts = await serverApiRequest<DemoAccount[]>(fetch, '/auth/demo-accounts');
		} catch {
			demoAccounts = [];
		}
	}

	return {
		redirectTo,
		isDev: dev,
		demoAccounts
	};
};

export const actions: Actions = {
	default: async ({ request, cookies, fetch, url, locals }) => {
		const t = createTranslator(locals.locale);
		const form = await request.formData();
		const email = String(form.get('email') ?? '').trim();
		const password = String(form.get('password') ?? '').trim();
		const redirectTo = safeRedirectPath(String(form.get('redirectTo') ?? url.searchParams.get('redirectTo') ?? '/app'));

		try {
			const result = await serverApiRequest<LoginResponse>(fetch, '/auth/login', {
				method: 'POST',
				body: { email, password }
			});

			cookies.set('servora_session', result.accessToken, {
				path: '/',
				httpOnly: true,
				sameSite: 'lax',
				secure: url.protocol === 'https:',
				expires: new Date(result.expiresAt)
			});

			throw redirect(303, redirectTo);
		} catch (error) {
			if (error instanceof ServerApiError) {
				return fail(error.status === 401 ? 401 : 400, {
					email,
					redirectTo,
					message: error.status === 401 ? t('auth.invalid') : error.message
				});
			}

			throw error;
		}
	}
};
