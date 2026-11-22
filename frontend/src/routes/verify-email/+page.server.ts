import { serverApiRequest, ServerApiError } from '$lib/server/api';
import type { PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ url, fetch }) => {
	const email = url.searchParams.get('email') ?? '';
	const token = url.searchParams.get('token') ?? '';

	if (!email || !token) {
		return { verified: false, errorMessage: null, invalidLink: true };
	}

	try {
		await serverApiRequest(fetch, '/auth/verify-email', {
			method: 'POST',
			body: { email, token }
		});
		return { verified: true, errorMessage: null, invalidLink: false };
	} catch (error) {
		if (error instanceof ServerApiError) {
			return { verified: false, errorMessage: error.message, invalidLink: false };
		}
		throw error;
	}
};
