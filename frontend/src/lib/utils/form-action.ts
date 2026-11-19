import { deserialize } from '$app/forms';
import type { ActionResult } from '@sveltejs/kit';
import {
	formatEnhanceUnexpectedError,
	isHtmlLikeResponse,
	isLoginRedirect,
	resolveHttpFailure,
	type Translator
} from './http';

export type SafeActionCallResult =
	| { ok: true; status: number; result: ActionResult }
	| { ok: false; status: number; message: string; errorKey: string; redirectedToLogin?: boolean };

export async function submitActionSafely(
	actionUrl: string,
	formData: FormData,
	t: Translator,
	signal?: AbortSignal
): Promise<SafeActionCallResult> {
	let response: Response;
	try {
		response = await fetch(actionUrl, {
			method: 'POST',
			headers: {
				accept: 'application/json',
				'x-sveltekit-action': 'true'
			},
			cache: 'no-store',
			body: formData,
			signal
		});
	} catch (error) {
		return {
			ok: false,
			status: 0,
			errorKey: 'tickets.create.networkError',
			message: formatEnhanceUnexpectedError(error, t)
		};
	}

	const contentType = response.headers.get('content-type') ?? '';
	const rawText = await response.text();

	if (isLoginRedirect(response.redirected, response.url) || isHtmlLikeResponse(rawText, contentType)) {
		const resolved = resolveHttpFailure(
			{ status: response.status, redirected: response.redirected, url: response.url, rawText, contentType },
			t
		);
		return {
			ok: false,
			status: resolved.redirectedToLogin ? 401 : response.status || 502,
			errorKey: resolved.errorKey,
			message: resolved.message,
			redirectedToLogin: resolved.redirectedToLogin
		};
	}

	try {
		const result = deserialize(rawText);
		if (result.type === 'error') {
			return {
				ok: false,
				status: response.status || 500,
				errorKey: 'common.error',
				message: formatEnhanceUnexpectedError(result.error, t)
			};
		}
		return { ok: true, status: response.status, result };
	} catch {
		const resolved = resolveHttpFailure({ status: response.status, rawText, contentType }, t);
		return {
			ok: false,
			status: response.status || 502,
			errorKey: resolved.errorKey === 'common.error' ? 'common.invalidServerResponse' : resolved.errorKey,
			message: resolved.errorKey === 'common.error' ? t('common.invalidServerResponse') : resolved.message
		};
	}
}
