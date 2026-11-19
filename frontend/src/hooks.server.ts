import { redirect, type Handle, type HandleServerError } from '@sveltejs/kit';
import { resolveLocale } from '$lib/i18n';

const protectedPrefixes = ['/app'];

export const handle: Handle = async ({ event, resolve }) => {
	event.locals.locale = resolveLocale(
		event.cookies.get('servora_locale') ?? event.request.headers.get('accept-language')
	);
	const isProtectedRoute = protectedPrefixes.some((prefix) => event.url.pathname.startsWith(prefix));
	const hasSession = event.cookies.get('servora_session');

	if (isProtectedRoute && !hasSession) {
		const isJsonEndpointRequest =
			event.request.method !== 'GET' &&
			event.request.headers.get('accept')?.toLowerCase().includes('application/json') &&
			!event.request.headers.get('x-sveltekit-action');
		if (isJsonEndpointRequest) {
			return new Response(
				JSON.stringify({ messageKey: 'common.sessionExpired', error: 'Unauthorized' }),
				{ status: 401, headers: { 'content-type': 'application/json' } }
			);
		}
		throw redirect(303, `/login?redirectTo=${encodeURIComponent(event.url.pathname)}`);
	}

	return resolve(event);
};

export const handleError: HandleServerError = ({ error, event }) => {
	console.error('SSR error on', event.url.pathname, error);
	return {
		message: error instanceof Error ? error.message : 'Internal Server Error'
	};
};
