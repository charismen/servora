import { fail } from '@sveltejs/kit';
import { env } from '$env/dynamic/public';
import { resolveSupportedImageMime } from '$lib/media';
import { serverApiRequest, ServerApiError } from '$lib/server/api';
import type { Actions, PageServerLoad } from './$types';
import type { InventoryItem, TicketInventoryUsage } from '$lib/types/inventory';
import type { Device } from '$lib/types/devices';
import type { CustomerLocation } from '$lib/types/maintenance';
import type { GeneratedWhatsAppMessage, TicketApproval, TicketDetail, TicketHistory, TicketPayment, TicketWorkItem, WorkItemType } from '$lib/types/service-tickets';
import type { ServiceTemplate, TicketChecklistItem } from '$lib/types/service-templates';
import type { StaffUser } from '$lib/types/shop-users';

export const load: PageServerLoad = async ({ fetch, cookies, params }) => {
	const token = cookies.get('servora_session');
	const id = Number(params.id);

	const [ticket, history, approvals, workItems, payment, inventoryItems, inventoryUsages, staff, serviceTemplates, checklistItems, locations, allAssets] = await Promise.all([
		serverApiRequest<TicketDetail>(fetch, `/service-tickets/${id}`, { token }),
		serverApiRequest<TicketHistory[]>(fetch, `/service-tickets/${id}/history`, { token }),
		serverApiRequest<TicketApproval[]>(fetch, `/service-tickets/${id}/approvals`, { token }),
		serverApiRequest<TicketWorkItem[]>(fetch, `/service-tickets/${id}/work-items`, { token }).catch(() => []),
		serverApiRequest<TicketPayment>(fetch, `/service-tickets/${id}/payment`, { token }),
		serverApiRequest<InventoryItem[]>(fetch, '/inventory/items', { token }).catch(() => []),
		serverApiRequest<TicketInventoryUsage[]>(fetch, `/service-tickets/${id}/inventory`, { token }).catch(() => []),
		serverApiRequest<StaffUser[]>(fetch, '/shop-users/staff', { token }).catch(() => []),
		serverApiRequest<ServiceTemplate[]>(fetch, '/service-templates', { token }).catch(() => []),
		serverApiRequest<TicketChecklistItem[]>(fetch, `/service-tickets/${id}/checklist`, { token }).catch(() => []),
		serverApiRequest<CustomerLocation[]>(fetch, '/locations', { token }).catch(() => []),
		serverApiRequest<Device[]>(fetch, '/service-assets', { token }).catch(() => [])
	]);

	return {
		ticket,
		history,
		approvals,
		workItems,
		payment,
		inventoryItems,
		inventoryUsages,
		staff,
		serviceTemplates,
		checklistItems,
		locations,
		allAssets,
		apiBaseUrl: env.PUBLIC_API_BASE_URL ?? 'http://localhost:5051/api/v1'
	};
};

export const actions: Actions = {
	update: async ({ request, fetch, cookies, params }) => {
		const form = await request.formData();
		const scheduledStart = nullable(form.get('scheduledVisitStart'));
		const scheduledEnd = nullable(form.get('scheduledVisitEnd'));
		const slaDue = nullable(form.get('slaDueAt'));
		const siteLoc = nullable(form.get('siteLocationId'));
		const warrantyDate = nullable(form.get('warrantyUntil'));
		const createdDate = nullable(form.get('createdAt'));

		if (warrantyDate && !isValidYmd(warrantyDate)) {
			return fail(400, { ok: false, success: false, action: 'update', messageKey: 'tickets.create.invalidDate', variant: 'error' });
		}
		if (createdDate && !isValidYmd(createdDate)) {
			return fail(400, { ok: false, success: false, action: 'update', messageKey: 'tickets.create.invalidDate', variant: 'error' });
		}

		const scheduledVisitStart = parseOptionalIsoDate(scheduledStart);
		if (scheduledStart && !scheduledVisitStart) {
			return fail(400, { ok: false, success: false, action: 'update', messageKey: 'tickets.create.invalidDate', variant: 'error' });
		}
		const scheduledVisitEnd = parseOptionalIsoDate(scheduledEnd);
		if (scheduledEnd && !scheduledVisitEnd) {
			return fail(400, { ok: false, success: false, action: 'update', messageKey: 'tickets.create.invalidDate', variant: 'error' });
		}
		const slaDueAt = parseOptionalIsoDate(slaDue);
		if (slaDue && !slaDueAt) {
			return fail(400, { ok: false, success: false, action: 'update', messageKey: 'tickets.create.invalidDate', variant: 'error' });
		}

		return submitJson(fetch, cookies.get('servora_session'), `/service-tickets/${params.id}`, 'PUT', {
			complaint: String(form.get('complaint') ?? ''),
			initialDiagnosis: nullable(form.get('initialDiagnosis')),
			physicalConditionNotes: nullable(form.get('physicalConditionNotes')),
			accessorySummary: nullable(form.get('accessorySummary')),
			photoNotes: nullable(form.get('photoNotes')),
			securityPasscode: nullable(form.get('securityPasscode')),
			securityPattern: nullable(form.get('securityPattern')),
			preRepairChecklist: nullable(form.get('preRepairChecklist')),
			diagnosis: nullable(form.get('diagnosis')),
			repairNotes: nullable(form.get('repairNotes')),
			warrantyUntil: warrantyDate ? `${warrantyDate}T23:59:59Z` : null,
			warrantyTerms: nullable(form.get('warrantyTerms')),
			priority: nullable(form.get('priority')),
			createdAt: createdDate ? `${createdDate}T00:00:00Z` : null,
			updatedAt: nullable(form.get('updatedAt')),
			serviceLocationType: nullable(form.get('serviceLocationType')),
			siteLocationId: siteLoc ? Number(siteLoc) : null,
			clearSiteLocationId: form.has('serviceLocationType') && !siteLoc,
			siteAddressSnapshot: nullable(form.get('siteAddressSnapshot')),
			scheduledVisitStart,
			scheduledVisitEnd,
			clearFieldServiceSchedule: form.has('serviceLocationType') && !scheduledStart && !scheduledEnd,
			slaDueAt,
			clearSlaDueAt: form.has('serviceLocationType') && !slaDue,
			departmentCode: form.has('departmentCode') ? String(form.get('departmentCode') ?? '') : null,
			costCenter: form.has('costCenter') ? String(form.get('costCenter') ?? '') : null
		}, { ok: true, action: 'update', messageKey: 'tickets.detail.actionSaved' });
	},
	updateCustomFields: async ({ request, fetch, cookies, params }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const assetId = Number(form.get('assetId') || 0);
		const assetValues = extractFieldInputs(form, 'assetField_');
		const ticketValues = extractFieldInputs(form, 'ticketField_');
		try {
			if (assetId > 0 && form.has('hasAssetCustomFields')) {
				await serverApiRequest(fetch, `/service-assets/${assetId}/custom-fields`, {
					method: 'PUT',
					token,
					body: { values: assetValues }
				});
			}
			if (form.has('hasTicketCustomFields')) {
				await serverApiRequest(fetch, `/service-tickets/${params.id}/custom-fields`, {
					method: 'PUT',
					token,
					body: { values: ticketValues }
				});
			}
			return { success: true, ok: true, action: 'updateCustomFields', messageKey: 'tickets.detail.savedSuccess' };
		} catch (error) {
			if (error instanceof ServerApiError) {
				return fail(error.status, { ok: false, success: false, action: 'updateCustomFields', message: error.message, variant: 'error' });
			}
			throw error;
		}
	},
	updateInspections: async ({ request, fetch, cookies, params }) => {
		const form = await request.formData();
		let items: unknown[] = [];
		const raw = nullable(form.get('inspectionsJson'));
		if (raw) {
			try {
				const parsed = JSON.parse(raw);
				if (Array.isArray(parsed)) items = parsed;
			} catch {
				items = [];
			}
		}
		return submitJson(fetch, cookies.get('servora_session'), `/service-tickets/${params.id}/inspections`, 'PUT', { items }, {
			ok: true,
			action: 'updateInspections',
			messageKey: 'tickets.detail.savedSuccess'
		});
	},
	linkAsset: async ({ request, fetch, cookies, params }) => {
		const form = await request.formData();
		const assetId = Number(form.get('assetId'));
		if (!Number.isFinite(assetId) || assetId <= 0) {
			return fail(400, { success: false, messageKey: 'tickets.create.deviceRequired', action: 'linkAsset', variant: 'error' });
		}
		return submitJson(fetch, cookies.get('servora_session'), `/service-tickets/${params.id}/assets`, 'POST', {
			assetId,
			notes: nullable(form.get('notes'))
		}, { ok: true, action: 'linkAsset', messageKey: 'tickets.detail.savedSuccess' });
	},
	unlinkAsset: async ({ request, fetch, cookies, params }) => {
		const form = await request.formData();
		const assetId = Number(form.get('assetId'));
		return submitJson(fetch, cookies.get('servora_session'), `/service-tickets/${params.id}/assets/${assetId}`, 'DELETE', {}, {
			ok: true,
			action: 'unlinkAsset',
			messageKey: 'tickets.detail.savedSuccess'
		});
	},
	changeStatus: async ({ request, fetch, cookies, params }) => {
		const form = await request.formData();
		const statusId = Number(form.get('statusId'));
		if (!Number.isFinite(statusId) || statusId <= 0) return fail(400, { success: false, messageKey: 'tickets.detail.statusRequired', action: 'status', variant: 'error' });
		return submitJson(fetch, cookies.get('servora_session'), `/service-tickets/${params.id}/change-status`, 'POST', {
			statusId,
			reason: nullable(form.get('reason')),
			overridePaymentBlock: form.get('overridePaymentBlock') === 'on'
		}, { ok: true, action: 'status', messageKey: 'tickets.detail.statusUpdated' });
	},
	assign: async ({ request, fetch, cookies, params }) => {
		const form = await request.formData();
		const value = nullable(form.get('technicianUserId'));
		return submitJson(fetch, cookies.get('servora_session'), `/service-tickets/${params.id}/assign`, 'POST', {
			technicianUserId: value ? Number(value) : null
		}, { ok: true, action: 'assign', messageKey: 'tickets.detail.assignmentUpdated' });
	},
	addNote: async ({ request, fetch, cookies, params }) => {
		const form = await request.formData();
		const note = String(form.get('note') ?? '').trim();
		if (!note) return fail(400, { success: false, messageKey: 'tickets.detail.noteRequired', action: 'note', variant: 'error' });
		return submitJson(fetch, cookies.get('servora_session'), `/service-tickets/${params.id}/notes`, 'POST', {
			note,
			isPublicVisible: form.get('isPublicVisible') === 'on'
		}, { ok: true, action: 'note', messageKey: 'tickets.detail.noteAdded' });
	},
	addAccessory: async ({ request, fetch, cookies, params }) => {
		const form = await request.formData();
		const name = String(form.get('name') ?? '').trim();
		const quantity = Number(form.get('quantity') || 1);
		if (!name) return fail(400, { success: false, messageKey: 'tickets.accessories.nameRequired', action: 'accessory', variant: 'error' });
		if (!Number.isFinite(quantity) || quantity <= 0) return fail(400, { success: false, messageKey: 'tickets.accessories.quantityInvalid', action: 'accessory', variant: 'error' });
		return submitJson(fetch, cookies.get('servora_session'), `/service-tickets/${params.id}/accessories`, 'POST', {
			name,
			quantity,
			notes: nullable(form.get('notes'))
		}, { ok: true, action: 'accessory', messageKey: 'tickets.accessories.added' });
	},
	deleteAccessory: async ({ request, fetch, cookies, params }) => {
		const form = await request.formData();
		return submitJson(fetch, cookies.get('servora_session'), `/service-tickets/${params.id}/accessories/${form.get('accessoryId')}`, 'DELETE', {}, { ok: true, action: 'deleteAccessory', messageKey: 'tickets.accessories.deleted' });
	},
	uploadAttachment: async ({ request, fetch, cookies, params }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const body = new FormData();
		const file = form.get('file');
		if (!(file instanceof File) || file.size === 0) return fail(400, { success: false, messageKey: 'tickets.attachments.fileRequired', action: 'uploadAttachment', variant: 'error' });
		if (file.size > 5 * 1024 * 1024) return fail(400, { success: false, messageKey: 'tickets.attachments.fileTooLarge', action: 'uploadAttachment', variant: 'error' });
		const mime = resolveSupportedImageMime(file);
		if (!mime) return fail(400, { success: false, messageKey: 'tickets.attachments.fileTypeInvalid', action: 'uploadAttachment', variant: 'error' });
		const uploadFile = file.type.toLowerCase() === mime
			? file
			: new File([file], file.name, { type: mime, lastModified: file.lastModified });
		body.set('file', uploadFile);
		body.set('attachmentType', String(form.get('attachmentType') ?? 'Other'));
		body.set('isPublicVisible', form.get('isPublicVisible') === 'on' ? 'true' : 'false');

		try {
			await serverApiRequest(fetch, `/service-tickets/${params.id}/attachments`, {
				method: 'POST',
				token,
				body
			});
			return { success: true, ok: true, action: 'uploadAttachment', messageKey: 'tickets.attachments.uploaded' };
		} catch (error) {
			if (error instanceof ServerApiError) {
				return fail(error.status, { success: false, message: error.message, action: 'uploadAttachment', variant: 'error' });
			}
			return fail(503, { success: false, messageKey: 'tickets.detail.apiUnavailable', action: 'uploadAttachment', variant: 'error' });
		}
	},
	deleteAttachment: async ({ request, fetch, cookies, params }) => {
		const form = await request.formData();
		return submitJson(fetch, cookies.get('servora_session'), `/service-tickets/${params.id}/attachments/${form.get('attachmentId')}`, 'DELETE', {}, { ok: true, action: 'deleteAttachment', messageKey: 'tickets.attachments.deleted' });
	},
	toggleAttachmentVisibility: async ({ request, fetch, cookies, params }) => {
		const form = await request.formData();
		const isPublicVisible = form.get('isPublicVisible') === 'true';
		return submitJson(
			fetch,
			cookies.get('servora_session'),
			`/service-tickets/${params.id}/attachments/${form.get('attachmentId')}/visibility`,
			'PUT',
			{ isPublicVisible },
			{ ok: true, action: 'toggleAttachmentVisibility', messageKey: 'tickets.attachments.visibilityUpdated' }
		);
	},
	createApproval: async ({ request, fetch, cookies, params }) => {
		const form = await request.formData();
		const title = String(form.get('title') ?? '').trim();
		const description = String(form.get('description') ?? '').trim();
		const parsedEstimatedAmount = parseOptionalAmount(form.get('estimatedAmount'));
		const proposedRaw = nullable(form.get('proposedWorkItemsJson'));
		if (!title) return fail(400, { success: false, messageKey: 'tickets.detail.approvalTitleRequired', action: 'approval', variant: 'error' });
		if (!description) return fail(400, { success: false, messageKey: 'tickets.detail.approvalDescriptionRequired', action: 'approval', variant: 'error' });
		if (parsedEstimatedAmount !== null && (!Number.isFinite(parsedEstimatedAmount) || parsedEstimatedAmount < 0)) return fail(400, { success: false, messageKey: 'tickets.detail.estimatedAmountInvalid', action: 'approval', variant: 'error' });

		let proposedWorkItems: Array<{ itemType: WorkItemType; title: string; description: string | null; quantity: number; unitPrice: number }> | undefined;
		if (proposedRaw) {
			try {
				const parsed = JSON.parse(proposedRaw);
				if (!Array.isArray(parsed)) {
					return fail(400, { success: false, messageKey: 'tickets.workItems.invalidProposedItems', action: 'approval', variant: 'error' });
				}
				proposedWorkItems = [];
				for (const item of parsed) {
					const itemType = String(item?.itemType ?? 'Service') as WorkItemType;
					const itemTitle = String(item?.title ?? '').trim();
					const itemDescription = typeof item?.description === 'string' && item.description.trim() ? item.description.trim() : null;
					const quantity = Number(item?.quantity);
					const unitPrice = item?.unitPrice === '' || item?.unitPrice === null || item?.unitPrice === undefined ? 0 : Number(item.unitPrice);
					if (!['Service', 'NonStockPart', 'Fee'].includes(itemType) || !itemTitle || !Number.isFinite(quantity) || quantity <= 0 || !Number.isFinite(unitPrice) || unitPrice < 0) {
						return fail(400, { success: false, messageKey: 'tickets.workItems.invalidProposedItems', action: 'approval', variant: 'error' });
					}
					proposedWorkItems.push({ itemType, title: itemTitle, description: itemDescription, quantity, unitPrice });
				}
			} catch {
				return fail(400, { success: false, messageKey: 'tickets.workItems.invalidProposedItems', action: 'approval', variant: 'error' });
			}
		}

		return submitJson(fetch, cookies.get('servora_session'), `/service-tickets/${params.id}/approvals`, 'POST', {
			title,
			description,
			estimatedAmount: parsedEstimatedAmount,
			proposedWorkItems: proposedWorkItems && proposedWorkItems.length > 0 ? proposedWorkItems : undefined
		}, { ok: true, action: 'approval', messageKey: 'tickets.detail.approvalCreated' });
	},
	approveApproval: async ({ request, fetch, cookies, params }) => {
		const form = await request.formData();
		return submitJson(fetch, cookies.get('servora_session'), `/service-tickets/${params.id}/approvals/${form.get('approvalId')}/approve`, 'POST', {
			note: nullable(form.get('note'))
		}, { ok: true, action: 'approvalResolution', messageKey: 'tickets.detail.approvalApproved' });
	},
	rejectApproval: async ({ request, fetch, cookies, params }) => {
		const form = await request.formData();
		return submitJson(fetch, cookies.get('servora_session'), `/service-tickets/${params.id}/approvals/${form.get('approvalId')}/reject`, 'POST', {
			note: nullable(form.get('note'))
		}, { ok: true, action: 'approvalResolution', messageKey: 'tickets.detail.approvalRejected' });
	},
	createWorkItem: async ({ request, fetch, cookies, params }) => {
		const form = await request.formData();
		const itemType = String(form.get('itemType') ?? 'Service');
		const title = String(form.get('title') ?? '').trim();
		const description = nullable(form.get('description'));
		const quantity = parseOptionalAmount(form.get('quantity'));
		const unitPrice = parseOptionalAmount(form.get('unitPrice')) ?? 0;
		const approvalRaw = nullable(form.get('approvalRequestId'));
		const approvalRequestId = approvalRaw ? Number(approvalRaw) : null;
		const isCompleted = form.get('isCompleted') === 'on' || form.get('isCompleted') === 'true';

		if (!['Service', 'NonStockPart', 'Fee'].includes(itemType)) {
			return fail(400, { success: false, messageKey: 'tickets.workItems.typeInvalid', action: 'workItem', variant: 'error' });
		}
		if (!title) {
			return fail(400, { success: false, messageKey: 'tickets.workItems.titleRequired', action: 'workItem', variant: 'error' });
		}
		if (quantity === null || !Number.isFinite(quantity) || quantity <= 0) {
			return fail(400, { success: false, messageKey: 'tickets.workItems.quantityInvalid', action: 'workItem', variant: 'error' });
		}
		if (!Number.isFinite(unitPrice) || unitPrice < 0) {
			return fail(400, { success: false, messageKey: 'tickets.workItems.unitPriceInvalid', action: 'workItem', variant: 'error' });
		}

		return submitJson(fetch, cookies.get('servora_session'), `/service-tickets/${params.id}/work-items`, 'POST', {
			itemType,
			title,
			description,
			quantity,
			unitPrice,
			isCompleted,
			approvalRequestId: approvalRequestId && Number.isFinite(approvalRequestId) && approvalRequestId > 0 ? approvalRequestId : null
		}, { ok: true, action: 'workItem', messageKey: 'tickets.workItems.created' });
	},
	updateWorkItem: async ({ request, fetch, cookies, params }) => {
		const form = await request.formData();
		const workItemId = Number(form.get('workItemId'));
		const itemType = String(form.get('itemType') ?? 'Service');
		const title = String(form.get('title') ?? '').trim();
		const description = nullable(form.get('description'));
		const quantity = parseOptionalAmount(form.get('quantity'));
		const unitPrice = parseOptionalAmount(form.get('unitPrice')) ?? 0;
		const isCompleted = form.get('isCompleted') === 'on' || form.get('isCompleted') === 'true';

		if (!Number.isFinite(workItemId) || workItemId <= 0) {
			return fail(400, { success: false, messageKey: 'tickets.detail.error', action: 'workItemUpdate', variant: 'error' });
		}
		if (!['Service', 'NonStockPart', 'Fee'].includes(itemType)) {
			return fail(400, { success: false, messageKey: 'tickets.workItems.typeInvalid', action: 'workItemUpdate', variant: 'error' });
		}
		if (!title) {
			return fail(400, { success: false, messageKey: 'tickets.workItems.titleRequired', action: 'workItemUpdate', variant: 'error' });
		}
		if (quantity === null || !Number.isFinite(quantity) || quantity <= 0) {
			return fail(400, { success: false, messageKey: 'tickets.workItems.quantityInvalid', action: 'workItemUpdate', variant: 'error' });
		}
		if (!Number.isFinite(unitPrice) || unitPrice < 0) {
			return fail(400, { success: false, messageKey: 'tickets.workItems.unitPriceInvalid', action: 'workItemUpdate', variant: 'error' });
		}

		return submitJson(fetch, cookies.get('servora_session'), `/service-tickets/${params.id}/work-items/${workItemId}`, 'PUT', {
			itemType,
			title,
			description,
			quantity,
			unitPrice,
			isCompleted
		}, { ok: true, action: 'workItemUpdate', messageKey: 'tickets.workItems.updated' });
	},
	deleteWorkItem: async ({ request, fetch, cookies, params }) => {
		const form = await request.formData();
		const workItemId = Number(form.get('workItemId'));
		if (!Number.isFinite(workItemId) || workItemId <= 0) {
			return fail(400, { success: false, messageKey: 'tickets.detail.error', action: 'workItemDelete', variant: 'error' });
		}
		return submitJson(fetch, cookies.get('servora_session'), `/service-tickets/${params.id}/work-items/${workItemId}`, 'DELETE', {}, {
			ok: true,
			action: 'workItemDelete',
			messageKey: 'tickets.workItems.deleted'
		});
	},
	recordPaymentTransaction: async ({ request, fetch, cookies, params }) => {
		const form = await request.formData();
		const entryType = String(form.get('entryType') ?? 'Payment');
		const amount = parseOptionalAmount(form.get('amount'));
		if (!['Payment', 'Refund'].includes(entryType)) {
			return fail(400, { success: false, messageKey: 'tickets.paymentLedger.entryTypeInvalid', action: 'paymentTransaction', variant: 'error' });
		}
		if (amount === null || !Number.isFinite(amount) || amount <= 0) {
			return fail(400, { success: false, messageKey: 'tickets.paymentLedger.amountInvalid', action: 'paymentTransaction', variant: 'error' });
		}
		return submitJson(fetch, cookies.get('servora_session'), `/service-tickets/${params.id}/payment/transactions`, 'POST', {
			entryType,
			amount,
			method: nullable(form.get('method')),
			referenceNumber: nullable(form.get('referenceNumber')),
			notes: nullable(form.get('notes'))
		}, {
			ok: true,
			action: entryType === 'Refund' ? 'paymentRefund' : 'paymentTransaction',
			messageKey: entryType === 'Refund' ? 'tickets.paymentLedger.refundRecorded' : 'tickets.paymentLedger.paymentRecorded'
		});
	},
	voidPaymentTransaction: async ({ request, fetch, cookies, params }) => {
		const form = await request.formData();
		const transactionId = Number(form.get('transactionId'));
		const reason = String(form.get('reason') ?? '').trim();
		if (!Number.isFinite(transactionId) || transactionId <= 0) {
			return fail(400, { success: false, messageKey: 'tickets.detail.error', action: 'paymentVoid', variant: 'error' });
		}
		if (!reason) {
			return fail(400, { success: false, messageKey: 'tickets.paymentLedger.voidReasonRequired', action: 'paymentVoid', variant: 'error' });
		}
		return submitJson(fetch, cookies.get('servora_session'), `/service-tickets/${params.id}/payment/transactions/${transactionId}/void`, 'POST', {
			reason
		}, { ok: true, action: 'paymentVoid', messageKey: 'tickets.paymentLedger.voidedSuccess' });
	},
	updateBilling: async ({ request, fetch, cookies, params }) => {
		const form = await request.formData();
		const discountAmount = parseOptionalAmount(form.get('discountAmount')) ?? 0;
		const manualTotalRaw = parseOptionalAmount(form.get('manualTotalAmount'));
		const wasOverridden = form.get('wasOverridden') === 'true';
		const clearRequested = form.get('clearManualOverride') === 'true';
		const isWaived = form.get('isWaived') === 'on';
		const notes = nullable(form.get('notes'));

		if (!Number.isFinite(discountAmount) || discountAmount < 0) {
			return fail(400, { success: false, messageKey: 'tickets.billing.discountInvalid', action: 'billing', variant: 'error' });
		}
		if (!clearRequested && manualTotalRaw !== null && (!Number.isFinite(manualTotalRaw) || manualTotalRaw < 0)) {
			return fail(400, { success: false, messageKey: 'tickets.detail.totalAmountInvalid', action: 'billing', variant: 'error' });
		}

		const clearManualOverride = clearRequested || (wasOverridden && manualTotalRaw === null);
		const manualTotalAmount = clearManualOverride ? null : manualTotalRaw;

		return submitJson(fetch, cookies.get('servora_session'), `/service-tickets/${params.id}/billing`, 'PUT', {
			discountAmount,
			isWaived,
			manualTotalAmount,
			clearManualOverride,
			notes
		}, { ok: true, action: 'billing', messageKey: 'tickets.billing.updated' });
	},
	updatePayment: async ({ request, fetch, cookies, params }) => {
		const form = await request.formData();
		const status = String(form.get('status') ?? '');
		const amountPaid = parseAmount(form.get('amountPaid'), 0);
		const parsedTotalAmount = parseOptionalAmount(form.get('totalAmount'));
		if (!['Unpaid', 'Partial', 'Paid', 'Waived'].includes(status)) return fail(400, { success: false, messageKey: 'tickets.detail.paymentStatusRequired', action: 'payment', variant: 'error' });
		if (!Number.isFinite(amountPaid) || amountPaid < 0) return fail(400, { success: false, messageKey: 'tickets.detail.amountPaidInvalid', action: 'payment', variant: 'error' });
		if (parsedTotalAmount !== null && (!Number.isFinite(parsedTotalAmount) || parsedTotalAmount < 0)) return fail(400, { success: false, messageKey: 'tickets.detail.totalAmountInvalid', action: 'payment', variant: 'error' });
		return submitJson(fetch, cookies.get('servora_session'), `/service-tickets/${params.id}/payment`, 'PUT', {
			status,
			amountPaid,
			totalAmount: parsedTotalAmount,
			method: nullable(form.get('method')),
			referenceNumber: nullable(form.get('referenceNumber')),
			notes: nullable(form.get('notes'))
		}, { ok: true, action: 'payment', messageKey: 'tickets.detail.paymentUpdated' });
	},
	pickupOverride: async ({ request, fetch, cookies, params }) => {
		const form = await request.formData();
		const reason = String(form.get('reason') ?? '').trim();
		if (!reason) return fail(400, { success: false, messageKey: 'tickets.detail.overrideReasonRequired', action: 'pickupOverride', variant: 'error' });
		return submitJson(fetch, cookies.get('servora_session'), `/service-tickets/${params.id}/pickup-override`, 'POST', {
			reason
		}, { ok: true, action: 'pickupOverride', messageKey: 'tickets.detail.pickupOverrideApplied' });
	},
	applyTemplate: async ({ request, fetch, cookies, params }) => {
		const form = await request.formData();
		const templateId = Number(form.get('templateId'));
		if (!Number.isFinite(templateId) || templateId <= 0) return fail(400, { success: false, messageKey: 'tickets.checklist.templateRequired', action: 'checklist', variant: 'error' });
		return submitJson(fetch, cookies.get('servora_session'), `/service-tickets/${params.id}/apply-template`, 'POST', {
			templateId
		}, { ok: true, action: 'checklist', messageKey: 'tickets.checklist.templateApplied' });
	},
	updateChecklistItem: async ({ request, fetch, cookies, params }) => {
		const form = await request.formData();
		return submitJson(fetch, cookies.get('servora_session'), `/service-tickets/${params.id}/checklist/${form.get('checklistItemId')}`, 'PUT', {
			isCompleted: form.get('isCompleted') === 'on'
		}, { ok: true, action: 'checklist', messageKey: 'tickets.checklist.updated' });
	},
	reserveInventory: async ({ request, fetch, cookies, params }) => {
		const form = await request.formData();
		const inventoryItemId = Number(form.get('inventoryItemId'));
		const quantity = Number(form.get('quantity') || 0);
		if (!Number.isFinite(inventoryItemId) || inventoryItemId <= 0) return fail(400, { success: false, messageKey: 'tickets.inventory.itemRequired', action: 'inventory', variant: 'error' });
		if (!Number.isFinite(quantity) || quantity <= 0) return fail(400, { success: false, messageKey: 'tickets.inventory.quantityInvalid', action: 'inventory', variant: 'error' });
		return submitJson(fetch, cookies.get('servora_session'), `/service-tickets/${params.id}/inventory/reserve`, 'POST', {
			inventoryItemId,
			quantity,
			notes: nullable(form.get('notes'))
		}, { ok: true, action: 'inventory', messageKey: 'tickets.inventory.reserved' });
	},
	consumeInventory: async ({ request, fetch, cookies, params }) => inventoryAction(request, fetch, cookies.get('servora_session'), params.id, 'consume'),
	releaseInventory: async ({ request, fetch, cookies, params }) => inventoryAction(request, fetch, cookies.get('servora_session'), params.id, 'release'),
	returnInventory: async ({ request, fetch, cookies, params }) => inventoryAction(request, fetch, cookies.get('servora_session'), params.id, 'return'),
	generateWhatsApp: async ({ request, fetch, cookies, params, url }) => {
		const form = await request.formData();

		try {
			const preview = await serverApiRequest<GeneratedWhatsAppMessage>(fetch, `/service-tickets/${params.id}/whatsapp-message`, {
				method: 'POST',
				token: cookies.get('servora_session'),
				body: {
					templateKey: String(form.get('templateKey') ?? 'ticket_created'),
					locale: String(form.get('locale') ?? 'en'),
					baseUrl: url.origin
				}
			});
			return { whatsapp: preview };
		} catch (error) {
			if (error instanceof ServerApiError) return fail(error.status, { success: false, message: error.message });
			throw error;
		}
	}
};

async function inventoryAction(request: Request, fetcher: typeof fetch, token: string | undefined, ticketId: string | undefined, action: 'consume' | 'release' | 'return') {
	const form = await request.formData();
	const quantity = nullable(form.get('quantity'));
	if (quantity && (!Number.isFinite(Number(quantity)) || Number(quantity) <= 0)) return fail(400, { success: false, messageKey: 'tickets.inventory.quantityInvalid', action: 'inventory', variant: 'error' });
	const messageKey = action === 'consume' ? 'tickets.inventory.consumedMessage' : action === 'release' ? 'tickets.inventory.releasedMessage' : 'tickets.inventory.returnedMessage';
	return submitJson(fetcher, token, `/service-tickets/${ticketId}/inventory/${form.get('usageId')}/${action}`, 'POST', {
		quantity: quantity ? Number(quantity) : null,
		reason: nullable(form.get('reason'))
	}, { ok: true, action: 'inventory', messageKey });
}

function isValidYmd(value: string) {
	if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) return false;
	const [y, m, d] = value.split('-').map(Number);
	const parsed = new Date(Date.UTC(y, m - 1, d));
	return parsed.getUTCFullYear() === y && parsed.getUTCMonth() === m - 1 && parsed.getUTCDate() === d;
}

function parseOptionalIsoDate(value: string | null) {
	if (!value) return null;
	const parsed = new Date(value);
	return Number.isNaN(parsed.getTime()) ? null : parsed.toISOString();
}

async function submitJson(fetcher: typeof fetch, token: string | undefined, path: string, method: 'POST' | 'PUT' | 'DELETE', body: unknown, success: Record<string, unknown> = {}) {
	try {
		await serverApiRequest(fetcher, path, { method, token, body });
		return { success: true, ...success };
	} catch (error) {
		if (error instanceof ServerApiError) {
			return fail(error.status, {
				...success,
				ok: false,
				success: false,
				message: error.message,
				messageKey: error.status === 409 && success.action === 'update' ? 'tickets.detail.conflict' : undefined,
				variant: error.status === 409 ? 'warning' : 'error'
			});
		}
		return fail(500, {
			...success,
			ok: false,
			success: false,
			message: error instanceof Error ? error.message : 'Request failed.',
			variant: 'error'
		});
	}
}

function nullable(value: FormDataEntryValue | null) {
	const text = String(value ?? '').trim();
	return text.length > 0 ? text : null;
}

function extractFieldInputs(form: FormData, prefix: string) {
	const values: { fieldDefinitionId: number; value: string | null }[] = [];
	for (const [key, val] of form.entries()) {
		if (!key.startsWith(prefix)) continue;
		const id = Number(key.slice(prefix.length));
		if (Number.isInteger(id) && id > 0) {
			values.push({ fieldDefinitionId: id, value: nullable(val) });
		}
	}
	return values;
}

function parseAmount(value: FormDataEntryValue | null, fallback = 0): number {
	const raw = nullable(value);
	if (!raw) return fallback;
	const clean = String(raw).replace(/[^\d.-]/g, '');
	const parsed = Number(clean);
	return Number.isFinite(parsed) ? parsed : fallback;
}

function parseOptionalAmount(value: FormDataEntryValue | null): number | null {
	const raw = nullable(value);
	if (!raw) return null;
	const clean = String(raw).replace(/[^\d.-]/g, '');
	const parsed = Number(clean);
	return Number.isFinite(parsed) ? parsed : null;
}
