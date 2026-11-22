<script lang="ts">
	import { enhance } from '$app/forms';
	import { beforeNavigate, goto } from '$app/navigation';
	import { onMount, tick, untrack } from 'svelte';
	import Breadcrumbs from '$lib/components/Breadcrumbs.svelte';
	import Dropdown from '$lib/components/ui/dropdown/Dropdown.svelte';
	import DeleteConfirmButton from '$lib/components/DeleteConfirmButton.svelte';
	import { toast } from '$lib/stores/toast.svelte';
	import type { ActionData, PageData } from './$types';
	import type { PaymentTransaction, TicketWorkItem, WorkItemType } from '$lib/types/service-tickets';
	import { createTranslator, formatCurrency, formatDate, resolveFeatureConfig, resolveTerminology } from '$lib/i18n';
	import DatePicker from '$lib/components/ui/date-picker/DatePicker.svelte';
	import GlowingBadge from '$lib/components/ui/glowing-badge/GlowingBadge.svelte';
	import PatternLock from '$lib/components/PatternLock.svelte';
	import CurrencyInput from '$lib/components/CurrencyInput.svelte';
	import DynamicFieldRenderer from '$lib/components/DynamicFieldRenderer.svelte';
	import InspectionChecklistRenderer, { type InspectionEntry } from '$lib/components/InspectionChecklistRenderer.svelte';
	import { compressImage, isSupportedImageFile } from '$lib/media';
	import { formatEnhanceUnexpectedError } from '$lib/utils/http';

	let { data, form }: { data: PageData; form: ActionData } = $props();
	const t = $derived(createTranslator(data.locale));
	const terms = $derived(resolveTerminology(data.user?.workspace?.terminology, data.locale));
	const features = $derived(
		resolveFeatureConfig(
			data.user?.workspace?.featureConfig ?? data.blueprintConfig?.featureConfig,
			data.user?.workspace?.operatingMode
		)
	);
	const detailAssetFields = $derived((data.blueprintConfig?.assetFields ?? []).filter((f) => f.showOnDetail));
	const detailTicketFields = $derived((data.blueprintConfig?.ticketFields ?? []).filter((f) => f.showOnDetail));
	const inspectionTemplates = $derived(data.blueprintConfig?.inspectionChecklists ?? []);

	type TicketWorkspaceTab = 'overview' | 'diagnosis' | 'photos' | 'parts' | 'history';

	// Workspace active tab state
	let activeTab = $state<TicketWorkspaceTab>('overview');
	let highlightedSectionId = $state<string | null>(null);

	async function openTab(tab: TicketWorkspaceTab, sectionId: string = '') {
		activeTab = tab;
		await tick();
		const targetId = sectionId || 'ticket-workspace-panel';
		const target = document.getElementById(targetId);
		if (!target) return;
		if (sectionId) {
			target.scrollIntoView({ behavior: 'smooth', block: 'start' });
			highlightedSectionId = sectionId;
			setTimeout(() => {
				if (highlightedSectionId === sectionId) highlightedSectionId = null;
			}, 1600);
		} else {
			const rect = target.getBoundingClientRect();
			if (rect.top < 84) {
				target.scrollIntoView({ behavior: 'smooth', block: 'start' });
			}
		}
	}

	// ponytail: sanitize and localize backend eventType strings
	function formatEventType(type: string) {
		const key = 'history.event.' + type;
		const translated = t(key);
		if (translated !== key) return translated;
		return type.replace(/([A-Z])/g, ' $1').trim();
	}

	function labelFor(prefix: string, value: string) {
		const key = `${prefix}.${value}`;
		const translated = t(key);
		return translated === key ? value : translated;
	}

	function toDateTimeLocal(value: string | null | undefined) {
		if (!value) return '';
		const d = new Date(value);
		if (Number.isNaN(d.getTime())) return '';
		const pad = (n: number) => String(n).padStart(2, '0');
		return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
	}

	function makeDetailDraft(current: PageData['ticket']) {
		return {
			complaint: current.complaint,
			priority: current.priority,
			initialDiagnosis: current.initialDiagnosis ?? '',
			createdAt: current.createdAt?.slice(0, 10) ?? '',
			physicalConditionNotes: current.physicalConditionNotes ?? '',
			accessorySummary: current.accessorySummary ?? '',
			photoNotes: current.photoNotes ?? '',
			securityPasscode: current.securityPasscode ?? '',
			securityPattern: current.securityPattern ?? '',
			preRepairChecklist: current.preRepairChecklist ?? '',
			diagnosis: current.diagnosis ?? '',
			repairNotes: current.repairNotes ?? '',
			warrantyUntil: current.warrantyUntil?.slice(0, 10) ?? '',
			warrantyTerms: current.warrantyTerms ?? '',
			serviceLocationType: (current.serviceLocationType ?? 'InShop') as 'InShop' | 'OnSite' | 'PickupDelivery',
			siteLocationId: current.siteLocationId ? String(current.siteLocationId) : '',
			siteAddressSnapshot: current.siteAddressSnapshot ?? '',
			scheduledVisitStart: toDateTimeLocal(current.scheduledVisitStart),
			scheduledVisitEnd: toDateTimeLocal(current.scheduledVisitEnd),
			slaDueAt: toDateTimeLocal(current.slaDueAt),
			departmentCode: current.departmentCode ?? '',
			costCenter: current.costCenter ?? ''
		};
	}

	type DraftWorkItem = {
		itemType: WorkItemType;
		title: string;
		description: string;
		quantity: string;
		unitPrice: string;
		isCompleted: boolean;
	};

	type ProposedWorkItemDraft = {
		id: number;
		itemType: WorkItemType;
		title: string;
		description: string;
		quantity: string;
		unitPrice: string;
	};

	function makeEmptyWorkItemDraft(): DraftWorkItem {
		return { itemType: 'Service', title: '', description: '', quantity: '1', unitPrice: '', isCompleted: false };
	}

	function makeBillingDraft(current: PageData['payment']) {
		return {
			discountAmount: String(current.discountAmount ?? 0),
			manualTotalAmount: current.isTotalManuallyOverridden && current.totalAmount != null ? String(current.totalAmount) : '',
			isWaived: current.status === 'Waived',
			notes: current.notes ?? ''
		};
	}

	function buildPaymentStateKey(current: PageData['payment']) {
		return JSON.stringify({
			status: current.status,
			amountPaid: current.amountPaid,
			totalAmount: current.totalAmount,
			calculatedTotalAmount: current.calculatedTotalAmount,
			discountAmount: current.discountAmount,
			isTotalManuallyOverridden: current.isTotalManuallyOverridden,
			notes: current.notes,
			lastUpdatedAt: current.lastUpdatedAt,
			txCount: current.transactions?.length ?? 0,
			voidedCount: (current.transactions ?? []).filter((tx) => tx.isVoided).length
		});
	}

	function formatFileSize(bytes: number) {
		const value = bytes >= 1024 * 1024 ? bytes / 1024 / 1024 : bytes / 1024;
		const unit = bytes >= 1024 * 1024 ? 'MB' : 'KB';
		return `${new Intl.NumberFormat(data.locale, { maximumFractionDigits: 1 }).format(value)} ${unit}`;
	}

	const draftKey = (draft: ReturnType<typeof makeDetailDraft>) => JSON.stringify(draft);

	const ticket = $derived(data.ticket);
	const showBilling = $derived(features.showBillingAndPayments && !ticket.isInternalWorkOrder);
	const showApprovals = $derived(features.showCustomerApprovals && !ticket.isInternalWorkOrder);
	const availableLinkAssets = $derived(
		(data.allAssets ?? []).filter(
			(a) => a.id !== ticket.deviceId && !(ticket.linkedAssets ?? []).some((l) => l.serviceAssetId === a.id)
		)
	);
	let linkAssetId = $state('');
	let linkAssetNotes = $state('');
	let assetFieldValues = $state<Record<number, string>>(
		untrack(() => Object.fromEntries((data.ticket.assetCustomFields ?? []).map((f) => [f.fieldDefinitionId, f.value ?? ''])))
	);
	let ticketFieldValues = $state<Record<number, string>>(
		untrack(() => Object.fromEntries((data.ticket.customFields ?? []).map((f) => [f.fieldDefinitionId, f.value ?? ''])))
	);
	let inspectionEntries = $state<InspectionEntry[]>([]);
	const imageAttachments = $derived(ticket.attachments.filter(attachment => attachment.mimeType.startsWith('image/')));
	const activeServiceTemplates = $derived((data.serviceTemplates ?? []).filter(template => template.isActive));
	const checklistDone = $derived((data.checklistItems ?? []).filter(item => item.isCompleted).length);
	const whatsapp = $derived(form?.whatsapp);
	const currencyCode = $derived(data.shopProfile?.currencyCode ?? 'IDR');
	const allowCurrencyDecimals = $derived(currencyCode !== 'IDR');
	const currencySymbol = $derived(
		currencyCode === 'IDR'
			? 'Rp'
			: currencyCode === 'MYR'
				? 'RM'
				: currencyCode === 'SGD'
					? 'S$'
					: currencyCode === 'EUR'
						? '€'
						: currencyCode === 'GBP'
							? '£'
							: currencyCode === 'AUD'
								? 'A$'
								: '$'
	);
	const workItems = $derived(data.workItems ?? []);
	const paymentSummary = $derived(data.payment);
	const paymentTransactions = $derived(data.payment.transactions ?? []);
	const paymentLinksData = $derived(data.paymentLinks);
	const paymentLinksList = $derived(paymentLinksData?.links ?? []);
	const activePendingLink = $derived(
		paymentLinksList.find((item) => item.status === 'Pending' && new Date(item.expiresAt).getTime() > Date.now()) ?? null
	);
	const pastPaymentLinks = $derived(
		paymentLinksList.filter((item) => !activePendingLink || item.id !== activePendingLink.id)
	);
	let paymentLinkAmount = $state('');
	let showPastPaymentLinks = $state(false);
	let showPaymentQrModal = $state(false);
	const activePaymentQrUrl = $derived(
		activePendingLink?.paymentUrl
			? `https://api.qrserver.com/v1/create-qr-code/?size=240x240&data=${encodeURIComponent(activePendingLink.paymentUrl)}`
			: ''
	);

	async function copyPaymentLinkUrl(url: string) {
		if (!url || !navigator.clipboard) return;
		await navigator.clipboard.writeText(url);
		toast.success(t('tickets.paymentLinks.copied'));
	}
	const formResult = $derived((form as ({ action?: string; message?: string; messageKey?: string; ok?: boolean; success?: boolean; variant?: 'success' | 'error' | 'warning' | 'info' } & Record<string, unknown>) | undefined));
	const formAction = $derived(formResult?.action);

	// svelte-ignore state_referenced_locally
	let detailDraft = $state(makeDetailDraft(data.ticket));
	// svelte-ignore state_referenced_locally
	let savedDetailDraft = $state(makeDetailDraft(data.ticket));
	let showDiscardConfirm = $state(false);
	let showLeaveConfirm = $state(false);
	let pendingNavigationUrl = $state('');
	let allowNavigation = false;

	// svelte-ignore state_referenced_locally
	let billingDraft = $state(makeBillingDraft(data.payment));
	// svelte-ignore state_referenced_locally
	let loadedPaymentKey = $state(buildPaymentStateKey(data.payment));
	let showBillingAdjustments = $state(false);
	let txDraft = $state<{
		entryType: 'Payment' | 'Refund';
		amount: string;
		method: string;
		referenceNumber: string;
		notes: string;
	}>({
		entryType: 'Payment',
		amount: '',
		method: '',
		referenceNumber: '',
		notes: ''
	});
	let voidDialog: HTMLDialogElement;
	let voidTargetTx = $state<PaymentTransaction | null>(null);
	let voidReason = $state('');

	let workItemDraft = $state<DraftWorkItem>(makeEmptyWorkItemDraft());
	let editingWorkItemId = $state<number | null>(null);
	let editWorkItemDraft = $state<DraftWorkItem>(makeEmptyWorkItemDraft());

	let statusDraft = $state({ statusId: '', reason: '', overridePaymentBlock: false });
	let accessoryDraft = $state({ name: '', quantity: '1', notes: '' });
	let accessoryNameInput = $state<HTMLInputElement | null>(null);
	let noteDraft = $state({ note: '', isPublicVisible: false });
	let approvalDraft = $state({ title: '', description: '', estimatedAmount: '' });
	let showProposedItemsBuilder = $state(false);
	let proposedWorkItems = $state<ProposedWorkItemDraft[]>([]);
	let nextProposedItemKey = 1;
	let pickupOverrideReason = $state('');
	let reserveDraft = $state({ inventoryItemId: '', quantity: '', notes: '' });
	let uploadPending = $state(false);
	let pendingAction = $state<string | null>(null);
	let failedAttachmentIds = $state<number[]>([]);
	let photoDialog: HTMLDialogElement;
	let selectedAttachmentId = $state<number | null>(null);
	let activeNoticeId = $state<string | undefined>();
	let revealPasscode = $state(false);
	let copiedPasscode = $state(false);

	interface ParsedInspectionItem {
		id: string;
		labelKey: string;
		status: 'pass' | 'fail' | 'untested' | 'na';
		notes: string;
	}

	const parsedChecklist = $derived.by<ParsedInspectionItem[]>(() => {
		if (!ticket.preRepairChecklist) return [];
		try {
			const parsed = JSON.parse(ticket.preRepairChecklist);
			return Array.isArray(parsed) ? parsed : [];
		} catch {
			return [];
		}
	});

	function copyPasscode(text: string) {
		navigator.clipboard.writeText(text);
		copiedPasscode = true;
		setTimeout(() => (copiedPasscode = false), 2000);
	}

	// svelte-ignore state_referenced_locally
	let loadedTicketKey = $state(`${data.ticket.id}|${data.ticket.updatedAt ?? data.ticket.createdAt}`);
	const detailDirty = $derived(draftKey(detailDraft) !== draftKey(savedDetailDraft));
	const selectedTransition = $derived(ticket.allowedTransitions.find(tr => tr.statusId.toString() === statusDraft.statusId));
	const hasPendingApproval = $derived(Boolean(ticket.hasPendingApproval) || data.approvals.some((approval) => approval.status === 'Pending'));
	const computedConsumedPartsSubtotal = $derived(
		data.inventoryUsages.reduce(
			(sum, usage) => sum + Math.max(0, usage.consumedQuantity - usage.returnedQuantity) * (usage.unitPrice ?? 0),
			0
		)
	);
	const consumedPartsSubtotal = $derived(
		paymentSummary.consumedPartsSubtotal ?? ticket.consumedPartsSubtotal ?? computedConsumedPartsSubtotal
	);
	const approvedWorkItemsSubtotal = $derived(
		paymentSummary.workItemsSubtotal ??
			ticket.workItemsSubtotal ??
			workItems.filter((item) => item.status === 'Approved').reduce((sum, item) => sum + item.lineTotal, 0)
	);
	const pendingWorkItemsSubtotal = $derived(
		workItems.filter((item) => item.status === 'PendingApproval').reduce((sum, item) => sum + item.lineTotal, 0)
	);
	const activeWorkItems = $derived(
		workItems.filter((item) => item.status === 'Approved' || item.status === 'PendingApproval')
	);
	const completedWorkItemsCount = $derived(activeWorkItems.filter((item) => item.isCompleted).length);
	const remainingBalance = $derived(
		paymentSummary.remainingBalance ?? Math.max(0, (paymentSummary.totalAmount ?? 0) - (paymentSummary.amountPaid ?? 0))
	);
	const overpaidAmount = $derived(
		paymentSummary.overpaidAmount ??
			(paymentSummary.totalAmount != null ? Math.max(0, (paymentSummary.amountPaid ?? 0) - paymentSummary.totalAmount) : 0)
	);
	const methodPresets = $derived([
		{ value: 'Cash', label: t('tickets.paymentLedger.methodCash') },
		{ value: 'QRIS', label: t('tickets.paymentLedger.methodQris') },
		{ value: 'Transfer', label: t('tickets.paymentLedger.methodTransfer') },
		{ value: 'Card (Credit/Debit)', label: t('tickets.paymentLedger.methodCard') }
	]);
	const formulaTotalBill = $derived(
		paymentSummary.calculatedTotalAmount ??
			Math.max(0, approvedWorkItemsSubtotal + consumedPartsSubtotal - (paymentSummary.discountAmount ?? 0))
	);
	const hasUnitemizedInitialTotal = $derived(
		(paymentSummary.totalAmount ?? 0) > 0 &&
			approvedWorkItemsSubtotal === 0 &&
			consumedPartsSubtotal === 0 &&
			!paymentSummary.isTotalManuallyOverridden &&
			formulaTotalBill !== paymentSummary.totalAmount
	);
	const hasActiveBillingAdjustments = $derived(
		(paymentSummary.discountAmount ?? 0) > 0 ||
			Boolean(paymentSummary.isTotalManuallyOverridden) ||
			paymentSummary.status === 'Waived'
	);
	const isAddingWorkItem = $derived(pendingAction === 'createWorkItem');
	const isCreatingApproval = $derived(pendingAction === 'createApproval');
	const isRecalculatingBilling = $derived(pendingAction === 'recalculateBilling');
	const isRecordingTransaction = $derived(pendingAction === 'recordPaymentTransaction');
	const isSavingBilling = $derived(pendingAction === 'updateBilling');
	const isCreatingPaymentLink = $derived(pendingAction === 'createPaymentLink');
	const isSyncingPaymentLink = $derived(pendingAction === 'syncPaymentLink');
	const isCancellingPaymentLink = $derived(pendingAction === 'cancelPaymentLink');
	const liveFormulaTotal = $derived(
		Math.max(0, approvedWorkItemsSubtotal + consumedPartsSubtotal - Math.max(0, Number(billingDraft.discountAmount || 0)))
	);
	const proposedWorkItemsTotal = $derived(
		proposedWorkItems.reduce(
			(sum, item) => sum + Math.max(0, Number(item.quantity || 0)) * Math.max(0, Number(item.unitPrice || 0)),
			0
		)
	);
	const serializedProposedWorkItems = $derived(
		JSON.stringify(
			proposedWorkItems
				.filter((item) => item.title.trim().length > 0)
				.map((item) => ({
					itemType: item.itemType,
					title: item.title.trim(),
					description: item.description.trim() || null,
					quantity: Number(item.quantity || 1),
					unitPrice: Number(item.unitPrice || 0)
				}))
		)
	);
	const workItemAddLineTotal = $derived(
		Math.max(0, Number(workItemDraft.quantity || 0)) * Math.max(0, Number(workItemDraft.unitPrice || 0))
	);
	const editWorkItemLineTotal = $derived(
		Math.max(0, Number(editWorkItemDraft.quantity || 0)) * Math.max(0, Number(editWorkItemDraft.unitPrice || 0))
	);
	const selectedAttachment = $derived(imageAttachments.find(attachment => attachment.id === selectedAttachmentId) ?? imageAttachments[0]);

	function startEditingWorkItem(item: TicketWorkItem) {
		editingWorkItemId = item.id;
		editWorkItemDraft = {
			itemType: (item.itemType as WorkItemType) || 'Service',
			title: item.title,
			description: item.description ?? '',
			quantity: String(item.quantity),
			unitPrice: String(item.unitPrice),
			isCompleted: item.isCompleted
		};
	}

	function addProposedWorkItem() {
		showProposedItemsBuilder = true;
		proposedWorkItems = [
			...proposedWorkItems,
			{ id: nextProposedItemKey++, itemType: 'Service', title: '', description: '', quantity: '1', unitPrice: '' }
		];
	}

	function removeProposedWorkItem(localId: number) {
		proposedWorkItems = proposedWorkItems.filter((item) => item.id !== localId);
	}

	function openVoidModal(tx: PaymentTransaction) {
		voidTargetTx = tx;
		voidReason = '';
		voidDialog?.showModal();
	}

	function closeVoidModal() {
		voidDialog?.close();
		voidTargetTx = null;
		voidReason = '';
	}

	function handleVoidDialogClick(event: MouseEvent) {
		if (!voidDialog || pendingAction === 'voidPaymentTransaction') return;
		const rect = voidDialog.getBoundingClientRect();
		const isInDialog =
			rect.top <= event.clientY &&
			event.clientY <= rect.top + rect.height &&
			rect.left <= event.clientX &&
			event.clientX <= rect.left + rect.width;
		if (!isInDialog) {
			closeVoidModal();
		}
	}

	function handleVoidDialogCancel(event: Event) {
		if (pendingAction === 'voidPaymentTransaction') {
			event.preventDefault();
			return;
		}
		closeVoidModal();
	}

	function workItemsForApproval(approvalId: number) {
		return workItems.filter((item) => item.approvalRequestId === approvalId);
	}

	function canAddChargeFromApproval(approval: PageData['approvals'][number]) {
		return (
			approval.status === 'Approved' &&
			approval.estimatedAmount != null &&
			approval.estimatedAmount > 0 &&
			!workItems.some((item) => item.approvalRequestId === approval.id && item.status !== 'Cancelled')
		);
	}

	const historyGroups = $derived(
		Object.values(
			data.history.reduce<Record<string, { item: (typeof data.history)[number]; count: number }>>((groups, item) => {
				const text = item.note ?? item.reason ?? [item.fromStatus, item.toStatus].filter(Boolean).join(' → ');
				const key = `${item.eventType}|${text}`;
				groups[key] ??= { item, count: 0 };
				groups[key].count += 1;
				return groups;
			}, {})
		)
	);

	function enhanceAction(actionKey: string) {
		return () => {
			pendingAction = actionKey;
			showPendingNotice(t('tickets.detail.actionPending'));
			return async ({ result, update }: { result: { type: string; error?: unknown }; update: (options?: { reset?: boolean; invalidateAll?: boolean }) => Promise<void> }) => {
				try {
					if (result.type === 'error') {
						toast.error(formatEnhanceUnexpectedError(result.error, t));
						return;
					}
					await update({ reset: false, invalidateAll: true });
				} finally {
					if (pendingAction === actionKey) pendingAction = null;
					clearPendingNotice();
				}
			};
		};
	}

	const preserveTicketDetailForm = enhanceAction('generic');

	const saveTicketDetailForm = preserveTicketDetailForm;
	const addAccessoryForm = preserveTicketDetailForm;
	let photoFileInput = $state<HTMLInputElement | null>(null);

	const uploadAttachmentForm = async ({ formData, cancel }: { formData: FormData; cancel: () => void }) => {
		const file = formData.get('file');
		if (file instanceof File && file.size > 0) {
			if (!isSupportedImageFile(file)) {
				toast.error(t('tickets.attachments.fileTypeInvalid'));
				cancel();
				return;
			}
			const compressed = await compressImage(file);
			if (compressed.size > 5 * 1024 * 1024) {
				toast.error(t('tickets.attachments.fileTooLarge'));
				cancel();
				return;
			}
			formData.set('file', compressed);
		}
		uploadPending = true;
		showPendingNotice(t('tickets.attachments.uploading'));
		return async ({ result, update }: { result: { type: string; error?: unknown }; update: (options?: { reset?: boolean; invalidateAll?: boolean }) => Promise<void> }) => {
			try {
				if (result.type === 'error') {
					toast.error(formatEnhanceUnexpectedError(result.error, t));
					return;
				}
				if (result.type === 'success') {
					if (photoFileInput) photoFileInput.value = '';
					await update({ reset: true, invalidateAll: true });
				} else {
					await update({ reset: false, invalidateAll: true });
				}
			} finally {
				uploadPending = false;
				clearPendingNotice();
			}
		};
	};

	function showPendingNotice(message: string) {
		if (activeNoticeId) toast.dismiss(activeNoticeId);
		activeNoticeId = toast.show(message, 'info', { duration: 0 });
	}

	function clearPendingNotice() {
		if (activeNoticeId) {
			toast.dismiss(activeNoticeId);
			activeNoticeId = undefined;
		}
	}

	function discardDetailChanges() {
		const next = makeDetailDraft(data.ticket);
		detailDraft = next;
		savedDetailDraft = next;
		showDiscardConfirm = false;
	}

	function stayOnTicket() {
		showLeaveConfirm = false;
		pendingNavigationUrl = '';
	}

	function leaveTicket() {
		const href = pendingNavigationUrl;
		allowNavigation = true;
		showLeaveConfirm = false;
		pendingNavigationUrl = '';
		if (href) void goto(href);
	}

	function markAttachmentPreviewFailed(id: number) {
		if (!failedAttachmentIds.includes(id)) failedAttachmentIds = [...failedAttachmentIds, id];
	}

	function openPhotoViewer(id: number) {
		selectedAttachmentId = id;
		photoDialog.showModal();
	}

	function movePhoto(step: number) {
		const index = imageAttachments.findIndex(attachment => attachment.id === selectedAttachment?.id);
		if (index < 0 || imageAttachments.length === 0) return;
		selectedAttachmentId = imageAttachments[(index + step + imageAttachments.length) % imageAttachments.length].id;
	}

	beforeNavigate((navigation) => {
		if (allowNavigation || !detailDirty || navigation.willUnload) return;
		const url = navigation.to?.url;
		if (!url) return;
		navigation.cancel();
		pendingNavigationUrl = `${url.pathname}${url.search}${url.hash}`;
		showLeaveConfirm = true;
	});

	onMount(() => {
		const warnBeforeUnload = (event: BeforeUnloadEvent) => {
			if (!detailDirty) return;
			event.preventDefault();
			event.returnValue = '';
		};
		window.addEventListener('beforeunload', warnBeforeUnload);
		return () => window.removeEventListener('beforeunload', warnBeforeUnload);
	});

	$effect(() => {
		const nextTicketKey = `${data.ticket.id}|${data.ticket.updatedAt ?? data.ticket.createdAt}`;
		if (nextTicketKey === loadedTicketKey) return;
		if (!detailDirty || (form?.success && formAction === 'update')) {
			const next = makeDetailDraft(data.ticket);
			detailDraft = next;
			savedDetailDraft = next;
			assetFieldValues = Object.fromEntries((data.ticket.assetCustomFields ?? []).map((f) => [f.fieldDefinitionId, f.value ?? '']));
			ticketFieldValues = Object.fromEntries((data.ticket.customFields ?? []).map((f) => [f.fieldDefinitionId, f.value ?? '']));
			loadedTicketKey = nextTicketKey;
			showDiscardConfirm = false;
		}
	});

	$effect(() => {
		const nextPaymentKey = buildPaymentStateKey(data.payment);
		if (nextPaymentKey === loadedPaymentKey) return;
		billingDraft = makeBillingDraft(data.payment);
		loadedPaymentKey = nextPaymentKey;
	});

	let lastHandledSuccessForm: unknown = null;
	$effect(() => {
		if (!form?.success || form === lastHandledSuccessForm) return;
		lastHandledSuccessForm = form;

		if (formAction === 'status') statusDraft = { statusId: '', reason: '', overridePaymentBlock: false };
		if (formAction === 'accessory') {
			accessoryDraft = { name: '', quantity: '1', notes: '' };
			setTimeout(() => accessoryNameInput?.focus(), 0);
		}
		if (formAction === 'note') noteDraft = { note: '', isPublicVisible: false };
		if (formAction === 'approval') {
			approvalDraft = { title: '', description: '', estimatedAmount: '' };
			proposedWorkItems = [];
			showProposedItemsBuilder = false;
		}
		if (formAction === 'workItem') {
			workItemDraft = makeEmptyWorkItemDraft();
		}
		if (formAction === 'workItemUpdate') {
			editingWorkItemId = null;
		}
		if (formAction === 'paymentTransaction' || formAction === 'paymentRefund') {
			txDraft = { entryType: 'Payment', amount: '', method: '', referenceNumber: '', notes: '' };
		}
		if (formAction === 'paymentVoid') {
			closeVoidModal();
		}
		if (formAction === 'billing') {
			billingDraft = makeBillingDraft(data.payment);
		}
		if (formAction === 'paymentLinkCreate') {
			paymentLinkAmount = '';
		}
		if (formAction === 'pickupOverride') pickupOverrideReason = '';
		if (formAction === 'inventory') reserveDraft = { inventoryItemId: '', quantity: '', notes: '' };
		if (formAction === 'linkAsset') {
			linkAssetId = '';
			linkAssetNotes = '';
		}
	});
</script>

<div class="space-y-6 pb-16">
	<!-- Top Ticket Summary & Header Bar -->
	<header class="bg-white border border-stone rounded-xl p-5 sm:p-6 shadow-2xs space-y-4">
		<div class="flex flex-col sm:flex-row sm:items-start sm:justify-between gap-4">
			<div class="min-w-0 space-y-1.5">
				<Breadcrumbs items={[{ label: t('common.console'), href: '/app' }, { label: terms.ticketPlural, href: '/app/tickets' }, { label: ticket.ticketNumber }]} />
				<div class="flex flex-wrap items-center gap-2.5 pt-1">
					<h1 class="text-headline-sm font-bold text-primary font-mono tracking-tight">{ticket.ticketNumber}</h1>
					<GlowingBadge status={ticket.status}>{ticket.status}</GlowingBadge>
					{#if showBilling}
						<GlowingBadge status={ticket.paymentStatus}>{labelFor('tickets.paymentStatus', ticket.paymentStatus)}</GlowingBadge>
					{:else if ticket.isInternalWorkOrder}
						<span class="px-2.5 py-0.5 rounded-full bg-indigo-50 text-indigo-700 border border-indigo-200 text-caption font-semibold">
							{t('workspaces.mode.InternalMaintenance')}
						</span>
					{/if}
					{#if hasPendingApproval && showApprovals}
						<span class="px-2.5 py-0.5 rounded-full bg-amber-50 text-amber-800 border border-amber-200 text-caption font-semibold">
							{t('tickets.detail.pendingApprovalBadge')}
						</span>
					{/if}
					{#if showBilling && overpaidAmount > 0}
						<span class="px-2.5 py-0.5 rounded-full bg-rose-50 text-rose-700 border border-rose-200 text-caption font-semibold font-mono">
							{t('tickets.detail.refundDueBadge')}: {formatCurrency(overpaidAmount, data.locale, currencyCode)}
						</span>
					{/if}
					{#if features.showWarrantySection && ticket.warrantyActive}
						<span class="px-2.5 py-0.5 rounded-full bg-emerald-50 text-emerald-700 border border-emerald-200 text-caption font-semibold uppercase tracking-wide">
							{t('tickets.detail.warrantyActive')}
						</span>
					{/if}
				</div>
				<div class="flex flex-wrap items-center gap-x-4 gap-y-1 text-body-sm text-secondary pt-0.5">
					<a href={`/app/customers/${ticket.customerId}`} class="font-semibold text-primary hover:underline inline-flex items-center gap-1">
						<span>{ticket.customerName}</span>
						{#if ticket.customerPhone}
							<span class="text-caption text-secondary">({ticket.customerPhone})</span>
						{/if}
					</a>
					<span>·</span>
					<span class="font-medium text-primary">{ticket.device}</span>
					<span>·</span>
					<span class="text-caption">{formatDate(ticket.createdAt, data.locale, { dateStyle: 'medium', timeStyle: 'short' })}</span>
				</div>
			</div>

			<div class="flex items-center gap-2.5 shrink-0">
				{#if detailDirty}
					<span class="px-2.5 py-1 rounded-full bg-amber-50 border border-amber-200 text-amber-800 text-caption font-semibold">
						{t('tickets.detail.unsavedChanges')}
					</span>
				{/if}
				<a
					class="px-button-x py-button-y rounded-button border border-stone bg-white text-primary font-medium hover:bg-ash active:scale-95 transition-all text-body-sm shadow-2xs inline-flex items-center gap-1.5"
					href={`/app/tickets/${ticket.id}/receipt`}
					target="_blank"
				>
					<span class="material-symbols-outlined text-[18px] text-secondary">print</span>
					<span>{t('receipt.printTicket')}</span>
				</a>
			</div>
		</div>

		<!-- Workspace Segmented Navigation Bar -->
		<div class="border-t border-stone/70 pt-3 flex items-center justify-between overflow-x-auto scrollbar-none">
			<div class="inline-flex rounded-input border border-stone/80 bg-ash/40 p-1 text-caption font-medium shadow-2xs" role="tablist">
				<button
					type="button"
					role="tab"
					aria-selected={activeTab === 'overview'}
					class="px-3.5 py-1.5 rounded-button transition-all cursor-pointer inline-flex items-center gap-1.5 {activeTab === 'overview' ? 'bg-white text-ink font-semibold shadow-xs' : 'text-secondary hover:text-ink'}"
					onclick={() => openTab('overview')}
				>
					<span class="material-symbols-outlined text-[16px]">info</span>
					<span>{t('tickets.detail.tabOverview')}</span>
				</button>
				<button
					type="button"
					role="tab"
					aria-selected={activeTab === 'diagnosis'}
					class="px-3.5 py-1.5 rounded-button transition-all cursor-pointer inline-flex items-center gap-1.5 {activeTab === 'diagnosis' ? 'bg-white text-ink font-semibold shadow-xs' : 'text-secondary hover:text-ink'}"
					onclick={() => openTab('diagnosis')}
				>
					<span class="material-symbols-outlined text-[16px]">build</span>
					<span>{t('tickets.detail.tabDiagnosis')} ({workItems.length})</span>
				</button>
				<button
					type="button"
					role="tab"
					aria-selected={activeTab === 'photos'}
					class="px-3.5 py-1.5 rounded-button transition-all cursor-pointer inline-flex items-center gap-1.5 {activeTab === 'photos' ? 'bg-white text-ink font-semibold shadow-xs' : 'text-secondary hover:text-ink'}"
					onclick={() => openTab('photos')}
				>
					<span class="material-symbols-outlined text-[16px]">photo_camera</span>
					<span>{t('tickets.detail.tabPhotos')} ({imageAttachments.length})</span>
				</button>
				<button
					type="button"
					role="tab"
					aria-selected={activeTab === 'parts'}
					class="px-3.5 py-1.5 rounded-button transition-all cursor-pointer inline-flex items-center gap-1.5 {activeTab === 'parts' ? 'bg-white text-ink font-semibold shadow-xs' : 'text-secondary hover:text-ink'}"
					onclick={() => openTab('parts')}
				>
					<span class="material-symbols-outlined text-[16px]">inventory_2</span>
					<span>{t('tickets.detail.tabParts')} ({data.inventoryUsages.length + data.checklistItems.length})</span>
				</button>
				<button
					type="button"
					role="tab"
					aria-selected={activeTab === 'history'}
					class="px-3.5 py-1.5 rounded-button transition-all cursor-pointer inline-flex items-center gap-1.5 {activeTab === 'history' ? 'bg-white text-ink font-semibold shadow-xs' : 'text-secondary hover:text-ink'}"
					onclick={() => openTab('history')}
				>
					<span class="material-symbols-outlined text-[16px]">history</span>
					<span>{t('tickets.detail.tabHistory')} ({data.history.length})</span>
				</button>
			</div>
		</div>
	</header>

	{#if showLeaveConfirm}
		<div class="fixed inset-0 z-[60] flex items-center justify-center bg-black/50 px-md py-6" role="presentation">
			<div class="w-[min(480px,calc(100vw-2rem))] rounded-xl border border-stone bg-white p-lg shadow-2xl space-y-md" role="dialog" aria-modal="true" aria-labelledby="leave-ticket-title">
				<h3 id="leave-ticket-title" class="font-subheading text-subheading font-semibold text-primary">{t('tickets.detail.leaveConfirmTitle')}</h3>
				<p class="text-body-sm text-secondary">{t('tickets.detail.leaveConfirmMessage')}</p>
				<div class="flex flex-wrap justify-end gap-sm pt-sm border-t border-stone">
					<button class="px-button-x py-button-y rounded-button border border-stone bg-white text-primary font-medium hover:bg-ash cursor-pointer" type="button" onclick={stayOnTicket}>{t('tickets.detail.stayOnTicket')}</button>
					<button class="px-button-x py-button-y rounded-button bg-amber-600 text-white font-medium hover:bg-amber-700 cursor-pointer shadow-xs" type="button" onclick={leaveTicket}>{t('tickets.detail.leaveAnyway')}</button>
				</div>
			</div>
		</div>
	{/if}

	<!-- 2-Column Main Workspace -->
	<div class="grid grid-cols-1 xl:grid-cols-[1fr_360px] gap-6 items-start">
		<!-- Left Main Pane (Tabbed Workspace Content) -->
		<div id="ticket-workspace-panel" class="space-y-6 scroll-mt-24">
			<!-- TAB 1: OVERVIEW & INTAKE -->
			{#if activeTab === 'overview'}
				<form method="POST" action="?/update" class="space-y-6" use:enhance={saveTicketDetailForm}>
					<input type="hidden" name="updatedAt" value={data.ticket.updatedAt ?? ''} />
					<input type="hidden" name="accessorySummary" bind:value={detailDraft.accessorySummary} />
					<input type="hidden" name="preRepairChecklist" bind:value={detailDraft.preRepairChecklist} />
					<input type="hidden" name="diagnosis" bind:value={detailDraft.diagnosis} />
					<input type="hidden" name="repairNotes" bind:value={detailDraft.repairNotes} />
					<input type="hidden" name="warrantyUntil" bind:value={detailDraft.warrantyUntil} />
					<input type="hidden" name="warrantyTerms" bind:value={detailDraft.warrantyTerms} />

					<!-- Card: Issue & Complaint -->
					<div class="bg-white border border-stone rounded-xl p-5 sm:p-6 shadow-2xs space-y-4">
						<div class="border-b border-stone/70 pb-3">
							<h2 class="text-subheading font-semibold text-primary">{t('tickets.detail.complaint')} & {t('tickets.detail.initialDiagnosis')}</h2>
							<p class="text-caption text-secondary mt-0.5">{t('tickets.create.intakeDetailsHint')}</p>
						</div>

						<label class="space-y-1.5 block">
							<span class="text-label-caps font-label-caps text-secondary uppercase">{t('tickets.detail.complaint')} *</span>
							<textarea
								name="complaint"
								maxlength="2000"
								rows="3"
								class="w-full border border-stone rounded-input p-3 text-body-sm bg-white focus:outline-none focus:border-ink focus:ring-1 focus:ring-ink/10 transition-colors"
								bind:value={detailDraft.complaint}
								required
							></textarea>
						</label>

						<div class="grid grid-cols-1 md:grid-cols-2 gap-4">
							<label class="space-y-1.5 block">
								<span class="text-label-caps font-label-caps text-secondary uppercase">{t('tickets.detail.initialDiagnosis')}</span>
								<input
									name="initialDiagnosis"
									maxlength="2000"
									class="w-full border border-stone rounded-input px-3.5 py-2 text-body-sm bg-white focus:outline-none focus:border-ink focus:ring-1 focus:ring-ink/10 transition-colors"
									bind:value={detailDraft.initialDiagnosis}
								/>
							</label>
							<div class="space-y-1.5">
								<span class="text-label-caps font-label-caps text-secondary uppercase">{t('tickets.detail.priority')}</span>
								<Dropdown
									name="priority"
									bind:value={detailDraft.priority}
									options={['Low','Normal','High','Urgent'].map(p => ({ value: p, label: labelFor('tickets.priority', p) }))}
								/>
							</div>
						</div>
					</div>

					{#if features.enableFieldService || data.ticket.serviceLocationType !== 'InShop' || data.ticket.isInternalWorkOrder || data.ticket.slaDueAt || data.ticket.scheduledVisitStart}
						<div class="bg-white border border-stone rounded-xl p-5 sm:p-6 shadow-2xs space-y-4">
							<div class="border-b border-stone/70 pb-3 flex flex-wrap items-center justify-between gap-2">
								<h2 class="text-subheading font-semibold text-primary">{t('tickets.detail.fieldServiceCardTitle')}</h2>
								<div class="flex items-center gap-2">
									{#if data.ticket.isInternalWorkOrder}
										<span class="rounded-full bg-indigo-50 border border-indigo-200 px-2.5 py-0.5 text-[11px] font-semibold text-indigo-700">
											{t('workspaces.mode.InternalMaintenance')}
										</span>
									{/if}
									<span class="rounded-full bg-ash border border-stone px-2.5 py-0.5 text-[11px] font-semibold text-ink">
										{detailDraft.serviceLocationType === 'OnSite'
											? t('fieldService.onSite')
											: detailDraft.serviceLocationType === 'PickupDelivery'
												? t('fieldService.pickupDelivery')
												: t('fieldService.inShop')}
									</span>
								</div>
							</div>

							<div class="grid grid-cols-1 md:grid-cols-2 gap-4">
								<label class="space-y-1.5 block">
									<span class="text-label-caps font-label-caps text-secondary uppercase">{t('fieldService.locationType')}</span>
									<select
										name="serviceLocationType"
										bind:value={detailDraft.serviceLocationType}
										class="w-full border border-stone rounded-input px-3 py-2 text-body-sm bg-white focus:outline-none focus:border-ink"
									>
										<option value="InShop">{t('fieldService.inShop')}</option>
										<option value="OnSite">{t('fieldService.onSite')}</option>
										<option value="PickupDelivery">{t('fieldService.pickupDelivery')}</option>
									</select>
								</label>

								<label class="space-y-1.5 block">
									<span class="text-label-caps font-label-caps text-secondary uppercase">{t('fieldService.slaDueAt')}</span>
									<input
										type="datetime-local"
										name="slaDueAt"
										bind:value={detailDraft.slaDueAt}
										class="w-full border border-stone rounded-input px-3 py-2 text-body-sm bg-white focus:outline-none focus:border-ink"
									/>
								</label>

								{#if detailDraft.serviceLocationType !== 'InShop'}
									<label class="space-y-1.5 block">
										<span class="text-label-caps font-label-caps text-secondary uppercase">{t('fieldService.siteLocation')}</span>
										<select
											name="siteLocationId"
											bind:value={detailDraft.siteLocationId}
											class="w-full border border-stone rounded-input px-3 py-2 text-body-sm bg-white focus:outline-none focus:border-ink"
										>
											<option value="">—</option>
											{#each (data.locations ?? []).filter((loc) => !loc.customerId || loc.customerId === ticket.customerId) as loc (loc.id)}
												<option value={String(loc.id)}>{loc.label} — {loc.address}</option>
											{/each}
										</select>
									</label>
									<label class="space-y-1.5 block">
										<span class="text-label-caps font-label-caps text-secondary uppercase">{t('fieldService.customSiteAddress')}</span>
										<input
											type="text"
											name="siteAddressSnapshot"
											bind:value={detailDraft.siteAddressSnapshot}
											class="w-full border border-stone rounded-input px-3 py-2 text-body-sm bg-white focus:outline-none focus:border-ink"
										/>
									</label>
									<label class="space-y-1.5 block">
										<span class="text-label-caps font-label-caps text-secondary uppercase">{t('fieldService.scheduledStart')}</span>
										<input
											type="datetime-local"
											name="scheduledVisitStart"
											bind:value={detailDraft.scheduledVisitStart}
											class="w-full border border-stone rounded-input px-3 py-2 text-body-sm bg-white focus:outline-none focus:border-ink"
										/>
									</label>
									<label class="space-y-1.5 block">
										<span class="text-label-caps font-label-caps text-secondary uppercase">{t('fieldService.scheduledEnd')}</span>
										<input
											type="datetime-local"
											name="scheduledVisitEnd"
											bind:value={detailDraft.scheduledVisitEnd}
											class="w-full border border-stone rounded-input px-3 py-2 text-body-sm bg-white focus:outline-none focus:border-ink"
										/>
									</label>
								{:else}
									<input type="hidden" name="siteLocationId" value={detailDraft.siteLocationId} />
									<input type="hidden" name="siteAddressSnapshot" value={detailDraft.siteAddressSnapshot} />
									<input type="hidden" name="scheduledVisitStart" value={detailDraft.scheduledVisitStart} />
									<input type="hidden" name="scheduledVisitEnd" value={detailDraft.scheduledVisitEnd} />
								{/if}

								{#if data.ticket.isInternalWorkOrder || data.user?.workspace?.operatingMode !== 'Commercial'}
									<label class="space-y-1.5 block">
										<span class="text-label-caps font-label-caps text-secondary uppercase">{t('fieldService.departmentCode')}</span>
										<input
											type="text"
											name="departmentCode"
											bind:value={detailDraft.departmentCode}
											class="w-full border border-stone rounded-input px-3 py-2 text-body-sm bg-white focus:outline-none focus:border-ink"
										/>
									</label>
									<label class="space-y-1.5 block">
										<span class="text-label-caps font-label-caps text-secondary uppercase">{t('fieldService.costCenter')}</span>
										<input
											type="text"
											name="costCenter"
											bind:value={detailDraft.costCenter}
											class="w-full border border-stone rounded-input px-3 py-2 text-body-sm bg-white focus:outline-none focus:border-ink"
										/>
									</label>
								{/if}
							</div>
						</div>
					{:else}
						<input type="hidden" name="serviceLocationType" value={detailDraft.serviceLocationType} />
						<input type="hidden" name="siteLocationId" value={detailDraft.siteLocationId} />
						<input type="hidden" name="siteAddressSnapshot" value={detailDraft.siteAddressSnapshot} />
						<input type="hidden" name="scheduledVisitStart" value={detailDraft.scheduledVisitStart} />
						<input type="hidden" name="scheduledVisitEnd" value={detailDraft.scheduledVisitEnd} />
						<input type="hidden" name="slaDueAt" value={detailDraft.slaDueAt} />
					{/if}

					<!-- Card: Physical Custody & Intake Conditions -->
					<div class="bg-white border border-stone rounded-xl p-5 sm:p-6 shadow-2xs space-y-4">
						<div class="border-b border-stone/70 pb-3">
							<h2 class="text-subheading font-semibold text-primary">{t('tickets.create.intakeDetails')}</h2>
							<p class="text-caption text-secondary mt-0.5">{t('tickets.create.intakeDetailsHint')}</p>
						</div>

						<div class="grid grid-cols-1 md:grid-cols-2 gap-4">
							<div class="space-y-1.5">
								<span class="text-label-caps font-label-caps text-secondary uppercase">{t('tickets.detail.intakeDate')}</span>
								<DatePicker name="createdAt" bind:value={detailDraft.createdAt} />
							</div>
							<label class="space-y-1.5 block">
								<span class="text-label-caps font-label-caps text-secondary uppercase">{t('tickets.detail.photoNotes')}</span>
								<input
									name="photoNotes"
									class="w-full border border-stone rounded-input px-3.5 py-2 text-body-sm bg-white focus:outline-none focus:border-ink focus:ring-1 focus:ring-ink/10 transition-colors"
									bind:value={detailDraft.photoNotes}
								/>
							</label>
						</div>

						{#if features.showPhysicalConditionNotes}
							<label class="space-y-1.5 block">
								<span class="text-label-caps font-label-caps text-secondary uppercase">{t('tickets.detail.physicalConditionNotes')}</span>
								<textarea
									name="physicalConditionNotes"
									rows="2"
									class="w-full border border-stone rounded-input p-3 text-body-sm bg-white focus:outline-none focus:border-ink focus:ring-1 focus:ring-ink/10 transition-colors"
									bind:value={detailDraft.physicalConditionNotes}
								></textarea>
							</label>
						{:else}
							<input type="hidden" name="physicalConditionNotes" value={detailDraft.physicalConditionNotes} />
						{/if}
					</div>

					<!-- Card: Access & Unlock Credentials (Blueprint Configurable) -->
					{#if features.showSecurityCredentials && features.securityCredentialMode !== 'None'}
						<div class="bg-white border border-stone rounded-xl p-5 sm:p-6 shadow-2xs space-y-4">
							<div class="border-b border-stone/70 pb-3">
								<h2 class="text-subheading font-semibold text-primary">{t('tickets.security.title')}</h2>
								<p class="text-caption text-secondary mt-0.5">{t('tickets.security.subtitle')}</p>
							</div>

							<div class="grid grid-cols-1 {features.securityCredentialMode === 'PasscodeAndPattern' ? 'md:grid-cols-2' : ''} gap-6 items-start">
								<div class="space-y-2 max-w-md">
									<span class="text-label-caps font-label-caps text-secondary uppercase block">{t('tickets.security.passcodeLabel')}</span>
									<div class="relative flex items-center">
										<input
											type={revealPasscode ? 'text' : 'password'}
											name="securityPasscode"
											maxlength="200"
											class="w-full border border-stone rounded-input px-3.5 py-2 text-body-sm bg-white pr-20 focus:outline-none focus:border-ink focus:ring-1 focus:ring-ink/10 transition-colors font-mono"
											bind:value={detailDraft.securityPasscode}
											placeholder={t('tickets.security.passcodePlaceholder')}
										/>
										<div class="absolute right-1.5 flex items-center gap-1">
											<button
												type="button"
												class="p-1 text-secondary hover:text-primary transition-colors cursor-pointer"
												onclick={() => (revealPasscode = !revealPasscode)}
											>
												<span class="material-symbols-outlined text-[18px]">{revealPasscode ? 'visibility_off' : 'visibility'}</span>
											</button>
											{#if detailDraft.securityPasscode}
												<button
													type="button"
													class="p-1 text-secondary hover:text-primary transition-colors cursor-pointer"
													onclick={() => copyPasscode(detailDraft.securityPasscode)}
												>
													<span class="material-symbols-outlined text-[18px]">{copiedPasscode ? 'check' : 'content_copy'}</span>
												</button>
											{/if}
										</div>
									</div>
								</div>

								{#if features.securityCredentialMode === 'PasscodeAndPattern'}
									<div class="space-y-2">
										<span class="text-label-caps font-label-caps text-secondary uppercase block">{t('tickets.security.pattern')}</span>
										<div class="flex items-center gap-4 p-3 bg-ash/30 rounded-lg border border-stone">
											<PatternLock bind:value={detailDraft.securityPattern} size={110} clearLabel={t('tickets.security.clearPattern')} />
											<div class="text-body-sm text-secondary min-w-0">
												{#if detailDraft.securityPattern}
													<p class="font-mono text-primary font-bold text-caption">{t('tickets.security.patternSequence')}:</p>
													<p class="font-mono text-primary font-semibold text-xs mt-0.5">{detailDraft.securityPattern}</p>
												{:else}
													<p class="text-caption text-secondary/70 italic">{t('tickets.security.patternHint')}</p>
												{/if}
											</div>
											<input type="hidden" name="securityPattern" bind:value={detailDraft.securityPattern} />
										</div>
									</div>
								{:else}
									<input type="hidden" name="securityPattern" value={detailDraft.securityPattern} />
								{/if}
							</div>
						</div>
					{:else}
						<input type="hidden" name="securityPasscode" value={detailDraft.securityPasscode} />
						<input type="hidden" name="securityPattern" value={detailDraft.securityPattern} />
					{/if}

					<!-- Legacy historical checklist fallback (only if ticket has old preRepairChecklist JSON and zero blueprint inspection items) -->
					{#if parsedChecklist.length > 0 && (ticket.inspections?.length ?? 0) === 0}
						<div class="bg-white border border-stone rounded-xl p-5 sm:p-6 shadow-2xs space-y-4">
							<div class="flex items-center justify-between border-b border-stone/70 pb-3">
								<div>
									<h2 class="text-subheading font-semibold text-primary">{t('tickets.inspection.title')}</h2>
									<p class="text-caption text-secondary mt-0.5">{t('tickets.inspection.subtitle')}</p>
								</div>
								<span class="text-caption text-secondary font-medium">{t('tickets.inspection.summary')}</span>
							</div>

							<div class="grid grid-cols-1 md:grid-cols-2 gap-3">
								{#each parsedChecklist as item}
									<div class="flex items-start justify-between gap-3 p-3 rounded-lg border border-stone/80 bg-white shadow-2xs">
										<div class="min-w-0">
											<span class="font-medium text-body-sm text-primary block truncate">{t(item.labelKey)}</span>
											{#if item.notes}
												<p class="text-caption text-secondary mt-0.5 italic">{item.notes}</p>
											{/if}
										</div>
										<div class="shrink-0">
											{#if item.status === 'pass'}
												<span class="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full bg-emerald-50 text-emerald-700 border border-emerald-200 font-semibold text-caption">✓ {t('tickets.inspection.pass')}</span>
											{:else if item.status === 'fail'}
												<span class="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full bg-rose-50 text-rose-700 border border-rose-200 font-semibold text-caption">✕ {t('tickets.inspection.fail')}</span>
											{:else if item.status === 'untested'}
												<span class="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full bg-ash text-secondary border border-stone font-semibold text-caption">? {t('tickets.inspection.untested')}</span>
											{:else}
												<span class="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full bg-ash text-stone font-semibold text-caption">- {t('tickets.inspection.na')}</span>
											{/if}
										</div>
									</div>
								{/each}
							</div>
						</div>
					{/if}

					<!-- Save Bar -->
					<div class="flex items-center justify-between p-4 bg-white border border-stone rounded-xl shadow-2xs">
						<div class="flex items-center gap-2">
							{#if detailDirty}
								<span class="text-caption font-semibold text-amber-700 bg-amber-50 border border-amber-200 px-2.5 py-1 rounded-full">
									{t('tickets.detail.unsavedChanges')}
								</span>
							{/if}
						</div>
						<div class="flex items-center gap-3">
							{#if detailDirty}
								<button class="px-button-x py-button-y rounded-button border border-stone bg-white text-secondary hover:text-primary hover:bg-ash text-body-sm font-medium transition-all cursor-pointer" type="button" onclick={() => (showDiscardConfirm = true)}>
									{t('tickets.detail.discardChanges')}
								</button>
							{/if}
							<button class="px-button-x py-button-y rounded-button bg-ink text-white font-medium hover:bg-ink/90 active:scale-95 transition-all text-body-sm shadow-xs cursor-pointer" type="submit">
								{t('tickets.detail.saveDetails')}
							</button>
						</div>
					</div>
				</form>

				<!-- Card: Blueprint Inspection Checklist (Editable) -->
				{#if features.showPreRepairChecklist && (inspectionTemplates.length > 0 || (ticket.inspections?.length ?? 0) > 0)}
					<form method="POST" action="?/updateInspections" class="bg-white border border-stone rounded-xl p-5 sm:p-6 shadow-2xs space-y-4" use:enhance={preserveTicketDetailForm}>
						<InspectionChecklistRenderer
							templates={inspectionTemplates}
							existingItems={ticket.inspections ?? []}
							bind:entries={inspectionEntries}
							locale={data.locale}
						/>
						<div class="flex justify-end pt-2 border-t border-stone/60">
							<button type="submit" class="px-button-x py-button-y rounded-button bg-ink text-white font-medium hover:bg-ink/90 active:scale-95 transition-all text-body-sm shadow-xs cursor-pointer">
								{t('tickets.detail.saveInspectionsBtn')}
							</button>
						</div>
					</form>
				{/if}

				<!-- Card: Blueprint Dynamic Custom Fields (Editable) -->
				{#if detailAssetFields.length > 0 || detailTicketFields.length > 0}
					<form method="POST" action="?/updateCustomFields" class="bg-white border border-stone rounded-xl p-5 sm:p-6 shadow-2xs space-y-4" use:enhance={preserveTicketDetailForm}>
						<div class="border-b border-stone/70 pb-3">
							<h2 class="text-subheading font-semibold text-primary">{t('fieldService.customFieldsSection')}</h2>
						</div>
						<input type="hidden" name="assetId" value={ticket.deviceId} />
						{#if detailAssetFields.length > 0}
							<input type="hidden" name="hasAssetCustomFields" value="1" />
							<div class="space-y-2">
								<span class="text-label-caps font-label-caps text-secondary uppercase block">{terms.assetSingular}</span>
								<DynamicFieldRenderer fields={detailAssetFields} bind:values={assetFieldValues} locale={data.locale} namePrefix="assetField" />
							</div>
						{/if}
						{#if detailTicketFields.length > 0}
							<input type="hidden" name="hasTicketCustomFields" value="1" />
							<div class="space-y-2 {detailAssetFields.length > 0 ? 'pt-3 border-t border-stone/60' : ''}">
								<span class="text-label-caps font-label-caps text-secondary uppercase block">{terms.ticketSingular}</span>
								<DynamicFieldRenderer fields={detailTicketFields} bind:values={ticketFieldValues} locale={data.locale} namePrefix="ticketField" />
							</div>
						{/if}
						<div class="flex justify-end pt-2 border-t border-stone/60">
							<button type="submit" class="px-button-x py-button-y rounded-button bg-ink text-white font-medium hover:bg-ink/90 active:scale-95 transition-all text-body-sm shadow-xs cursor-pointer">
								{t('tickets.detail.saveCustomFieldsBtn')}
							</button>
						</div>
					</form>
				{/if}

				<!-- Card: Multi-Unit Linked Assets -->
				{#if features.enableFieldService || (ticket.linkedAssets?.length ?? 0) > 0 || availableLinkAssets.length > 0}
					<div class="bg-white border border-stone rounded-xl p-5 sm:p-6 shadow-2xs space-y-4">
						<div class="border-b border-stone/70 pb-3">
							<h2 class="text-subheading font-semibold text-primary">{t('tickets.detail.linkedAssetsTitle')}</h2>
						</div>
						{#if availableLinkAssets.length > 0}
							<form method="POST" action="?/linkAsset" class="flex flex-col sm:flex-row gap-3" use:enhance={preserveTicketDetailForm}>
								<select name="assetId" bind:value={linkAssetId} required class="flex-1 border border-stone rounded-input px-3 py-2 text-body-sm bg-white">
									<option value="">{t('tickets.detail.selectAssetToLink')}</option>
									{#each availableLinkAssets as candidate (candidate.id)}
										<option value={String(candidate.id)}>
											{candidate.deviceType} · {candidate.brand} {candidate.model}{candidate.serialNumber ? ` (${candidate.serialNumber})` : ''}
										</option>
									{/each}
								</select>
								<input type="text" name="notes" bind:value={linkAssetNotes} placeholder={t('tickets.inventory.notes')} class="sm:w-48 border border-stone rounded-input px-3 py-2 text-body-sm bg-white" />
								<button type="submit" class="px-button-x py-2 rounded-button bg-ink text-white text-body-sm font-medium hover:bg-ink/90 shrink-0 cursor-pointer">
									{t('tickets.detail.linkAssetBtn')}
								</button>
							</form>
						{/if}

						{#if (ticket.linkedAssets?.length ?? 0) === 0}
							<p class="text-caption text-secondary italic">{t('tickets.detail.noLinkedAssets')}</p>
						{:else}
							<div class="grid grid-cols-1 sm:grid-cols-2 gap-2.5">
								{#each ticket.linkedAssets ?? [] as linked (linked.serviceAssetId)}
									<div class="flex items-start justify-between gap-2 p-2.5 rounded-lg border border-stone bg-ash/20">
										<div class="min-w-0">
											<p class="font-medium text-body-sm text-primary truncate">{linked.brand} {linked.model}</p>
											{#if linked.primaryIdentifier}
												<p class="text-caption text-secondary font-mono">{linked.primaryIdentifier}</p>
											{/if}
											{#if linked.notes}
												<p class="text-caption text-secondary italic">{linked.notes}</p>
											{/if}
										</div>
										<form method="POST" action="?/unlinkAsset" use:enhance={preserveTicketDetailForm}>
											<input type="hidden" name="assetId" value={linked.serviceAssetId} />
											<button type="submit" class="p-1 rounded text-secondary hover:text-error hover:bg-error/10 cursor-pointer" title={t('tickets.detail.unlinkAssetBtn')}>
												<span class="material-symbols-outlined text-[16px]">close</span>
											</button>
										</form>
									</div>
								{/each}
							</div>
						{/if}
					</div>
				{/if}

				<!-- Card: Accessories Custody & Add Form -->
				{#if features.showAccessories || ticket.accessories.length > 0}
					<div class="bg-white border border-stone rounded-xl p-5 sm:p-6 shadow-2xs space-y-4">
						<div class="border-b border-stone/70 pb-3">
							<h2 class="text-subheading font-semibold text-primary">{t('tickets.accessories.title')}</h2>
							<p class="text-caption text-secondary mt-0.5">{t('tickets.detail.itemsLoggedCount', { count: ticket.accessories.length })}</p>
						</div>

					<form method="POST" action="?/addAccessory" class="space-y-3" use:enhance={addAccessoryForm}>
						<div class="grid grid-cols-1 sm:grid-cols-[1fr_120px] gap-3">
							<input
								bind:this={accessoryNameInput}
								name="name"
								class="border border-stone rounded-input px-3.5 py-2 text-body-sm bg-white"
								placeholder={t('tickets.accessories.name')}
								bind:value={accessoryDraft.name}
								required
							/>
							<input
								name="quantity"
								type="number"
								min="0.01"
								step="0.01"
								class="border border-stone rounded-input px-3 py-2 text-body-sm bg-white"
								placeholder={t('tickets.inventory.quantity')}
								bind:value={accessoryDraft.quantity}
								required
							/>
						</div>
						<div class="flex flex-col sm:flex-row gap-3">
							<input
								name="notes"
								class="flex-1 border border-stone rounded-input px-3.5 py-2 text-body-sm bg-white"
								placeholder={t('tickets.inventory.notes')}
								bind:value={accessoryDraft.notes}
							/>
							<button class="px-button-x py-2 rounded-button bg-ink text-white font-medium hover:bg-ink/90 active:scale-95 transition-all text-body-sm shadow-xs cursor-pointer shrink-0" type="submit">
								{t('tickets.accessories.add')}
							</button>
						</div>
					</form>

					<div class="grid grid-cols-1 sm:grid-cols-2 gap-2.5 pt-2 border-t border-stone/50">
						{#if ticket.accessories.length === 0}
							<p class="text-caption text-secondary sm:col-span-2 italic">{t('tickets.accessories.empty')}</p>
						{:else}
							{#each ticket.accessories as accessory}
								<div class="flex items-start justify-between gap-2 p-2.5 rounded-lg border border-stone bg-ash/20">
									<div class="min-w-0">
										<p class="font-medium text-body-sm text-primary truncate leading-tight">{accessory.name}</p>
										<p class="text-caption text-secondary mt-0.5">{t('tickets.inventory.quantity')}: {accessory.quantity}</p>
										{#if accessory.notes}<p class="text-caption text-secondary/80 italic mt-0.5">{accessory.notes}</p>{/if}
									</div>
									<form method="POST" action="?/deleteAccessory">
										<input type="hidden" name="accessoryId" value={accessory.id} />
										<button
											type="submit"
											class="p-1 rounded text-secondary hover:text-error hover:bg-error/10 transition-colors cursor-pointer"
											title={t('common.delete')}
										>
											<span class="material-symbols-outlined text-[16px]">close</span>
										</button>
									</form>
								</div>
							{/each}
						{/if}
					</div>
				</div>
				{/if}
			{/if}

			<!-- TAB 2: DIAGNOSIS & REPAIR -->
			{#if activeTab === 'diagnosis'}
				<div class="space-y-6">
					<form method="POST" action="?/update" class="space-y-6" use:enhance={saveTicketDetailForm}>
						<input type="hidden" name="updatedAt" value={data.ticket.updatedAt ?? ''} />
						<input type="hidden" name="complaint" bind:value={detailDraft.complaint} />
						<input type="hidden" name="priority" bind:value={detailDraft.priority} />
						<input type="hidden" name="initialDiagnosis" bind:value={detailDraft.initialDiagnosis} />
						<input type="hidden" name="createdAt" bind:value={detailDraft.createdAt} />
						<input type="hidden" name="physicalConditionNotes" bind:value={detailDraft.physicalConditionNotes} />
						<input type="hidden" name="accessorySummary" bind:value={detailDraft.accessorySummary} />
						<input type="hidden" name="photoNotes" bind:value={detailDraft.photoNotes} />
						<input type="hidden" name="securityPasscode" bind:value={detailDraft.securityPasscode} />
						<input type="hidden" name="securityPattern" bind:value={detailDraft.securityPattern} />
						<input type="hidden" name="preRepairChecklist" bind:value={detailDraft.preRepairChecklist} />
						<input type="hidden" name="serviceLocationType" value={detailDraft.serviceLocationType} />
						<input type="hidden" name="siteLocationId" value={detailDraft.siteLocationId} />
						<input type="hidden" name="siteAddressSnapshot" value={detailDraft.siteAddressSnapshot} />
						<input type="hidden" name="scheduledVisitStart" value={detailDraft.scheduledVisitStart} />
						<input type="hidden" name="scheduledVisitEnd" value={detailDraft.scheduledVisitEnd} />
						<input type="hidden" name="slaDueAt" value={detailDraft.slaDueAt} />
						<input type="hidden" name="departmentCode" value={detailDraft.departmentCode} />
						<input type="hidden" name="costCenter" value={detailDraft.costCenter} />

						<div class="bg-white border border-stone rounded-xl p-5 sm:p-6 shadow-2xs space-y-4">
							<div class="border-b border-stone/70 pb-3">
								<h2 class="text-subheading font-semibold text-primary">{t('tickets.detail.diagnosis')} & {t('tickets.detail.repairNotes')}</h2>
								<p class="text-caption text-secondary mt-0.5">{t('tickets.detail.subtitle')}</p>
							</div>

							<label class="space-y-1.5 block">
								<span class="text-label-caps font-label-caps text-secondary uppercase">{t('tickets.detail.diagnosis')}</span>
								<textarea
									name="diagnosis"
									maxlength="4000"
									rows="3"
									class="w-full border border-stone rounded-input p-3 text-body-sm bg-white focus:outline-none focus:border-ink focus:ring-1 focus:ring-ink/10 transition-colors"
									bind:value={detailDraft.diagnosis}
									placeholder={t('tickets.detail.diagnosisPlaceholder')}
								></textarea>
							</label>

							<label class="space-y-1.5 block">
								<span class="text-label-caps font-label-caps text-secondary uppercase">{t('tickets.detail.repairNotes')}</span>
								<textarea
									name="repairNotes"
									maxlength="4000"
									rows="3"
									class="w-full border border-stone rounded-input p-3 text-body-sm bg-white focus:outline-none focus:border-ink focus:ring-1 focus:ring-ink/10 transition-colors"
									bind:value={detailDraft.repairNotes}
									placeholder={t('tickets.detail.repairNotesPlaceholder')}
								></textarea>
							</label>
						</div>

						<!-- Warranty terms (Blueprint Configurable) -->
						{#if features.showWarrantySection}
							<div class="bg-white border border-stone rounded-xl p-5 sm:p-6 shadow-2xs space-y-4">
								<div class="border-b border-stone/70 pb-3">
									<h2 class="text-subheading font-semibold text-primary">{t('tickets.detail.warrantyTerms')}</h2>
									<p class="text-caption text-secondary mt-0.5">{t('tickets.detail.warrantyActive')}</p>
								</div>

								<div class="grid grid-cols-1 md:grid-cols-2 gap-4">
									<div class="space-y-1.5">
										<span class="text-label-caps font-label-caps text-secondary uppercase">{t('tickets.detail.warrantyUntil')}</span>
										<DatePicker name="warrantyUntil" bind:value={detailDraft.warrantyUntil} clearable />
									</div>
									<label class="space-y-1.5 block">
										<span class="text-label-caps font-label-caps text-secondary uppercase">{t('tickets.detail.warrantyTerms')}</span>
										<textarea
											name="warrantyTerms"
											maxlength="1000"
											rows="2"
											class="w-full border border-stone rounded-input p-3 text-body-sm bg-white focus:outline-none focus:border-ink focus:ring-1 focus:ring-ink/10 transition-colors"
											bind:value={detailDraft.warrantyTerms}
											placeholder={t('tickets.detail.warrantyTermsPlaceholder')}
										></textarea>
									</label>
								</div>
							</div>
						{:else}
							<input type="hidden" name="warrantyUntil" value={detailDraft.warrantyUntil} />
							<input type="hidden" name="warrantyTerms" value={detailDraft.warrantyTerms} />
						{/if}

						<!-- Save Bar -->
						<div class="flex items-center justify-end gap-3 p-4 bg-white border border-stone rounded-xl shadow-2xs">
							<button class="px-button-x py-button-y rounded-button bg-ink text-white font-medium hover:bg-ink/90 active:scale-95 transition-all text-body-sm shadow-xs cursor-pointer" type="submit">
								{t('tickets.detail.saveDetails')}
							</button>
						</div>
					</form>

					<!-- Work Items & Repair Charges Card -->
					<div class="bg-white border border-stone rounded-xl p-5 sm:p-6 shadow-2xs space-y-4">
						<div class="flex flex-wrap items-start justify-between gap-3 border-b border-stone/70 pb-3.5">
							<div>
								<h2 class="text-subheading font-semibold text-primary m-0">{t('tickets.workItems.title')}</h2>
								<p class="text-caption text-secondary mt-1 mb-0">{t('tickets.workItems.subtitle')}</p>
							</div>
							<div class="flex flex-wrap items-center gap-2">
								<span class="text-caption font-medium tabular-nums text-secondary bg-ash px-2.5 py-1 rounded-full border border-stone">
									{completedWorkItemsCount} / {activeWorkItems.length} {t('tickets.workItems.completedCount')}
								</span>
								<span class="text-caption font-semibold font-mono tabular-nums bg-emerald-50 text-emerald-700 border border-emerald-200 px-2.5 py-1 rounded-full">
									{t('tickets.workItems.approvedSubtotal')}: {formatCurrency(approvedWorkItemsSubtotal, data.locale, currencyCode)}
								</span>
								{#if pendingWorkItemsSubtotal > 0}
									<span class="text-caption font-semibold font-mono tabular-nums bg-amber-50 text-amber-800 border border-amber-200 px-2.5 py-1 rounded-full">
										{t('tickets.workItems.pendingSubtotal')}: {formatCurrency(pendingWorkItemsSubtotal, data.locale, currencyCode)}
									</span>
								{/if}
							</div>
						</div>

						<!-- Consumed stock parts billing link -->
						<div class="text-caption text-secondary bg-ash/40 border border-stone/80 rounded-lg px-3.5 py-2.5 flex flex-wrap items-center justify-between gap-2">
							<span class="flex flex-wrap items-center gap-x-1 gap-y-0.5">
								<span>{t('tickets.workItems.stockPartsLink')}</span>
								<button
									type="button"
									class="font-semibold text-claytile hover:text-ink underline underline-offset-2 cursor-pointer transition-colors"
									onclick={() => openTab('parts', 'ticket-parts-inventory')}
								>
									{t('tickets.workItems.openPartsTab')}
								</button>
							</span>
							<span class="font-mono font-semibold tabular-nums text-primary">{formatCurrency(consumedPartsSubtotal, data.locale, currencyCode)}</span>
						</div>

						<!-- Existing Work Items List -->
						<div class="space-y-2.5">
							{#if workItems.length === 0}
								<div class="rounded-lg border border-dashed border-slate/80 bg-ash/20 px-4 py-3.5 text-caption text-secondary">
									{t('tickets.workItems.empty')}
								</div>
							{:else}
								{#each workItems as item (item.id)}
									{#if editingWorkItemId === item.id}
										{@const isSavingEdit = pendingAction === `updateWorkItem:${item.id}`}
										<form method="POST" action="?/updateWorkItem" class="border border-claytile/50 rounded-lg p-4 bg-ash/25 space-y-3" use:enhance={enhanceAction(`updateWorkItem:${item.id}`)}>
											<input type="hidden" name="workItemId" value={item.id} />
											<div class="grid grid-cols-1 sm:grid-cols-[175px_1fr] gap-3">
												<div class="space-y-1.5">
													<span class="text-label-caps font-label-caps text-secondary uppercase block">{t('tickets.workItems.type')}</span>
													<Dropdown
														name="itemType"
														bind:value={editWorkItemDraft.itemType}
														options={['Service', 'NonStockPart', 'Fee'].map(v => ({ value: v, label: labelFor('tickets.workItems.itemType', v) }))}
													/>
												</div>
												<label class="space-y-1.5 block">
													<span class="text-label-caps font-label-caps text-secondary uppercase block">{t('tickets.workItems.itemTitle')} *</span>
													<input
														name="title"
														class="ticket-input h-10 w-full px-3 py-2 text-body-sm"
														placeholder={t('tickets.workItems.titlePlaceholder')}
														bind:value={editWorkItemDraft.title}
														required
													/>
												</label>
											</div>
											<label class="space-y-1.5 block">
												<span class="text-label-caps font-label-caps text-secondary uppercase block">{t('tickets.workItems.descriptionLabel')}</span>
												<input
													name="description"
													class="ticket-input h-10 w-full px-3 py-2 text-body-sm"
													placeholder={t('tickets.workItems.descriptionPlaceholder')}
													bind:value={editWorkItemDraft.description}
												/>
											</label>
											<div class="grid grid-cols-1 sm:grid-cols-[96px_minmax(180px,1fr)_minmax(140px,auto)] gap-3 items-end">
												<label class="space-y-1.5 block">
													<span class="text-label-caps font-label-caps text-secondary uppercase block">{t('tickets.workItems.quantity')} *</span>
													<input
														name="quantity"
														type="number"
														min="0.01"
														step="0.01"
														class="ticket-input h-10 w-full px-3 py-2 text-body-sm font-mono tabular-nums"
														placeholder="1"
														bind:value={editWorkItemDraft.quantity}
														required
													/>
												</label>
												<div class="space-y-1.5">
													<span class="text-label-caps font-label-caps text-secondary uppercase block">{t('tickets.workItems.unitPrice')}</span>
													<CurrencyInput
														name="unitPrice"
														prefix={currencySymbol}
														locale={data.locale}
														currency={currencyCode}
														allowDecimals={allowCurrencyDecimals}
														inputClass="h-[38px] px-3 py-2 text-body-sm font-mono tabular-nums"
														placeholder="0"
														bind:value={editWorkItemDraft.unitPrice}
													/>
												</div>
												<div class="space-y-1.5">
													<span class="text-label-caps font-label-caps text-secondary uppercase block sm:text-right">{t('tickets.workItems.lineTotal')}</span>
													<div class="h-10 px-3 rounded-input border border-stone bg-white flex items-center justify-end font-mono font-semibold text-body-sm tabular-nums text-primary whitespace-nowrap">
														{formatCurrency(editWorkItemLineTotal, data.locale, currencyCode)}
													</div>
												</div>
											</div>
											<div class="flex flex-wrap items-center justify-between gap-3 pt-2 border-t border-stone/60">
												<label class="flex items-center gap-2 min-h-8 text-caption font-medium text-primary cursor-pointer select-none">
													<input type="checkbox" name="isCompleted" bind:checked={editWorkItemDraft.isCompleted} class="h-4 w-4 rounded border-slate accent-ink cursor-pointer" />
													<span>{t('tickets.workItems.completed')}</span>
												</label>
												<div class="flex items-center gap-2">
													<button
														type="button"
														disabled={isSavingEdit}
														class="px-3.5 py-1.5 rounded-button border border-slate bg-white text-secondary hover:text-primary hover:bg-ash text-caption font-medium cursor-pointer transition-colors disabled:opacity-50"
														onclick={() => (editingWorkItemId = null)}
													>
														{t('common.cancel')}
													</button>
													<button
														type="submit"
														disabled={isSavingEdit}
														class="px-3.5 py-1.5 rounded-button bg-ink text-white hover:bg-ink/90 text-caption font-medium cursor-pointer inline-flex items-center gap-1.5 transition-all disabled:opacity-60"
													>
														{#if isSavingEdit}
															<span class="material-symbols-outlined text-[14px] animate-spin">progress_activity</span>
															<span>{t('common.saving')}</span>
														{:else}
															<span>{t('tickets.workItems.save')}</span>
														{/if}
													</button>
												</div>
											</div>
										</form>
									{:else}
										{@const isTogglingItem = pendingAction === `toggleWorkItem:${item.id}`}
										{@const isDeletingItem = pendingAction === `deleteWorkItem:${item.id}`}
										<div class="border border-stone/90 rounded-lg p-3.5 bg-ash/20 hover:border-slate transition-colors flex flex-col sm:flex-row sm:items-start justify-between gap-3">
											<div class="flex items-start gap-2.5 min-w-0 flex-1">
												{#if item.status === 'Approved' || item.status === 'PendingApproval'}
													<form method="POST" action="?/updateWorkItem" class="pt-0.5 shrink-0" use:enhance={enhanceAction(`toggleWorkItem:${item.id}`)}>
														<input type="hidden" name="workItemId" value={item.id} />
														<input type="hidden" name="itemType" value={item.itemType} />
														<input type="hidden" name="title" value={item.title} />
														<input type="hidden" name="description" value={item.description ?? ''} />
														<input type="hidden" name="quantity" value={item.quantity} />
														<input type="hidden" name="unitPrice" value={item.unitPrice} />
														<input
															type="checkbox"
															name="isCompleted"
															checked={item.isCompleted}
															disabled={isTogglingItem}
															aria-label={`${t('tickets.workItems.completed')}: ${item.title}`}
															class="h-4 w-4 rounded border-slate accent-ink cursor-pointer disabled:opacity-50"
															onchange={(event) => event.currentTarget.form?.requestSubmit()}
														/>
													</form>
												{/if}
												<div class="min-w-0 flex-1 space-y-1">
													<div class="flex flex-wrap items-center gap-1.5">
														<span class="font-semibold text-body-sm text-primary {item.isCompleted ? 'line-through opacity-70' : ''}">{item.title}</span>
														<span class="text-[10px] uppercase font-semibold px-2 py-0.5 rounded bg-white border border-stone text-secondary">
															{labelFor('tickets.workItems.itemType', item.itemType)}
														</span>
														<span class="text-[10px] uppercase font-bold px-2 py-0.5 rounded {item.status === 'Approved' ? 'bg-emerald-50 text-emerald-700 border border-emerald-200' : item.status === 'PendingApproval' ? 'bg-amber-50 text-amber-800 border border-amber-200' : item.status === 'Rejected' ? 'bg-rose-50 text-rose-700 border border-rose-200' : 'bg-ash text-secondary border border-stone'}">
															{labelFor('tickets.workItems.status', item.status)}
														</span>
														{#if item.approvalRequestId}
															<span class="text-[10px] font-mono tabular-nums px-2 py-0.5 rounded bg-ash text-primary border border-stone">
																{t('tickets.workItems.approvalLinked')} #{item.approvalRequestId}
															</span>
														{/if}
													</div>
													{#if item.description}
														<p class="text-caption text-secondary mb-0">{item.description}</p>
													{/if}
													<p class="text-caption text-secondary font-mono tabular-nums mb-0">
														{item.quantity} × {formatCurrency(item.unitPrice, data.locale, currencyCode)}
													</p>
												</div>
											</div>
											<div class="flex sm:flex-col items-center sm:items-end justify-between gap-2 shrink-0">
												<span class="font-mono font-bold tabular-nums text-body-sm text-primary {item.status === 'Rejected' || item.status === 'Cancelled' ? 'line-through opacity-50' : ''}">
													{formatCurrency(item.lineTotal, data.locale, currencyCode)}
												</span>
												<div class="flex items-center gap-1.5">
													{#if item.status === 'Approved' || item.status === 'PendingApproval'}
														<button
															type="button"
															class="px-2.5 py-1 rounded-button border border-slate bg-white text-primary hover:bg-ash text-caption font-medium cursor-pointer transition-colors"
															onclick={() => startEditingWorkItem(item)}
														>
															{t('tickets.workItems.edit')}
														</button>
													{/if}
													{#if item.status !== 'Cancelled'}
														<form method="POST" action="?/deleteWorkItem" use:enhance={enhanceAction(`deleteWorkItem:${item.id}`)}>
															<input type="hidden" name="workItemId" value={item.id} />
															<DeleteConfirmButton
																label={t('common.delete')}
																confirmTitle={t('common.deleteConfirmTitle')}
																confirmMessage={t('common.deleteConfirm')}
																cancelLabel={t('common.cancel')}
																submitting={isDeletingItem}
																class="px-2.5 py-1 rounded-button border border-rose-200 bg-rose-50 hover:bg-rose-100 text-rose-700 text-caption font-medium cursor-pointer transition-colors"
															/>
														</form>
													{/if}
												</div>
											</div>
										</div>
									{/if}
								{/each}
							{/if}
						</div>

						<!-- Add New Work Item Form -->
						<form method="POST" action="?/createWorkItem" class="rounded-lg border border-stone bg-ash/25 p-4 space-y-3.5" use:enhance={enhanceAction('createWorkItem')}>
							<h3 class="text-body-sm font-semibold text-primary m-0">{t('tickets.workItems.addTitle')}</h3>
							<div class="grid grid-cols-1 sm:grid-cols-[175px_1fr] gap-3">
								<div class="space-y-1.5">
									<span class="text-label-caps font-label-caps text-secondary uppercase block">{t('tickets.workItems.type')}</span>
									<Dropdown
										name="itemType"
										bind:value={workItemDraft.itemType}
										options={['Service', 'NonStockPart', 'Fee'].map(v => ({ value: v, label: labelFor('tickets.workItems.itemType', v) }))}
									/>
								</div>
								<label class="space-y-1.5 block">
									<span class="text-label-caps font-label-caps text-secondary uppercase block">{t('tickets.workItems.itemTitle')} *</span>
									<input
										name="title"
										class="ticket-input h-10 w-full px-3.5 py-2 text-body-sm"
										placeholder={t('tickets.workItems.titlePlaceholder')}
										bind:value={workItemDraft.title}
										required
									/>
								</label>
							</div>
							<label class="space-y-1.5 block">
								<span class="text-label-caps font-label-caps text-secondary uppercase block">{t('tickets.workItems.descriptionLabel')}</span>
								<input
									name="description"
									class="ticket-input h-10 w-full px-3.5 py-2 text-body-sm"
									placeholder={t('tickets.workItems.descriptionPlaceholder')}
									bind:value={workItemDraft.description}
								/>
							</label>
							<div class="grid grid-cols-1 sm:grid-cols-[96px_minmax(180px,1fr)_minmax(140px,auto)] gap-3 items-end">
								<label class="space-y-1.5 block">
									<span class="text-label-caps font-label-caps text-secondary uppercase block">{t('tickets.workItems.quantity')} *</span>
									<input
										name="quantity"
										type="number"
										min="0.01"
										step="0.01"
										class="ticket-input h-10 w-full px-3 py-2 text-body-sm font-mono tabular-nums"
										placeholder="1"
										bind:value={workItemDraft.quantity}
										required
									/>
								</label>
								<div class="space-y-1.5">
									<span class="text-label-caps font-label-caps text-secondary uppercase block">{t('tickets.workItems.unitPrice')}</span>
									<CurrencyInput
										name="unitPrice"
										prefix={currencySymbol}
										locale={data.locale}
										currency={currencyCode}
										allowDecimals={allowCurrencyDecimals}
										inputClass="h-[38px] px-3 py-2 text-body-sm font-mono tabular-nums"
										placeholder="0"
										bind:value={workItemDraft.unitPrice}
									/>
								</div>
								<div class="space-y-1.5">
									<span class="text-label-caps font-label-caps text-secondary uppercase block sm:text-right">{t('tickets.workItems.lineTotal')}</span>
									<div class="h-10 px-3 rounded-input border border-stone bg-white flex items-center justify-end font-mono font-semibold text-body-sm tabular-nums text-primary whitespace-nowrap">
										{formatCurrency(workItemAddLineTotal, data.locale, currencyCode)}
									</div>
								</div>
							</div>
							<div class="flex flex-wrap items-center justify-between gap-3 pt-1">
								<label class="flex items-center gap-2 min-h-8 text-caption font-medium text-secondary hover:text-primary cursor-pointer select-none">
									<input type="checkbox" name="isCompleted" bind:checked={workItemDraft.isCompleted} class="h-4 w-4 rounded border-slate accent-ink cursor-pointer" />
									<span>{t('tickets.workItems.completed')}</span>
								</label>
								<button
									class="px-button-x py-button-y rounded-button bg-ink text-white font-medium hover:bg-ink/90 active:scale-95 transition-all text-body-sm shadow-xs cursor-pointer inline-flex items-center gap-1.5 disabled:opacity-60 disabled:cursor-not-allowed"
									type="submit"
									disabled={isAddingWorkItem}
								>
									{#if isAddingWorkItem}
										<span class="material-symbols-outlined text-[16px] animate-spin">progress_activity</span>
										<span>{t('tickets.workItems.adding')}</span>
									{:else}
										<span class="material-symbols-outlined text-[16px]">add</span>
										<span>{t('tickets.workItems.addButton')}</span>
									{/if}
								</button>
							</div>
						</form>
					</div>

					<!-- Customer Approvals Card -->
					{#if showApprovals}
					<div class="bg-white border border-stone rounded-xl p-5 sm:p-6 shadow-2xs space-y-4">
						<div class="border-b border-stone/70 pb-3.5">
							<h2 class="text-subheading font-semibold text-primary m-0">{t('tickets.detail.approvals')}</h2>
							<p class="text-caption text-secondary mt-1 mb-0">{t('tickets.detail.requiresApproval')}</p>
						</div>

						<div class="space-y-3">
							{#if data.approvals.length === 0}
								<div class="rounded-lg border border-dashed border-slate/80 bg-ash/20 px-4 py-3.5 text-caption text-secondary">
									{t('tickets.detail.noApprovals')}
								</div>
							{:else}
								{#each data.approvals as approval (approval.id)}
									{@const linkedItems = workItemsForApproval(approval.id)}
									{@const isApproving = pendingAction === `approveApproval:${approval.id}`}
									{@const isRejecting = pendingAction === `rejectApproval:${approval.id}`}
									{@const isAddingCharge = pendingAction === `createWorkItemFromApproval:${approval.id}`}
									<div class="border border-stone/90 rounded-lg p-4 space-y-2.5 bg-ash/20">
										<div class="flex flex-wrap items-start justify-between gap-2">
											<p class="font-semibold text-primary text-body-sm mb-0">{approval.title}</p>
											<span class="text-[10px] uppercase font-bold px-2 py-0.5 rounded shrink-0 {approval.status === 'Approved' ? 'bg-emerald-50 text-emerald-700 border border-emerald-200' : approval.status === 'Rejected' ? 'bg-rose-50 text-rose-700 border border-rose-200' : approval.status === 'Cancelled' ? 'bg-ash text-secondary border border-stone' : 'bg-amber-50 text-amber-800 border border-amber-200'}">
												{labelFor('tickets.approvalStatus', approval.status)}
											</span>
										</div>
										<p class="text-body-sm text-secondary mb-0">{approval.description}</p>
										{#if approval.estimatedAmount}
											<p class="text-caption font-semibold text-primary font-mono tabular-nums mb-0">
												{t('tickets.detail.estimatedAmount')}: {formatCurrency(approval.estimatedAmount, data.locale, currencyCode)}
											</p>
										{/if}
										{#if approval.resolutionNote}
											<p class="text-caption text-secondary italic mb-0">{t('tickets.detail.resolutionNote')}: {approval.resolutionNote}</p>
										{/if}
										{#if linkedItems.length > 0}
											<div class="rounded-lg border border-stone/80 bg-white overflow-hidden">
												<div class="px-3 py-1.5 bg-ash/40 border-b border-stone/60">
													<span class="text-[10px] font-label-caps uppercase font-bold text-secondary block">{t('tickets.detail.linkedWorkItems')}</span>
												</div>
												<div class="divide-y divide-stone/60">
													{#each linkedItems as linked (linked.id)}
														<div class="flex flex-wrap items-center justify-between gap-2 px-3 py-2 text-caption">
															<div class="min-w-0 flex-1">
																<span class="font-medium text-primary block truncate">{linked.title}</span>
																<span class="text-[11px] font-mono tabular-nums text-secondary">
																	{linked.quantity} × {formatCurrency(linked.unitPrice, data.locale, currencyCode)}
																</span>
															</div>
															<div class="flex items-center gap-2 shrink-0">
																<span class="text-[10px] uppercase font-semibold px-1.5 py-0.5 rounded {linked.status === 'Approved' ? 'bg-emerald-50 text-emerald-700 border border-emerald-200' : linked.status === 'PendingApproval' ? 'bg-amber-50 text-amber-800 border border-amber-200' : 'bg-ash text-secondary border border-stone'}">{labelFor('tickets.workItems.status', linked.status)}</span>
																<span class="font-mono font-semibold tabular-nums text-primary">{formatCurrency(linked.lineTotal, data.locale, currencyCode)}</span>
															</div>
														</div>
													{/each}
												</div>
											</div>
										{/if}
										{#if approval.status === 'Pending'}
											{#if linkedItems.length > 0}
												<p class="text-caption text-amber-800 bg-amber-50/70 border border-amber-200/80 rounded px-2.5 py-1.5 mb-0">{t('tickets.detail.approvedWorkHint')}</p>
											{/if}
											<div class="grid grid-cols-1 sm:grid-cols-2 gap-2.5 pt-2 border-t border-stone/60">
												<form method="POST" action="?/approveApproval" class="flex gap-2" use:enhance={enhanceAction(`approveApproval:${approval.id}`)}>
													<input type="hidden" name="approvalId" value={approval.id} />
													<input name="note" class="ticket-input h-9 min-w-0 flex-1 px-2.5 py-1.5 text-caption" placeholder={t('tickets.detail.resolutionNote')} />
													<button
														class="px-3 py-1.5 rounded-button bg-emerald-700 hover:bg-emerald-800 text-white font-medium text-caption cursor-pointer shrink-0 inline-flex items-center gap-1 transition-colors disabled:opacity-60"
														type="submit"
														disabled={isApproving || isRejecting}
													>
														{#if isApproving}
															<span class="material-symbols-outlined text-[14px] animate-spin">progress_activity</span>
														{/if}
														<span>{t('tickets.detail.approve')}</span>
													</button>
												</form>
												<form method="POST" action="?/rejectApproval" class="flex gap-2" use:enhance={enhanceAction(`rejectApproval:${approval.id}`)}>
													<input type="hidden" name="approvalId" value={approval.id} />
													<input name="note" class="ticket-input h-9 min-w-0 flex-1 px-2.5 py-1.5 text-caption" placeholder={t('tickets.detail.resolutionNote')} />
													<button
														class="px-3 py-1.5 rounded-button border border-error/40 text-error bg-white hover:bg-error/10 font-medium text-caption cursor-pointer shrink-0 inline-flex items-center gap-1 transition-colors disabled:opacity-60"
														type="submit"
														disabled={isApproving || isRejecting}
													>
														{#if isRejecting}
															<span class="material-symbols-outlined text-[14px] animate-spin">progress_activity</span>
														{/if}
														<span>{t('tickets.detail.reject')}</span>
													</button>
												</form>
											</div>
										{:else if canAddChargeFromApproval(approval)}
											<form method="POST" action="?/createWorkItem" class="pt-2 border-t border-stone/60" use:enhance={enhanceAction(`createWorkItemFromApproval:${approval.id}`)}>
												<input type="hidden" name="itemType" value="Service" />
												<input type="hidden" name="title" value={approval.title} />
												<input type="hidden" name="description" value={approval.description} />
												<input type="hidden" name="quantity" value="1" />
												<input type="hidden" name="unitPrice" value={approval.estimatedAmount ?? 0} />
												<input type="hidden" name="approvalRequestId" value={approval.id} />
												<button
													class="px-3 py-1.5 rounded-button border border-slate bg-white hover:bg-ash text-primary font-medium text-caption cursor-pointer inline-flex items-center gap-1.5 transition-colors disabled:opacity-60"
													type="submit"
													disabled={isAddingCharge}
												>
													{#if isAddingCharge}
														<span class="material-symbols-outlined text-[14px] animate-spin">progress_activity</span>
													{:else}
														<span class="material-symbols-outlined text-[14px]">add_circle</span>
													{/if}
													<span class="tabular-nums">{t('tickets.detail.addChargeFromApproval')} ({formatCurrency(approval.estimatedAmount ?? 0, data.locale, currencyCode)})</span>
												</button>
											</form>
										{/if}
									</div>
								{/each}
							{/if}
						</div>

						<!-- Request Approval Form -->
						<form method="POST" action="?/createApproval" class="rounded-lg border border-stone bg-ash/25 p-4 space-y-3.5" use:enhance={enhanceAction('createApproval')}>
							<input type="hidden" name="proposedWorkItemsJson" value={proposedWorkItems.length > 0 ? serializedProposedWorkItems : ''} />
							<h3 class="text-body-sm font-semibold text-primary m-0">{t('tickets.detail.createApproval')}</h3>

							<label class="space-y-1.5 block">
								<span class="text-label-caps font-label-caps text-secondary uppercase block">{t('tickets.detail.approvalTitle')} *</span>
								<input
									name="title"
									class="ticket-input h-10 w-full px-3.5 py-2 text-body-sm"
									placeholder={t('tickets.detail.approvalTitlePlaceholder')}
									bind:value={approvalDraft.title}
									required
								/>
							</label>

							<label class="space-y-1.5 block">
								<span class="text-label-caps font-label-caps text-secondary uppercase block">{t('tickets.detail.approvalDescription')} *</span>
								<textarea
									name="description"
									rows="2"
									class="ticket-textarea w-full p-3 text-body-sm"
									placeholder={t('tickets.detail.approvalDescriptionPlaceholder')}
									bind:value={approvalDraft.description}
									required
								></textarea>
							</label>

							<!-- Optional Proposed Work Items Builder -->
							<div class="rounded-lg border border-stone bg-white p-3.5 space-y-3">
								<div class="flex flex-wrap items-center justify-between gap-2">
									<div>
										<button type="button" class="text-caption font-semibold text-primary inline-flex items-center gap-1 cursor-pointer" onclick={() => (showProposedItemsBuilder = !showProposedItemsBuilder)}>
											<span class="material-symbols-outlined text-[16px] text-secondary">{showProposedItemsBuilder ? 'expand_less' : 'expand_more'}</span>
											<span>{t('tickets.detail.proposedWorkItems')}</span>
										</button>
										<p class="text-[11px] text-secondary mt-0.5 mb-0">{t('tickets.detail.proposedWorkItemsHint')}</p>
									</div>
									<button type="button" class="px-2.5 py-1.5 rounded-button border border-slate bg-white text-primary hover:bg-ash text-caption font-medium cursor-pointer inline-flex items-center gap-1 transition-colors" onclick={addProposedWorkItem}>
										<span class="material-symbols-outlined text-[14px]">add</span>
										<span>{t('tickets.detail.addProposedWorkItem')}</span>
									</button>
								</div>

								{#if showProposedItemsBuilder && proposedWorkItems.length > 0}
									<div class="space-y-2.5 pt-2 border-t border-stone/60">
										{#each proposedWorkItems as row (row.id)}
											<div class="rounded-lg border border-stone/90 bg-ash/20 p-3 space-y-2.5">
												<div class="grid grid-cols-1 sm:grid-cols-[150px_1fr_auto] gap-2 items-end">
													<div class="space-y-1">
														<span class="text-[11px] font-semibold uppercase tracking-wide text-secondary block">{t('tickets.workItems.type')}</span>
														<Dropdown
															size="sm"
															bind:value={row.itemType}
															options={['Service', 'NonStockPart', 'Fee'].map(v => ({ value: v, label: labelFor('tickets.workItems.itemType', v) }))}
														/>
													</div>
													<label class="space-y-1 block">
														<span class="text-[11px] font-semibold uppercase tracking-wide text-secondary block">{t('tickets.workItems.itemTitle')} *</span>
														<input
															class="ticket-input h-8 w-full px-2.5 py-1 text-caption"
															placeholder={t('tickets.workItems.titlePlaceholder')}
															bind:value={row.title}
															required
														/>
													</label>
													<button
														type="button"
														class="h-8 w-8 inline-flex items-center justify-center rounded-button border border-stone bg-white text-secondary hover:text-error hover:border-rose-200 cursor-pointer transition-colors"
														title={t('tickets.workItems.removeProposedItem')}
														aria-label={t('tickets.workItems.removeProposedItem')}
														onclick={() => removeProposedWorkItem(row.id)}
													>
														<span class="material-symbols-outlined text-[16px]">close</span>
													</button>
												</div>
												<div class="grid grid-cols-1 sm:grid-cols-[84px_minmax(150px,1fr)_1fr] gap-2 items-end">
													<label class="space-y-1 block">
														<span class="text-[11px] font-semibold uppercase tracking-wide text-secondary block">{t('tickets.workItems.quantity')} *</span>
														<input
															type="number"
															min="0.01"
															step="0.01"
															class="ticket-input h-8 w-full px-2.5 py-1 text-caption font-mono tabular-nums"
															placeholder="1"
															bind:value={row.quantity}
															required
														/>
													</label>
													<div class="space-y-1">
														<span class="text-[11px] font-semibold uppercase tracking-wide text-secondary block">{t('tickets.workItems.unitPrice')}</span>
														<CurrencyInput
															prefix={currencySymbol}
															locale={data.locale}
															currency={currencyCode}
															allowDecimals={allowCurrencyDecimals}
															inputClass="h-[30px] px-2.5 py-1 text-caption font-mono tabular-nums"
															placeholder="0"
															bind:value={row.unitPrice}
														/>
													</div>
													<label class="space-y-1 block">
														<span class="text-[11px] font-semibold uppercase tracking-wide text-secondary block">{t('tickets.workItems.descriptionLabel')}</span>
														<input
															class="ticket-input h-8 w-full px-2.5 py-1 text-caption"
															placeholder={t('tickets.workItems.descriptionPlaceholder')}
															bind:value={row.description}
														/>
													</label>
												</div>
											</div>
										{/each}
										<div class="flex justify-end text-caption font-mono font-semibold tabular-nums text-primary pt-0.5">
											{t('tickets.detail.proposedItemsTotal')}: {formatCurrency(proposedWorkItemsTotal, data.locale, currencyCode)}
										</div>
									</div>
								{/if}
							</div>

							<div class="flex flex-col sm:flex-row items-stretch sm:items-end justify-between gap-3 pt-1">
								<div class="flex-1 max-w-xs space-y-1.5">
									<span class="text-label-caps font-label-caps text-secondary uppercase block">{t('tickets.detail.estimatedAmount')}</span>
									<CurrencyInput
										name="estimatedAmount"
										prefix={currencySymbol}
										locale={data.locale}
										currency={currencyCode}
										allowDecimals={allowCurrencyDecimals}
										inputClass="h-[38px] px-3 py-2 text-body-sm font-mono tabular-nums"
										placeholder="0"
										bind:value={approvalDraft.estimatedAmount}
									/>
								</div>
								<button
									class="px-button-x py-button-y rounded-button border border-slate bg-white text-primary hover:bg-ash active:scale-95 transition-all text-body-sm font-medium cursor-pointer inline-flex items-center justify-center gap-1.5 disabled:opacity-60 disabled:cursor-not-allowed"
									type="submit"
									disabled={isCreatingApproval}
								>
									{#if isCreatingApproval}
										<span class="material-symbols-outlined text-[16px] animate-spin">progress_activity</span>
										<span>{t('tickets.detail.creatingApproval')}</span>
									{:else}
										<span>{t('tickets.detail.createApproval')}</span>
									{/if}
								</button>
							</div>
						</form>
					</div>
					{/if}
				</div>
			{/if}

			<!-- TAB 3: PHOTO EVIDENCE GALLERY -->
			{#if activeTab === 'photos'}
				<div class="space-y-6">
					<!-- Upload Photo Card -->
					<div class="bg-white border border-stone rounded-xl p-5 sm:p-6 shadow-2xs space-y-4">
						<div class="border-b border-stone/70 pb-3">
							<h2 class="text-subheading font-semibold text-primary">{t('tickets.detail.uploadPhotoTitle')}</h2>
							<p class="text-caption text-secondary mt-0.5">{t('tickets.attachments.fileHint')}</p>
						</div>

						<form method="POST" action="?/uploadAttachment" enctype="multipart/form-data" class="space-y-4" use:enhance={uploadAttachmentForm}>
							<div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
								<div class="space-y-1.5">
									<span class="text-label-caps font-label-caps text-secondary uppercase">{t('inventory.itemType')}</span>
									<Dropdown
										name="attachmentType"
										value={features.attachmentTypes.includes('Other') ? 'Other' : (features.attachmentTypes[0] ?? 'Other')}
										options={features.attachmentTypes.map((value) => ({ value, label: labelFor('tickets.attachmentType', value) }))}
									/>
								</div>
								<div class="space-y-1.5">
									<span class="text-label-caps font-label-caps text-secondary uppercase">{t('tickets.detail.photoVisibility')}</span>
									<label class="flex items-center gap-2 p-2.5 rounded-input border border-stone bg-ash/20 cursor-pointer select-none">
										<input type="checkbox" name="isPublicVisible" class="rounded border-stone text-ink" />
										<span class="text-body-sm text-primary font-medium">{t('tickets.attachments.publicVisible')}</span>
									</label>
								</div>
							</div>

							<div class="flex flex-col sm:flex-row items-stretch sm:items-center gap-3">
								<input
									bind:this={photoFileInput}
									name="file"
									type="file"
									accept="image/jpeg,image/png,image/webp"
									class="flex-1 border border-stone rounded-input p-2 text-body-sm bg-white"
									required
								/>
								<button
									class="px-button-x py-button-y rounded-button bg-ink text-white font-medium hover:bg-ink/90 active:scale-95 transition-all disabled:opacity-50 text-body-sm shadow-xs cursor-pointer inline-flex items-center justify-center gap-2"
									type="submit"
									disabled={uploadPending}
								>
									{#if uploadPending}
										<svg class="w-4 h-4 animate-spin text-white" viewBox="0 0 24 24" fill="none">
											<circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
											<path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
										</svg>
										<span>{t('tickets.attachments.uploading')}</span>
									{:else}
										<span class="material-symbols-outlined text-[18px]">add_a_photo</span>
										<span>{t('tickets.attachments.upload')}</span>
									{/if}
								</button>
							</div>
						</form>
					</div>

					<!-- Visual Gallery Grid -->
					<div class="bg-white border border-stone rounded-xl p-5 sm:p-6 shadow-2xs space-y-4">
						<div class="flex items-center justify-between border-b border-stone/70 pb-3">
							<div>
								<h2 class="text-subheading font-semibold text-primary">{t('tickets.detail.tabPhotos')}</h2>
								<p class="text-caption text-secondary mt-0.5">{ticket.attachments.length} {t('tickets.attachments.title')}</p>
							</div>
						</div>

						{#if ticket.attachments.length === 0}
							<div class="text-center py-12 text-secondary space-y-2">
								<span class="material-symbols-outlined text-4xl text-stone">photo_camera</span>
								<p class="text-body-sm">{t('tickets.detail.noPhotosUploaded')}</p>
							</div>
						{:else}
							<div class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-4">
								{#each ticket.attachments as attachment}
									<div class="group relative rounded-xl border border-stone bg-white overflow-hidden shadow-2xs hover:border-ink/50 transition-all flex flex-col justify-between">
										<!-- Thumbnail with click-to-preview -->
										<!-- svelte-ignore a11y_click_events_have_key_events -->
										<!-- svelte-ignore a11y_no_static_element_interactions -->
										<div
											class="relative aspect-[4/3] bg-ash overflow-hidden cursor-pointer"
											onclick={() => openPhotoViewer(attachment.id)}
										>
											{#if attachment.mimeType.startsWith('image/') && !failedAttachmentIds.includes(attachment.id)}
												<img
													src={`/app/tickets/${ticket.id}/attachments/${attachment.id}`}
													alt={attachment.fileName}
													loading="lazy"
													class="w-full h-full object-cover group-hover:scale-105 transition-transform duration-300"
													onerror={() => markAttachmentPreviewFailed(attachment.id)}
												/>
											{:else}
												<div class="w-full h-full flex items-center justify-center text-secondary">
													<span class="material-symbols-outlined text-3xl">broken_image</span>
												</div>
											{/if}

											<!-- Type tag badge -->
											<span class="absolute top-2 left-2 px-2 py-0.5 rounded-full text-[10px] font-bold uppercase tracking-wider bg-black/70 text-white backdrop-blur-xs">
												{labelFor('tickets.attachmentType', attachment.attachmentType)}
											</span>

											<!-- Public / Internal indicator -->
											<span class={`absolute top-2 right-2 px-2 py-0.5 rounded-full text-[10px] font-bold uppercase tracking-wider shadow-xs ${attachment.isPublicVisible ? 'bg-emerald-600 text-white' : 'bg-stone/80 text-white'}`}>
												{attachment.isPublicVisible ? t('tickets.attachments.publicBadge') : t('tickets.attachments.internalBadge')}
											</span>

											<!-- Hover zoom icon -->
											<div class="absolute inset-0 bg-ink/20 opacity-0 group-hover:opacity-100 transition-opacity flex items-center justify-center">
												<div class="w-8 h-8 rounded-full bg-white/90 text-ink flex items-center justify-center shadow-xs">
													<span class="material-symbols-outlined text-[18px]">zoom_in</span>
												</div>
											</div>
										</div>

										<!-- Caption and Action Row -->
										<div class="p-3 space-y-2 border-t border-stone/60">
											<div class="min-w-0">
												<p class="font-medium text-body-sm text-primary truncate" title={attachment.fileName}>{attachment.fileName}</p>
												<p class="text-caption text-secondary">{formatFileSize(attachment.fileSize)} · {formatDate(attachment.createdAt, data.locale, { dateStyle: 'medium' })}</p>
											</div>

											<div class="flex items-center justify-between gap-2 pt-1 border-t border-stone/40">
												<form method="POST" action="?/toggleAttachmentVisibility" use:enhance={preserveTicketDetailForm}>
													<input type="hidden" name="attachmentId" value={attachment.id} />
													<input type="hidden" name="isPublicVisible" value={(!attachment.isPublicVisible).toString()} />
													<button
														type="submit"
														class="text-caption font-medium text-secondary hover:text-ink cursor-pointer inline-flex items-center gap-1"
													>
														<span class="material-symbols-outlined text-[14px]">{attachment.isPublicVisible ? 'visibility_off' : 'visibility'}</span>
														<span>{attachment.isPublicVisible ? t('tickets.attachments.makeInternal') : t('tickets.attachments.makePublic')}</span>
													</button>
												</form>

												<form method="POST" action="?/deleteAttachment">
													<input type="hidden" name="attachmentId" value={attachment.id} />
													<DeleteConfirmButton
														label={t('common.delete')}
														confirmTitle={t('common.deleteConfirmTitle')}
														confirmMessage={t('common.deleteConfirm')}
														cancelLabel={t('common.cancel')}
														class="text-caption text-secondary hover:text-error cursor-pointer p-0.5"
													/>
												</form>
											</div>
										</div>
									</div>
								{/each}
							</div>
						{/if}
					</div>
				</div>
			{/if}

			<!-- TAB 4: PARTS & CHECKLIST -->
			{#if activeTab === 'parts'}
				<div class="space-y-6">
					<!-- Checklist Card -->
					<div id="ticket-parts-checklist" class="bg-white border border-stone rounded-xl p-5 sm:p-6 shadow-2xs space-y-4 scroll-mt-24">
						<div class="flex items-center justify-between border-b border-stone/70 pb-3">
							<div>
								<h2 class="text-subheading font-semibold text-primary m-0">{t('tickets.checklist.title')}</h2>
								<p class="text-caption text-secondary mt-0.5 mb-0">{checklistDone} / {data.checklistItems.length} {t('tickets.checklist.completed')}</p>
							</div>
						</div>

						<form method="POST" action="?/applyTemplate" class="flex flex-col sm:flex-row gap-3" use:enhance={preserveTicketDetailForm}>
							<div class="flex-1">
								<Dropdown
									name="templateId"
									value=""
									options={[
										{ value: '', label: t('tickets.checklist.selectTemplate') },
										...activeServiceTemplates.map(template => ({ value: template.id.toString(), label: `${template.name} (${template.checklistItems.length})` }))
									]}
								/>
							</div>
							<button class="px-button-x py-button-y rounded-button border border-stone bg-white text-primary hover:bg-ash text-body-sm font-medium cursor-pointer" type="submit" disabled={activeServiceTemplates.length === 0}>
								{t('tickets.checklist.applyTemplate')}
							</button>
						</form>

						<div class="space-y-2 pt-2">
							{#if data.checklistItems.length === 0}
								<p class="text-caption text-secondary italic">{t('tickets.checklist.empty')}</p>
							{:else}
								{#each data.checklistItems as item}
									<form method="POST" action="?/updateChecklistItem" class="p-3 rounded-lg border border-stone bg-ash/20" use:enhance={preserveTicketDetailForm}>
										<input type="hidden" name="checklistItemId" value={item.id} />
										<label class="flex items-start gap-3 text-body-sm cursor-pointer">
											<input class="mt-1 rounded border-stone text-ink focus:ring-ink" type="checkbox" name="isCompleted" checked={item.isCompleted} onchange={(event) => event.currentTarget.form?.requestSubmit()} />
											<span class="flex-1 min-w-0">
												<span class="font-medium text-primary block {item.isCompleted ? 'line-through opacity-70' : ''}">{item.title}</span>
												{#if item.description}<span class="block text-caption text-secondary mt-0.5">{item.description}</span>{/if}
											</span>
										</label>
									</form>
								{/each}
							{/if}
						</div>
					</div>

					<!-- Inventory & Parts Reservation Card -->
					<div
						id="ticket-parts-inventory"
						class="bg-white border rounded-xl p-5 sm:p-6 shadow-2xs space-y-4 scroll-mt-24 transition-all {highlightedSectionId === 'ticket-parts-inventory' ? 'border-claytile ring-3 ring-claytile/20' : 'border-stone'}"
					>
						<div class="flex items-center justify-between border-b border-stone/70 pb-3">
							<div>
								<h2 class="text-subheading font-semibold text-primary m-0">{t('tickets.inventory.title')}</h2>
								<p class="text-caption text-secondary mt-0.5 mb-0">{data.inventoryUsages.length} parts recorded</p>
							</div>
							{#if consumedPartsSubtotal > 0}
								<span class="text-caption font-semibold font-mono tabular-nums text-primary bg-ash px-2.5 py-1 rounded-full border border-stone">
									Subtotal: {formatCurrency(consumedPartsSubtotal, data.locale, currencyCode)}
								</span>
							{/if}
						</div>

						<form method="POST" action="?/reserveInventory" class="space-y-3" use:enhance={preserveTicketDetailForm}>
							<div class="grid grid-cols-1 sm:grid-cols-[1fr_120px] gap-3">
								<Dropdown
									name="inventoryItemId"
									bind:value={reserveDraft.inventoryItemId}
									searchable={true}
									searchPlaceholder={t('common.search')}
									noOptionsLabel={t('common.noOptions')}
									options={[
										{ value: '', label: t('tickets.inventory.selectItem') },
										...data.inventoryItems.map(item => ({ value: item.id.toString(), label: `${item.sku} · ${item.name} (${item.availableQuantity} ${item.unit})` }))
									]}
								/>
								<input
									name="quantity"
									type="number"
									min="0.01"
									step="0.01"
									class="w-full border border-stone rounded-input px-3 py-2 text-body-sm bg-white"
									placeholder={t('tickets.inventory.quantity')}
									bind:value={reserveDraft.quantity}
									required
								/>
							</div>
							<div class="flex gap-3">
								<input
									name="notes"
									class="flex-1 border border-stone rounded-input px-3.5 py-2 text-body-sm bg-white"
									placeholder={t('tickets.inventory.notes')}
									bind:value={reserveDraft.notes}
								/>
								<button class="px-button-x py-button-y rounded-button bg-ink text-white font-medium hover:bg-ink/90 active:scale-95 transition-all text-body-sm shadow-xs cursor-pointer shrink-0" type="submit">
									{t('tickets.inventory.reserve')}
								</button>
							</div>
						</form>

						<!-- Reserved & Consumed Parts List -->
						<div class="space-y-3 pt-2">
							{#if data.inventoryUsages.length === 0}
								<p class="text-caption text-secondary italic">{t('tickets.inventory.empty')}</p>
							{:else}
								{#each data.inventoryUsages as usage}
									<div class="border border-stone rounded-lg p-3.5 space-y-2 bg-ash/20">
										<div class="flex items-start justify-between gap-3">
											<div>
												<a class="font-semibold text-primary hover:underline text-body-sm" href={`/app/inventory/${usage.inventoryItemId}`}>{usage.itemName}</a>
												<p class="text-caption text-secondary mt-0.5">
													{usage.sku} · {labelFor('tickets.inventoryUsageStatus', usage.status)}
													{#if usage.unitPrice}
														· {formatCurrency(usage.unitPrice, data.locale, currencyCode)} / {usage.unit}
													{/if}
												</p>
											</div>
											<span class="text-caption font-mono font-semibold text-primary">{usage.reservedQuantity} / {usage.quantity} {usage.unit}</span>
										</div>

										<div class="grid grid-cols-3 gap-2 text-caption text-secondary py-1 border-t border-stone/40">
											<span>{t('tickets.inventory.consumed')}: <strong class="text-primary">{usage.consumedQuantity}</strong></span>
											<span>{t('tickets.inventory.released')}: <strong class="text-primary">{usage.releasedQuantity}</strong></span>
											<span>{t('tickets.inventory.returned')}: <strong class="text-primary">{usage.returnedQuantity}</strong></span>
										</div>

										<div class="grid grid-cols-1 sm:grid-cols-3 gap-2 pt-1 border-t border-stone/40">
											<form method="POST" action="?/consumeInventory" class="flex gap-1" use:enhance={preserveTicketDetailForm}>
												<input type="hidden" name="usageId" value={usage.id} />
												<input name="quantity" type="number" min="0.01" step="0.01" max={usage.reservedQuantity} class="w-16 border border-stone rounded px-2 py-1 text-caption bg-white" placeholder="Qty" />
												<button class="flex-1 px-2 py-1 border border-stone rounded bg-white text-caption font-medium hover:bg-ash cursor-pointer disabled:opacity-40" disabled={usage.reservedQuantity <= 0} type="submit">{t('tickets.inventory.consume')}</button>
											</form>
											<form method="POST" action="?/releaseInventory" class="flex gap-1" use:enhance={preserveTicketDetailForm}>
												<input type="hidden" name="usageId" value={usage.id} />
												<input name="quantity" type="number" min="0.01" step="0.01" max={usage.reservedQuantity} class="w-16 border border-stone rounded px-2 py-1 text-caption bg-white" placeholder="Qty" />
												<button class="flex-1 px-2 py-1 border border-stone rounded bg-white text-caption font-medium hover:bg-ash cursor-pointer disabled:opacity-40" disabled={usage.reservedQuantity <= 0} type="submit">{t('tickets.inventory.release')}</button>
											</form>
											<form method="POST" action="?/returnInventory" class="flex gap-1" use:enhance={preserveTicketDetailForm}>
												<input type="hidden" name="usageId" value={usage.id} />
												<input name="quantity" type="number" min="0.01" step="0.01" max={usage.consumedQuantity - usage.returnedQuantity} class="w-16 border border-stone rounded px-2 py-1 text-caption bg-white" placeholder="Qty" />
												<button class="flex-1 px-2 py-1 border border-stone rounded bg-white text-caption font-medium hover:bg-ash cursor-pointer disabled:opacity-40" disabled={usage.consumedQuantity - usage.returnedQuantity <= 0} type="submit">{t('tickets.inventory.return')}</button>
											</form>
										</div>
									</div>
								{/each}
							{/if}
						</div>
					</div>
				</div>
			{/if}

			<!-- TAB 5: ACTIVITY TIMELINE -->
			{#if activeTab === 'history'}
				<div class="bg-white border border-stone rounded-xl p-5 sm:p-6 shadow-2xs space-y-4">
					<div class="border-b border-stone/70 pb-3">
						<h2 class="text-subheading font-semibold text-primary">{t('tickets.detail.history')}</h2>
						<p class="text-caption text-secondary mt-0.5">{historyGroups.length} event items recorded</p>
					</div>

					<div class="space-y-4 pt-1">
						{#if historyGroups.length === 0}
							<p class="text-body-sm text-secondary italic py-4 text-center">{t('tickets.detail.noHistory')}</p>
						{:else}
							{#each historyGroups as group}
								{@const item = group.item}
								<div class="flex items-start gap-3 pb-3 border-b border-stone/40 last:border-b-0">
									<div class="w-7 h-7 rounded-full bg-ash border border-stone flex items-center justify-center text-secondary text-xs shrink-0 mt-0.5 shadow-2xs">
										<span class="material-symbols-outlined text-[16px]">feed</span>
									</div>
									<div class="flex-1 min-w-0">
										<p class="font-medium text-body-sm text-primary">
											{formatEventType(item.eventType)}
											{#if group.count > 1}
												<span class="text-caption text-secondary font-mono">({group.count})</span>
											{/if}
										</p>
										<p class="text-body-sm text-secondary mt-0.5">{item.note ?? item.reason ?? [item.fromStatus, item.toStatus].filter(Boolean).join(' → ')}</p>
										<p class="text-caption text-secondary/70 mt-1">{formatDate(item.createdAt, data.locale, { dateStyle: 'medium', timeStyle: 'short' })}</p>
									</div>
								</div>
							{/each}
						{/if}
					</div>
				</div>
			{/if}
		</div>

		<!-- Right Command Sidebar (Workflow, Assignee, Payment, WhatsApp) -->
		<aside class="space-y-6 xl:sticky xl:top-[80px]">
			<!-- Card 1: Status Workflow Transitions -->
			<div class="bg-white border border-stone rounded-xl p-5 shadow-2xs space-y-4">
				<div class="border-b border-stone/70 pb-2.5">
					<h3 class="text-body-base font-semibold text-primary">{t('tickets.detail.changeStatus')}</h3>
					<div class="flex items-center gap-2 mt-1">
						<span class="text-caption text-secondary">Current:</span>
						<GlowingBadge status={ticket.status}>{ticket.status}</GlowingBadge>
					</div>
				</div>

				{#if hasPendingApproval}
					<div class="rounded-lg border border-amber-300 bg-amber-50/80 p-2.5 text-caption text-amber-900 flex items-start gap-2">
						<span class="material-symbols-outlined text-[16px] text-amber-700 shrink-0 mt-0.5">hourglass_top</span>
						<span>{t('tickets.detail.pendingApprovalGuardNotice')}</span>
					</div>
				{/if}

				<form method="POST" action="?/changeStatus" class="space-y-3" use:enhance={preserveTicketDetailForm}>
					{#if ticket.allowedTransitions.length === 0}
						<p class="rounded-lg border border-stone bg-ash/50 p-2.5 text-caption text-secondary">{t('tickets.detail.noTransitions')}</p>
					{:else}
						<div class="space-y-1">
							<span class="text-label-caps font-label-caps text-secondary uppercase">{t('tickets.detail.nextStatus')}</span>
							<Dropdown
								name="statusId"
								bind:value={statusDraft.statusId}
								options={[{ value: '', label: t('tickets.detail.selectNextStatus') }, ...ticket.allowedTransitions.map(tr => ({ value: tr.statusId.toString(), label: tr.name }))]}
							/>
						</div>
					{/if}

					{#if selectedTransition && (selectedTransition.requiresReason || selectedTransition.requiresApproval || selectedTransition.requiresPaymentCompleted)}
						<div class="space-y-1 rounded-lg border border-amber-200 bg-amber-50/50 p-2.5 text-caption text-amber-900">
							{#if selectedTransition.requiresReason}<p class="font-medium">• {t('tickets.detail.requiresReason')}</p>{/if}
							{#if selectedTransition.requiresApproval}<p class="font-medium">• {t('tickets.detail.requiresApproval')}</p>{/if}
							{#if selectedTransition.requiresPaymentCompleted}<p class="font-medium">• {t('tickets.detail.requiresPaymentCompleted')}</p>{/if}
						</div>
					{/if}

					<label class="space-y-1 block">
						<span class="text-label-caps font-label-caps text-secondary uppercase">{t('tickets.detail.reason')}</span>
						<input
							name="reason"
							class="w-full border border-stone rounded-input px-3 py-1.5 text-body-sm bg-white focus:outline-none focus:border-ink transition-colors"
							bind:value={statusDraft.reason}
							required={selectedTransition?.requiresReason}
							placeholder="Optional or required note..."
						/>
					</label>

					{#if selectedTransition?.requiresPaymentCompleted}
						<label class="flex items-center gap-2 text-caption text-secondary cursor-pointer select-none">
							<input type="checkbox" name="overridePaymentBlock" bind:checked={statusDraft.overridePaymentBlock} class="rounded border-stone text-ink" />
							<span>{t('tickets.detail.overridePayment')}</span>
						</label>
					{/if}

					<button
						class="w-full px-button-x py-2 rounded-button bg-ink text-white font-medium hover:bg-ink/90 active:scale-95 transition-all text-body-sm shadow-xs cursor-pointer disabled:opacity-40"
						type="submit"
						disabled={ticket.allowedTransitions.length === 0 || !statusDraft.statusId}
					>
						{t('tickets.detail.applyStatus')}
					</button>
				</form>

				{#if selectedTransition?.requiresPaymentCompleted && (ticket.paymentStatus === 'Unpaid' || ticket.paymentStatus === 'Partial') && statusDraft.overridePaymentBlock}
					<form method="POST" action="?/pickupOverride" class="border-t border-stone/70 pt-3 space-y-2" use:enhance={preserveTicketDetailForm}>
						<label class="space-y-1 block">
							<span class="text-label-caps font-label-caps text-secondary uppercase">{t('tickets.detail.pickupOverride')} · {t('tickets.detail.overrideReason')}</span>
							<input
								name="reason"
								class="w-full border border-stone rounded-input px-3 py-1.5 text-body-sm bg-white focus:outline-none focus:border-ink transition-colors"
								bind:value={pickupOverrideReason}
								placeholder={t('tickets.detail.overrideReason')}
								required
							/>
						</label>
						<button
							type="submit"
							class="w-full px-button-x py-1.5 rounded-button border border-stone bg-white text-primary hover:bg-ash active:scale-95 transition-all text-caption font-medium cursor-pointer"
						>
							{t('tickets.detail.applyOverride')}
						</button>
					</form>
				{/if}
			</div>

			<!-- Card 2: Technician Assignment -->
			<div class="bg-white border border-stone rounded-xl p-5 shadow-2xs space-y-3">
				<div class="border-b border-stone/70 pb-2">
					<h3 class="text-body-base font-semibold text-primary">{t('tickets.detail.assignment')}</h3>
					<p class="text-caption text-secondary mt-0.5">
						{t('tickets.detail.assignedTechnician')}: <strong class="text-primary">{ticket.assignedTechnician ?? t('tickets.detail.unassigned')}</strong>
					</p>
				</div>

				<form method="POST" action="?/assign" class="space-y-3" use:enhance={preserveTicketDetailForm}>
					<div class="space-y-1">
						<Dropdown
							name="technicianUserId"
							value={ticket.assignedTechnicianUserId?.toString() ?? ''}
							options={[{ value: '', label: t('tickets.detail.unassigned') }, ...data.staff.map(s => ({ value: s.id.toString(), label: `${s.fullName} · ${s.roles.join(', ') || s.email}` }))]}
						/>
					</div>
					<button class="w-full px-button-x py-2 rounded-button border border-stone bg-white text-primary hover:bg-ash active:scale-95 transition-all text-body-sm font-medium cursor-pointer" type="submit">
						{t('tickets.detail.assign')}
					</button>
				</form>
			</div>

			<!-- Card 3: Payment & Accounting Balance -->
			{#if showBilling}
			<div class="bg-white border border-stone rounded-xl p-5 shadow-2xs space-y-4">
				<div class="flex items-center justify-between gap-2 border-b border-stone/70 pb-3">
					<h3 class="text-body-base font-semibold text-primary m-0">{t('tickets.detail.paymentPanel')}</h3>
					<GlowingBadge status={paymentSummary.status}>{labelFor('tickets.paymentStatus', paymentSummary.status)}</GlowingBadge>
				</div>

				{#if paymentSummary.status === 'Waived'}
					<p class="rounded-lg border border-emerald-200 bg-emerald-50/70 px-3 py-2 text-caption text-emerald-800 font-medium mb-0">
						{t('tickets.billing.waivedNotice')}
					</p>
				{/if}

				<!-- Balance Breakdown -->
				<div class="rounded-lg border border-stone/90 bg-ash/30 p-3.5 space-y-2 text-caption">
					<div class="flex items-baseline justify-between gap-2 text-secondary">
						<span>{t('tickets.billing.workItemsSubtotal')}</span>
						<span class="font-mono tabular-nums font-medium text-primary text-right">{formatCurrency(approvedWorkItemsSubtotal, data.locale, currencyCode)}</span>
					</div>
					<div class="flex items-baseline justify-between gap-2 text-secondary">
						<span>{t('tickets.billing.partsSubtotal')}</span>
						<span class="font-mono tabular-nums font-medium text-primary text-right">{formatCurrency(consumedPartsSubtotal, data.locale, currencyCode)}</span>
					</div>
					{#if (paymentSummary.discountAmount ?? 0) > 0}
						<div class="flex items-baseline justify-between gap-2 text-secondary">
							<span>{t('tickets.billing.discount')}</span>
							<span class="font-mono tabular-nums font-medium text-emerald-700 text-right">-{formatCurrency(paymentSummary.discountAmount ?? 0, data.locale, currencyCode)}</span>
						</div>
					{/if}
					{#if hasUnitemizedInitialTotal}
						<p class="text-[11px] text-secondary leading-snug mb-0 pt-0.5">
							{t('tickets.billing.unitemizedHint')}
						</p>
					{/if}
					<div class="border-t border-stone/80 pt-2 flex items-baseline justify-between gap-2">
						<div class="flex flex-wrap items-center gap-1.5 min-w-0">
							<span class="font-semibold text-primary">{t('tickets.billing.totalBill')}</span>
							{#if paymentSummary.isTotalManuallyOverridden}
								<span class="text-[10px] font-semibold px-1.5 py-0.5 rounded bg-amber-100 text-amber-800 border border-amber-200">
									{t('tickets.billing.manualBadge')}
								</span>
								<form method="POST" action="?/updateBilling" class="inline" use:enhance={enhanceAction('recalculateBilling')}>
									<input type="hidden" name="discountAmount" value={paymentSummary.discountAmount ?? 0} />
									<input type="hidden" name="wasOverridden" value="true" />
									<input type="hidden" name="clearManualOverride" value="true" />
									{#if paymentSummary.status === 'Waived'}
										<input type="hidden" name="isWaived" value="on" />
									{/if}
									<input type="hidden" name="notes" value={paymentSummary.notes ?? ''} />
									<button
										type="submit"
										disabled={isRecalculatingBilling}
										class="text-[11px] font-semibold text-claytile hover:text-ink underline underline-offset-2 cursor-pointer disabled:opacity-50"
									>
										{isRecalculatingBilling ? t('common.saving') : t('tickets.billing.recalculate')}
									</button>
								</form>
							{/if}
						</div>
						<span class="font-mono font-bold tabular-nums text-body-sm text-primary text-right shrink-0">
							{paymentSummary.totalAmount == null ? '-' : formatCurrency(paymentSummary.totalAmount, data.locale, currencyCode)}
						</span>
					</div>
					<div class="flex items-baseline justify-between gap-2 text-secondary">
						<span>{t('tickets.billing.totalPaid')}</span>
						<span class="font-mono tabular-nums font-semibold text-primary text-right">{formatCurrency(paymentSummary.amountPaid ?? 0, data.locale, currencyCode)}</span>
					</div>
					{#if overpaidAmount > 0}
						<div class="flex items-baseline justify-between gap-2 text-rose-700 font-semibold pt-0.5">
							<span>{t('tickets.billing.refundDue')}</span>
							<span class="font-mono tabular-nums text-right">{formatCurrency(overpaidAmount, data.locale, currencyCode)}</span>
						</div>
					{:else}
						<div class="flex items-baseline justify-between gap-2 {remainingBalance > 0 ? 'text-amber-800' : 'text-emerald-700'} font-semibold pt-0.5">
							<span>{t('tickets.billing.remainingBalance')}</span>
							<span class="font-mono tabular-nums text-right">{formatCurrency(remainingBalance, data.locale, currencyCode)}</span>
						</div>
					{/if}
				</div>

				<!-- Online Payment Links (Optional Gateway) -->
				{#if paymentLinksData?.gatewayEnabled && paymentSummary.status !== 'Waived'}
					<div class="border-t border-stone/70 pt-3.5 space-y-3">
						<div class="flex items-center justify-between gap-2">
							<span class="text-body-sm font-semibold text-primary">{t('tickets.paymentLinks.title')}</span>
							<span class="text-[10px] font-mono uppercase px-1.5 py-0.5 rounded bg-ash border border-stone text-secondary">
								{paymentLinksData.provider} · {paymentLinksData.environment}
							</span>
						</div>

						{#if activePendingLink && activePendingLink.paymentUrl}
							<div class="rounded-lg border border-claytile/40 bg-tertiary-container/25 p-3 space-y-2.5 text-caption">
								<div class="flex items-center justify-between gap-2">
									<span class="font-semibold text-primary flex items-center gap-1.5">
										<span class="w-2 h-2 rounded-full bg-claytile"></span>
										<span>{t('tickets.paymentLinks.activePending')}</span>
									</span>
									<span class="font-mono font-bold tabular-nums text-primary">
										{formatCurrency(activePendingLink.amount, data.locale, activePendingLink.currencyCode)}
									</span>
								</div>
								<p class="text-[11px] text-secondary m-0">
									{t('tickets.paymentLinks.expiresAt')}: {formatDate(activePendingLink.expiresAt, data.locale, { dateStyle: 'medium', timeStyle: 'short' })}
								</p>
								<div class="flex flex-wrap gap-1.5 pt-0.5">
									<button
										type="button"
										onclick={() => copyPaymentLinkUrl(activePendingLink.paymentUrl ?? '')}
										class="px-2.5 py-1 rounded border border-stone bg-white hover:bg-ash text-[11px] font-semibold text-primary cursor-pointer inline-flex items-center gap-1"
									>
										<span class="material-symbols-outlined text-[14px]">content_copy</span>
										<span>{t('tickets.paymentLinks.copyLink')}</span>
									</button>
									<button
										type="button"
										onclick={() => (showPaymentQrModal = true)}
										class="px-2.5 py-1 rounded border border-stone bg-white hover:bg-ash text-[11px] font-semibold text-primary cursor-pointer inline-flex items-center gap-1"
									>
										<span class="material-symbols-outlined text-[14px]">qr_code_2</span>
										<span>{t('tickets.paymentLinks.showQr')}</span>
									</button>
									<a
										href={activePendingLink.paymentUrl}
										target="_blank"
										rel="noopener noreferrer"
										class="px-2.5 py-1 rounded border border-stone bg-white hover:bg-ash text-[11px] font-semibold text-primary inline-flex items-center gap-1"
									>
										<span class="material-symbols-outlined text-[14px]">open_in_new</span>
										<span>{t('tickets.paymentLinks.openLink')}</span>
									</a>
									<form method="POST" action="?/syncPaymentLink" class="inline" use:enhance={enhanceAction('syncPaymentLink')}>
										<input type="hidden" name="intentId" value={activePendingLink.id} />
										<button
											type="submit"
											disabled={isSyncingPaymentLink}
											class="px-2.5 py-1 rounded border border-stone bg-white hover:bg-ash text-[11px] font-semibold text-primary cursor-pointer inline-flex items-center gap-1 disabled:opacity-60"
										>
											<span class="material-symbols-outlined text-[14px]">sync</span>
											<span>{isSyncingPaymentLink ? t('tickets.paymentLinks.syncing') : t('tickets.paymentLinks.syncStatus')}</span>
										</button>
									</form>
									<form method="POST" action="?/cancelPaymentLink" class="inline" use:enhance={enhanceAction('cancelPaymentLink')}>
										<input type="hidden" name="intentId" value={activePendingLink.id} />
										<button
											type="submit"
											disabled={isCancellingPaymentLink}
											class="px-2.5 py-1 rounded border border-rose-200 bg-white hover:bg-rose-50 text-[11px] font-semibold text-rose-700 cursor-pointer inline-flex items-center gap-1 disabled:opacity-60"
										>
											<span>{isCancellingPaymentLink ? t('tickets.paymentLinks.cancelling') : t('tickets.paymentLinks.cancelLink')}</span>
										</button>
									</form>
								</div>
							</div>
						{/if}

						{#if remainingBalance > 0 || paymentSummary.totalAmount == null}
							<form method="POST" action="?/createPaymentLink" class="space-y-2" use:enhance={enhanceAction('createPaymentLink')}>
								<div class="flex flex-wrap items-baseline justify-between gap-2">
									<span class="text-label-caps font-label-caps text-secondary uppercase">{t('tickets.paymentLinks.amountLabel')}</span>
									{#if remainingBalance > 0}
										<button
											type="button"
											class="text-[11px] font-semibold text-claytile hover:text-ink underline underline-offset-2 cursor-pointer tabular-nums"
											onclick={() => (paymentLinkAmount = String(remainingBalance))}
										>
											{t('tickets.paymentLedger.fillBalance')} ({formatCurrency(remainingBalance, data.locale, currencyCode)})
										</button>
									{/if}
								</div>
								<CurrencyInput
									name="amount"
									prefix={currencySymbol}
									locale={data.locale}
									currency={currencyCode}
									allowDecimals={allowCurrencyDecimals}
									inputClass="h-[38px] px-3 py-2 text-body-sm font-mono tabular-nums"
									placeholder={remainingBalance > 0 ? String(remainingBalance) : '0'}
									bind:value={paymentLinkAmount}
								/>
								<p class="text-[11px] text-secondary m-0">{t('tickets.paymentLinks.amountHint')}</p>
								<button
									type="submit"
									disabled={isCreatingPaymentLink}
									class="w-full px-button-x py-button-y rounded-button border border-ink bg-white text-ink hover:bg-ink hover:text-white transition-colors text-body-sm font-medium cursor-pointer inline-flex items-center justify-center gap-1.5 disabled:opacity-60"
								>
									<span class="material-symbols-outlined text-[16px]">link</span>
									<span>{isCreatingPaymentLink ? t('tickets.paymentLinks.creating') : t('tickets.paymentLinks.createButton')}</span>
								</button>
							</form>
						{/if}

						{#if pastPaymentLinks.length > 0}
							<div class="pt-1">
								<button
									type="button"
									class="w-full flex items-center justify-between text-caption font-semibold text-secondary hover:text-primary cursor-pointer py-0.5"
									onclick={() => (showPastPaymentLinks = !showPastPaymentLinks)}
								>
									<span>{t('tickets.paymentLinks.pastLinks')} ({pastPaymentLinks.length})</span>
									<span class="material-symbols-outlined text-[16px] transition-transform {showPastPaymentLinks ? 'rotate-180' : ''}">expand_more</span>
								</button>
								{#if showPastPaymentLinks}
									<div class="mt-2 space-y-1.5 max-h-48 overflow-y-auto pr-1">
										{#each pastPaymentLinks as link (link.id)}
											<div class="rounded border border-stone/80 bg-ash/20 px-2.5 py-2 text-[11px] space-y-0.5">
												<div class="flex items-center justify-between gap-2">
													<span class="font-semibold text-primary">
														{labelFor('tickets.paymentLinks.status', link.status)} · {link.provider}
													</span>
													<span class="font-mono font-semibold tabular-nums text-primary">
														{formatCurrency(link.amount, data.locale, link.currencyCode)}
													</span>
												</div>
												<div class="flex items-center justify-between gap-2 text-secondary">
													<span class="font-mono truncate">{link.providerPaymentMethod || link.externalReference}</span>
													<span class="shrink-0 tabular-nums">{formatDate(link.paidAt || link.createdAt, data.locale, { dateStyle: 'short', timeStyle: 'short' })}</span>
												</div>
											</div>
										{/each}
									</div>
								{/if}
							</div>
						{/if}
					</div>
				{:else if paymentLinksData?.workspaceEligible && !paymentLinksData?.gatewayEnabled}
					<div class="border-t border-stone/70 pt-3 flex items-center justify-between gap-2 text-caption text-secondary">
						<span>{t('tickets.paymentLinks.configurePrompt')}</span>
						<a href="/app/settings/payment-gateway" class="font-semibold text-claytile hover:text-ink underline underline-offset-2 shrink-0">
							{t('tickets.paymentLinks.configureLink')}
						</a>
					</div>
				{/if}

				<!-- Record Payment / Refund Form -->
				<form method="POST" action="?/recordPaymentTransaction" class="space-y-3 border-t border-stone/70 pt-3.5" use:enhance={enhanceAction('recordPaymentTransaction')}>
					<input type="hidden" name="entryType" value={txDraft.entryType} />
					<div class="space-y-2">
						<span class="text-body-sm font-semibold text-primary block">{t('tickets.paymentLedger.recordSection')}</span>
						<div class="grid grid-cols-2 rounded-lg border border-stone bg-ash/60 p-1 gap-1 text-caption font-semibold">
							<button
								type="button"
								aria-pressed={txDraft.entryType === 'Payment'}
								class="py-1.5 rounded-md cursor-pointer transition-all {txDraft.entryType === 'Payment' ? 'bg-white text-ink shadow-2xs font-semibold' : 'text-secondary hover:text-primary'}"
								onclick={() => (txDraft.entryType = 'Payment')}
							>
								{t('tickets.paymentLedger.typePayment')}
							</button>
							<button
								type="button"
								aria-pressed={txDraft.entryType === 'Refund'}
								class="py-1.5 rounded-md cursor-pointer transition-all {txDraft.entryType === 'Refund' ? 'bg-white text-rose-700 shadow-2xs font-semibold' : 'text-secondary hover:text-primary'}"
								onclick={() => (txDraft.entryType = 'Refund')}
							>
								{t('tickets.paymentLedger.typeRefund')}
							</button>
						</div>
					</div>

					<div class="space-y-1.5">
						<div class="flex flex-wrap items-baseline justify-between gap-x-2 gap-y-1">
							<span class="text-label-caps font-label-caps text-secondary uppercase">{t('tickets.paymentLedger.amount')} *</span>
							{#if txDraft.entryType === 'Payment' && remainingBalance > 0}
								<button
									type="button"
									class="text-[11px] font-semibold text-claytile hover:text-ink underline underline-offset-2 cursor-pointer tabular-nums transition-colors"
									onclick={() => (txDraft.amount = String(remainingBalance))}
								>
									{t('tickets.paymentLedger.fillBalance')} ({formatCurrency(remainingBalance, data.locale, currencyCode)})
								</button>
							{:else if txDraft.entryType === 'Refund' && (overpaidAmount > 0 || (paymentSummary.amountPaid ?? 0) > 0)}
								{@const refundPreset = overpaidAmount > 0 ? overpaidAmount : (paymentSummary.amountPaid ?? 0)}
								<button
									type="button"
									class="text-[11px] font-semibold text-rose-700 hover:text-rose-900 underline underline-offset-2 cursor-pointer tabular-nums transition-colors"
									onclick={() => (txDraft.amount = String(refundPreset))}
								>
									{t('tickets.paymentLedger.fillRefund')} ({formatCurrency(refundPreset, data.locale, currencyCode)})
								</button>
							{/if}
						</div>
						<CurrencyInput
							name="amount"
							prefix={currencySymbol}
							locale={data.locale}
							currency={currencyCode}
							allowDecimals={allowCurrencyDecimals}
							tone={txDraft.entryType === 'Refund' ? 'negative' : 'default'}
							inputClass="h-[38px] px-3 py-2 text-body-sm font-mono tabular-nums"
							placeholder="0"
							bind:value={txDraft.amount}
							required
						/>
					</div>

					<div class="space-y-1.5">
						<span class="text-label-caps font-label-caps text-secondary uppercase block">{t('tickets.detail.paymentMethod')}</span>
						<div class="flex flex-wrap gap-1.5" role="group" aria-label={t('tickets.paymentLedger.methodPresets')}>
							{#each methodPresets as preset}
								{@const isSelectedMethod = txDraft.method === preset.value}
								<button
									type="button"
									aria-pressed={isSelectedMethod}
									class="px-2.5 py-1 rounded border text-[11px] font-medium cursor-pointer transition-colors {isSelectedMethod ? 'border-claytile bg-tertiary-container text-on-tertiary-fixed-variant font-semibold' : 'border-slate/80 bg-white text-secondary hover:border-lead hover:text-primary hover:bg-ash/50'}"
									onclick={() => (txDraft.method = preset.value)}
								>
									{preset.label}
								</button>
							{/each}
						</div>
						<input
							name="method"
							class="ticket-input h-10 w-full px-3 py-2 text-body-sm"
							placeholder={t('tickets.paymentLedger.methodPlaceholder')}
							bind:value={txDraft.method}
						/>
					</div>

					<label class="space-y-1.5 block">
						<span class="text-label-caps font-label-caps text-secondary uppercase block">{t('tickets.detail.referenceNumber')}</span>
						<input
							name="referenceNumber"
							class="ticket-input h-10 w-full px-3 py-2 text-body-sm font-mono"
							placeholder={t('tickets.paymentLedger.referencePlaceholder')}
							bind:value={txDraft.referenceNumber}
						/>
					</label>

					<label class="space-y-1.5 block">
						<span class="text-label-caps font-label-caps text-secondary uppercase block">{t('tickets.detail.paymentNotes')}</span>
						<textarea
							name="notes"
							rows="2"
							class="ticket-textarea w-full p-2.5 text-body-sm"
							placeholder={t('tickets.paymentLedger.notesPlaceholder')}
							bind:value={txDraft.notes}
						></textarea>
					</label>

					<button
						class="w-full px-button-x py-button-y rounded-button font-medium active:scale-95 transition-all text-body-sm shadow-xs cursor-pointer inline-flex items-center justify-center gap-1.5 disabled:opacity-60 disabled:cursor-not-allowed {txDraft.entryType === 'Refund' ? 'bg-error text-white hover:bg-error/90' : 'bg-ink text-white hover:bg-ink/90'}"
						type="submit"
						disabled={isRecordingTransaction}
					>
						{#if isRecordingTransaction}
							<span class="material-symbols-outlined text-[16px] animate-spin">progress_activity</span>
							<span>{t('tickets.paymentLedger.recording')}</span>
						{:else}
							<span>{txDraft.entryType === 'Refund' ? t('tickets.paymentLedger.recordRefundButton') : t('tickets.paymentLedger.recordPaymentButton')}</span>
						{/if}
					</button>
				</form>

				<!-- Collapsible Billing Adjustments (Discount / Manual Override / Waived) -->
				<div class="border-t border-stone/70 pt-3.5 space-y-2.5">
					<button
						type="button"
						aria-expanded={showBillingAdjustments}
						class="w-full flex items-center justify-between gap-2 text-body-sm font-semibold text-primary cursor-pointer py-0.5"
						onclick={() => (showBillingAdjustments = !showBillingAdjustments)}
					>
						<span class="flex items-center gap-2">
							<span>{t('tickets.billing.adjustments')}</span>
							{#if hasActiveBillingAdjustments}
								<span class="text-[10px] font-semibold uppercase px-1.5 py-0.5 rounded border border-claytile/40 bg-tertiary-container text-on-tertiary-fixed-variant">
									{t('tickets.billing.adjustmentsActive')}
								</span>
							{/if}
						</span>
						<span class="material-symbols-outlined text-[18px] text-secondary transition-transform {showBillingAdjustments ? 'rotate-180' : ''}">expand_more</span>
					</button>

					{#if showBillingAdjustments}
						<form method="POST" action="?/updateBilling" class="rounded-lg border border-stone/90 bg-ash/25 p-3 space-y-3" use:enhance={enhanceAction('updateBilling')}>
							<input type="hidden" name="wasOverridden" value={paymentSummary.isTotalManuallyOverridden ? 'true' : 'false'} />
							<div class="space-y-1.5">
								<span class="text-label-caps font-label-caps text-secondary uppercase block">{t('tickets.billing.discountAmount')}</span>
								<CurrencyInput
									name="discountAmount"
									prefix={currencySymbol}
									locale={data.locale}
									currency={currencyCode}
									allowDecimals={allowCurrencyDecimals}
									inputClass="h-[38px] px-3 py-2 text-body-sm font-mono tabular-nums"
									placeholder="0"
									bind:value={billingDraft.discountAmount}
								/>
							</div>
							<div class="space-y-1.5">
								<span class="text-label-caps font-label-caps text-secondary uppercase block">{t('tickets.billing.manualTotalOverride')}</span>
								<CurrencyInput
									name="manualTotalAmount"
									prefix={currencySymbol}
									locale={data.locale}
									currency={currencyCode}
									allowDecimals={allowCurrencyDecimals}
									inputClass="h-[38px] px-3 py-2 text-body-sm font-mono tabular-nums"
									placeholder={t('tickets.billing.manualTotalPlaceholder')}
									bind:value={billingDraft.manualTotalAmount}
								/>
							</div>
							<p class="text-[11px] text-secondary font-mono tabular-nums mb-0 bg-white border border-stone/70 rounded px-2.5 py-1.5">
								{t('tickets.billing.calculatedFormulaHint')}: {formatCurrency(liveFormulaTotal, data.locale, currencyCode)}
							</p>
							<label class="flex items-center gap-2 min-h-8 text-caption font-medium text-secondary hover:text-primary cursor-pointer select-none">
								<input type="checkbox" name="isWaived" bind:checked={billingDraft.isWaived} class="h-4 w-4 rounded border-slate accent-ink cursor-pointer" />
								<span>{t('tickets.billing.markWaived')}</span>
							</label>
							<label class="space-y-1.5 block">
								<span class="text-label-caps font-label-caps text-secondary uppercase block">{t('tickets.billing.notes')}</span>
								<textarea
									name="notes"
									rows="2"
									class="ticket-textarea w-full p-2.5 text-body-sm"
									placeholder={t('tickets.billing.notes')}
									bind:value={billingDraft.notes}
								></textarea>
							</label>
							<button
								class="w-full px-button-x py-button-y rounded-button border border-slate bg-white text-primary hover:bg-ash text-caption font-medium cursor-pointer inline-flex items-center justify-center gap-1.5 transition-colors disabled:opacity-60 disabled:cursor-not-allowed"
								type="submit"
								disabled={isSavingBilling}
							>
								{#if isSavingBilling}
									<span class="material-symbols-outlined text-[14px] animate-spin">progress_activity</span>
									<span>{t('common.saving')}</span>
								{:else}
									<span>{t('tickets.billing.save')}</span>
								{/if}
							</button>
						</form>
					{/if}
				</div>

				<!-- Payment Ledger History -->
				<div class="border-t border-stone/70 pt-3.5 space-y-2.5">
					<div class="flex items-center justify-between gap-2">
						<span class="text-label-caps font-label-caps text-secondary uppercase">{t('tickets.paymentLedger.historyTitle')}</span>
						<span class="text-[11px] font-mono tabular-nums font-semibold text-secondary bg-ash border border-stone px-2 py-0.5 rounded-full">
							{paymentTransactions.length}
						</span>
					</div>

					{#if paymentTransactions.length === 0}
						<div class="rounded-lg border border-dashed border-slate/80 bg-ash/20 px-3 py-3 text-caption text-secondary text-center">
							{t('tickets.paymentLedger.historyEmpty')}
						</div>
					{:else}
						<div class="space-y-2 max-h-80 overflow-y-auto pr-1">
							{#each paymentTransactions as tx (tx.id)}
								<div class="rounded-lg border border-stone/90 p-3 space-y-1.5 text-caption {tx.isVoided ? 'bg-ash/50 opacity-70' : 'bg-ash/20'}">
									<div class="flex items-center justify-between gap-2">
										<div class="flex flex-wrap items-center gap-1.5 min-w-0">
											<span class="font-semibold px-1.5 py-0.5 rounded text-[10px] uppercase {tx.entryType === 'Refund' ? 'bg-rose-50 text-rose-700 border border-rose-200' : tx.entryType === 'BillingUpdate' ? 'bg-ash text-primary border border-stone' : 'bg-emerald-50 text-emerald-700 border border-emerald-200'}">
												{labelFor('tickets.paymentLedger.entryType', tx.entryType)}
											</span>
											{#if tx.isVoided}
												<span class="text-[10px] font-bold uppercase px-1.5 py-0.5 rounded bg-rose-100 text-rose-800 border border-rose-200">
													{t('tickets.paymentLedger.voidedBadge')}
												</span>
											{/if}
										</div>
										<span class="font-mono font-bold tabular-nums shrink-0 {tx.isVoided ? 'line-through text-secondary' : tx.entryType === 'Refund' ? 'text-rose-700' : 'text-primary'}">
											{#if tx.entryType === 'BillingUpdate'}
												{labelFor('tickets.paymentStatus', tx.status)}
											{:else if tx.entryType === 'Refund'}
												-{formatCurrency(tx.transactionAmount, data.locale, currencyCode)}
											{:else}
												+{formatCurrency(tx.transactionAmount, data.locale, currencyCode)}
											{/if}
										</span>
									</div>
									<div class="flex items-baseline justify-between gap-2 text-[11px] text-secondary">
										<span class="truncate font-medium">
											{[tx.method, tx.referenceNumber].filter(Boolean).join(' · ') || tx.updatedByName || t('tickets.paymentLedger.onlinePaymentActor')}
										</span>
										<span class="shrink-0 tabular-nums">{formatDate(tx.recordedAt, data.locale, { dateStyle: 'medium', timeStyle: 'short' })}</span>
									</div>
									{#if tx.notes}
										<p class="text-[11px] text-secondary/90 italic mb-0">{tx.notes}</p>
									{/if}
									{#if tx.isVoided && tx.voidReason}
										<p class="text-[11px] text-rose-700 mb-0">{t('tickets.paymentLedger.voidReason')}: {tx.voidReason}</p>
									{:else if !tx.isVoided && tx.entryType !== 'BillingUpdate'}
										<div class="flex items-center justify-between gap-2 pt-1 border-t border-stone/50">
											<span class="text-[11px] text-secondary truncate">{tx.updatedByName || t('tickets.paymentLedger.onlinePaymentActor')}</span>
											<button
												type="button"
												class="px-2 py-0.5 rounded border border-rose-200 bg-white hover:bg-rose-50 text-[11px] font-semibold text-rose-700 cursor-pointer shrink-0 transition-colors"
												onclick={() => openVoidModal(tx)}
											>
												{t('tickets.paymentLedger.void')}
											</button>
										</div>
									{/if}
								</div>
							{/each}
						</div>
					{/if}
				</div>
			</div>
			{:else if ticket.isInternalWorkOrder}
				<div class="bg-indigo-50/70 border border-indigo-200 rounded-xl p-4 text-caption text-indigo-800">
					{t('tickets.detail.internalBillingSkippedHint')}
				</div>
			{/if}

			<!-- Card 4: Customer WhatsApp Notification -->
			{#if !ticket.isInternalWorkOrder && ticket.customerPhone}
			<div class="bg-white border border-stone rounded-xl p-5 shadow-2xs space-y-3">
				<div class="border-b border-stone/70 pb-2">
					<h3 class="text-body-base font-semibold text-primary">{t('tickets.whatsapp.title')}</h3>
					<p class="text-caption text-secondary mt-0.5">{ticket.customerPhone}</p>
				</div>

				<form method="POST" action="?/generateWhatsApp" class="space-y-3" use:enhance={preserveTicketDetailForm}>
					<Dropdown name="templateKey" value="ticket_created" options={[
						{ value: 'ticket_created', label: t('tickets.whatsapp.template.ticketCreated') },
						{ value: 'waiting_approval', label: t('tickets.whatsapp.template.waitingApproval') },
						{ value: 'service_completed', label: t('tickets.whatsapp.template.serviceCompleted') },
						{ value: 'ready_pickup', label: t('tickets.whatsapp.template.readyPickup') },
						{ value: 'payment_reminder', label: t('tickets.whatsapp.template.paymentReminder') },
					]} />
					<Dropdown name="locale" value={data.locale} options={[
						{ value: 'en', label: t('language.en') },
						{ value: 'id', label: t('language.id') },
					]} />
					<button class="w-full px-button-x py-2 rounded-button border border-stone bg-white text-primary hover:bg-ash active:scale-95 transition-all text-body-sm font-medium cursor-pointer" type="submit">
						{t('tickets.whatsapp.generate')}
					</button>
				</form>

				{#if whatsapp}
					<div class="border-t border-stone/70 pt-3 space-y-2">
						<textarea class="w-full min-h-24 border border-stone rounded-input p-2 text-caption bg-ash/20" readonly>{whatsapp.message}</textarea>
						<div class="flex gap-2">
							<button class="flex-1 py-1.5 rounded-button border border-stone bg-white text-secondary hover:text-ink text-caption font-medium cursor-pointer" type="button" onclick={() => navigator.clipboard.writeText(whatsapp.message)}>{t('common.copy')}</button>
							<a class="flex-1 py-1.5 rounded-button bg-emerald-600 text-white hover:bg-emerald-700 text-caption font-medium text-center shadow-xs" href={whatsapp.whatsAppUrl} target="_blank" rel="noreferrer">{t('tickets.whatsapp.open')}</a>
						</div>
					</div>
				{/if}
			</div>
			{/if}

			<!-- Card 5: Internal Staff Note -->
			<div class="bg-white border border-stone rounded-xl p-5 shadow-2xs space-y-3">
				<div class="border-b border-stone/70 pb-2">
					<h3 class="text-body-base font-semibold text-primary">{t('tickets.detail.addNote')}</h3>
				</div>

				<form method="POST" action="?/addNote" class="space-y-3" use:enhance={preserveTicketDetailForm}>
					<textarea name="note" rows="2" class="w-full border border-stone rounded-input p-2.5 text-body-sm bg-white" placeholder={t('tickets.detail.note')} bind:value={noteDraft.note} required></textarea>
					<label class="flex items-center gap-2 text-caption text-secondary cursor-pointer select-none">
						<input type="checkbox" name="isPublicVisible" bind:checked={noteDraft.isPublicVisible} class="rounded border-stone text-ink" />
						<span>{t('tickets.detail.publicVisible')}</span>
					</label>
					<button class="w-full px-button-x py-2 rounded-button border border-stone bg-white text-primary hover:bg-ash active:scale-95 transition-all text-body-sm font-medium cursor-pointer" type="submit">
						{t('tickets.detail.addNoteAction')}
					</button>
				</form>
			</div>
		</aside>
	</div>
</div>

<!-- Interactive Lightbox Modal Dialog -->
<dialog bind:this={photoDialog} class="photo-dialog" onclose={() => (selectedAttachmentId = null)}>
	{#if selectedAttachment}
		<div class="grid max-h-[calc(100vh-32px)] gap-4 lg:grid-cols-[140px_minmax(0,1fr)]">
			<!-- Carousel Thumbnails -->
			<div class="order-2 flex gap-2 overflow-x-auto border-t border-stone pt-2 lg:order-1 lg:max-h-[calc(100vh-96px)] lg:flex-col lg:overflow-y-auto lg:overflow-x-hidden lg:border-r lg:border-t-0 lg:pr-3 lg:pt-0">
				{#each imageAttachments as attachment}
					<button
						type="button"
						class={`h-20 w-24 shrink-0 overflow-hidden rounded-lg border bg-ash lg:w-full transition-all cursor-pointer ${selectedAttachment.id === attachment.id ? 'border-ink ring-2 ring-ink/20 shadow-xs' : 'border-stone/80 opacity-70 hover:opacity-100'}`}
						onclick={() => (selectedAttachmentId = attachment.id)}
						aria-label={attachment.fileName}
					>
						<img class="h-full w-full object-cover" src={`/app/tickets/${ticket.id}/attachments/${attachment.id}`} alt={attachment.fileName} loading="lazy" />
					</button>
				{/each}
			</div>

			<!-- Main Viewer Container -->
			<div class="order-1 min-w-0 space-y-3 lg:order-2">
				<div class="flex items-start justify-between gap-4 border-b border-stone/60 pb-3">
					<div class="min-w-0">
						<h3 class="truncate text-body-base font-semibold text-primary">{selectedAttachment.fileName}</h3>
						<p class="text-caption text-secondary mt-0.5">
							<span class="font-medium">{labelFor('tickets.attachmentType', selectedAttachment.attachmentType)}</span> · {formatFileSize(selectedAttachment.fileSize)} · {formatDate(selectedAttachment.createdAt, data.locale, { dateStyle: 'medium', timeStyle: 'short' })}
						</p>
					</div>
					<button class="px-3 py-1 rounded-button border border-stone bg-white text-caption font-semibold text-primary hover:bg-ash cursor-pointer shadow-2xs" type="button" onclick={() => photoDialog.close()}>
						✕ {t('common.close')}
					</button>
				</div>

				<div class="relative flex min-h-[300px] items-center justify-center rounded-xl bg-black/95 p-2 overflow-hidden">
					<img class="max-h-[calc(100vh-210px)] max-w-full object-contain rounded select-none shadow-2xl" src={`/app/tickets/${ticket.id}/attachments/${selectedAttachment.id}`} alt={selectedAttachment.fileName} />
					{#if imageAttachments.length > 1}
						<button class="absolute left-3 w-10 h-10 rounded-full bg-white/20 hover:bg-white/40 text-white flex items-center justify-center transition-all cursor-pointer backdrop-blur-xs shadow-lg" type="button" onclick={() => movePhoto(-1)} aria-label={t('common.previous')}>
							<span class="material-symbols-outlined text-[20px]">chevron_left</span>
						</button>
						<button class="absolute right-3 w-10 h-10 rounded-full bg-white/20 hover:bg-white/40 text-white flex items-center justify-center transition-all cursor-pointer backdrop-blur-xs shadow-lg" type="button" onclick={() => movePhoto(1)} aria-label={t('common.next')}>
							<span class="material-symbols-outlined text-[20px]">chevron_right</span>
						</button>
					{/if}
				</div>
			</div>
		</div>
	{/if}
</dialog>

<!-- Void Payment Confirmation Dialog -->
<!-- svelte-ignore a11y_click_events_have_key_events -->
<!-- svelte-ignore a11y_no_noninteractive_element_interactions -->
<dialog bind:this={voidDialog} class="void-dialog" onclick={handleVoidDialogClick} oncancel={handleVoidDialogCancel}>
	{#if voidTargetTx}
		{@const isVoidingTx = pendingAction === 'voidPaymentTransaction'}
		<form method="POST" action="?/voidPaymentTransaction" class="space-y-4" use:enhance={enhanceAction('voidPaymentTransaction')}>
			<input type="hidden" name="transactionId" value={voidTargetTx.id} />
			<div class="flex items-start gap-3.5">
				<div class="flex h-10 w-10 shrink-0 items-center justify-center rounded-full border border-rose-200 bg-rose-50 text-rose-600 shadow-2xs mt-0.5">
					<span class="material-symbols-outlined text-[20px]" aria-hidden="true">warning</span>
				</div>
				<div class="min-w-0 flex-1">
					<h3 class="font-subheading text-subheading font-semibold text-primary leading-snug m-0">{t('tickets.paymentLedger.voidModalTitle')}</h3>
					<p class="mt-1 text-body-sm text-secondary leading-relaxed mb-0">{t('tickets.paymentLedger.voidModalMessage')}</p>
				</div>
			</div>
			<div class="rounded-lg border border-stone bg-ash/30 p-3 text-caption flex items-center justify-between gap-3">
				<div class="min-w-0 space-y-0.5">
					<div class="font-medium text-primary truncate">
						{[labelFor('tickets.paymentLedger.entryType', voidTargetTx.entryType), voidTargetTx.method, voidTargetTx.referenceNumber].filter(Boolean).join(' · ')}
					</div>
					<div class="text-[11px] text-secondary tabular-nums">
						{formatDate(voidTargetTx.recordedAt, data.locale, { dateStyle: 'medium', timeStyle: 'short' })}
					</div>
				</div>
				<span class="font-mono font-bold tabular-nums text-body-sm text-primary shrink-0">
					{formatCurrency(voidTargetTx.transactionAmount, data.locale, currencyCode)}
				</span>
			</div>
			<label class="space-y-1.5 block">
				<span class="text-label-caps font-label-caps text-secondary uppercase block">{t('tickets.paymentLedger.voidReason')} *</span>
				<input
					name="reason"
					class="ticket-input h-10 w-full px-3 py-2 text-body-sm"
					placeholder={t('tickets.paymentLedger.voidReasonPlaceholder')}
					bind:value={voidReason}
					disabled={isVoidingTx}
					required
				/>
			</label>
			<div class="flex justify-end gap-2.5 pt-2 border-t border-stone/60">
				<button
					type="button"
					disabled={isVoidingTx}
					class="rounded-button border border-stone bg-white px-4 py-2 text-body-sm font-medium text-secondary hover:text-primary hover:bg-ash active:scale-[0.98] transition-all shadow-2xs cursor-pointer disabled:opacity-60"
					onclick={closeVoidModal}
				>
					{t('common.cancel')}
				</button>
				<button
					type="submit"
					disabled={isVoidingTx}
					class="rounded-button border border-error/30 bg-error hover:bg-error/90 px-4 py-2 text-body-sm font-semibold text-white active:scale-[0.98] transition-all shadow-xs cursor-pointer inline-flex items-center gap-1.5 disabled:opacity-60"
				>
					{#if isVoidingTx}
						<span class="material-symbols-outlined text-[16px] animate-spin" aria-hidden="true">progress_activity</span>
						<span>{t('tickets.paymentLedger.voiding')}</span>
					{:else}
						<span class="material-symbols-outlined text-[16px]" aria-hidden="true">cancel</span>
						<span>{t('tickets.paymentLedger.confirmVoid')}</span>
					{/if}
				</button>
			</div>
		</form>
	{/if}
</dialog>

{#if showPaymentQrModal && activePendingLink && activePendingLink.paymentUrl}
	<!-- svelte-ignore a11y_click_events_have_key_events -->
	<!-- svelte-ignore a11y_no_static_element_interactions -->
	<div
		class="fixed inset-0 z-50 bg-ink/60 backdrop-blur-xs flex items-center justify-center p-4"
		onclick={() => (showPaymentQrModal = false)}
	>
		<div
			class="w-full max-w-sm rounded-xl border border-stone bg-white p-6 shadow-xl text-center space-y-4"
			onclick={(e) => e.stopPropagation()}
		>
			<div class="flex items-start justify-between gap-2 text-left">
				<div>
					<h3 class="text-subheading font-bold text-primary m-0">{t('tickets.paymentLinks.qrModalTitle')}</h3>
					<p class="text-caption text-secondary mt-0.5 m-0">{t('tickets.paymentLinks.qrModalSubtitle')}</p>
				</div>
				<button
					type="button"
					class="w-8 h-8 rounded-lg hover:bg-ash text-secondary hover:text-primary flex items-center justify-center cursor-pointer"
					onclick={() => (showPaymentQrModal = false)}
					aria-label={t('common.close')}
				>
					✕
				</button>
			</div>

			<div class="rounded-xl border border-stone bg-ash/30 p-4 flex flex-col items-center space-y-2">
				<img
					src={activePaymentQrUrl}
					alt={t('tickets.paymentLinks.qrModalTitle')}
					class="w-56 h-56 rounded-lg border border-stone bg-white p-2"
				/>
				<div class="font-mono font-bold text-body text-primary">
					{formatCurrency(activePendingLink.amount, data.locale, activePendingLink.currencyCode)}
				</div>
				<p class="text-[11px] font-mono text-secondary break-all m-0">{activePendingLink.paymentUrl}</p>
			</div>

			<div class="flex items-center justify-center gap-2">
				<button
					type="button"
					onclick={() => copyPaymentLinkUrl(activePendingLink.paymentUrl ?? '')}
					class="h-9 px-3.5 rounded-button border border-stone bg-white hover:bg-ash text-caption font-semibold text-primary cursor-pointer inline-flex items-center gap-1.5"
				>
					<span class="material-symbols-outlined text-[16px]">content_copy</span>
					<span>{t('tickets.paymentLinks.copyLink')}</span>
				</button>
				<a
					href={activePendingLink.paymentUrl}
					target="_blank"
					rel="noopener noreferrer"
					class="h-9 px-3.5 rounded-button bg-ink text-white hover:bg-ink/90 text-caption font-semibold inline-flex items-center gap-1.5"
				>
					<span class="material-symbols-outlined text-[16px]">open_in_new</span>
					<span>{t('tickets.paymentLinks.openLink')}</span>
				</a>
			</div>
		</div>
	</div>
{/if}

<style>
	.ticket-input,
	.ticket-textarea {
		border: 1px solid var(--color-stone, #e6dfd5);
		border-radius: var(--radius-input, 7px);
		background-color: #ffffff;
		color: var(--color-ink, #1c1815);
		transition: border-color 0.2s ease-in-out, box-shadow 0.2s ease-in-out;
	}

	.ticket-input::placeholder,
	.ticket-textarea::placeholder {
		color: var(--color-lead, #b5a897);
	}

	.ticket-input:hover,
	.ticket-textarea:hover {
		border-color: var(--color-lead, #b5a897);
	}

	.ticket-input:focus,
	.ticket-textarea:focus {
		outline: none;
		border-color: var(--color-claytile, #b45039);
		box-shadow: 0 0 0 3px rgba(180, 80, 57, 0.2);
	}

	.ticket-input:disabled,
	.ticket-textarea:disabled {
		background-color: var(--color-ash, #f4f0ea);
		color: var(--color-lead, #b5a897);
		cursor: not-allowed;
	}

	.void-dialog {
		position: fixed;
		inset: 0;
		margin: auto;
		width: min(460px, calc(100vw - 32px));
		height: fit-content;
		max-height: calc(100vh - 32px);
		border: 1px solid var(--color-stone, #e6dfd5);
		border-radius: 12px;
		padding: 20px 24px;
		background: #ffffff;
		box-shadow: 0 20px 25px -5px rgb(0 0 0 / 0.1), 0 8px 10px -6px rgb(0 0 0 / 0.1);
		color: var(--color-ink, #1c1815);
		text-align: left;
		white-space: normal;
		overflow-x: hidden;
		overflow-y: auto;
	}

	.void-dialog::backdrop {
		background: rgba(0, 0, 0, 0.45);
		backdrop-filter: blur(2px);
	}

	.void-dialog[open] {
		animation: void-dialog-appear 0.15s cubic-bezier(0.16, 1, 0.3, 1);
	}

	@keyframes void-dialog-appear {
		from {
			opacity: 0;
			transform: scale(0.96);
		}
		to {
			opacity: 1;
			transform: scale(1);
		}
	}

	.photo-dialog {
		position: fixed;
		inset: 50% auto auto 50%;
		transform: translate(-50%, -50%);
		width: min(1080px, calc(100vw - 32px));
		max-height: calc(100vh - 32px);
		margin: 0;
		border: 1px solid var(--color-stone);
		border-radius: var(--radius-cards);
		padding: var(--spacing-16);
		background: var(--color-white);
		box-shadow: var(--shadow-subtle-2);
		overflow: auto;
	}

	.photo-dialog::backdrop {
		background: rgb(15 23 42 / 0.72);
		backdrop-filter: blur(6px);
	}
</style>
