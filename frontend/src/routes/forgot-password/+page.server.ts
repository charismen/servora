import { fail } from '@sveltejs/kit';
import { serverApiRequest, ServerApiError } from '$lib/server/api';
import type { Actions } from './$types';

export const actions: Actions = {
	default: async ({ request, fetch }) => {
		const form = await request.formData();
		const email = String(form.get('email') ?? '').trim();
		if (!email) {
			return fail(400, { email, messageKey: 'auth.forgotEmailRequired' });
		}

		try {
			await serverApiRequest(fetch, '/auth/forgot-password', {
				method: 'POST',
				body: { email }
			});
			return { sent: true, email };
		} catch (error) {
			if (error instanceof ServerApiError) {
				return fail(error.status, { email, message: error.message });
			}
			throw error;
		}
	}
};
