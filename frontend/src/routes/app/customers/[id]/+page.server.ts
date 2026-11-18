import { fail, redirect } from '@sveltejs/kit';
import { serverApiRequest, ServerApiError } from '$lib/server/api';
import type { Customer } from '$lib/types/customers';
import type { Device } from '$lib/types/devices';
import type { RepeatCustomerSummary, ServiceHistoryTicket } from '$lib/types/service-tickets';
import type { Actions, PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ parent, params, cookies, fetch }) => {
	await parent();
	const token = cookies.get('servora_session');
	const customerId = Number(params.id);

	try {
		const [customer, devices, serviceHistory, repeatSummary] = await Promise.all([
			serverApiRequest<Customer>(fetch, `/customers/${customerId}`, { token }),
			serverApiRequest<Device[]>(fetch, `/customers/${customerId}/devices`, { token }).catch(() => []),
			serverApiRequest<ServiceHistoryTicket[]>(fetch, `/customers/${customerId}/service-history`, { token }).catch(() => []),
			serverApiRequest<RepeatCustomerSummary>(fetch, `/customers/${customerId}/repeat-summary`, { token }).catch(() => null)
		]);
		const deviceHistoryEntries = await Promise.all(
			devices.map(async (device) => [
				device.id,
				await serverApiRequest<ServiceHistoryTicket[]>(
					fetch,
					`/customers/${customerId}/devices/${device.id}/service-history`,
					{ token }
				).catch(() => [])
			] as const)
		);
		return {
			customer,
			devices,
			serviceHistory,
			repeatSummary,
			deviceHistories: Object.fromEntries(deviceHistoryEntries)
		};
	} catch (error) {
		console.error(`Failed to load customer ${customerId}:`, error);
		throw redirect(303, '/app/customers');
	}
};

export const actions: Actions = {
	createDevice: async ({ params, request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const customerId = Number(params.id);
		const form = await request.formData();

		try {
			await serverApiRequest(fetch, `/customers/${customerId}/devices`, {
				method: 'POST',
				token,
				body: devicePayload(form)
			});
			return { success: true };
		} catch (error) {
			if (error instanceof ServerApiError) {
				return fail(400, { message: error.message });
			}
			throw error;
		}
	},
	updateDevice: async ({ params, request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const customerId = Number(params.id);
		const form = await request.formData();
		const deviceId = Number(form.get('deviceId'));

		try {
			await serverApiRequest(fetch, `/customers/${customerId}/devices/${deviceId}`, {
				method: 'PUT',
				token,
				body: devicePayload(form)
			});
			return { success: true };
		} catch (error) {
			if (error instanceof ServerApiError) {
				return fail(400, { message: error.message });
			}
			throw error;
		}
	},
	deleteDevice: async ({ params, request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const customerId = Number(params.id);
		const form = await request.formData();
		const deviceId = Number(form.get('deviceId'));

		try {
			await serverApiRequest(fetch, `/customers/${customerId}/devices/${deviceId}`, {
				method: 'DELETE',
				token
			});
			return { success: true };
		} catch (error) {
			if (error instanceof ServerApiError) {
				return fail(400, { message: error.message });
			}
			throw error;
		}
	},
	star: async ({ params, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const customerId = Number(params.id);

		try {
			await serverApiRequest(fetch, `/customers/${customerId}/star`, {
				method: 'POST',
				token
			});
			return { success: true };
		} catch (error) {
			if (error instanceof ServerApiError) {
				return fail(400, { message: error.message });
			}
			throw error;
		}
	},
	unstar: async ({ params, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const customerId = Number(params.id);

		try {
			await serverApiRequest(fetch, `/customers/${customerId}/unstar`, {
				method: 'POST',
				token
			});
			return { success: true };
		} catch (error) {
			if (error instanceof ServerApiError) {
				return fail(400, { message: error.message });
			}
			throw error;
		}
	},
	ban: async ({ params, request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const customerId = Number(params.id);
		const form = await request.formData();
		const reason = String(form.get('reason') ?? '').trim();

		try {
			await serverApiRequest(fetch, `/customers/${customerId}/ban`, {
				method: 'POST',
				token,
				body: { reason: reason || undefined }
			});
			return { success: true };
		} catch (error) {
			if (error instanceof ServerApiError) {
				return fail(400, { message: error.message });
			}
			throw error;
		}
	},
	unban: async ({ params, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const customerId = Number(params.id);

		try {
			await serverApiRequest(fetch, `/customers/${customerId}/unban`, {
				method: 'POST',
				token
			});
			return { success: true };
		} catch (error) {
			if (error instanceof ServerApiError) {
				return fail(400, { message: error.message });
			}
			throw error;
		}
	},
	delete: async ({ params, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const customerId = Number(params.id);

		try {
			await serverApiRequest(fetch, `/customers/${customerId}`, {
				method: 'DELETE',
				token
			});
		} catch (error) {
			if (error instanceof ServerApiError) {
				return fail(400, { message: error.message });
			}
			throw error;
		}

		throw redirect(303, '/app/customers');
	}
};

function devicePayload(form: FormData) {
	return {
		deviceType: String(form.get('deviceType') ?? '').trim(),
		brand: String(form.get('brand') ?? '').trim(),
		model: String(form.get('model') ?? '').trim(),
		serialNumber: optionalString(form.get('serialNumber')),
		identifierLabel: optionalString(form.get('identifierLabel')),
		notes: optionalString(form.get('notes'))
	};
}

function optionalString(value: FormDataEntryValue | null) {
	const normalized = String(value ?? '').trim();
	return normalized || undefined;
}
