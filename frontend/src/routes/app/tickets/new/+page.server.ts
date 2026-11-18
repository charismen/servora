import { fail, isHttpError, isRedirect, redirect } from '@sveltejs/kit';
import { serverApiRequest, ServerApiError } from '$lib/server/api';
import type { Actions, PageServerLoad } from './$types';
import type { CustomersResponse } from '$lib/types/customers';
import type { Device } from '$lib/types/devices';
import type { CustomerLocation } from '$lib/types/maintenance';
import type { ShopProfileSettings } from '$lib/types/settings';
import type { StaffUser } from '$lib/types/shop-users';

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
						deviceType: String(form.get('newDeviceType') ?? ''),
						brand: String(form.get('newDeviceBrand') ?? ''),
						model: String(form.get('newDeviceModel') ?? ''),
						serialNumber: nullable(form.get('newDeviceSerialNumber')),
						identifierLabel: nullable(form.get('newDeviceIdentifierLabel')),
						meterReading: numberOrNull(form.get('newDeviceMeterReading')),
						meterUnit: nullable(form.get('newDeviceMeterUnit')),
						siteLocationId: numberOrNull(form.get('siteLocationId')),
						notes: nullable(form.get('newDeviceNotes')),
						customFields: assetCustomFields
					}
				: null;

		try {
			if (!customerId) return fail(400, { success: false, messageKey: 'tickets.create.customerRequired' });
			if (deviceMode !== 'new' && !deviceId) return fail(400, { success: false, messageKey: 'tickets.create.deviceRequired' });
			const settings = await serverApiRequest<ShopProfileSettings>(fetch, '/settings/shop-profile', { token }).catch(() => null);
			const intakePhotos = form.getAll('intakePhotos').filter((file): file is File => file instanceof File && file.size > 0);
			const intakePhotoVisibility = form.getAll('intakePhotoVisibility').map((v) => String(v) === 'true');
			if (settings?.requirePhotoEvidenceOnIntake && intakePhotos.length === 0) return fail(400, { success: false, messageKey: 'tickets.create.intakePhotoRequired' });
			for (const file of intakePhotos) {
				if (file.size > 5 * 1024 * 1024) return fail(400, { success: false, messageKey: 'tickets.create.intakePhotoTooLarge' });
				if (!['image/jpeg', 'image/jpg', 'image/png', 'image/webp'].includes(file.type.toLowerCase())) {
					return fail(400, { success: false, messageKey: 'tickets.create.intakePhotoTypeInvalid' });
				}
			}

			const createdAtVal = nullable(form.get('createdAt'));
			const createdAt = createdAtVal ? new Date(createdAtVal).toISOString() : null;
			const scheduledStartVal = nullable(form.get('scheduledVisitStart'));
			const scheduledEndVal = nullable(form.get('scheduledVisitEnd'));
			const slaDueVal = nullable(form.get('slaDueAt'));
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
					assignedTechnicianUserId: numberOrNull(form.get('assignedTechnicianUserId')),
					approvalRequired: form.get('approvalRequired') === 'on',
					isPreApproved: form.get('isPreApproved') === 'on',
					createdAt,
					idempotencyKey: nullable(form.get('idempotencyKey')),
					bannedCustomerOverrideReason: nullable(form.get('bannedCustomerOverrideReason')),
					intakePhotoCount: intakePhotos.length,
					isInternalWorkOrder: form.has('isInternalWorkOrderPresent') ? form.get('isInternalWorkOrder') === 'on' : null,
					departmentCode: nullable(form.get('departmentCode')),
					costCenter: nullable(form.get('costCenter')),
					slaDueAt: slaDueVal ? new Date(slaDueVal).toISOString() : null,
					serviceLocationType: nullable(form.get('serviceLocationType')),
					siteLocationId: numberOrNull(form.get('siteLocationId')),
					siteAddressSnapshot: nullable(form.get('siteAddressSnapshot')),
					scheduledVisitStart: scheduledStartVal ? new Date(scheduledStartVal).toISOString() : null,
					scheduledVisitEnd: scheduledEndVal ? new Date(scheduledEndVal).toISOString() : null,
					additionalAssetIds: additionalAssetIds.length > 0 ? additionalAssetIds : null,
					customFields: ticketCustomFields,
					inspections
				}
			});
			for (let i = 0; i < intakePhotos.length; i++) {
				const rawFile = intakePhotos[i];
				const uploadFile =
					rawFile.type.toLowerCase() === 'image/jpg'
						? new File([rawFile], rawFile.name, { type: 'image/jpeg', lastModified: rawFile.lastModified })
						: rawFile;
				const isPublicVisible = intakePhotoVisibility[i] ?? true;
				const body = new FormData();
				body.set('file', uploadFile);
				body.set('attachmentType', 'Other');
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

function numberOrNull(value: FormDataEntryValue | null) {
	const text = nullable(value);
	return text ? Number(text) : null;
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
