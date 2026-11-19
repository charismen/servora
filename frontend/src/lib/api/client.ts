import { env } from '$env/dynamic/public';

type RequestOptions = Omit<RequestInit, 'body'> & {
	body?: unknown;
	token?: string | null;
};

export class ApiError extends Error {
	constructor(
		message: string,
		public readonly status: number,
		public readonly details?: unknown
	) {
		super(message);
		this.name = 'ApiError';
	}
}

export async function apiRequest<TResponse>(
	path: string,
	options: RequestOptions = {}
): Promise<TResponse> {
	const apiBaseUrl = env.PUBLIC_API_BASE_URL ?? 'http://localhost:5051/api/v1';
	const headers = new Headers(options.headers);
	headers.set('Accept', 'application/json');

	if (options.body !== undefined) {
		headers.set('Content-Type', 'application/json');
	}

	if (options.token) {
		headers.set('Authorization', `Bearer ${options.token}`);
	}

	let response: Response;
	try {
		response = await fetch(`${apiBaseUrl}${path}`, {
			...options,
			headers,
			body: options.body === undefined ? undefined : JSON.stringify(options.body)
		});
	} catch {
		throw new ApiError(`API unavailable at ${apiBaseUrl}`, 503);
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
		throw new ApiError(response.statusText || 'API request failed', response.status, payload);
	}

	return payload as TResponse;
}
