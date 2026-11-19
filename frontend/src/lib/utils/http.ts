export type Translator = (key: string, params?: Record<string, string | number>) => string;

export type SafeJsonResult<T = unknown> =
	| { ok: true; status: number; data: T }
	| { ok: false; status: number; message: string; errorKey: string; redirectedToLogin?: boolean };

export function isHtmlLikeResponse(rawText: string, contentType = ''): boolean {
	if (contentType.toLowerCase().includes('text/html')) return true;
	const trimmed = rawText.trimStart();
	return trimmed.startsWith('<');
}

export function isLoginRedirect(redirected: boolean, url: string): boolean {
	if (!redirected || !url) return false;
	try {
		return new URL(url, 'http://localhost').pathname.startsWith('/login');
	} catch {
		return url.includes('/login');
	}
}

export function extractErrorFromJsonPayload(payload: unknown): string {
	if (!payload || typeof payload !== 'object') return '';
	if ('message' in payload && typeof payload.message === 'string' && payload.message.trim()) {
		return payload.message.trim();
	}
	if ('error' in payload && typeof payload.error === 'string' && payload.error.trim()) {
		return payload.error.trim();
	}
	if ('detail' in payload && typeof payload.detail === 'string' && payload.detail.trim()) {
		return payload.detail.trim();
	}
	if ('title' in payload && typeof payload.title === 'string' && payload.title.trim()) {
		return payload.title.trim();
	}
	return '';
}

export function resolveHttpFailure(
	params: { status: number; redirected?: boolean; url?: string; rawText?: string; contentType?: string },
	t: Translator
): { errorKey: string; message: string; redirectedToLogin?: boolean } {
	const { status, redirected = false, url = '', rawText = '', contentType = '' } = params;

	if (status === 401 || isLoginRedirect(redirected, url)) {
		return {
			errorKey: 'common.sessionExpired',
			message: t('common.sessionExpired'),
			redirectedToLogin: true
		};
	}
	if (status === 413) {
		return { errorKey: 'common.payloadTooLarge', message: t('common.payloadTooLarge') };
	}
	if (status === 403) {
		return { errorKey: 'common.forbiddenAction', message: t('common.forbiddenAction') };
	}
	if (status === 502 || status === 503 || status === 504 || status === 524) {
		return { errorKey: 'common.serverUnavailable', message: t('common.serverUnavailable') };
	}
	if (isHtmlLikeResponse(rawText, contentType)) {
		return { errorKey: 'common.invalidServerResponse', message: t('common.invalidServerResponse') };
	}

	const trimmed = rawText.trim();
	if (trimmed.startsWith('{') && trimmed.endsWith('}')) {
		try {
			const parsed = JSON.parse(trimmed);
			if (parsed && typeof parsed === 'object' && 'messageKey' in parsed && typeof parsed.messageKey === 'string') {
				const translated = t(parsed.messageKey);
				if (translated !== parsed.messageKey) {
					return { errorKey: parsed.messageKey, message: translated };
				}
			}
			const extracted = extractErrorFromJsonPayload(parsed);
			if (extracted) {
				return { errorKey: 'common.error', message: extracted };
			}
		} catch {
			// fall through to plain text check
		}
	}

	if (trimmed && !trimmed.startsWith('<') && trimmed.length <= 400) {
		return { errorKey: 'common.error', message: trimmed };
	}

	return { errorKey: 'common.error', message: t('common.error') };
}

export async function parseSafeJsonResponse<T = unknown>(
	response: Response,
	t: Translator
): Promise<SafeJsonResult<T>> {
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
			message: resolved.message,
			errorKey: resolved.errorKey,
			redirectedToLogin: resolved.redirectedToLogin
		};
	}

	const trimmed = rawText.trim();
	if (!trimmed) {
		if (!response.ok) {
			const resolved = resolveHttpFailure({ status: response.status, rawText, contentType }, t);
			return { ok: false, status: response.status, message: resolved.message, errorKey: resolved.errorKey };
		}
		return { ok: true, status: response.status, data: {} as T };
	}

	try {
		const parsed = JSON.parse(trimmed) as T;
		if (!response.ok) {
			const resolved = resolveHttpFailure({ status: response.status, rawText, contentType }, t);
			return {
				ok: false,
				status: response.status,
				message: resolved.message,
				errorKey: resolved.errorKey
			};
		}
		return { ok: true, status: response.status, data: parsed };
	} catch {
		const resolved = resolveHttpFailure({ status: response.status, rawText, contentType }, t);
		return {
			ok: false,
			status: response.status || 502,
			message: resolved.errorKey === 'common.error' ? t('common.invalidServerResponse') : resolved.message,
			errorKey: resolved.errorKey === 'common.error' ? 'common.invalidServerResponse' : resolved.errorKey
		};
	}
}

export function formatEnhanceUnexpectedError(error: unknown, t: Translator): string {
	const raw = error instanceof Error
		? error.message
		: error && typeof error === 'object' && 'message' in error
			? String(error.message)
			: '';
	const lower = raw.toLowerCase();

	if (
		error instanceof SyntaxError ||
		lower.includes('json') ||
		lower.includes('unrecognized token') ||
		lower.includes('unexpected token')
	) {
		return t('common.invalidServerResponse');
	}
	if (
		lower.includes('failed to fetch') ||
		lower.includes('load failed') ||
		lower.includes('networkerror') ||
		lower.includes('network error')
	) {
		return t('tickets.create.networkError');
	}
	if (lower.includes('413') || lower.includes('payload too large') || lower.includes('exceeds limit')) {
		return t('common.payloadTooLarge');
	}
	return raw.trim() || t('common.error');
}
