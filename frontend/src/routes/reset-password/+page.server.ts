import { fail, redirect } from '@sveltejs/kit';
import { serverApiRequest, ServerApiError } from '$lib/server/api';
import type { Actions, PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ url }) => {
	return {
		email: url.searchParams.get('email') ?? '',
		token: url.searchParams.get('token') ?? ''
	};
};

export const actions: Actions = {
	default: async ({ request, fetch }) => {
		const form = await request.formData();
		const email = String(form.get('email') ?? '').trim();
		const token = String(form.get('token') ?? '').trim();
		const newPassword = String(form.get('newPassword') ?? '');
		const confirmPassword = String(form.get('confirmPassword') ?? '');

		if (!email || !token) {
			return fail(400, { email, token, messageKey: 'auth.resetInvalidLink' });
		}
		if (newPassword.length < 6) {
			return fail(400, { email, token, messageKey: 'auth.passwordTooShort' });
		}
		if (newPassword !== confirmPassword) {
			return fail(400, { email, token, messageKey: 'auth.passwordMismatch' });
		}

		try {
			await serverApiRequest(fetch, '/auth/reset-password', {
				method: 'POST',
				body: { email, token, newPassword }
			});
			throw redirect(303, '/login?reset=success');
		} catch (error) {
			if (error instanceof ServerApiError) {
				return fail(error.status, { email, token, message: error.message });
			}
			throw error;
		}
	}
};
