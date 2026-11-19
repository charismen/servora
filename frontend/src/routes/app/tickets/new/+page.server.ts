import { fail, isHttpError, isRedirect, redirect } from '@sveltejs/kit';
import { resolveSupportedImageMime } from '$lib/media';
import { serverApiRequest, ServerApiError } from '$lib/server/api';
import type { Actions, PageServerLoad } from './$types';
import type { CustomersResponse } from '$lib/types/customers';
import type { Device } from '$lib/types/devices';
import type { CustomerLocation } from '$lib/types/maintenance';
import type { ShopProfileSettings } from '$lib/types/settings';
import type { StaffUser } from '$lib/types/shop-users';

const MAX_INTAKE_PHOTO_BYTES = 5 * 1024 * 1024;

export const load: PageServerLoad = async ({ parent, cookies, fetch }) => {
	const parentData = await parent();
	const token = cookies.get('servora_session');
	const [customers, allDevices, staff, locations] = await Promise.all([
		serverApiRequest<CustomersResponse>(fetch, '/customers?page=1&pageSize=100', { token }).catch(() => ({
			items: [],
			totalCount: 0,
			page: 1,
			pageSize: 100
		})),
		serverApiRequest<Device[]>(fetch, '/service-assets', { token }).catch(() => []),
		serverApiRequest<StaffUser[]>(fetch, '/shop-users/staff', { token }).catch(() => []),
		serverApiRequest<CustomerLocation[]>(fetch, '/locations', { token }).catch(() => [])
	]);

	const devicesByCustomer = new Map<number, Device[]>();
	for (const device of allDevices) {
		const list = devicesByCustomer.get(device.customerId) ?? [];
		list.push(device);
		devicesByCustomer.set(device.customerId, list);
	}

	const deviceGroups = customers.items.map((customer) => ({
		customerId: customer.id,
		devices: devicesByCustomer.get(customer.id) ?? []
	}));

	return {
		customers: customers.items,
		allDevices,
		deviceGroups,
		staff,
		locations,
		idempotencyKey: crypto.randomUUID(),
		requirePhotoEvidenceOnIntake: parentData.shopProfile?.requirePhotoEvidenceOnIntake ?? false
	};
};

export const actions: Actions = {
	default: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const customerId = Number(form.get('customerId'));
		const deviceId = Number(form.get('deviceId') || 0);
		const deviceMode = String(form.get('deviceMode') ?? 'existing');
		const deferAttachments = String(form.get('deferAttachments') ?? '') === 'true';

		const assetCustomFields = extractPrefixedFields(form, 'assetField_');
		const ticketCustomFields = extractPrefixedFields(form, 'ticketField_');

		let inspections: unknown[] = [];
		const inspectionsJson = nullable(form.get('inspectionsJson'));
		if (inspectionsJson) {
			try {
				const parsed = JSON.parse(inspectionsJson);
				if (Array.isArray(parsed)) inspections = parsed;
			} catch {
				inspections = [];
			}
		}

		const newDevice =
			deviceMode === 'new'
				? {
						deviceType: String(form.get('newDeviceType') ?? '').trim(),
						brand: String(form.get('newDeviceBrand') ?? '').trim(),
						model: String(form.get('newDeviceModel') ?? '').trim(),
						serialNumber: nullable(form.get('newDeviceSerialNumber')),
						identifierLabel: nullable(form.get('newDeviceIdentifierLabel')),
						meterReading: nonNegativeNumberOrNull(form.get('newDeviceMeterReading')),
						meterUnit: nullable(form.get('newDeviceMeterUnit')),
						siteLocationId: positiveIdOrNull(form.get('siteLocationId')),
						notes: nullable(form.get('newDeviceNotes')),
						customFields: assetCustomFields
					}
				: null;

		try {
			if (!Number.isFinite(customerId) || customerId <= 0) {
				return fail(400, { success: false, messageKey: 'tickets.create.customerRequired' });
			}
			if (deviceMode !== 'new' && (!Number.isFinite(deviceId) || deviceId <= 0)) {
				return fail(400, { success: false, messageKey: 'tickets.create.deviceRequired' });
			}
			if (deviceMode === 'new' && (!newDevice?.deviceType || !newDevice?.brand || !newDevice?.model)) {
				return fail(400, { success: false, messageKey: 'tickets.create.deviceDetailsRequired' });
			}

			const createdAtVal = nullable(form.get('createdAt'));
			const createdAt = parseOptionalIso(createdAtVal);
			if (createdAtVal && !createdAt) {
				return fail(400, { success: false, messageKey: 'tickets.create.invalidDate' });
			}
			const scheduledStartVal = nullable(form.get('scheduledVisitStart'));
			const scheduledVisitStart = parseOptionalIso(scheduledStartVal);
			if (scheduledStartVal && !scheduledVisitStart) {
				return fail(400, { success: false, messageKey: 'tickets.create.invalidDate' });
			}
			const scheduledEndVal = nullable(form.get('scheduledVisitEnd'));
			const scheduledVisitEnd = parseOptionalIso(scheduledEndVal);
			if (scheduledEndVal && !scheduledVisitEnd) {
				return fail(400, { success: false, messageKey: 'tickets.create.invalidDate' });
			}
			const slaDueVal = nullable(form.get('slaDueAt'));
			const slaDueAt = parseOptionalIso(slaDueVal);
			if (slaDueVal && !slaDueAt) {
				return fail(400, { success: false, messageKey: 'tickets.create.invalidDate' });
			}

			const settings = await serverApiRequest<ShopProfileSettings>(fetch, '/settings/shop-profile', { token }).catch(() => null);
			const intakePhotos = deferAttachments
				? []
				: form.getAll('intakePhotos').filter((file): file is File => file instanceof File && file.size > 0);
			const intakePhotoVisibility = deferAttachments
				? []
				: form.getAll('intakePhotoVisibility').map((v) => String(v) === 'true');
			const intakePhotoTypes = deferAttachments
				? []
				: form.getAll('intakePhotoTypes').map((v) => String(v ?? '').trim() || 'Other');
			const deferredCountRaw = Number(form.get('intakePhotoCount') ?? 0);
			const intakePhotoCount = deferAttachments
				? (Number.isFinite(deferredCountRaw) && deferredCountRaw > 0 ? Math.trunc(deferredCountRaw) : 0)
				: intakePhotos.length;

			if (settings?.requirePhotoEvidenceOnIntake && intakePhotoCount === 0) {
				return fail(400, { success: false, messageKey: 'tickets.create.intakePhotoRequired' });
			}

			const normalizedPhotos: File[] = [];
			for (const file of intakePhotos) {
				if (file.size > MAX_INTAKE_PHOTO_BYTES) {
					return fail(400, { success: false, messageKey: 'tickets.create.intakePhotoTooLarge' });
				}
				const mime = resolveSupportedImageMime(file);
				if (!mime) {
					return fail(400, { success: false, messageKey: 'tickets.create.intakePhotoTypeInvalid' });
				}
				normalizedPhotos.push(
					file.type.toLowerCase() === mime
						? file
						: new File([file], file.name, { type: mime, lastModified: file.lastModified })
				);
			}

			const additionalAssetIds = form
				.getAll('additionalAssetIds')
				.map((val) => Number(val))
				.filter((id) => Number.isInteger(id) && id > 0 && id !== deviceId);

			const result = await serverApiRequest<{ id: number; ticketNumber: string; publicTrackingToken: string }>(fetch, '/service-tickets', {
				method: 'POST',
				token,
				body: {
					customerId,
					deviceId: deviceMode === 'new' ? null : deviceId,
					newDevice,
					complaint: String(form.get('complaint') ?? ''),
					initialDiagnosis: nullable(form.get('initialDiagnosis')),
					physicalConditionNotes: nullable(form.get('physicalConditionNotes')),
					accessorySummary: nullable(form.get('accessorySummary')),
					photoNotes: nullable(form.get('photoNotes')),
					securityPasscode: nullable(form.get('securityPasscode')),
					securityPattern: nullable(form.get('securityPattern')),
					preRepairChecklist: null,
					priority: String(form.get('priority') ?? 'Normal'),
					assignedTechnicianUserId: positiveIdOrNull(form.get('assignedTechnicianUserId')),
					approvalRequired: form.get('approvalRequired') === 'on',
					isPreApproved: form.get('isPreApproved') === 'on',
					createdAt,
					idempotencyKey: nullable(form.get('idempotencyKey')),
					bannedCustomerOverrideReason: nullable(form.get('bannedCustomerOverrideReason')),
					intakePhotoCount,
					isInternalWorkOrder: form.has('isInternalWorkOrderPresent') ? form.get('isInternalWorkOrder') === 'on' : null,
					departmentCode: nullable(form.get('departmentCode')),
					costCenter: nullable(form.get('costCenter')),
					slaDueAt,
					serviceLocationType: nullable(form.get('serviceLocationType')),
					siteLocationId: positiveIdOrNull(form.get('siteLocationId')),
					siteAddressSnapshot: nullable(form.get('siteAddressSnapshot')),
					scheduledVisitStart,
					scheduledVisitEnd,
					additionalAssetIds: additionalAssetIds.length > 0 ? additionalAssetIds : null,
					customFields: ticketCustomFields,
					inspections
				}
			});

			if (deferAttachments) {
				return {
					success: true,
					ticketId: result.id,
					ticketNumber: result.ticketNumber
				};
			}

			for (let i = 0; i < normalizedPhotos.length; i++) {
				const isPublicVisible = intakePhotoVisibility[i] ?? true;
				const body = new FormData();
				body.set('file', normalizedPhotos[i]);
				body.set('attachmentType', intakePhotoTypes[i] ?? 'Other');
				body.set('isPublicVisible', isPublicVisible ? 'true' : 'false');
				await serverApiRequest(fetch, `/service-tickets/${result.id}/attachments`, { method: 'POST', token, body });
			}
			throw redirect(303, `/app/tickets/${result.id}/receipt`);
		} catch (error) {
			if (isRedirect(error) || isHttpError(error)) throw error;
			if (error instanceof ServerApiError) return fail(error.status, { success: false, message: error.message });
			const message = error instanceof Error ? error.message : 'Failed to create ticket.';
			return fail(500, { success: false, message });
		}
	}
};

function nullable(value: FormDataEntryValue | null) {
	const text = String(value ?? '').trim();
	return text.length > 0 ? text : null;
}

function positiveIdOrNull(value: FormDataEntryValue | null) {
	const text = nullable(value);
	if (!text) return null;
	const parsed = Number(text);
	return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
}

function nonNegativeNumberOrNull(value: FormDataEntryValue | null) {
	const text = nullable(value);
	if (!text) return null;
	const parsed = Number(text);
	return Number.isFinite(parsed) && parsed >= 0 ? parsed : null;
}

function parseOptionalIso(value: string | null) {
	if (!value) return null;
	const parsed = new Date(value);
	return Number.isNaN(parsed.getTime()) ? null : parsed.toISOString();
}

function extractPrefixedFields(form: FormData, prefix: string) {
	const values: { fieldDefinitionId: number; value: string }[] = [];
	for (const [key, val] of form.entries()) {
		if (!key.startsWith(prefix)) continue;
		const id = Number(key.slice(prefix.length));
		const text = String(val ?? '').trim();
		if (Number.isInteger(id) && id > 0 && text.length > 0) {
			values.push({ fieldDefinitionId: id, value: text });
		}
	}
	return values;
}
