import { fail } from '@sveltejs/kit';
import { serverApiRequest, ServerApiError } from '$lib/server/api';
import type { TwoFactorStatusDto } from '$lib/types/notifications';
import type { Actions, PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ parent, cookies, fetch }) => {
	await parent();
	const token = cookies.get('servora_session');
	const status = await serverApiRequest<TwoFactorStatusDto>(fetch, '/auth/2fa/status', { token });
	return { status };
};

export const actions: Actions = {
	setupTotp: async ({ cookies, fetch }) => {
		const token = cookies.get('servora_session');
		try {
			const totp = await serverApiRequest<{ sharedKey: string; otpAuthUri: string }>(fetch, '/auth/2fa/totp/setup', {
				method: 'POST',
				token
			});
			return { totpSetup: totp };
		} catch (error) {
			if (error instanceof ServerApiError) return fail(error.status, { message: error.message, ok: false });
			throw error;
		}
	},

	confirmTotp: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const code = String(form.get('code') ?? '').trim();
		try {
			const res = await serverApiRequest<{ enabled: boolean; method: string; recoveryCodes: string[] }>(
				fetch,
				'/auth/2fa/totp/confirm',
				{ method: 'POST', token, body: { code } }
			);
			return {
				ok: true,
				messageKey: 'security.totpEnabledSuccess',
				recoveryCodes: res.recoveryCodes
			};
		} catch (error) {
			if (error instanceof ServerApiError) return fail(error.status, { message: error.message, ok: false });
			throw error;
		}
	},

	startEmailOtp: async ({ cookies, fetch }) => {
		const token = cookies.get('servora_session');
		try {
			const res = await serverApiRequest<{ challengeToken: string; maskedEmail: string }>(
				fetch,
				'/auth/2fa/email/start',
				{ method: 'POST', token }
			);
			return { emailEnrollment: res };
		} catch (error) {
			if (error instanceof ServerApiError) return fail(error.status, { message: error.message, ok: false });
			throw error;
		}
	},

	confirmEmailOtp: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const challengeToken = String(form.get('challengeToken') ?? '').trim();
		const code = String(form.get('code') ?? '').trim();
		try {
			const res = await serverApiRequest<{ enabled: boolean; method: string; recoveryCodes: string[] }>(
				fetch,
				'/auth/2fa/email/confirm',
				{ method: 'POST', token, body: { challengeToken, code } }
			);
			return {
				ok: true,
				messageKey: 'security.emailOtpEnabledSuccess',
				recoveryCodes: res.recoveryCodes
			};
		} catch (error) {
			if (error instanceof ServerApiError) {
				return fail(error.status, {
					message: error.message,
					ok: false,
					emailEnrollment: { challengeToken, maskedEmail: String(form.get('maskedEmail') ?? '') }
				});
			}
			throw error;
		}
	},

	regenerateRecovery: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const currentPassword = String(form.get('currentPassword') ?? '');
		try {
			const res = await serverApiRequest<{ recoveryCodes: string[] }>(
				fetch,
				'/auth/2fa/recovery-codes/regenerate',
				{ method: 'POST', token, body: { currentPassword } }
			);
			return {
				ok: true,
				messageKey: 'security.recoveryRegenerated',
				recoveryCodes: res.recoveryCodes
			};
		} catch (error) {
			if (error instanceof ServerApiError) return fail(error.status, { message: error.message, ok: false });
			throw error;
		}
	},

	disable2fa: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const currentPassword = String(form.get('currentPassword') ?? '');
		try {
			await serverApiRequest(fetch, '/auth/2fa/disable', {
				method: 'POST',
				token,
				body: { currentPassword }
			});
			return { ok: true, messageKey: 'security.disabledSuccess' };
		} catch (error) {
			if (error instanceof ServerApiError) return fail(error.status, { message: error.message, ok: false });
			throw error;
		}
	}
};
