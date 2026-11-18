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
		const requesterType = String(body.requesterType ?? 'Customer').trim() || 'Customer';
		const phoneNumber = sanitizePhoneNumber(String(body.phoneNumber ?? ''));
		const emailVal = String(body.email ?? '').trim();
		const email = emailVal === '' ? undefined : emailVal;
		const addressVal = String(body.address ?? '').trim();
		const address = addressVal === '' ? undefined : addressVal;
		const companyName = String(body.companyName ?? '').trim() || undefined;
		const departmentName = String(body.departmentName ?? '').trim() || undefined;
		const departmentCode = String(body.departmentCode ?? '').trim() || undefined;
		const costCenter = String(body.costCenter ?? '').trim() || undefined;
		const employeeId = String(body.employeeId ?? '').trim() || undefined;
		const notesVal = String(body.notes ?? '').trim();
		const notes = notesVal === '' ? undefined : notesVal;

		if (!fullName || (requesterType === 'Customer' && !phoneNumber)) {
			return json({ error: 'Full name and phone number are required for retail customers.' }, { status: 400 });
		}

		const payload = {
			fullName,
			requesterType,
			phoneNumber,
			email,
			address,
			companyName,
			departmentName,
			departmentCode,
			costCenter,
			employeeId,
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
				requesterType,
				fullName,
				phoneNumber,
				email,
				address,
				companyName,
				departmentName,
				departmentCode,
				costCenter,
				employeeId,
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
