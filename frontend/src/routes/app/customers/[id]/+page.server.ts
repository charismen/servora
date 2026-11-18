import { fail, redirect } from '@sveltejs/kit';
import { serverApiRequest, ServerApiError } from '$lib/server/api';
import type { Customer } from '$lib/types/customers';
import type { Device } from '$lib/types/devices';
import type { CustomerLocation } from '$lib/types/maintenance';
import type { RepeatCustomerSummary, ServiceHistoryTicket } from '$lib/types/service-tickets';
import type { Actions, PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ parent, params, cookies, fetch }) => {
	await parent();
	const token = cookies.get('servora_session');
	const customerId = Number(params.id);

	try {
		const [customer, devices, locations, serviceHistory, repeatSummary] = await Promise.all([
			serverApiRequest<Customer>(fetch, `/customers/${customerId}`, { token }),
			serverApiRequest<Device[]>(fetch, `/customers/${customerId}/devices`, { token }).catch(() => []),
			serverApiRequest<CustomerLocation[]>(fetch, `/locations?customerId=${customerId}`, { token }).catch(() => []),
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
			locations,
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
	deleteLocation: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const locationId = Number(form.get('locationId'));
		try {
			await serverApiRequest(fetch, `/locations/${locationId}`, { method: 'DELETE', token });
			return { success: true, messageKey: 'customers.locations.deleted' };
		} catch (error) {
			if (error instanceof ServerApiError) return fail(400, { message: error.message });
			throw error;
		}
	},
	createLocation: async ({ params, request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const customerId = Number(params.id);
		const form = await request.formData();
		const label = String(form.get('label') ?? '').trim();
		const address = String(form.get('address') ?? '').trim();
		if (!label || !address) {
			return fail(400, { message: 'Site name and address are required.' });
		}
		try {
			await serverApiRequest(fetch, '/locations', {
				method: 'POST',
				token,
				body: {
					customerId,
					label,
					address,
					contactName: optionalString(form.get('contactName')),
					contactPhone: optionalString(form.get('contactPhone'))
				}
			});
			return { success: true, messageKey: 'customers.locations.created' };
		} catch (error) {
			if (error instanceof ServerApiError) return fail(400, { message: error.message });
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
	const meterRaw = optionalString(form.get('meterReading'));
	const siteRaw = optionalString(form.get('siteLocationId'));
	const customFields: { fieldDefinitionId: number; value: string | null }[] = [];
	for (const [key, val] of form.entries()) {
		if (!key.startsWith('assetField_')) continue;
		const id = Number(key.slice('assetField_'.length));
		if (Number.isInteger(id) && id > 0) {
			customFields.push({ fieldDefinitionId: id, value: optionalString(val) ?? null });
		}
	}
	return {
		deviceType: String(form.get('deviceType') ?? '').trim(),
		brand: String(form.get('brand') ?? '').trim(),
		model: String(form.get('model') ?? '').trim(),
		serialNumber: optionalString(form.get('serialNumber')),
		identifierLabel: optionalString(form.get('identifierLabel')),
		notes: optionalString(form.get('notes')),
		meterReading: meterRaw ? Number(meterRaw) : null,
		meterUnit: optionalString(form.get('meterUnit')),
		siteLocationId: siteRaw ? Number(siteRaw) : null,
		customFields: customFields.length > 0 ? customFields : null
	};
}

function optionalString(value: FormDataEntryValue | null) {
	const normalized = String(value ?? '').trim();
	return normalized || undefined;
}
