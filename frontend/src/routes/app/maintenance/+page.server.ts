import { fail, redirect } from '@sveltejs/kit';
import { serverApiRequest, ServerApiError } from '$lib/server/api';
import type { MaintenanceSchedule } from '$lib/types/maintenance';
import type { Device } from '$lib/types/devices';
import type { ServiceTemplate } from '$lib/types/service-templates';
import type { Actions, PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ cookies, fetch, url }) => {
	const token = cookies.get('servora_session');
	if (!token) throw redirect(303, '/login');

	const dueOnly = url.searchParams.get('dueOnly') === 'true';

	const [schedules, assets, templates] = await Promise.all([
		serverApiRequest<MaintenanceSchedule[]>(fetch, `/maintenance-schedules?dueOnly=${dueOnly}`, { token }).catch(() => []),
		serverApiRequest<Device[]>(fetch, '/service-assets', { token }).catch(() => []),
		serverApiRequest<ServiceTemplate[]>(fetch, '/service-templates', { token }).catch(() => [])
	]);

	return { schedules, assets, templates, dueOnly };
};

export const actions: Actions = {
	create: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		if (!token) throw redirect(303, '/login');

		const formData = await request.formData();
		const serviceAssetId = Number(formData.get('serviceAssetId'));
		const serviceTemplateIdRaw = String(formData.get('serviceTemplateId') ?? '').trim();
		const title = String(formData.get('title') ?? '').trim();
		const description = String(formData.get('description') ?? '').trim() || null;
		const triggerType = String(formData.get('triggerType') ?? 'Calendar');
		const intervalValueRaw = String(formData.get('intervalValue') ?? '').trim();
		const intervalUnit = String(formData.get('intervalUnit') ?? 'Months');
		const meterIntervalRaw = String(formData.get('meterInterval') ?? '').trim();
		const nextDueAtRaw = String(formData.get('nextDueAt') ?? '').trim();
		const nextDueMeterRaw = String(formData.get('nextDueMeter') ?? '').trim();
		const leadTimeDays = Number(formData.get('leadTimeDays') ?? 7) || 7;
		const autoCreateTicket = formData.get('autoCreateTicket') === 'on';
		const defaultLocationType = String(formData.get('defaultLocationType') ?? 'InShop');

		if (!serviceAssetId || !title) {
			return fail(400, { success: false, message: 'Service asset and title are required.' });
		}

		try {
			await serverApiRequest(fetch, '/maintenance-schedules', {
				method: 'POST',
				token,
				body: {
					serviceAssetId,
					serviceTemplateId: serviceTemplateIdRaw ? Number(serviceTemplateIdRaw) : null,
					title,
					description,
					triggerType,
					intervalValue: intervalValueRaw ? Number(intervalValueRaw) : null,
					intervalUnit: intervalValueRaw ? intervalUnit : null,
					meterInterval: meterIntervalRaw ? Number(meterIntervalRaw) : null,
					nextDueAt: nextDueAtRaw ? new Date(nextDueAtRaw).toISOString() : null,
					nextDueMeter: nextDueMeterRaw ? Number(nextDueMeterRaw) : null,
					leadTimeDays,
					autoCreateTicket,
					defaultLocationType,
					status: 'Active'
				}
			});
			return { success: true, messageKey: 'maintenance.created' };
		} catch (error) {
			if (error instanceof ServerApiError) return fail(error.status, { success: false, message: error.message });
			return fail(500, { success: false, message: 'Unable to create schedule.' });
		}
	},

	generateTicket: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		if (!token) throw redirect(303, '/login');
		const id = Number((await request.formData()).get('id'));
		try {
			await serverApiRequest(fetch, `/maintenance-schedules/${id}/generate-ticket`, { method: 'POST', token });
			return { success: true, messageKey: 'maintenance.ticketGenerated' };
		} catch (error) {
			if (error instanceof ServerApiError) return fail(error.status, { success: false, message: error.message });
			return fail(500, { success: false, message: 'Failed to generate ticket.' });
		}
	},

	completeCycle: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		if (!token) throw redirect(303, '/login');
		const formData = await request.formData();
		const id = Number(formData.get('id'));
		const meterRaw = String(formData.get('meterReading') ?? '').trim();
		try {
			await serverApiRequest(fetch, `/maintenance-schedules/${id}/complete`, {
				method: 'POST',
				token,
				body: { meterReading: meterRaw ? Number(meterRaw) : null }
			});
			return { success: true, messageKey: 'maintenance.cycleCompleted' };
		} catch (error) {
			if (error instanceof ServerApiError) return fail(error.status, { success: false, message: error.message });
			return fail(500, { success: false, message: 'Failed to complete cycle.' });
		}
	},

	runDueCheck: async ({ cookies, fetch }) => {
		const token = cookies.get('servora_session');
		if (!token) throw redirect(303, '/login');
		try {
			const res = await serverApiRequest<{ scannedSchedules: number; upcomingNotified: number; ticketsAutoCreated: number }>(
				fetch,
				'/maintenance-schedules/run-due-check',
				{ method: 'POST', token }
			);
			return {
				success: true,
				message: `${res.scannedSchedules} scanned · ${res.upcomingNotified} notified · ${res.ticketsAutoCreated} auto-created`
			};
		} catch (error) {
			if (error instanceof ServerApiError) return fail(error.status, { success: false, message: error.message });
			return fail(500, { success: false, message: 'Due check failed.' });
		}
	},

	delete: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		if (!token) throw redirect(303, '/login');
		const id = Number((await request.formData()).get('id'));
		try {
			await serverApiRequest(fetch, `/maintenance-schedules/${id}`, { method: 'DELETE', token });
			return { success: true, messageKey: 'maintenance.deleted' };
		} catch (error) {
			if (error instanceof ServerApiError) return fail(error.status, { success: false, message: error.message });
			return fail(500, { success: false, message: 'Failed to delete schedule.' });
		}
	}
};
