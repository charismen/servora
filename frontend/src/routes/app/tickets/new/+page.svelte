<script lang="ts">
	import { beforeNavigate, goto } from '$app/navigation';
	import { onMount, untrack } from 'svelte';
	import PageHeader from '$lib/components/PageHeader.svelte';
	import Dropdown from '$lib/components/ui/dropdown/Dropdown.svelte';
	import DatePicker from '$lib/components/ui/date-picker/DatePicker.svelte';
	import PatternLock from '$lib/components/PatternLock.svelte';
	import DynamicFieldRenderer from '$lib/components/DynamicFieldRenderer.svelte';
	import InspectionChecklistRenderer, { type InspectionEntry } from '$lib/components/InspectionChecklistRenderer.svelte';
	import { compressImage, isSupportedImageFile } from '$lib/media';
	import { formatEnhanceUnexpectedError, parseSafeJsonResponse } from '$lib/utils/http';
	import { submitActionSafely } from '$lib/utils/form-action';
	import { sanitizePhoneNumber, handlePhonePaste } from '$lib/utils/phone';
	import type { ActionData, PageData } from './$types';
	import { createTranslator, resolveFeatureConfig, resolveTerminology } from '$lib/i18n';

	let { data, form }: { data: PageData; form: ActionData } = $props();
	const t = $derived(createTranslator(data.locale));
	const terms = $derived(resolveTerminology(data.user.workspace?.terminology, data.locale));
	const features = $derived(
		resolveFeatureConfig(
			data.user.workspace?.featureConfig ?? data.blueprintConfig?.featureConfig,
			data.user.workspace?.operatingMode
		)
	);
	const intakeAssetFields = $derived((data.blueprintConfig?.assetFields ?? []).filter((f) => f.showOnIntake));
	const intakeTicketFields = $derived((data.blueprintConfig?.ticketFields ?? []).filter((f) => f.showOnIntake));
	const inspectionTemplates = $derived(data.blueprintConfig?.inspectionChecklists ?? []);
	const defaultAttachmentType = $derived(
		features.attachmentTypes.includes('Other')
			? 'Other'
			: (features.attachmentTypes[0] ?? 'Other')
	);
	let serviceLocationType = $state<'InShop' | 'OnSite' | 'PickupDelivery'>(
		untrack(() => features.defaultServiceLocationType)
	);
	let isInternalWorkOrder = $state(untrack(() => data.user.workspace?.operatingMode === 'InternalMaintenance'));
	let assetFieldValues = $state<Record<number, string>>({});
	let ticketFieldValues = $state<Record<number, string>>({});
	let additionalAssetIds = $state<number[]>([]);
	let showMultiUnitPicker = $state(false);
	let submitError = $state('');
	let photoError = $state('');
	let createdTicketId = $state<number | null>(null);
	let createdTicketNumber = $state('');
	let uploadProgressText = $state('');
	let createTicketFormEl: HTMLFormElement | undefined = $state();
	const formResult = $derived(form as { message?: string; messageKey?: string } | null | undefined);
	const activeFormError = $derived(
		submitError || (formResult?.messageKey ? t(formResult.messageKey) : (formResult?.message ?? ''))
	);

	function attachmentTypeLabel(value: string) {
		const key = `tickets.attachmentType.${value}`;
		const translated = t(key);
		return translated === key ? value : translated;
	}

	const todayStr = new Date().toISOString().slice(0, 10);
	let createdAt = $state(todayStr);

	let customers = $state(untrack(() => data.customers));
	let deviceGroups = $state(untrack(() => data.deviceGroups));

	// ponytail: customerId drives availableDevices; Dropdown bind:value syncs it
	let customerId = $state(untrack(() => data.customers[0]?.id?.toString() ?? ''));
	let deviceMode = $state<'existing' | 'new'>('existing');
	const availableDevices = $derived(deviceGroups.find((group) => group.customerId === Number(customerId))?.devices ?? []);
	const additionalAssetCandidates = $derived(
		(availableDevices.length > 1 ? availableDevices : (data.allDevices ?? [])).filter(
			(d) => String(d.id) !== deviceId
		)
	);
	const filteredLocations = $derived(
		(data.locations ?? []).filter((loc) => !loc.customerId || loc.customerId === Number(customerId))
	);
	const selectedCustomer = $derived(customers.find((customer) => customer.id === Number(customerId)));
	const duplicatePhoneCustomers = $derived(selectedCustomer && selectedCustomer.phoneNumber
		? customers.filter((customer) => customer.id !== selectedCustomer.id && customer.phoneNumber === selectedCustomer.phoneNumber)
		: []);
	let submitting = $state(false);

	let complaintText = $state('');
	let initialDiagnosisText = $state('');
	let newDeviceBrand = $state('');
	let newDeviceModel = $state('');

	// Quick Customer / Requester Modal State
	let showQuickCustomerModal = $state(false);
	let quickRequesterType = $state<'Customer' | 'InternalEmployee' | 'Department' | 'Branch'>(
		untrack(() => (data.user.workspace?.operatingMode === 'InternalMaintenance' ? 'Department' : 'Customer'))
	);
	let quickFullName = $state('');
	let quickPhoneNumber = $state('');
	let quickEmail = $state('');
	let quickAddress = $state('');
	let quickCompanyName = $state('');
	let quickDepartmentName = $state('');
	let quickDepartmentCode = $state('');
	let quickCostCenter = $state('');
	let quickEmployeeId = $state('');
	let quickNotes = $state('');
	let quickSaving = $state(false);
	let quickError = $state('');
	let quickSuccessMessage = $state('');
	let quickSuccessTimer: ReturnType<typeof setTimeout> | undefined;

	// Unsaved draft protection state
	let allowNavigation = $state(false);
	let showUnsavedModal = $state(false);
	let pendingNavigationUrl = $state<string | null>(null);

	// Configurable inspection checklist state
	let inspectionEntries = $state<InspectionEntry[]>([]);

	const isDirty = $derived.by(() => {
		if (photoPreviews.length > 0) return true;
		if (accessories.length > 0) return true;
		if (additionalAssetIds.length > 0) return true;
		if (complaintText.trim().length > 0) return true;
		if (initialDiagnosisText.trim().length > 0) return true;
		if (deviceMode === 'new' && (newDeviceBrand.trim().length > 0 || newDeviceModel.trim().length > 0)) return true;
		if (lockType !== 'none' || securityPasscode.trim().length > 0 || securityPattern.trim().length > 0) return true;
		if (inspectionEntries.some((item) => item.status !== 'Unchecked' || item.notes.trim().length > 0)) return true;
		return false;
	});

	beforeNavigate((navigation) => {
		if (allowNavigation || submitting || !isDirty || navigation.willUnload) return;
		const url = navigation.to?.url;
		if (!url) return;
		navigation.cancel();
		pendingNavigationUrl = `${url.pathname}${url.search}${url.hash}`;
		showUnsavedModal = true;
	});

	function confirmLeave() {
		allowNavigation = true;
		showUnsavedModal = false;
		if (pendingNavigationUrl) {
			goto(pendingNavigationUrl);
		}
	}

	function cancelLeave() {
		showUnsavedModal = false;
		pendingNavigationUrl = null;
	}

	function handleWindowKeydown(e: KeyboardEvent) {
		if (e.key !== 'Escape') return;
		if (showUnsavedModal) {
			e.preventDefault();
			cancelLeave();
		} else if (showQuickCustomerModal && !quickSaving) {
			e.preventDefault();
			showQuickCustomerModal = false;
		}
	}

	onMount(() => {
		const handleBeforeUnload = (e: BeforeUnloadEvent) => {
			if (isDirty && !submitting && !allowNavigation) {
				e.preventDefault();
				e.returnValue = '';
			}
		};
		window.addEventListener('beforeunload', handleBeforeUnload);
		return () => {
			window.removeEventListener('beforeunload', handleBeforeUnload);
			if (quickSuccessTimer) clearTimeout(quickSuccessTimer);
		};
	});

	async function handleQuickCustomerSubmit(e: SubmitEvent) {
		e.preventDefault();
		quickError = '';
		if (!quickFullName.trim()) {
			quickError = t('customers.form.fullNameRequired');
			return;
		}
		if (quickRequesterType === 'Customer' && !quickPhoneNumber.trim()) {
			quickError = t('customers.form.phoneRequired');
			return;
		}
		quickSaving = true;
		try {
			const res = await fetch('/app/customers', {
				method: 'POST',
				headers: {
					'Content-Type': 'application/json',
					Accept: 'application/json'
				},
				body: JSON.stringify({
					fullName: quickFullName.trim(),
					requesterType: quickRequesterType,
					phoneNumber: sanitizePhoneNumber(quickPhoneNumber),
					email: quickEmail.trim() || undefined,
					address: quickAddress.trim() || undefined,
					companyName: quickCompanyName.trim() || undefined,
					departmentName: quickDepartmentName.trim() || undefined,
					departmentCode: quickDepartmentCode.trim() || undefined,
					costCenter: quickCostCenter.trim() || undefined,
					employeeId: quickEmployeeId.trim() || undefined,
					notes: quickNotes.trim() || undefined
				})
			});
			const parsed = await parseSafeJsonResponse<{
				customer?: (typeof customers)[number];
				error?: string;
			}>(res, t);
			if (!parsed.ok) {
				quickError = parsed.message || t('tickets.create.quickCustomerError');
				quickSaving = false;
				return;
			}
			const newCust = parsed.data.customer;
			if (!newCust) {
				quickError = parsed.data.error || t('tickets.create.quickCustomerError');
				quickSaving = false;
				return;
			}
			customers = [newCust, ...customers];
			deviceGroups = [{ customerId: newCust.id, devices: [] }, ...deviceGroups];
			customerId = newCust.id.toString();
			deviceMode = 'new';
			showQuickCustomerModal = false;
			quickFullName = '';
			quickPhoneNumber = '';
			quickEmail = '';
			quickAddress = '';
			quickCompanyName = '';
			quickDepartmentName = '';
			quickDepartmentCode = '';
			quickCostCenter = '';
			quickEmployeeId = '';
			quickNotes = '';
			quickSaving = false;
			quickSuccessMessage = t('tickets.create.quickCustomerSuccess');
			if (quickSuccessTimer) clearTimeout(quickSuccessTimer);
			quickSuccessTimer = setTimeout(() => {
				quickSuccessMessage = '';
			}, 4000);
		} catch (err) {
			quickError = err instanceof Error ? err.message : t('tickets.create.networkError');
			quickSaving = false;
		}
	}

	// ponytail: reset deviceId when customer changes so stale id never gets submitted
	let deviceId = $state('');
	$effect(() => { deviceId = availableDevices[0]?.id?.toString() ?? ''; });

	// ponytail: automatically set deviceMode on customer selection based on device availability
	$effect(() => {
		if (availableDevices.length > 0) {
			deviceMode = 'existing';
		} else {
			deviceMode = 'new';
		}
	});

	const priorityOptions = $derived([
		{ value: 'Normal', label: t('tickets.priority.Normal') },
		{ value: 'Low', label: t('tickets.priority.Low') },
		{ value: 'High', label: t('tickets.priority.High') },
		{ value: 'Urgent', label: t('tickets.priority.Urgent') },
	]);

	let accessories = $state<string[]>([]);
	let accessoryInput = $state('');

	const quickAccessoryList = $derived(
		data.locale === 'id' ? features.quickAccessoriesId : features.quickAccessoriesEn
	);

	function addAccessory(val: string) {
		const trimmed = val.trim();
		if (trimmed && !accessories.includes(trimmed)) {
			accessories.push(trimmed);
		}
	}

	function removeAccessory(index: number) {
		accessories.splice(index, 1);
	}

	function handleAccessoryKeydown(e: KeyboardEvent) {
		if (e.key === 'Enter') {
			e.preventDefault();
			addAccessory(accessoryInput);
			accessoryInput = '';
		}
	}

	function toggleAdditionalAsset(id: number) {
		if (additionalAssetIds.includes(id)) {
			additionalAssetIds = additionalAssetIds.filter((item) => item !== id);
		} else {
			additionalAssetIds = [...additionalAssetIds, id];
		}
	}

	// Security credential state (respecting featureConfig.securityCredentialMode)
	let lockType = $state<'none' | 'passcode' | 'pattern'>('none');
	let securityPasscode = $state('');
	let securityPattern = $state('');
	let showPasscode = $state(false);
	$effect(() => {
		if (features.securityCredentialMode === 'PasscodeOnly' && lockType === 'pattern') {
			lockType = 'passcode';
			securityPattern = '';
		}
	});

	// Photo upload state & previews
	let photoFiles = $state<File[]>([]);
	let photoPreviews = $state<{
		id: string;
		url: string;
		name: string;
		size: string;
		file: File;
		compressedFile?: File;
		attachmentType: string;
		isPublicVisible: boolean;
		uploaded: boolean;
	}[]>([]);
	let fileInputRef: HTMLInputElement | undefined = $state();
	let isDragging = $state(false);

	function handleFiles(files: FileList | File[] | null) {
		if (!files) return;
		photoError = '';
		for (const file of Array.from(files)) {
			if (!isSupportedImageFile(file)) {
				photoError = t('tickets.create.intakePhotoTypeInvalid');
				continue;
			}
			if (photoFiles.some(f => f.name === file.name && f.size === file.size)) continue;
			photoFiles.push(file);
			photoPreviews.push({
				id: `${file.name}-${file.size}-${Date.now()}-${Math.random()}`,
				url: URL.createObjectURL(file),
				name: file.name,
				size: (file.size / 1024).toFixed(0) + ' KB',
				file,
				attachmentType: defaultAttachmentType,
				isPublicVisible: true,
				uploaded: false
			});
		}
	}

	function scrollToCreateTicketError() {
		setTimeout(() => {
			document.getElementById('create-ticket-error')?.scrollIntoView({ behavior: 'smooth', block: 'center' });
		}, 0);
	}

	async function handleCreateTicketSubmit(event: SubmitEvent) {
		event.preventDefault();
		if (submitting || !createTicketFormEl) return;

		submitError = '';
		photoError = '';
		uploadProgressText = '';

		if (data.requirePhotoEvidenceOnIntake && photoPreviews.length === 0) {
			photoError = t('tickets.create.intakePhotoRequired');
			submitError = t('tickets.create.intakePhotoRequired');
			scrollToCreateTicketError();
			return;
		}

		submitting = true;
		try {
			for (const preview of photoPreviews) {
				if (preview.uploaded || preview.compressedFile) continue;
				const compressed = await compressImage(preview.file);
				if (compressed.size > 5 * 1024 * 1024) {
					photoError = t('tickets.create.intakePhotoTooLarge');
					submitError = t('tickets.create.intakePhotoTooLarge');
					scrollToCreateTicketError();
					return;
				}
				preview.compressedFile = compressed;
			}

			if (!createdTicketId) {
				const metadataFormData = new FormData(createTicketFormEl);
				metadataFormData.delete('intakePhotos');
				metadataFormData.delete('intakePhotoVisibility');
				metadataFormData.delete('intakePhotoTypes');
				metadataFormData.set('deferAttachments', 'true');
				metadataFormData.set('intakePhotoCount', String(photoPreviews.length));

				const actionResponse = await submitActionSafely(window.location.href, metadataFormData, t);
				if (!actionResponse.ok) {
					submitError = actionResponse.message;
					scrollToCreateTicketError();
					return;
				}

				const result = actionResponse.result;
				if (result.type === 'redirect') {
					allowNavigation = true;
					await goto(result.location);
					return;
				}
				if (result.type === 'failure') {
					const failureData = result.data as { message?: string; messageKey?: string } | undefined;
					submitError = failureData?.messageKey
						? t(failureData.messageKey)
						: (failureData?.message || t('common.error'));
					scrollToCreateTicketError();
					return;
				}
				if (result.type === 'success') {
					const payload = result.data as { ticketId?: number; ticketNumber?: string } | undefined;
					if (!payload?.ticketId) {
						submitError = t('common.invalidServerResponse');
						scrollToCreateTicketError();
						return;
					}
					createdTicketId = payload.ticketId;
					createdTicketNumber = payload.ticketNumber ?? `#${payload.ticketId}`;
				}
			}

			const pendingPhotos = photoPreviews.filter((preview) => !preview.uploaded);
			for (let i = 0; i < pendingPhotos.length; i++) {
				const preview = pendingPhotos[i];
				uploadProgressText = t('tickets.create.uploadingPhoto', {
					current: i + 1,
					total: pendingPhotos.length
				});
				const fileToUpload = preview.compressedFile ?? preview.file;
				const uploadBody = new FormData();
				uploadBody.set('file', fileToUpload);
				uploadBody.set('attachmentType', preview.attachmentType || defaultAttachmentType);
				uploadBody.set('isPublicVisible', preview.isPublicVisible ? 'true' : 'false');

				let uploadResponse: Response;
				try {
					uploadResponse = await fetch(`/app/tickets/${createdTicketId}/attachments`, {
						method: 'POST',
						headers: { Accept: 'application/json' },
						body: uploadBody
					});
				} catch (error) {
					const reason = formatEnhanceUnexpectedError(error, t);
					photoError = reason;
					submitError = t('tickets.create.photoUploadPartial', {
						ticketNumber: createdTicketNumber || `#${createdTicketId}`,
						current: i + 1,
						reason
					});
					scrollToCreateTicketError();
					return;
				}

				const parsedUpload = await parseSafeJsonResponse(uploadResponse, t);
				if (!parsedUpload.ok) {
					photoError = parsedUpload.message;
					submitError = t('tickets.create.photoUploadPartial', {
						ticketNumber: createdTicketNumber || `#${createdTicketId}`,
						current: i + 1,
						reason: parsedUpload.message
					});
					scrollToCreateTicketError();
					return;
				}
				preview.uploaded = true;
			}

			if (createdTicketId) {
				allowNavigation = true;
				await goto(`/app/tickets/${createdTicketId}/receipt`);
			}
		} catch (error) {
			submitError = formatEnhanceUnexpectedError(error, t);
			scrollToCreateTicketError();
		} finally {
			submitting = false;
			uploadProgressText = '';
		}
	}

	function togglePhotoPublicVisibility(id: string) {
		const item = photoPreviews.find(p => p.id === id);
		if (item && !item.uploaded) item.isPublicVisible = !item.isPublicVisible;
	}

	function removePhoto(id: string) {
		const idx = photoPreviews.findIndex(p => p.id === id);
		if (idx !== -1 && !photoPreviews[idx].uploaded) {
			URL.revokeObjectURL(photoPreviews[idx].url);
			photoPreviews.splice(idx, 1);
			photoFiles.splice(idx, 1);
		}
	}

	function handleDrop(e: DragEvent) {
		e.preventDefault();
		isDragging = false;
		if (e.dataTransfer?.files) {
			handleFiles(e.dataTransfer.files);
		}
	}

	function handleDragOver(e: DragEvent) {
		e.preventDefault();
		isDragging = true;
	}

	function handleDragLeave(e: DragEvent) {
		e.preventDefault();
		isDragging = false;
	}

	$effect(() => {
		return () => {
			for (const p of photoPreviews) {
				URL.revokeObjectURL(p.url);
			}
		};
	});
</script>

<svelte:window onkeydown={handleWindowKeydown} />

<div class="space-y-lg max-w-4xl mx-auto pb-12">
	<PageHeader
		items={[
			{ label: t('common.console'), href: '/app' },
			{ label: terms.ticketPlural, href: '/app/tickets' },
			{ label: `${t('nav.newTicket')} (${terms.ticketSingular})` }
		]}
		title={`${t('tickets.create.title')} — ${terms.ticketSingular}`}
		subtitle={t('tickets.create.subtitleDynamic', {
			ticket: terms.ticketSingular.toLowerCase(),
			requester: terms.requesterSingular.toLowerCase(),
			asset: terms.assetSingular.toLowerCase()
		})}
	/>

	<form
		bind:this={createTicketFormEl}
		method="POST"
		enctype="multipart/form-data"
		class="space-y-lg !overflow-visible relative z-20"
		onsubmit={handleCreateTicketSubmit}
	>
		<input type="hidden" name="idempotencyKey" value={data.idempotencyKey} />

		{#if activeFormError}
			<div id="create-ticket-error" class="scroll-mt-24 rounded-xl border border-error/30 bg-error/5 p-4 text-body-sm text-error space-y-2.5 shadow-2xs" role="alert">
				<div class="flex items-start gap-2.5">
					<span class="material-symbols-outlined text-[18px] text-error shrink-0 mt-0.5">error</span>
					<span class="min-w-0 break-words">{activeFormError}</span>
				</div>
				{#if createdTicketId}
					<div class="flex flex-wrap items-center gap-2 pl-7">
						<button
							type="button"
							disabled={submitting}
							onclick={() => createTicketFormEl?.requestSubmit()}
							class="px-3 py-1.5 rounded-button bg-ink text-white text-caption font-medium hover:bg-ink/90 disabled:opacity-50 cursor-pointer"
						>
							{t('tickets.create.retryPhotoUpload')}
						</button>
						<a
							href={`/app/tickets/${createdTicketId}`}
							onclick={() => (allowNavigation = true)}
							class="px-3 py-1.5 rounded-button border border-stone bg-white text-ink text-caption font-medium hover:bg-ash"
						>
							{t('tickets.create.openCreatedTicket', { ticket: terms.ticketSingular.toLowerCase(), ticketNumber: createdTicketNumber || `#${createdTicketId}` })}
						</a>
					</div>
				{/if}
			</div>
		{/if}

		<!-- Section 1: Requester & Assignment -->
		<section class="bg-white border border-stone rounded-xl p-5 sm:p-6 space-y-5 shadow-2xs">
			<div class="border-b border-stone/70 pb-3">
				<h2 class="text-subheading font-semibold text-primary">{terms.requesterSingular} & {t('tickets.detail.assignedTechnician')}</h2>
				<p class="text-caption text-secondary mt-0.5">{t('tickets.create.customerSectionHint')}</p>
			</div>

			<div class="grid grid-cols-1 md:grid-cols-2 gap-4">
				<div class="space-y-1.5 md:col-span-2">
					<div class="flex flex-wrap items-center justify-between gap-2">
						<span class="text-label-caps font-label-caps text-secondary uppercase">{terms.requesterSingular} *</span>
						<button
							type="button"
							class="text-caption text-secondary hover:text-ink font-medium transition-colors cursor-pointer inline-flex items-center gap-1"
							onclick={() => {
								quickError = '';
								showQuickCustomerModal = true;
							}}
						>
							<span class="material-symbols-outlined text-[16px]">person_add</span>
							<span>{t('tickets.create.newCustomerLink')}</span>
						</button>
					</div>
					{#if quickSuccessMessage}
						<div class="rounded-lg border border-emerald-500/30 bg-emerald-50 p-2.5 text-body-sm text-emerald-800 flex items-center gap-2">
							<span class="material-symbols-outlined text-[18px] text-emerald-600 shrink-0">check_circle</span>
							<span>{quickSuccessMessage}</span>
						</div>
					{/if}
					<Dropdown
						name="customerId"
						bind:value={customerId}
						options={customers.map((c) => ({
							value: c.id.toString(),
							label: `${c.fullName}${c.departmentCode ? ` [${c.departmentCode}]` : ''}${c.phoneNumber ? ` · ${c.phoneNumber}` : ''}`
						}))}
						placeholder={terms.requesterSingular}
						searchable={true}
						searchPlaceholder={t('tickets.create.searchCustomerPlaceholder')}
						noOptionsLabel={t('tickets.create.noCustomersFound')}
					/>
					{#if customers.length === 0}
						<p class="text-caption text-amber-800 bg-amber-50 border border-amber-200/80 rounded-lg px-3 py-2">
							{t('tickets.create.noCustomerHint')}
						</p>
					{/if}
					{#if selectedCustomer?.isBanned}
						<div class="rounded-lg border border-error/30 bg-error/5 p-3 text-body-sm text-error space-y-1">
							<p class="font-semibold">{t('tickets.create.bannedCustomerTitle')}</p>
							{#if selectedCustomer.banReason}<p class="text-caption">{selectedCustomer.banReason}</p>{/if}
						</div>
						<label class="block space-y-1 pt-1">
							<span class="text-caption font-semibold uppercase text-secondary">{t('tickets.create.bannedCustomerReason')} *</span>
							<input
								type="text"
								name="bannedCustomerOverrideReason"
								class="w-full border border-stone rounded-input px-3.5 py-2 text-body-sm bg-white focus:outline-none focus:border-ink focus:ring-1 focus:ring-ink/10 transition-colors"
								placeholder={t('tickets.create.bannedCustomerReasonPlaceholder')}
								required
							/>
						</label>
					{/if}
					{#if duplicatePhoneCustomers.length > 0}
						<p class="rounded-lg border border-warning/30 bg-warning/10 p-3 text-body-sm text-primary">
							{t('tickets.create.duplicatePhoneHint')} {duplicatePhoneCustomers.map((customer) => customer.fullName).join(', ')}
						</p>
					{/if}
				</div>

				<div class="space-y-1.5">
					<span class="text-label-caps font-label-caps text-secondary uppercase">{t('tickets.detail.priority')}</span>
					<Dropdown name="priority" value="Normal" options={priorityOptions} />
				</div>

				<div class="space-y-1.5">
					<span class="text-label-caps font-label-caps text-secondary uppercase">{t('tickets.detail.assignedTechnician')}</span>
					<Dropdown
						name="assignedTechnicianUserId"
						value=""
						options={[
							{ value: '', label: t('tickets.detail.unassigned') },
							...data.staff.map(s => ({ value: s.id.toString(), label: `${s.fullName} · ${s.roles.join(', ') || s.email}` }))
						]}
					/>
				</div>
			</div>
		</section>

		<!-- Section 2: Service Asset / Unit Details -->
		<section class="bg-white border border-stone rounded-xl p-5 sm:p-6 space-y-5 shadow-2xs">
			<div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3 border-b border-stone/70 pb-3">
				<div>
					<h2 class="text-subheading font-semibold text-primary">{terms.assetSingular}</h2>
					<p class="text-caption text-secondary mt-0.5">
						{t('tickets.create.deviceSectionHintDynamic', {
							requester: terms.requesterSingular.toLowerCase(),
							asset: terms.assetSingular.toLowerCase()
						})}
					</p>
				</div>
				<div class="inline-flex flex-wrap self-start sm:self-auto rounded-input border border-stone/80 bg-ash/50 p-1 text-caption font-medium shadow-2xs">
					<button
						type="button"
						class="px-3 py-1.5 rounded-button transition-all cursor-pointer {deviceMode === 'existing' ? 'bg-white text-ink font-semibold shadow-xs' : 'text-secondary hover:text-ink disabled:opacity-40 disabled:cursor-not-allowed'}"
						disabled={availableDevices.length === 0}
						onclick={() => (deviceMode = 'existing')}
					>
						{t('tickets.create.existingDevice')}
					</button>
					<button
						type="button"
						class="px-3 py-1.5 rounded-button transition-all cursor-pointer {deviceMode === 'new' ? 'bg-white text-ink font-semibold shadow-xs' : 'text-secondary hover:text-ink'}"
						onclick={() => (deviceMode = 'new')}
					>
						+ {terms.assetSingular}
					</button>
					<input type="hidden" name="deviceMode" value={deviceMode} />
				</div>
			</div>

			{#if deviceMode === 'existing'}
				<div class="space-y-3">
					<div class="space-y-1.5">
						<span class="text-label-caps font-label-caps text-secondary uppercase">{terms.assetSingular} *</span>
						{#if availableDevices.length > 0}
							<Dropdown
								name="deviceId"
								bind:value={deviceId}
								options={availableDevices.map((d) => ({
									value: d.id.toString(),
									label: `${d.deviceType} · ${d.brand} ${d.model}${d.serialNumber ? ` · ${d.serialNumber}` : ''}`
								}))}
								placeholder={terms.assetSingular}
							/>
						{:else}
							<div class="p-4 border border-stone/80 rounded-lg bg-ash/30 text-body-sm text-secondary flex flex-wrap items-center justify-between gap-3">
								<span>{t('tickets.create.deviceRequired')}</span>
								<button
									type="button"
									class="text-ink font-semibold hover:underline cursor-pointer shrink-0"
									onclick={() => (deviceMode = 'new')}
								>
									+ {terms.assetSingular} &rarr;
								</button>
							</div>
						{/if}
					</div>

					{#if additionalAssetCandidates.length > 0}
						<div class="rounded-lg border border-stone/70 bg-surface/50 p-3 space-y-2">
							<button
								type="button"
								class="flex w-full items-center justify-between text-left text-xs font-semibold text-ink cursor-pointer"
								onclick={() => (showMultiUnitPicker = !showMultiUnitPicker)}
							>
								<span>
									{t('tickets.create.additionalAssets', { assets: terms.assetPlural })}
									{#if additionalAssetIds.length > 0}
										<span class="ml-1.5 rounded-full bg-claytile px-2 py-0.5 text-[10px] text-white">{additionalAssetIds.length}</span>
									{/if}
								</span>
								<span class="material-symbols-outlined text-[16px] text-secondary">{showMultiUnitPicker ? 'expand_less' : 'expand_more'}</span>
							</button>
							{#if showMultiUnitPicker}
								<p class="text-caption text-secondary">{t('tickets.create.additionalAssetsHint')}</p>
								<div class="grid grid-cols-1 sm:grid-cols-2 gap-2 max-h-40 overflow-y-auto pt-1">
									{#each additionalAssetCandidates as unit (unit.id)}
										<label class="flex items-center gap-2 rounded border border-stone/70 bg-white px-2.5 py-1.5 text-xs cursor-pointer hover:border-claytile/60">
											<input
												type="checkbox"
												name="additionalAssetIds"
												value={unit.id}
												checked={additionalAssetIds.includes(unit.id)}
												onchange={() => toggleAdditionalAsset(unit.id)}
												class="rounded border-stone text-claytile"
											/>
											<span class="truncate text-ink font-medium">{unit.deviceType} · {unit.brand} {unit.model}{unit.serialNumber ? ` (${unit.serialNumber})` : ''}</span>
										</label>
									{/each}
								</div>
							{:else}
								{#each additionalAssetIds as extraId (extraId)}
									<input type="hidden" name="additionalAssetIds" value={extraId} />
								{/each}
							{/if}
						</div>
					{/if}
				</div>
			{:else}
				<div class="grid grid-cols-1 md:grid-cols-3 gap-4">
					<label class="space-y-1.5">
						<span class="text-label-caps font-label-caps text-secondary uppercase">{t('tickets.create.deviceType')} *</span>
						<input
							type="text"
							name="newDeviceType"
							maxlength="80"
							class="w-full border border-stone rounded-input px-3.5 py-2 text-body-sm bg-white focus:outline-none focus:border-ink focus:ring-1 focus:ring-ink/10 transition-colors"
							placeholder={t('tickets.create.deviceTypePlaceholder')}
							required={deviceMode === 'new'}
						/>
					</label>
					<label class="space-y-1.5">
						<span class="text-label-caps font-label-caps text-secondary uppercase">{t('tickets.create.brand')} *</span>
						<input
							type="text"
							name="newDeviceBrand"
							maxlength="80"
							bind:value={newDeviceBrand}
							class="w-full border border-stone rounded-input px-3.5 py-2 text-body-sm bg-white focus:outline-none focus:border-ink focus:ring-1 focus:ring-ink/10 transition-colors"
							placeholder={t('tickets.create.brandPlaceholder')}
							required={deviceMode === 'new'}
						/>
					</label>
					<label class="space-y-1.5">
						<span class="text-label-caps font-label-caps text-secondary uppercase">{t('tickets.create.model')} *</span>
						<input
							type="text"
							name="newDeviceModel"
							maxlength="120"
							bind:value={newDeviceModel}
							class="w-full border border-stone rounded-input px-3.5 py-2 text-body-sm bg-white focus:outline-none focus:border-ink focus:ring-1 focus:ring-ink/10 transition-colors"
							placeholder={t('tickets.create.modelPlaceholder')}
							required={deviceMode === 'new'}
						/>
					</label>
					<label class="space-y-1.5">
						<span class="text-label-caps font-label-caps text-secondary uppercase">{terms.primaryIdentifierLabel}</span>
						<input
							type="text"
							name="newDeviceSerialNumber"
							maxlength="120"
							class="w-full border border-stone rounded-input px-3.5 py-2 text-body-sm bg-white focus:outline-none focus:border-ink focus:ring-1 focus:ring-ink/10 transition-colors"
							placeholder={t('tickets.create.serialPlaceholder')}
						/>
					</label>
					<label class="space-y-1.5 md:col-span-2">
						<span class="text-label-caps font-label-caps text-secondary uppercase">{terms.secondaryIdentifierLabel}</span>
						<input
							type="text"
							name="newDeviceIdentifierLabel"
							maxlength="150"
							class="w-full border border-stone rounded-input px-3.5 py-2 text-body-sm bg-white focus:outline-none focus:border-ink focus:ring-1 focus:ring-ink/10 transition-colors"
							placeholder={t('tickets.create.identifierPlaceholder')}
						/>
					</label>
					<label class="space-y-1.5">
						<span class="text-label-caps font-label-caps text-secondary uppercase">{terms.meterLabel}</span>
						<input
							type="number"
							step="any"
							name="newDeviceMeterReading"
							class="w-full border border-stone rounded-input px-3.5 py-2 text-body-sm bg-white focus:outline-none focus:border-ink focus:ring-1 focus:ring-ink/10 transition-colors"
							placeholder="0"
						/>
					</label>
					<label class="space-y-1.5">
						<span class="text-label-caps font-label-caps text-secondary uppercase">{t('fieldService.meterUnit')}</span>
						<input
							type="text"
							name="newDeviceMeterUnit"
							maxlength="30"
							class="w-full border border-stone rounded-input px-3.5 py-2 text-body-sm bg-white focus:outline-none focus:border-ink focus:ring-1 focus:ring-ink/10 transition-colors"
							placeholder="km / hours"
						/>
					</label>
					<label class="space-y-1.5 md:col-span-3">
						<span class="text-label-caps font-label-caps text-secondary uppercase">{t('tickets.create.deviceNotes')}</span>
						<textarea
							name="newDeviceNotes"
							maxlength="2000"
							rows="2"
							class="w-full border border-stone rounded-input p-3 text-body-sm bg-white focus:outline-none focus:border-ink focus:ring-1 focus:ring-ink/10 transition-colors"
							placeholder={t('tickets.create.physicalConditionPlaceholder')}
						></textarea>
					</label>
					{#if intakeAssetFields.length > 0}
						<div class="md:col-span-3 pt-2 border-t border-stone/60">
							<p class="text-label-caps font-label-caps text-secondary uppercase mb-3">{t('fieldService.customFieldsSection')}</p>
							<DynamicFieldRenderer fields={intakeAssetFields} bind:values={assetFieldValues} locale={data.locale} namePrefix="assetField" />
						</div>
					{/if}
				</div>
			{/if}
		</section>

		<!-- Servora Field Service, Internal Mode & Blueprint Dynamic Sections -->
		<section class="bg-white border border-stone rounded-xl p-5 sm:p-6 space-y-5 shadow-2xs">
			<div class="border-b border-stone/70 pb-3">
				<h2 class="text-subheading font-semibold text-primary">{t('fieldService.sectionTitle')}</h2>
			</div>
			<div class="grid grid-cols-1 md:grid-cols-3 gap-4">
				<label class="space-y-1.5">
					<span class="text-label-caps font-label-caps text-secondary uppercase">{t('fieldService.locationType')}</span>
					<select
						name="serviceLocationType"
						bind:value={serviceLocationType}
						class="w-full border border-stone rounded-input px-3.5 py-2 text-body-sm bg-white focus:outline-none focus:border-ink"
					>
						<option value="InShop">{t('fieldService.inShop')}</option>
						<option value="OnSite">{t('fieldService.onSite')}</option>
						<option value="PickupDelivery">{t('fieldService.pickupDelivery')}</option>
					</select>
				</label>

				{#if serviceLocationType !== 'InShop'}
					<label class="space-y-1.5">
						<span class="text-label-caps font-label-caps text-secondary uppercase">{t('fieldService.siteLocation')}</span>
						<select name="siteLocationId" class="w-full border border-stone rounded-input px-3.5 py-2 text-body-sm bg-white focus:outline-none focus:border-ink">
							<option value="">—</option>
							{#each filteredLocations as loc (loc.id)}
								<option value={loc.id}>{loc.label} — {loc.address}</option>
							{/each}
						</select>
					</label>
					<label class="space-y-1.5">
						<span class="text-label-caps font-label-caps text-secondary uppercase">{t('fieldService.customSiteAddress')}</span>
						<input type="text" name="siteAddressSnapshot" maxlength="500" class="w-full border border-stone rounded-input px-3.5 py-2 text-body-sm bg-white focus:outline-none focus:border-ink" />
					</label>
					<label class="space-y-1.5">
						<span class="text-label-caps font-label-caps text-secondary uppercase">{t('fieldService.scheduledStart')}</span>
						<input type="datetime-local" name="scheduledVisitStart" class="w-full border border-stone rounded-input px-3.5 py-2 text-body-sm bg-white focus:outline-none focus:border-ink" />
					</label>
					<label class="space-y-1.5">
						<span class="text-label-caps font-label-caps text-secondary uppercase">{t('fieldService.scheduledEnd')}</span>
						<input type="datetime-local" name="scheduledVisitEnd" class="w-full border border-stone rounded-input px-3.5 py-2 text-body-sm bg-white focus:outline-none focus:border-ink" />
					</label>
				{/if}

				<label class="space-y-1.5">
					<span class="text-label-caps font-label-caps text-secondary uppercase">{t('fieldService.slaDueAt')}</span>
					<input type="datetime-local" name="slaDueAt" class="w-full border border-stone rounded-input px-3.5 py-2 text-body-sm bg-white focus:outline-none focus:border-ink" />
				</label>
			</div>

			{#if data.user.workspace?.operatingMode !== 'Commercial'}
				<input type="hidden" name="isInternalWorkOrderPresent" value="1" />
				<div class="space-y-3 rounded-lg border border-stone/70 bg-surface p-3.5">
					<label class="flex items-center gap-2 text-sm font-medium text-ink">
						<input type="checkbox" name="isInternalWorkOrder" bind:checked={isInternalWorkOrder} class="h-4 w-4 rounded border-stone text-claytile" />
						<span>{t('fieldService.internalWorkOrder')}</span>
					</label>
					{#if isInternalWorkOrder}
						<div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
							<input type="text" name="departmentCode" maxlength="50" placeholder={t('fieldService.departmentCode')} class="rounded-lg border border-stone bg-white px-3 py-1.5 text-xs" />
							<input type="text" name="costCenter" maxlength="60" placeholder={t('fieldService.costCenter')} class="rounded-lg border border-stone bg-white px-3 py-1.5 text-xs" />
						</div>
					{/if}
				</div>
			{/if}

			{#if intakeTicketFields.length > 0}
				<div class="pt-2 border-t border-stone/60">
					<p class="text-label-caps font-label-caps text-secondary uppercase mb-3">{t('fieldService.customFieldsSection')}</p>
					<DynamicFieldRenderer fields={intakeTicketFields} bind:values={ticketFieldValues} locale={data.locale} namePrefix="ticketField" />
				</div>
			{/if}
		</section>

		<!-- Section 3: Complaint & Diagnosis -->
		<section class="bg-white border border-stone rounded-xl p-5 sm:p-6 space-y-5 shadow-2xs">
			<div class="border-b border-stone/70 pb-3">
				<h2 class="text-subheading font-semibold text-primary">{t('tickets.detail.complaint')} & {t('tickets.detail.initialDiagnosis')}</h2>
				<p class="text-caption text-secondary mt-0.5">{t('tickets.create.complaintSectionHint')}</p>
			</div>

			<label class="space-y-1.5 block">
				<span class="text-label-caps font-label-caps text-secondary uppercase">{t('tickets.detail.complaint')} *</span>
				<textarea
					name="complaint"
					maxlength="2000"
					bind:value={complaintText}
					rows="3"
					class="w-full border border-stone rounded-input p-3 text-body-sm bg-white focus:outline-none focus:border-ink focus:ring-1 focus:ring-ink/10 transition-colors"
					placeholder={t('tickets.create.complaintPlaceholder')}
					required
				></textarea>
			</label>

			<label class="space-y-1.5 block">
				<span class="text-label-caps font-label-caps text-secondary uppercase">{t('tickets.detail.initialDiagnosis')}</span>
				<textarea
					name="initialDiagnosis"
					maxlength="2000"
					bind:value={initialDiagnosisText}
					rows="2"
					class="w-full border border-stone rounded-input p-3 text-body-sm bg-white focus:outline-none focus:border-ink focus:ring-1 focus:ring-ink/10 transition-colors"
					placeholder={t('tickets.create.initialDiagnosisPlaceholder')}
				></textarea>
			</label>
		</section>

		<!-- Section 4: Physical Custody & Evidence -->
		<section class="bg-white border border-stone rounded-xl p-5 sm:p-6 space-y-5 shadow-2xs">
			<div class="border-b border-stone/70 pb-3">
				<h2 class="text-subheading font-semibold text-primary">{t('tickets.create.intakeDetails')}</h2>
				<p class="text-caption text-secondary mt-0.5">{t('tickets.create.intakeDetailsHint')}</p>
			</div>

			<div class="grid grid-cols-1 md:grid-cols-2 gap-4">
				<div class="space-y-1.5">
					<span class="text-label-caps font-label-caps text-secondary uppercase">{t('tickets.create.intakeDate')}</span>
					<DatePicker name="createdAt" bind:value={createdAt} />
				</div>
			</div>

			{#if features.showPhysicalConditionNotes}
				<label class="space-y-1.5 block">
					<span class="text-label-caps font-label-caps text-secondary uppercase">{t('tickets.detail.physicalConditionNotes')}</span>
					<textarea
						name="physicalConditionNotes"
						rows="2"
						class="w-full border border-stone rounded-input p-3 text-body-sm bg-white focus:outline-none focus:border-ink focus:ring-1 focus:ring-ink/10 transition-colors"
						placeholder={t('tickets.create.physicalConditionPlaceholder')}
					></textarea>
				</label>
			{/if}

			{#if features.showAccessories}
				<!-- Accessories received -->
				<div class="space-y-2">
					<span class="text-label-caps font-label-caps text-secondary uppercase">{t('tickets.detail.accessorySummary')}</span>
					
					{#if accessories.length > 0}
						<div class="flex flex-wrap gap-1.5 p-2 border border-stone/60 rounded-input bg-ash/20">
							{#each accessories as item, index (item)}
								<span class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-white border border-stone text-caption font-medium text-ink shadow-2xs">
									<span>{item}</span>
									<button
										type="button"
										class="text-secondary hover:text-error text-xs font-bold leading-none cursor-pointer w-4 h-4 inline-flex items-center justify-center rounded-full hover:bg-stone/30 transition-colors"
										onclick={() => removeAccessory(index)}
										aria-label={t('tickets.create.removeAccessory')}
									>
										×
									</button>
								</span>
							{/each}
						</div>
					{/if}

					<div class="flex items-center gap-2">
						<input
							type="text"
							bind:value={accessoryInput}
							onkeydown={handleAccessoryKeydown}
							class="flex-1 h-10 border border-stone rounded-input px-3.5 text-body-sm bg-white focus:outline-none focus:border-ink focus:ring-1 focus:ring-ink/10 transition-colors"
							placeholder={`${t('tickets.create.accessoryPlaceholder')} ${t('tickets.create.pressEnterToAdd')}`}
						/>
						<button
							type="button"
							class="h-10 px-3.5 rounded-button border border-stone bg-ash/50 text-ink hover:bg-ash active:scale-95 text-caption font-medium transition-all cursor-pointer shrink-0 whitespace-nowrap"
							onclick={() => {
								addAccessory(accessoryInput);
								accessoryInput = '';
							}}
						>
							{t('tickets.create.addAccessory')}
						</button>
					</div>

					{#if quickAccessoryList.length > 0}
						<div class="flex flex-wrap items-center gap-1.5 pt-1">
							<span class="text-caption text-secondary font-medium mr-1">{t('tickets.create.quickAccessories')}</span>
							{#each quickAccessoryList as item (item)}
								<button
									type="button"
									class="inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-caption border border-stone/80 bg-white text-secondary hover:text-ink hover:border-ink/40 active:scale-95 transition-all cursor-pointer font-medium disabled:opacity-40 disabled:pointer-events-none"
									disabled={accessories.includes(item)}
									onclick={() => addAccessory(item)}
								>
									<span>+</span> {item}
								</button>
							{/each}
						</div>
					{/if}

					<input type="hidden" name="accessorySummary" value={accessories.join(', ')} />
				</div>
			{/if}

			<!-- Intake Photos Upload & Preview Dropzone -->
			<div class="space-y-2">
				<div class="flex flex-wrap items-center justify-between gap-2">
					<span class="text-label-caps font-label-caps text-secondary uppercase">{t('tickets.create.intakePhotos')}</span>
					{#if data.requirePhotoEvidenceOnIntake}
						<span class="text-caption font-semibold text-warning">{t('tickets.create.intakePhotoRequired')}</span>
					{/if}
				</div>

				<!-- Hidden file input -->
				<input
					bind:this={fileInputRef}
					type="file"
					accept="image/jpeg,image/png,image/webp"
					multiple
					class="sr-only"
					onchange={(e) => {
						const input = e.currentTarget as HTMLInputElement;
						handleFiles(input.files);
						input.value = '';
					}}
				/>

				<!-- Interactive Dropzone -->
				<div
					role="button"
					tabindex="0"
					class="border-2 border-dashed rounded-lg p-6 text-center cursor-pointer transition-all flex flex-col items-center justify-center gap-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ink/40 {isDragging ? 'border-ink bg-ash/60' : 'border-stone/80 hover:border-ink/50 bg-ash/20 hover:bg-ash/40'}"
					ondragover={handleDragOver}
					ondragleave={handleDragLeave}
					ondrop={handleDrop}
					onclick={() => fileInputRef?.click()}
					onkeydown={(e) => {
						if (e.key === 'Enter' || e.key === ' ') {
							e.preventDefault();
							fileInputRef?.click();
						}
					}}
				>
					<div class="w-10 h-10 rounded-full bg-white border border-stone flex items-center justify-center text-secondary shadow-2xs">
						<svg class="w-5 h-5 text-secondary" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8">
							<path stroke-linecap="round" stroke-linejoin="round" d="M6.827 6.175A2.31 2.31 0 015.186 7.23c-.38.054-.757.112-1.134.175C2.999 7.58 2.25 8.507 2.25 9.574V18a2.25 2.25 0 002.25 2.25h15A2.25 2.25 0 0021.75 18V9.574c0-1.067-.75-1.994-1.802-2.169a47.865 47.865 0 00-1.134-.175 2.31 2.31 0 01-1.64-1.055l-.822-1.316a2.192 2.192 0 00-1.736-1.039 48.774 48.774 0 00-5.232 0 2.192 2.192 0 00-1.736 1.039l-.821 1.316z" />
							<path stroke-linecap="round" stroke-linejoin="round" d="M16.5 12.75a4.5 4.5 0 11-9 0 4.5 4.5 0 019 0zM18.75 10.5h.008v.008h-.008V10.5z" />
						</svg>
					</div>
					<p class="text-body-sm font-medium text-primary">{t('tickets.create.dropzoneHint')}</p>
					<p class="text-caption text-secondary">{t('tickets.create.dropzoneLimit')}</p>
				</div>

				{#if photoError}
					<p class="text-caption text-error font-medium">{photoError}</p>
				{/if}

				<!-- Thumbnail Previews -->
				{#if photoPreviews.length > 0}
					<div class="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 gap-3 pt-2">
						{#each photoPreviews as photo (photo.id)}
							<div class="relative group rounded-lg border border-stone bg-white p-1.5 shadow-2xs space-y-1.5">
								<div class="relative aspect-video rounded overflow-hidden bg-ash">
									<img src={photo.url} alt={photo.name} class="w-full h-full object-cover" />
									{#if photo.uploaded}
										<span class="absolute top-1 right-1 px-1.5 py-0.5 rounded bg-emerald-600 text-white text-[10px] font-bold uppercase shadow-xs">
											{t('tickets.create.photoUploadedBadge')}
										</span>
									{:else}
										<button
											type="button"
											class="absolute top-1 right-1 w-6 h-6 rounded-full bg-ink/80 text-white flex items-center justify-center hover:bg-error cursor-pointer transition-colors shadow-xs"
											onclick={(e) => {
												e.stopPropagation();
												removePhoto(photo.id);
											}}
											aria-label={t('tickets.create.removePhoto')}
										>
											×
										</button>
									{/if}
								</div>
								<div class="px-1 flex items-center justify-between gap-1 text-[11px] text-secondary" title={photo.name}>
									<div class="min-w-0">
										<span class="font-medium text-primary block truncate">{photo.name}</span>
										<span>{photo.size}</span>
									</div>
									<button
										type="button"
										disabled={photo.uploaded}
										class={`shrink-0 px-1.5 py-0.5 rounded text-[10px] font-bold uppercase transition-colors disabled:opacity-60 disabled:cursor-default ${photo.isPublicVisible ? 'bg-success/15 text-success hover:bg-success/25' : 'bg-stone/50 text-secondary hover:bg-stone/80'} ${photo.uploaded ? '' : 'cursor-pointer'}`}
										onclick={(e) => {
											e.stopPropagation();
											togglePhotoPublicVisibility(photo.id);
										}}
										title={t('tickets.attachments.publicVisible')}
									>
										{photo.isPublicVisible ? t('tickets.attachments.publicBadge') : t('tickets.attachments.internalBadge')}
									</button>
								</div>
								{#if features.attachmentTypes.length > 1}
									<select
										aria-label={t('tickets.create.intakePhotos')}
										disabled={photo.uploaded}
										bind:value={photo.attachmentType}
										onclick={(e) => e.stopPropagation()}
										class="w-full rounded border border-stone/80 bg-surface px-1.5 py-1 text-[11px] text-ink focus:border-ink focus:outline-none disabled:opacity-60"
									>
										{#each features.attachmentTypes as attType (attType)}
											<option value={attType}>{attachmentTypeLabel(attType)}</option>
										{/each}
									</select>
								{/if}
							</div>
						{/each}
					</div>
				{/if}

				<label class="space-y-1.5 block pt-2">
					<span class="text-label-caps font-label-caps text-secondary uppercase">{t('tickets.detail.photoNotes')}</span>
					<input
						type="text"
						name="photoNotes"
						class="w-full border border-stone rounded-input px-3.5 py-2 text-body-sm bg-white focus:outline-none focus:border-ink focus:ring-1 focus:ring-ink/10 transition-colors"
						placeholder={t('tickets.create.photoNotesPlaceholder')}
					/>
				</label>
			</div>
		</section>

		<!-- Section 5: Access & Unlock Credentials (Blueprint Configurable) -->
		{#if features.showSecurityCredentials && features.securityCredentialMode !== 'None'}
			<section class="bg-white border border-stone rounded-xl p-5 sm:p-6 space-y-5 shadow-2xs">
				<div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3 border-b border-stone/70 pb-3">
					<div>
						<h2 class="text-subheading font-semibold text-primary">{t('tickets.security.title')}</h2>
						<p class="text-caption text-secondary mt-0.5">{t('tickets.security.subtitle')}</p>
					</div>
					<div class="inline-flex flex-wrap self-start sm:self-auto rounded-input border border-stone/80 bg-ash/50 p-1 text-caption font-medium shadow-2xs">
						<button
							type="button"
							class="px-3 py-1.5 rounded-button transition-all cursor-pointer {lockType === 'none' ? 'bg-white text-ink font-semibold shadow-xs' : 'text-secondary hover:text-ink'}"
							onclick={() => (lockType = 'none')}
						>
							{t('tickets.security.none')}
						</button>
						<button
							type="button"
							class="px-3 py-1.5 rounded-button transition-all cursor-pointer {lockType === 'passcode' ? 'bg-white text-ink font-semibold shadow-xs' : 'text-secondary hover:text-ink'}"
							onclick={() => (lockType = 'passcode')}
						>
							{t('tickets.security.passcode')}
						</button>
						{#if features.securityCredentialMode === 'PasscodeAndPattern'}
							<button
								type="button"
								class="px-3 py-1.5 rounded-button transition-all cursor-pointer {lockType === 'pattern' ? 'bg-white text-ink font-semibold shadow-xs' : 'text-secondary hover:text-ink'}"
								onclick={() => (lockType = 'pattern')}
							>
								{t('tickets.security.pattern')}
							</button>
						{/if}
					</div>
				</div>

				{#if lockType === 'passcode'}
					<div class="space-y-1.5 max-w-md">
						<span class="text-label-caps font-label-caps text-secondary uppercase">{t('tickets.security.passcodeLabel')}</span>
						<div class="relative flex items-center">
							<input
								type={showPasscode ? 'text' : 'password'}
								name="securityPasscode"
								maxlength="200"
								autocomplete="new-password"
								bind:value={securityPasscode}
								class="w-full border border-stone rounded-input px-3.5 py-2.5 text-body-sm bg-white pr-20 focus:outline-none focus:border-ink focus:ring-1 focus:ring-ink/10 transition-colors"
								placeholder={t('tickets.security.passcodePlaceholder')}
							/>
							<button
								type="button"
								class="absolute right-2 px-2.5 py-1 text-xs text-secondary hover:text-ink font-medium cursor-pointer transition-colors"
								onclick={() => (showPasscode = !showPasscode)}
							>
								{showPasscode ? t('tickets.security.hide') : t('tickets.security.show')}
							</button>
						</div>
					</div>
				{:else if lockType === 'pattern' && features.securityCredentialMode === 'PasscodeAndPattern'}
					<div class="flex flex-col sm:flex-row items-start sm:items-center gap-6 bg-ash/20 p-5 rounded-lg border border-stone/80">
						<PatternLock bind:value={securityPattern} clearLabel={t('tickets.security.clearPattern')} />
						<div class="space-y-2 text-body-sm">
							<p class="font-semibold text-primary">{t('tickets.security.pattern')}</p>
							<p class="text-secondary text-caption">{t('tickets.security.patternHint')}</p>
							{#if securityPattern}
								<div class="inline-flex items-center gap-1.5 px-3 py-1 rounded bg-ash border border-stone text-ink font-mono text-xs font-bold shadow-2xs">
									<span>{t('tickets.security.patternSequence')}:</span>
									<span>{securityPattern}</span>
								</div>
							{/if}
							<input type="hidden" name="securityPattern" value={securityPattern} />
						</div>
					</div>
				{/if}

				{#if lockType === 'none'}
					<input type="hidden" name="securityPasscode" value="" />
					<input type="hidden" name="securityPattern" value="" />
				{/if}
			</section>
		{/if}

		<!-- Section 6: Configurable Intake Inspection Checklist (Driven by Blueprint) -->
		{#if features.showPreRepairChecklist && inspectionTemplates.length > 0}
			<section class="bg-white border border-stone rounded-xl p-5 sm:p-6 shadow-2xs">
				<InspectionChecklistRenderer
					templates={inspectionTemplates}
					bind:entries={inspectionEntries}
					locale={data.locale}
				/>
			</section>
		{/if}

		<!-- Section 7: Actions & Approvals -->
		<div class="bg-white border border-stone rounded-xl p-5 sm:p-6 shadow-2xs flex flex-col sm:flex-row sm:items-center justify-between gap-4">
			<div class="flex flex-wrap items-center gap-6 text-body-sm text-secondary">
				{#if features.showCustomerApprovals && !isInternalWorkOrder}
					<label class="flex items-center gap-2 cursor-pointer select-none">
						<input type="checkbox" name="approvalRequired" class="rounded" />
						<span class="text-primary font-medium">{t('tickets.create.approvalRequired')}</span>
					</label>
					<label class="flex items-center gap-2 cursor-pointer select-none">
						<input type="checkbox" name="isPreApproved" class="rounded" />
						<span class="text-primary font-medium">{t('tickets.create.preApproved')}</span>
					</label>
				{/if}
			</div>

			<div class="flex flex-col-reverse sm:flex-row items-stretch sm:items-center justify-end gap-3">
				<a
					href="/app/tickets"
					class="px-4 h-10 rounded-button border border-stone bg-white text-secondary hover:text-primary hover:bg-ash active:scale-[0.99] font-medium transition-all text-body-sm inline-flex items-center justify-center"
				>
					{t('settings.cancel')}
				</a>
				<button
					class="px-5 h-10 rounded-button bg-ink text-white font-medium hover:bg-ink/90 active:scale-[0.99] transition-all disabled:opacity-50 disabled:cursor-not-allowed shadow-xs inline-flex items-center justify-center gap-2 text-body-sm cursor-pointer"
					type="submit"
					disabled={customers.length === 0 || submitting}
				>
					{#if submitting}
						<svg class="w-4 h-4 animate-spin text-white" viewBox="0 0 24 24" fill="none">
							<circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
							<path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
						</svg>
						<span>{uploadProgressText || t('tickets.create.submitting')}</span>
					{:else if createdTicketId}
						<span>{t('tickets.create.retryPhotoUpload')}</span>
					{:else}
						<span>{t('tickets.create.submit')} — {terms.ticketSingular}</span>
					{/if}
				</button>
			</div>
		</div>
	</form>

	<!-- Quick New Customer Modal -->
	{#if showQuickCustomerModal}
		<div
			class="fixed inset-0 z-[60] flex items-center justify-center p-4 bg-ink/40 backdrop-blur-xs transition-opacity"
			onclick={(e) => {
				if (e.target === e.currentTarget && !quickSaving) showQuickCustomerModal = false;
			}}
			role="presentation"
		>
			<div
				class="bg-white rounded-xl border border-stone shadow-xl max-w-lg w-full p-6 space-y-5 animate-in fade-in zoom-in-95 duration-150"
				role="dialog"
				aria-modal="true"
				aria-labelledby="quick-customer-title"
			>
				<div class="flex items-start justify-between gap-4 border-b border-stone/70 pb-3">
					<div>
						<h3 id="quick-customer-title" class="text-heading font-semibold text-primary">
							{t('tickets.create.quickCustomerTitle', { requester: terms.requesterSingular })}
						</h3>
						<p class="text-caption text-secondary mt-0.5">
							{t('tickets.create.quickCustomerSubtitle')}
						</p>
					</div>
					<button
						type="button"
						class="p-1 rounded-md text-secondary hover:text-ink hover:bg-ash transition-colors cursor-pointer"
						onclick={() => (showQuickCustomerModal = false)}
						disabled={quickSaving}
						aria-label={t('common.close')}
					>
						<span class="material-symbols-outlined text-[20px]">close</span>
					</button>
				</div>

				{#if quickError}
					<div class="rounded-lg border border-error/30 bg-error/5 p-3 text-body-sm text-error flex items-start gap-2">
						<span class="material-symbols-outlined text-[18px] text-error shrink-0 mt-0.5">error</span>
						<span>{quickError}</span>
					</div>
				{/if}

				<form onsubmit={handleQuickCustomerSubmit} class="space-y-4">
					<label class="space-y-1 block">
						<span class="text-label-caps font-label-caps text-secondary uppercase text-[11px] font-bold tracking-wider">
							{t('customers.form.requesterType')}
						</span>
						<select
							bind:value={quickRequesterType}
							class="w-full h-10 border border-stone rounded-input px-3 text-body-sm bg-white focus:outline-none focus:border-ink"
						>
							<option value="Customer">{t('customers.type.Customer')}</option>
							<option value="Department">{t('customers.type.Department')}</option>
							<option value="InternalEmployee">{t('customers.type.InternalEmployee')}</option>
							<option value="Branch">{t('customers.type.Branch')}</option>
						</select>
					</label>

					<div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
						<label class="space-y-1 block sm:col-span-1">
							<span class="text-label-caps font-label-caps text-secondary uppercase text-[11px] font-bold tracking-wider">
								{t('customers.form.fullName')} *
							</span>
							<input
								type="text"
								bind:value={quickFullName}
								required
								placeholder={t('customers.form.fullNamePlaceholder')}
								class="w-full h-10 border border-stone rounded-input px-3 text-body-sm bg-white focus:outline-none focus:border-ink focus:ring-1 focus:ring-ink/10 transition-colors"
							/>
						</label>

						<label class="space-y-1 block sm:col-span-1">
							<span class="text-label-caps font-label-caps text-secondary uppercase text-[11px] font-bold tracking-wider">
								{t('customers.form.phoneNumber')} {quickRequesterType === 'Customer' ? '*' : ''}
							</span>
							<input
								type="tel"
								value={quickPhoneNumber}
								oninput={(e) => {
									quickPhoneNumber = sanitizePhoneNumber((e.currentTarget as HTMLInputElement).value);
									(e.currentTarget as HTMLInputElement).value = quickPhoneNumber;
								}}
								onpaste={(e) => {
									handlePhonePaste(e);
									quickPhoneNumber = (e.currentTarget as HTMLInputElement).value;
								}}
								required={quickRequesterType === 'Customer'}
								placeholder={quickRequesterType === 'Customer' ? t('customers.form.phonePlaceholder') : t('customers.form.phoneOptionalInternal')}
								class="w-full h-10 border border-stone rounded-input px-3 text-body-sm font-mono bg-white focus:outline-none focus:border-ink focus:ring-1 focus:ring-ink/10 transition-colors"
							/>
						</label>
					</div>

					{#if quickRequesterType !== 'Customer'}
						<div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
							<label class="space-y-1 block">
								<span class="text-label-caps font-label-caps text-secondary uppercase text-[11px] font-bold tracking-wider">
									{t('customers.form.departmentName')}
								</span>
								<input
									type="text"
									bind:value={quickDepartmentName}
									placeholder={t('customers.form.departmentNamePlaceholder')}
									class="w-full h-10 border border-stone rounded-input px-3 text-body-sm bg-white focus:outline-none focus:border-ink"
								/>
							</label>
							<label class="space-y-1 block">
								<span class="text-label-caps font-label-caps text-secondary uppercase text-[11px] font-bold tracking-wider">
									{t('customers.form.departmentCode')}
								</span>
								<input
									type="text"
									bind:value={quickDepartmentCode}
									placeholder={t('customers.form.departmentCodePlaceholder')}
									class="w-full h-10 border border-stone rounded-input px-3 text-body-sm bg-white focus:outline-none focus:border-ink"
								/>
							</label>
							<label class="space-y-1 block">
								<span class="text-label-caps font-label-caps text-secondary uppercase text-[11px] font-bold tracking-wider">
									{t('customers.form.costCenter')}
								</span>
								<input
									type="text"
									bind:value={quickCostCenter}
									placeholder={t('customers.form.costCenterPlaceholder')}
									class="w-full h-10 border border-stone rounded-input px-3 text-body-sm bg-white focus:outline-none focus:border-ink"
								/>
							</label>
							<label class="space-y-1 block">
								<span class="text-label-caps font-label-caps text-secondary uppercase text-[11px] font-bold tracking-wider">
									{t('customers.form.employeeId')}
								</span>
								<input
									type="text"
									bind:value={quickEmployeeId}
									placeholder={t('customers.form.employeeIdPlaceholder')}
									class="w-full h-10 border border-stone rounded-input px-3 text-body-sm bg-white focus:outline-none focus:border-ink"
								/>
							</label>
						</div>
					{:else}
						<label class="space-y-1 block">
							<span class="text-label-caps font-label-caps text-secondary uppercase text-[11px] font-bold tracking-wider">
								{t('customers.form.companyName')}
							</span>
							<input
								type="text"
								bind:value={quickCompanyName}
								placeholder={t('customers.form.companyNamePlaceholder')}
								class="w-full h-10 border border-stone rounded-input px-3 text-body-sm bg-white focus:outline-none focus:border-ink"
							/>
						</label>
					{/if}

					<div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
						<label class="space-y-1 block sm:col-span-1">
							<span class="text-label-caps font-label-caps text-secondary uppercase text-[11px] font-bold tracking-wider">
								{t('customers.form.email')}
							</span>
							<input
								type="email"
								bind:value={quickEmail}
								placeholder={t('customers.form.emailPlaceholder')}
								class="w-full h-10 border border-stone rounded-input px-3 text-body-sm bg-white focus:outline-none focus:border-ink focus:ring-1 focus:ring-ink/10 transition-colors"
							/>
						</label>

						<label class="space-y-1 block sm:col-span-1">
							<span class="text-label-caps font-label-caps text-secondary uppercase text-[11px] font-bold tracking-wider">
								{t('customers.form.address')}
							</span>
							<input
								type="text"
								bind:value={quickAddress}
								placeholder={t('customers.form.addressPlaceholder')}
								class="w-full h-10 border border-stone rounded-input px-3 text-body-sm bg-white focus:outline-none focus:border-ink focus:ring-1 focus:ring-ink/10 transition-colors"
							/>
						</label>
					</div>

					<label class="space-y-1 block">
						<span class="text-label-caps font-label-caps text-secondary uppercase text-[11px] font-bold tracking-wider">
							{t('customers.form.notes')}
						</span>
						<input
							type="text"
							bind:value={quickNotes}
							placeholder={t('customers.form.notesPlaceholder')}
							class="w-full h-10 border border-stone rounded-input px-3 text-body-sm bg-white focus:outline-none focus:border-ink focus:ring-1 focus:ring-ink/10 transition-colors"
						/>
					</label>

					<div class="flex items-center justify-end gap-2.5 pt-3 border-t border-stone/70">
						<button
							type="button"
							class="px-4 h-10 rounded-button border border-stone bg-white text-secondary hover:text-ink hover:bg-ash text-body-sm font-medium transition-colors cursor-pointer"
							onclick={() => (showQuickCustomerModal = false)}
							disabled={quickSaving}
						>
							{t('tickets.create.quickCustomerCancel')}
						</button>
						<button
							type="submit"
							class="px-5 h-10 rounded-button bg-ink text-white font-medium text-body-sm hover:bg-ink/90 active:scale-95 transition-all shadow-xs cursor-pointer flex items-center gap-2"
							disabled={quickSaving}
						>
							{#if quickSaving}
								<span class="material-symbols-outlined text-[18px] animate-spin">progress_activity</span>
								<span>{t('tickets.create.quickCustomerSaving')}</span>
							{:else}
								<span class="material-symbols-outlined text-[18px]">person_add</span>
								<span>{t('tickets.create.quickCustomerSubmit')}</span>
							{/if}
						</button>
					</div>
				</form>
			</div>
		</div>
	{/if}

	<!-- Unsaved Draft Warning Dialog -->
	{#if showUnsavedModal}
		<div
			class="fixed inset-0 z-[60] flex items-center justify-center p-4 bg-ink/40 backdrop-blur-xs transition-opacity"
			onclick={(e) => {
				if (e.target === e.currentTarget) cancelLeave();
			}}
			role="presentation"
		>
			<div
				class="bg-white rounded-xl border border-stone shadow-xl max-w-md w-full p-6 space-y-4 animate-in fade-in zoom-in-95 duration-150"
				role="dialog"
				aria-modal="true"
				aria-labelledby="unsaved-draft-title"
			>
				<div class="flex items-start gap-3">
					<div class="w-10 h-10 rounded-full bg-amber-500/15 flex items-center justify-center text-amber-700 shrink-0">
						<span class="material-symbols-outlined text-[24px]">warning</span>
					</div>
					<div class="space-y-1">
						<h3 id="unsaved-draft-title" class="text-heading font-semibold text-primary">
							{t('tickets.create.leaveConfirmTitle')}
						</h3>
						<p class="text-body-sm text-secondary">
							{t('tickets.create.leaveConfirmMessage')}
						</p>
					</div>
				</div>
				<div class="flex items-center justify-end gap-2.5 pt-3 border-t border-stone/70">
					<button
						type="button"
						class="px-4 h-10 rounded-button border border-stone bg-white text-primary hover:bg-ash text-body-sm font-medium transition-colors cursor-pointer"
						onclick={cancelLeave}
					>
						{t('tickets.create.stayOnIntake')}
					</button>
					<button
						type="button"
						class="px-4 h-10 rounded-button bg-error text-white hover:bg-error/90 text-body-sm font-medium transition-all shadow-xs cursor-pointer"
						onclick={confirmLeave}
					>
						{t('tickets.create.leaveAnyway')}
					</button>
				</div>
			</div>
		</div>
	{/if}
</div>
