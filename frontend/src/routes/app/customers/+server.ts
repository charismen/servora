import { json } from '@sveltejs/kit';
import { serverApiRequest, ServerApiError } from '$lib/server/api';
import { sanitizePhoneNumber } from '$lib/utils/phone';
import type { RequestHandler } from './$types';

export const POST: RequestHandler = async ({ request, cookies, fetch }) => {
	const token = cookies.get('servora_session');
	if (!token) {
		return json({ error: 'Unauthorized' }, { status: 401 });
	}

	try {
		const body = await request.json();
		const fullName = String(body.fullName ?? '').trim();
		const phoneNumber = sanitizePhoneNumber(String(body.phoneNumber ?? ''));
		const emailVal = String(body.email ?? '').trim();
		const email = emailVal === '' ? undefined : emailVal;
		const addressVal = String(body.address ?? '').trim();
		const address = addressVal === '' ? undefined : addressVal;
		const notesVal = String(body.notes ?? '').trim();
		const notes = notesVal === '' ? undefined : notesVal;

		if (!fullName || !phoneNumber) {
			return json({ error: 'Full name and phone number are required.' }, { status: 400 });
		}

		const payload = {
			fullName,
			phoneNumber,
			email,
			address,
			notes
		};

		const created = await serverApiRequest<{ id: number }>(fetch, '/customers', {
			method: 'POST',
			token,
			body: payload
		});

		return json({
			customer: {
				id: created.id,
				fullName,
				phoneNumber,
				email,
				address,
				notes,
				isBanned: false,
				isStarred: false,
				totalTicketsCount: 0,
				activeTicketsCount: 0,
				totalSpent: 0
			}
		});
	} catch (error) {
		if (error instanceof ServerApiError) {
			return json({ error: error.message }, { status: error.status || 400 });
		}
		return json({ error: 'Failed to create customer' }, { status: 500 });
	}
};
