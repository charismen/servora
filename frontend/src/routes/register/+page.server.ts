import { fail } from '@sveltejs/kit';
import { serverApiRequest, ServerApiError } from '$lib/server/api';
import type { Actions, PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ fetch }) => {
	let allowPublicRegistration = false;
	try {
		const cfg = await serverApiRequest<{ allowPublicRegistration?: boolean }>(fetch, '/auth/config');
		allowPublicRegistration = Boolean(cfg.allowPublicRegistration);
	} catch {
		allowPublicRegistration = false;
	}
	return { allowPublicRegistration };
};

export const actions: Actions = {
	default: async ({ request, fetch }) => {
		const form = await request.formData();
		const firstName = String(form.get('firstName') ?? '').trim();
		const lastName = String(form.get('lastName') ?? '').trim();
		const email = String(form.get('email') ?? '').trim();
		const password = String(form.get('password') ?? '');
		const organizationName = String(form.get('organizationName') ?? '').trim();
		const workspaceName = String(form.get('workspaceName') ?? '').trim();

		try {
			const res = await serverApiRequest<{ requiresEmailVerification?: boolean; message?: string }>(
				fetch,
				'/auth/register',
				{
					method: 'POST',
					body: { firstName, lastName, email, password, organizationName, workspaceName }
				}
			);
			return {
				registered: true,
				email,
				requiresEmailVerification: Boolean(res.requiresEmailVerification)
			};
		} catch (error) {
			if (error instanceof ServerApiError) {
				return fail(error.status, {
					firstName,
					lastName,
					email,
					organizationName,
					workspaceName,
					message: error.message
				});
			}
			throw error;
		}
	}
};
