import { json } from '@sveltejs/kit';
import { resolveSupportedImageMime } from '$lib/media';
import { serverApiRequest, ServerApiError } from '$lib/server/api';
import type { RequestHandler } from './$types';

const MAX_ATTACHMENT_BYTES = 5 * 1024 * 1024;

export const POST: RequestHandler = async ({ request, cookies, fetch, params }) => {
	const token = cookies.get('servora_session');
	if (!token) {
		return json({ messageKey: 'common.sessionExpired', message: 'Unauthorized' }, { status: 401 });
	}

	const ticketId = Number(params.id);
	if (!Number.isFinite(ticketId) || ticketId <= 0) {
		return json({ message: 'Invalid ticket id.' }, { status: 400 });
	}

	let form: FormData;
	try {
		form = await request.formData();
	} catch {
		return json({ messageKey: 'common.payloadTooLarge', message: 'Invalid upload payload.' }, { status: 400 });
	}

	const rawFile = form.get('file');
	if (!(rawFile instanceof File) || rawFile.size === 0) {
		return json({ messageKey: 'tickets.attachments.fileRequired', message: 'File is required.' }, { status: 400 });
	}
	if (rawFile.size > MAX_ATTACHMENT_BYTES) {
		return json({ messageKey: 'tickets.attachments.fileTooLarge', message: 'File must be 5MB or smaller.' }, { status: 400 });
	}

	const mime = resolveSupportedImageMime(rawFile);
	if (!mime) {
		return json({ messageKey: 'tickets.attachments.fileTypeInvalid', message: 'Only JPEG, PNG, or WEBP images are allowed.' }, { status: 400 });
	}

	const uploadFile = rawFile.type.toLowerCase() === mime
		? rawFile
		: new File([rawFile], rawFile.name, { type: mime, lastModified: rawFile.lastModified });

	const body = new FormData();
	body.set('file', uploadFile);
	body.set('attachmentType', String(form.get('attachmentType') ?? 'Other'));
	body.set('isPublicVisible', String(form.get('isPublicVisible') ?? 'false') === 'true' ? 'true' : 'false');

	try {
		await serverApiRequest(fetch, `/service-tickets/${ticketId}/attachments`, {
			method: 'POST',
			token,
			body
		});
		return json({ ok: true });
	} catch (error) {
		if (error instanceof ServerApiError) {
			return json({ message: error.message }, { status: error.status || 400 });
		}
		return json({ message: 'Failed to upload attachment.' }, { status: 500 });
	}
};
