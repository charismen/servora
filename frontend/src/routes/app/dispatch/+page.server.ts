import { fail } from '@sveltejs/kit';
import { serverApiRequest, ServerApiError } from '$lib/server/api';
import type { DispatchBoardResponse, DispatchConflict, ServiceResource } from '$lib/types/dispatch';
import type { Actions, PageServerLoad } from './$types';

function formatYmd(date: Date): string {
	const y = date.getFullYear();
	const m = String(date.getMonth() + 1).padStart(2, '0');
	const d = String(date.getDate()).padStart(2, '0');
	return `${y}-${m}-${d}`;
}

function parseYmd(raw: string | null): Date {
	if (raw && /^\d{4}-\d{2}-\d{2}$/.test(raw)) {
		const [y, m, d] = raw.split('-').map(Number);
		const parsed = new Date(y, m - 1, d);
		if (!Number.isNaN(parsed.getTime())) return parsed;
	}
	const now = new Date();
	return new Date(now.getFullYear(), now.getMonth(), now.getDate());
}

function startOfWeekMonday(date: Date): Date {
	const copy = new Date(date.getFullYear(), date.getMonth(), date.getDate());
	const day = copy.getDay();
	const diff = day === 0 ? -6 : 1 - day;
	copy.setDate(copy.getDate() + diff);
	return copy;
}

export const load: PageServerLoad = async ({ parent, cookies, fetch, url }) => {
	await parent();
	const token = cookies.get('servora_session');

	const view = url.searchParams.get('view') === 'week' ? 'week' : 'day';
	const groupBy = url.searchParams.get('groupBy') === 'resource' ? 'resource' : 'technician';
	const locationType = url.searchParams.get('locationType') ?? '';
	const anchorDate = parseYmd(url.searchParams.get('date'));
	const anchorDateStr = formatYmd(anchorDate);

	const rangeStartLocal = view === 'week' ? startOfWeekMonday(anchorDate) : anchorDate;
	const rangeEndLocal = new Date(rangeStartLocal);
	rangeEndLocal.setDate(rangeEndLocal.getDate() + (view === 'week' ? 7 : 1));

	// Pad by 1 day on both sides for timezone-safe board queries
	const queryFrom = new Date(rangeStartLocal.getTime() - 24 * 60 * 60 * 1000).toISOString();
	const queryTo = new Date(rangeEndLocal.getTime() + 24 * 60 * 60 * 1000).toISOString();

	const params = new URLSearchParams({
		from: queryFrom,
		to: queryTo
	});
	if (locationType) params.set('locationType', locationType);

	const [board, allResources] = await Promise.all([
		serverApiRequest<DispatchBoardResponse>(fetch, `/dispatch/board?${params.toString()}`, { token }),
		serverApiRequest<ServiceResource[]>(fetch, '/service-resources?includeInactive=true', { token }).catch(() => [])
	]);

	return {
		board,
		allResources,
		filters: {
			view,
			groupBy,
			locationType,
			date: anchorDateStr,
			weekStart: formatYmd(rangeStartLocal)
		}
	};
};

function extractConflicts(details: unknown): DispatchConflict[] {
	if (details && typeof details === 'object' && 'conflicts' in details) {
		const c = (details as { conflicts?: unknown }).conflicts;
		if (Array.isArray(c)) return c as DispatchConflict[];
	}
	return [];
}

function parseNullableInt(value: FormDataEntryValue | null): number | null {
	const raw = String(value ?? '').trim();
	if (!raw) return null;
	const num = Number(raw);
	return Number.isInteger(num) && num > 0 ? num : null;
}

export const actions: Actions = {
	scheduleTicket: async ({ request, fetch, cookies }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const ticketId = Number(form.get('ticketId') ?? 0);
		const scheduledVisitStart = String(form.get('scheduledVisitStart') ?? '').trim();
		const scheduledVisitEnd = String(form.get('scheduledVisitEnd') ?? '').trim() || null;
		const assignedTechnicianUserId = parseNullableInt(form.get('assignedTechnicianUserId'));
		const serviceResourceId = parseNullableInt(form.get('serviceResourceId'));
		const serviceLocationType = String(form.get('serviceLocationType') ?? '').trim() || null;
		const siteAddressSnapshot = form.has('siteAddressSnapshot')
			? String(form.get('siteAddressSnapshot') ?? '').trim()
			: null;
		const dispatchNotes = form.has('dispatchNotes')
			? String(form.get('dispatchNotes') ?? '').trim()
			: null;
		const allowConflict = form.get('allowConflict') === 'true';

		try {
			await serverApiRequest(fetch, `/dispatch/tickets/${ticketId}/schedule`, {
				method: 'PUT',
				token,
				body: {
					scheduledVisitStart,
					scheduledVisitEnd,
					assignedTechnicianUserId,
					serviceResourceId,
					serviceLocationType,
					siteAddressSnapshot,
					dispatchNotes,
					allowConflict
				}
			});
			return { ok: true, success: true, action: 'scheduleTicket', messageKey: 'dispatch.action.scheduledSaved' };
		} catch (error) {
			if (error instanceof ServerApiError) {
				if (error.status === 409) {
					return fail(409, {
						ok: false,
						success: false,
						variant: 'warning',
						action: 'scheduleTicket',
						conflict: true,
						ticketId,
						conflicts: extractConflicts(error.details),
						message: error.message
					});
				}
				return fail(error.status, { ok: false, success: false, action: 'scheduleTicket', message: error.message });
			}
			throw error;
		}
	},

	unscheduleTicket: async ({ request, fetch, cookies }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const ticketId = Number(form.get('ticketId') ?? 0);

		try {
			await serverApiRequest(fetch, `/dispatch/tickets/${ticketId}/schedule`, {
				method: 'DELETE',
				token
			});
			return { ok: true, success: true, action: 'unscheduleTicket', messageKey: 'dispatch.action.unscheduledSaved' };
		} catch (error) {
			if (error instanceof ServerApiError) {
				return fail(error.status, { ok: false, success: false, action: 'unscheduleTicket', message: error.message });
			}
			throw error;
		}
	},

	updateDispatchStatus: async ({ request, fetch, cookies }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const ticketId = Number(form.get('ticketId') ?? 0);
		const dispatchStatus = String(form.get('dispatchStatus') ?? '').trim();
		const note = String(form.get('note') ?? '').trim() || null;

		try {
			await serverApiRequest(fetch, `/dispatch/tickets/${ticketId}/dispatch-status`, {
				method: 'POST',
				token,
				body: { dispatchStatus, note }
			});
			return { ok: true, success: true, action: 'updateDispatchStatus', messageKey: 'dispatch.action.statusUpdated' };
		} catch (error) {
			if (error instanceof ServerApiError) {
				return fail(error.status, { ok: false, success: false, action: 'updateDispatchStatus', message: error.message });
			}
			throw error;
		}
	},

	scheduleMaintenance: async ({ request, fetch, cookies }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const scheduleId = Number(form.get('scheduleId') ?? 0);
		const scheduledVisitStart = String(form.get('scheduledVisitStart') ?? '').trim();
		const scheduledVisitEnd = String(form.get('scheduledVisitEnd') ?? '').trim() || null;
		const assignedTechnicianUserId = parseNullableInt(form.get('assignedTechnicianUserId'));
		const serviceResourceId = parseNullableInt(form.get('serviceResourceId'));
		const serviceLocationType = String(form.get('serviceLocationType') ?? '').trim() || null;
		const siteAddressSnapshot = String(form.get('siteAddressSnapshot') ?? '').trim() || null;
		const dispatchNotes = String(form.get('dispatchNotes') ?? '').trim() || null;
		const allowConflict = form.get('allowConflict') === 'true';

		try {
			await serverApiRequest(fetch, `/dispatch/maintenance/${scheduleId}/schedule`, {
				method: 'POST',
				token,
				body: {
					scheduledVisitStart,
					scheduledVisitEnd,
					assignedTechnicianUserId,
					serviceResourceId,
					serviceLocationType,
					siteAddressSnapshot,
					dispatchNotes,
					allowConflict
				}
			});
			return { ok: true, success: true, action: 'scheduleMaintenance', messageKey: 'dispatch.action.pmScheduled' };
		} catch (error) {
			if (error instanceof ServerApiError) {
				if (error.status === 409) {
					return fail(409, {
						ok: false,
						success: false,
						variant: 'warning',
						action: 'scheduleMaintenance',
						conflict: true,
						scheduleId,
						conflicts: extractConflicts(error.details),
						message: error.message
					});
				}
				return fail(error.status, { ok: false, success: false, action: 'scheduleMaintenance', message: error.message });
			}
			throw error;
		}
	},

	saveResource: async ({ request, fetch, cookies }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const id = Number(form.get('id') ?? 0);
		const payload = {
			name: String(form.get('name') ?? '').trim(),
			code: String(form.get('code') ?? '').trim() || null,
			resourceType: String(form.get('resourceType') ?? 'Bay'),
			color: String(form.get('color') ?? '#b45039'),
			notes: String(form.get('notes') ?? '').trim() || null,
			sortOrder: Number(form.get('sortOrder') ?? 0) || 0,
			isActive: form.get('isActive') !== 'false'
		};

		try {
			await serverApiRequest(fetch, id > 0 ? `/service-resources/${id}` : '/service-resources', {
				method: id > 0 ? 'PUT' : 'POST',
				token,
				body: payload
			});
			return { ok: true, success: true, action: 'saveResource', messageKey: 'dispatch.resources.saved' };
		} catch (error) {
			if (error instanceof ServerApiError) {
				return fail(error.status, { ok: false, success: false, action: 'saveResource', message: error.message });
			}
			throw error;
		}
	},

	deleteResource: async ({ request, fetch, cookies }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const id = Number(form.get('id') ?? 0);

		try {
			await serverApiRequest(fetch, `/service-resources/${id}`, {
				method: 'DELETE',
				token
			});
			return { ok: true, success: true, action: 'deleteResource', messageKey: 'dispatch.resources.deleted' };
		} catch (error) {
			if (error instanceof ServerApiError) {
				return fail(error.status, { ok: false, success: false, action: 'deleteResource', message: error.message });
			}
			throw error;
		}
	}
};
