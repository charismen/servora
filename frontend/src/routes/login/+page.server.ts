import { fail, redirect } from '@sveltejs/kit';
import { dev } from '$app/environment';
import { createTranslator } from '$lib/i18n';
import { serverApiRequest, ServerApiError } from '$lib/server/api';
import type { LoginApiResponse, LoginResponse } from '$lib/types/auth';
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
	if (dev) {
		try {
			demoAccounts = await serverApiRequest<DemoAccount[]>(fetch, '/auth/demo-accounts');
		} catch {
			demoAccounts = [];
		}
	}

	let allowPublicRegistration = false;
	try {
		const cfg = await serverApiRequest<{ allowPublicRegistration?: boolean }>(fetch, '/auth/config');
		allowPublicRegistration = Boolean(cfg.allowPublicRegistration);
	} catch {
		allowPublicRegistration = false;
	}

	return {
		redirectTo,
		isDev: dev,
		demoAccounts,
		allowPublicRegistration,
		resetDone: url.searchParams.get('reset') === 'success',
		verifiedDone: url.searchParams.get('verified') === 'success'
	};
};

export const actions: Actions = {
	login: async ({ request, cookies, fetch, url, locals }) => {
		const t = createTranslator(locals.locale);
		const form = await request.formData();
		const email = String(form.get('email') ?? '').trim();
		const password = String(form.get('password') ?? '').trim();
		const redirectTo = safeRedirectPath(String(form.get('redirectTo') ?? url.searchParams.get('redirectTo') ?? '/app'));

		try {
			const result = await serverApiRequest<LoginApiResponse>(fetch, '/auth/login', {
				method: 'POST',
				body: { email, password }
			});

			if ('requiresTwoFactor' in result && result.requiresTwoFactor) {
				return {
					twoFactorStep: true,
					challengeToken: result.challengeToken,
					method: result.method,
					maskedEmail: result.maskedEmail ?? null,
					email,
					redirectTo
				};
			}

			const authResult = result as LoginResponse;
			cookies.set('servora_session', authResult.accessToken, {
				path: '/',
				httpOnly: true,
				sameSite: 'lax',
				secure: url.protocol === 'https:',
				expires: new Date(authResult.expiresAt)
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
	},

	verify2fa: async ({ request, cookies, fetch, url }) => {
		const form = await request.formData();
		const challengeToken = String(form.get('challengeToken') ?? '').trim();
		const code = String(form.get('code') ?? '').trim();
		const useRecoveryCode = form.get('useRecoveryCode') === 'true';
		const method = String(form.get('method') ?? 'AuthenticatorTotp');
		const maskedEmail = String(form.get('maskedEmail') ?? '') || null;
		const email = String(form.get('email') ?? '').trim();
		const redirectTo = safeRedirectPath(String(form.get('redirectTo') ?? '/app'));

		try {
			const result = await serverApiRequest<LoginResponse>(fetch, '/auth/login/verify-2fa', {
				method: 'POST',
				body: { challengeToken, code, useRecoveryCode }
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
				return fail(error.status, {
					twoFactorStep: true,
					challengeToken,
					method,
					maskedEmail,
					email,
					redirectTo,
					useRecoveryCode,
					message: error.message
				});
			}
			throw error;
		}
	},

	resend2fa: async ({ request, fetch }) => {
		const form = await request.formData();
		const challengeToken = String(form.get('challengeToken') ?? '').trim();
		const method = String(form.get('method') ?? 'EmailOtp');
		const maskedEmail = String(form.get('maskedEmail') ?? '') || null;
		const email = String(form.get('email') ?? '').trim();
		const redirectTo = safeRedirectPath(String(form.get('redirectTo') ?? '/app'));

		try {
			const res = await serverApiRequest<{ challengeToken: string; maskedEmail?: string | null }>(
				fetch,
				'/auth/login/resend-2fa-otp',
				{
					method: 'POST',
					body: { challengeToken }
				}
			);
			return {
				twoFactorStep: true,
				challengeToken: res.challengeToken,
				method,
				maskedEmail: res.maskedEmail ?? maskedEmail,
				email,
				redirectTo,
				resent: true
			};
		} catch (error) {
			if (error instanceof ServerApiError) {
				return fail(error.status, {
					twoFactorStep: true,
					challengeToken,
					method,
					maskedEmail,
					email,
					redirectTo,
					message: error.message
				});
			}
			throw error;
		}
	}
};
