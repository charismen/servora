import { redirect, type RequestHandler } from '@sveltejs/kit';
import { dev } from '$app/environment';
import { resolveLocale } from '$lib/i18n';

export const POST: RequestHandler = async ({ request, cookies }) => {
	const form = await request.formData();
	const locale = resolveLocale(String(form.get('locale') ?? 'en'));
	const redirectTo = String(form.get('redirectTo') ?? '/');

	cookies.set('servora_locale', locale, {
		path: '/',
		httpOnly: true,
		sameSite: 'lax',
		secure: !dev,
		maxAge: 60 * 60 * 24 * 365
	});

	throw redirect(303, redirectTo.startsWith('/') && !redirectTo.startsWith('//') ? redirectTo : '/');
};
