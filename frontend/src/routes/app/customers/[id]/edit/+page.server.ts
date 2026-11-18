import { fail, redirect } from '@sveltejs/kit';
import { serverApiRequest, ServerApiError } from '$lib/server/api';
import { sanitizePhoneNumber } from '$lib/utils/phone';
import type { Customer } from '$lib/types/customers';
import type { Actions, PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ parent, params, cookies, fetch }) => {
	await parent();
	const token = cookies.get('servora_session');
	const customerId = Number(params.id);

	try {
		const customer = await serverApiRequest<Customer>(fetch, `/customers/${customerId}`, { token });
		return { customer };
	} catch (error) {
		console.error(`Failed to load customer ${customerId} for editing:`, error);
		throw redirect(303, '/app/customers');
	}
};

export const actions: Actions = {
	default: async ({ params, request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const customerId = Number(params.id);
		const form = await request.formData();
		const fullName = String(form.get('fullName') ?? '').trim();
		const requesterType = String(form.get('requesterType') ?? 'Customer').trim() || 'Customer';
		const phoneNumber = sanitizePhoneNumber(String(form.get('phoneNumber') ?? ''));
		const emailVal = String(form.get('email') ?? '').trim();
		const email = emailVal === '' ? undefined : emailVal;
		const addressVal = String(form.get('address') ?? '').trim();
		const address = addressVal === '' ? undefined : addressVal;
		const companyName = String(form.get('companyName') ?? '').trim() || undefined;
		const departmentName = String(form.get('departmentName') ?? '').trim() || undefined;
		const departmentCode = String(form.get('departmentCode') ?? '').trim() || undefined;
		const costCenter = String(form.get('costCenter') ?? '').trim() || undefined;
		const employeeId = String(form.get('employeeId') ?? '').trim() || undefined;
		const notesVal = String(form.get('notes') ?? '').trim();
		const notes = notesVal === '' ? undefined : notesVal;
		const updatedAt = String(form.get('updatedAt') ?? '') || undefined;

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
			notes,
			updatedAt
		};

		if (!fullName) {
			return fail(400, { values: payload, messageKey: 'customers.form.fullNameRequired' });
		}
		if (requesterType === 'Customer' && !phoneNumber) {
			return fail(400, { values: payload, messageKey: 'customers.form.phoneRequired' });
		}

		try {
			await serverApiRequest(fetch, `/customers/${customerId}`, {
				method: 'PUT',
				token,
				body: payload
			});
		} catch (error) {
			if (error instanceof ServerApiError) {
				return fail(error.status, {
					values: payload,
					message: error.message,
					messageKey: error.status === 409 ? 'customers.form.conflict' : undefined
				});
			}
			throw error;
		}

		throw redirect(303, `/app/customers/${customerId}`);
	}
};
