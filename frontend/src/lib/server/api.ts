import { env as publicEnv } from '$env/dynamic/public';
import { env as privateEnv } from '$env/dynamic/private';

export class ServerApiError extends Error {
	constructor(
		message: string,
		public readonly status: number,
		public readonly details?: unknown
	) {
		super(message);
		this.name = 'ServerApiError';
	}
}

type RequestOptions = Omit<RequestInit, 'body'> & {
	body?: unknown;
	token?: string;
	workspaceId?: number | string;
};

export async function serverApiRequest<TResponse>(
	fetcher: typeof fetch,
	path: string,
	options: RequestOptions = {}
): Promise<TResponse> {
	const apiBaseUrl =
		privateEnv.INTERNAL_API_BASE_URL ??
		publicEnv.PUBLIC_API_BASE_URL ??
		'http://localhost:5051/api/v1';
	const headers = new Headers(options.headers);
	headers.set('Accept', 'application/json');
	const isFormData = options.body instanceof FormData;

	if (options.body !== undefined && !isFormData) {
		headers.set('Content-Type', 'application/json');
	}

	if (options.token) {
		headers.set('Authorization', `Bearer ${options.token}`);
	}

	if (options.workspaceId !== undefined && options.workspaceId !== '') {
		headers.set('X-Workspace-Id', String(options.workspaceId));
	}

	const requestBody: BodyInit | undefined =
		options.body === undefined
			? undefined
			: isFormData
				? (options.body as FormData)
				: JSON.stringify(options.body);

	let response: Response;
	try {
		response = await fetcher(`${apiBaseUrl.replace(/\/+$/, '')}${path}`, {
			...options,
			headers,
			body: requestBody
		});
	} catch {
		throw new ServerApiError(`API unavailable at ${apiBaseUrl}`, 503);
	}

	const contentType = response.headers.get('content-type')?.toLowerCase() ?? '';
	const rawText = await response.text();
	let payload: unknown = rawText;
	if (contentType.includes('json') && rawText.trim().length > 0) {
		try {
			payload = JSON.parse(rawText);
		} catch {
			payload = rawText;
		}
	}

	if (!response.ok) {
		const message = extractErrorMessage(payload) || response.statusText || 'API request failed';
		throw new ServerApiError(message, response.status, payload);
	}

	return payload as TResponse;
}

function extractErrorMessage(payload: unknown): string {
	if (typeof payload === 'string') {
		const trimmed = payload.trim();
		if (trimmed.startsWith('{') && trimmed.endsWith('}')) {
			try {
				return extractErrorMessage(JSON.parse(trimmed));
			} catch {
				return payload;
			}
		}
		return payload;
	}
	if (!payload || typeof payload !== 'object') return '';
	if ('detail' in payload && payload.detail) return String(payload.detail);
	if ('errors' in payload && payload.errors && typeof payload.errors === 'object') {
		return Object.values(payload.errors)
			.flatMap((value) => (Array.isArray(value) ? value : [value]))
			.filter(Boolean)
			.map(String)
			.join(' ');
	}
	if ('title' in payload && payload.title) return String(payload.title);
	return '';
}
