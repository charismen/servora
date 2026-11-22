<script lang="ts">
	import { enhance } from '$app/forms';
	import { createTranslator, formatCurrency, formatDate, resolveTerminology } from '$lib/i18n';
	import { apiAssetUrl } from '$lib/media';
	import { formatEnhanceUnexpectedError } from '$lib/utils/http';
	import type { ActionData, PageData } from './$types';

	let { data, form }: { data: PageData; form: ActionData } = $props();
	const t = $derived(createTranslator(data.locale));
	const tracking = $derived(data.tracking);
	const terms = $derived(resolveTerminology(tracking?.terminology, data.locale));
	const shopLogoUrl = $derived(apiAssetUrl(tracking?.shopLogoUrl));
	let verifying = $state(false);
	let verifyError = $state('');
	let phoneLastFour = $state('');
	let startingCheckout = $state(false);
	let checkoutError = $state('');
	const unlockedAttachments = $derived(form?.attachments ?? tracking?.attachments ?? []);
	const verifiedPhone = $derived(form?.verifiedPhoneLastFour ?? '');
	const isUnlocked = $derived(Boolean(form?.verified) || unlockedAttachments.length > 0);
	const hasPublicPhotos = $derived((tracking?.publicPhotoCount ?? 0) > 0 || unlockedAttachments.length > 0);
	const onlinePaymentEnabled = $derived(Boolean(tracking?.onlinePaymentEnabled));
	const paymentRequiresPhone = $derived(Boolean(tracking?.paymentRequiresPhoneVerification ?? true));
	const unlockedPaymentSummary = $derived(
		form?.paymentSummary ??
			(onlinePaymentEnabled && !paymentRequiresPhone
				? {
						onlinePaymentEnabled: true,
						requiresPhoneVerification: false,
						paymentStatus: tracking?.paymentStatus ?? 'Unpaid',
						totalAmount: tracking?.totalAmount ?? null,
						amountPaid: tracking?.amountPaid ?? 0,
						remainingBalance: tracking?.remainingBalance ?? 0,
						currencyCode: tracking?.currencyCode ?? 'IDR',
						activePaymentUrl: tracking?.activePaymentUrl ?? null,
						activePaymentExpiresAt: tracking?.activePaymentExpiresAt ?? null
					}
				: null)
	);
	const isPaymentUnlocked = $derived(Boolean(unlockedPaymentSummary));

	$effect(() => {
		if (form?.checkoutUrl && typeof window !== 'undefined') {
			window.location.assign(form.checkoutUrl);
		}
	});

	// Lightbox state
	let activePhotoIndex = $state<number | null>(null);
	const currentPhoto = $derived(
		activePhotoIndex !== null && unlockedAttachments[activePhotoIndex]
			? unlockedAttachments[activePhotoIndex]
			: null
	);

	function openLightbox(index: number) {
		activePhotoIndex = index;
	}

	function closeLightbox() {
		activePhotoIndex = null;
	}

	function nextPhoto() {
		if (activePhotoIndex === null || unlockedAttachments.length <= 1) return;
		activePhotoIndex = (activePhotoIndex + 1) % unlockedAttachments.length;
	}

	function prevPhoto() {
		if (activePhotoIndex === null || unlockedAttachments.length <= 1) return;
		activePhotoIndex = (activePhotoIndex - 1 + unlockedAttachments.length) % unlockedAttachments.length;
	}

	function handleKeydown(e: KeyboardEvent) {
		if (activePhotoIndex === null) return;
		if (e.key === 'Escape') closeLightbox();
		if (e.key === 'ArrowRight') nextPhoto();
		if (e.key === 'ArrowLeft') prevPhoto();
	}

	function formatEventType(type: string) {
		const key = 'history.event.' + type;
		const translated = t(key);
		if (translated !== key) return translated;
		return type.replace(/([A-Z])/g, ' $1').trim();
	}

	function formatNote(note: string | null | undefined) {
		if (!note) return '';
		if (note === 'Ticket created.') return t('activity.action.TicketCreated');
		if (note === 'Ticket note added.') return t('activity.action.TicketNoteAdded');
		if (note === 'WhatsApp message generated.') return t('activity.action.WhatsAppMessageGenerated');
		if (note === 'Receipt printed.') return t('history.event.ReceiptPrinted');
		if (note === 'Ticket details updated.') return t('history.event.Updated');
		return note;
	}

	function getHistoryDetail(item: NonNullable<typeof tracking>['history'][number]) {
		const title = formatEventType(item.eventType);
		const transition = [item.fromStatus, item.toStatus].filter(Boolean).join(' → ');
		const note = item.note ? formatNote(item.note) : '';
		const cleanNote = note.trim().toLowerCase() === title.trim().toLowerCase() ? '' : note;
		if (transition && cleanNote) return `${transition} · ${cleanNote}`;
		return cleanNote || transition;
	}

	function getLifecycleStep(statusName: string = ''): number {
		const lower = statusName.toLowerCase();
		if (lower.includes('complete') || lower.includes('closed') || lower.includes('picked') || lower.includes('selesai')) return 4;
		if (lower.includes('ready') || lower.includes('siap')) return 3;
		if (lower.includes('progress') || lower.includes('repair') || lower.includes('kerjakan') || lower.includes('diagnos') || lower.includes('approval') || lower.includes('waiting') || lower.includes('part')) return 2;
		return 1;
	}

	const currentStep = $derived(getLifecycleStep(tracking?.status));
</script>

<svelte:head>
	<title>{tracking ? `${tracking.ticketNumber} · ${t('tracking.title')}` : t('tracking.notFoundTitle')}</title>
</svelte:head>

<svelte:window onkeydown={handleKeydown} />

<main class="min-h-screen bg-ash/60 px-3.5 py-5 sm:px-6 sm:py-12">
	<div class="mx-auto max-w-2xl space-y-4 sm:space-y-6">
		{#if data.notFound || !tracking}
			<section class="bg-white border border-stone rounded-xl p-6 sm:p-12 shadow-2xs text-center space-y-3">
				<div class="w-12 h-12 rounded-full bg-ash border border-stone flex items-center justify-center mx-auto text-secondary">
					<svg class="w-6 h-6" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
						<circle cx="12" cy="12" r="10" />
						<line x1="12" y1="8" x2="12" y2="12" />
						<line x1="12" y1="16" x2="12.01" y2="16" />
					</svg>
				</div>
				<p class="m-0 text-caption uppercase tracking-wider text-secondary font-semibold">{t('tracking.title')}</p>
				<h1 class="m-0 font-headline-sm text-xl sm:text-headline-sm font-bold text-primary">{t('tracking.notFoundTitle')}</h1>
				<p class="m-0 text-body-sm text-secondary max-w-md mx-auto">{t('tracking.notFoundCopy')}</p>
			</section>
		{:else}
			<!-- Header Card: Shop Identity & Ticket Info -->
			<section class="bg-white border border-stone rounded-xl p-5 sm:p-8 shadow-2xs space-y-5 sm:space-y-6">
				<!-- Brand Header -->
				<div class="flex flex-wrap items-center justify-between gap-x-3 gap-y-2.5 border-b border-stone/70 pb-4 sm:pb-5">
					<div class="flex items-center gap-3 min-w-0 flex-1">
						{#if shopLogoUrl}
							<img src={shopLogoUrl} alt={tracking.shopName} class="h-10 w-10 sm:h-11 sm:w-11 rounded-lg border border-stone/80 object-contain p-1 shrink-0 bg-white" />
						{:else}
							<div class="h-10 w-10 sm:h-11 sm:w-11 rounded-lg border border-stone bg-ash/50 flex items-center justify-center text-primary font-bold text-xs sm:text-sm shrink-0">
								{tracking.shopName.slice(0, 2).toUpperCase()}
							</div>
						{/if}
						<div class="min-w-0 flex-1">
							<h1 class="m-0 font-subheading text-base sm:text-subheading font-bold text-primary leading-tight tracking-tight truncate">
								{tracking.shopName}
							</h1>
							<p class="m-0 text-caption text-secondary flex items-center gap-1.5 mt-0.5 leading-snug">
								<span class="inline-block w-1.5 h-1.5 rounded-full bg-emerald-600 shrink-0"></span>
								<span class="truncate">{t('tracking.title')}</span>
							</p>
						</div>
					</div>
					<span
						class="inline-flex items-center gap-1.5 px-2.5 py-1 sm:px-3 sm:py-1 rounded-full text-[11px] sm:text-caption font-semibold uppercase tracking-wider border shrink-0 max-w-full leading-none"
						style={`background-color: ${tracking.color}14; color: ${tracking.color}; border-color: ${tracking.color}38;`}
					>
						<span class="w-1.5 h-1.5 rounded-full shrink-0" style={`background-color: ${tracking.color};`}></span>
						<span class="truncate">{tracking.status}</span>
					</span>
				</div>

				<!-- Ticket & Device Summary -->
				<div class="grid grid-cols-1 sm:grid-cols-2 gap-3.5 sm:gap-6 rounded-lg bg-ash/40 border border-stone/70 p-3.5 sm:p-4">
					<div class="min-w-0">
						<span class="text-[11px] font-semibold text-secondary uppercase tracking-wider block mb-1">{t('tickets.list.ticketNumber')}</span>
						<p class="m-0 text-base sm:text-lg font-bold text-primary tracking-tight font-mono tabular-nums break-all">{tracking.ticketNumber}</p>
					</div>
					<div class="min-w-0 border-t border-stone/60 pt-3 sm:border-t-0 sm:border-l sm:border-stone/60 sm:pt-0 sm:pl-5">
						<span class="text-[11px] font-semibold text-secondary uppercase tracking-wider block mb-1">{terms.assetSingular}</span>
						<p class="m-0 text-body-sm sm:text-body-base font-semibold text-primary break-words">{tracking.deviceSnapshot}</p>
					</div>
				</div>

				{#if (tracking.serviceLocationType && tracking.serviceLocationType !== 'InShop') || (tracking.publicFields && tracking.publicFields.length > 0)}
					<div class="grid grid-cols-1 sm:grid-cols-2 gap-3 rounded-lg bg-surface border border-stone/60 p-3.5 text-xs">
						{#if tracking.serviceLocationType && tracking.serviceLocationType !== 'InShop'}
							<div>
								<span class="text-[10px] font-semibold uppercase tracking-wide text-secondary block">{t('fieldService.locationType')}</span>
								<span class="font-semibold text-primary">
									{tracking.serviceLocationType === 'OnSite' ? t('fieldService.onSite') : t('fieldService.pickupDelivery')}
								</span>
								{#if tracking.siteAddressSnapshot}
									<p class="m-0 mt-0.5 text-secondary">{tracking.siteAddressSnapshot}</p>
								{/if}
							</div>
						{/if}
						{#if tracking.scheduledVisitStart}
							<div>
								<span class="text-[10px] font-semibold uppercase tracking-wide text-secondary block">{t('fieldService.scheduledStart')}</span>
								<span class="font-semibold text-primary">{formatDate(tracking.scheduledVisitStart, data.locale, { dateStyle: 'medium', timeStyle: 'short' })}</span>
							</div>
						{/if}
						{#each tracking.publicFields ?? [] as pf (pf.fieldDefinitionId)}
							<div>
								<span class="text-[10px] font-semibold uppercase tracking-wide text-secondary block">
									{data.locale === 'id' ? pf.labelId : pf.labelEn}
								</span>
								<span class="font-semibold text-primary">{pf.value}</span>
							</div>
						{/each}
					</div>
				{/if}

				<!-- Visual Milestone Progress Stepper -->
				<div class="pt-0.5">
					<span class="text-[11px] font-semibold text-secondary uppercase tracking-wider block mb-2.5">{t('tracking.statusTimeline')}</span>
					<div class="grid grid-cols-4 gap-1.5 sm:gap-2 text-center">
						<!-- Step 1: Received -->
						<div class="space-y-1.5">
							<div class="h-2 rounded-full {currentStep >= 1 ? 'bg-ink' : 'bg-stone/50'} transition-all"></div>
							<span class="text-[11px] leading-tight block {currentStep >= 1 ? 'text-primary font-semibold' : 'text-secondary/60 font-medium'}">
								{t('tracking.progressIntake')}
							</span>
						</div>
						<!-- Step 2: In Progress -->
						<div class="space-y-1.5">
							<div class="h-2 rounded-full {currentStep >= 2 ? 'bg-ink' : 'bg-stone/50'} transition-all"></div>
							<span class="text-[11px] leading-tight block {currentStep >= 2 ? 'text-primary font-semibold' : 'text-secondary/60 font-medium'}">
								{t('tracking.progressRepair')}
							</span>
						</div>
						<!-- Step 3: Ready for Pickup -->
						<div class="space-y-1.5">
							<div class="h-2 rounded-full {currentStep >= 3 ? 'bg-ink' : 'bg-stone/50'} transition-all"></div>
							<span class="text-[11px] leading-tight block {currentStep >= 3 ? 'text-primary font-semibold' : 'text-secondary/60 font-medium'}">
								{t('tracking.progressReady')}
							</span>
						</div>
						<!-- Step 4: Completed -->
						<div class="space-y-1.5">
							<div class="h-2 rounded-full {currentStep >= 4 ? 'bg-emerald-600' : 'bg-stone/50'} transition-all"></div>
							<span class="text-[11px] leading-tight block {currentStep >= 4 ? 'text-emerald-700 font-semibold' : 'text-secondary/60 font-medium'}">
								{t('tracking.progressCompleted')}
							</span>
						</div>
					</div>
				</div>
			</section>

			<!-- Payment Return Banner -->
			{#if data.paymentReturn === 'success'}
				<div class="rounded-xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-body-sm text-emerald-800 flex items-center gap-2.5 shadow-2xs">
					<svg class="w-5 h-5 shrink-0 text-emerald-600" viewBox="0 0 20 20" fill="currentColor">
						<path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.857-9.809a.75.75 0 00-1.214-.882l-3.483 4.79-1.88-1.88a.75.75 0 10-1.06 1.061l2.5 2.5a.75.75 0 001.137-.089l4-5.5z" clip-rule="evenodd" />
					</svg>
					<span>{t('tracking.payment.returnSuccess')}</span>
				</div>
			{:else if data.paymentReturn === 'cancelled'}
				<div class="rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-body-sm text-amber-900 flex items-center gap-2.5 shadow-2xs">
					<svg class="w-5 h-5 shrink-0 text-amber-600" viewBox="0 0 20 20" fill="currentColor">
						<path fill-rule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-8-5a.75.75 0 01.75.75v4.5a.75.75 0 01-1.5 0v-4.5A.75.75 0 0110 5zm0 10a1 1 0 100-2 1 1 0 000 2z" clip-rule="evenodd" />
					</svg>
					<span>{t('tracking.payment.returnCancelled')}</span>
				</div>
			{/if}

			<!-- Online Payment Section -->
			{#if onlinePaymentEnabled}
				<section class="bg-white border border-stone rounded-xl p-5 sm:p-8 shadow-2xs space-y-4 sm:space-y-5">
					<div class="flex flex-wrap items-center justify-between gap-2.5 border-b border-stone/70 pb-3.5">
						<div class="min-w-0 flex-1">
							<h2 class="m-0 font-subheading text-base sm:text-subheading font-bold text-primary leading-snug">{t('tracking.payment.title')}</h2>
							<p class="m-0 text-caption text-secondary mt-0.5">{t('tracking.payment.subtitle')}</p>
						</div>
						{#if tracking.paymentStatus}
							<span class="px-2.5 py-1 rounded-full bg-ash border border-stone text-caption font-semibold text-primary leading-none">
								{t(`tickets.paymentStatus.${unlockedPaymentSummary?.paymentStatus ?? tracking.paymentStatus}`)}
							</span>
						{/if}
					</div>

					{#if !isPaymentUnlocked}
						<div class="rounded-lg border border-stone bg-ash/30 p-4 sm:p-6 space-y-4">
							<p class="m-0 text-body-sm text-secondary">{t('tracking.payment.lockedDescription')}</p>

							{#if verifyError || form?.errorKey}
								<div class="rounded-lg border border-error/30 bg-error/10 px-3.5 py-2 text-body-sm text-error">
									{verifyError || (form?.errorKey ? t(form.errorKey) : '')}
								</div>
							{/if}

							<form
								method="POST"
								action="?/verifyPhotos"
								class="flex flex-col sm:flex-row items-stretch sm:items-end gap-3"
								use:enhance={() => {
									verifying = true;
									verifyError = '';
									return async ({ result, update }) => {
										try {
											if (result.type === 'error') {
												verifyError = formatEnhanceUnexpectedError(result.error, t);
												return;
											}
											await update({ reset: false });
										} finally {
											verifying = false;
										}
									};
								}}
							>
								<input type="hidden" name="includePayment" value="true" />
								<input type="hidden" name="includePhotos" value={hasPublicPhotos ? 'true' : 'false'} />
								<label class="space-y-1.5 flex-1">
									<span class="block text-caption font-semibold uppercase text-secondary">{t('tracking.phoneLastFourLabel')}</span>
									<input
										name="phoneLastFour"
										type="text"
										inputmode="numeric"
										maxlength="4"
										bind:value={phoneLastFour}
										oninput={(e) => {
											phoneLastFour = e.currentTarget.value.replace(/\D/g, '').slice(0, 4);
										}}
										placeholder={t('tracking.phoneLastFourPlaceholder')}
										class="w-full rounded-input border border-stone bg-white px-4 py-2.5 font-mono text-center text-lg tracking-[0.25em] text-primary focus:border-ink focus:ring-1 focus:ring-ink/10 focus:outline-none transition-colors"
										required
									/>
								</label>
								<button
									type="submit"
									disabled={verifying || phoneLastFour.replace(/\D/g, '').length !== 4}
									class="px-button-x py-2.5 rounded-button bg-ink text-white font-medium hover:bg-ink/90 active:scale-95 transition-all disabled:opacity-50 disabled:cursor-not-allowed shadow-xs inline-flex items-center justify-center gap-2 cursor-pointer h-[42px]"
								>
									<span>{verifying ? t('tracking.payment.unlocking') : t('tracking.payment.unlockButton')}</span>
								</button>
							</form>
						</div>
					{:else if unlockedPaymentSummary}
						<div class="rounded-lg border border-stone/80 bg-ash/30 p-4 space-y-2 text-body-sm">
							<div class="flex justify-between gap-4 text-secondary">
								<span>{t('tracking.payment.totalBill')}</span>
								<span class="font-mono font-semibold text-primary tabular-nums">
									{unlockedPaymentSummary.totalAmount != null
										? formatCurrency(unlockedPaymentSummary.totalAmount, data.locale, unlockedPaymentSummary.currencyCode)
										: '—'}
								</span>
							</div>
							<div class="flex justify-between gap-4 text-secondary">
								<span>{t('tracking.payment.amountPaid')}</span>
								<span class="font-mono font-semibold text-primary tabular-nums">
									{formatCurrency(unlockedPaymentSummary.amountPaid, data.locale, unlockedPaymentSummary.currencyCode)}
								</span>
							</div>
							<div class="flex justify-between gap-4 pt-2 border-t border-stone/70 font-bold text-primary">
								<span>{t('tracking.payment.remainingBalance')}</span>
								<span class="font-mono tabular-nums text-claytile">
									{formatCurrency(unlockedPaymentSummary.remainingBalance, data.locale, unlockedPaymentSummary.currencyCode)}
								</span>
							</div>
						</div>

						{#if checkoutError || form?.paymentErrorMessage || form?.paymentErrorKey}
							<div class="rounded-lg border border-error/30 bg-error/10 px-3.5 py-2 text-body-sm text-error">
								{checkoutError || form?.paymentErrorMessage || (form?.paymentErrorKey ? t(form.paymentErrorKey) : '')}
							</div>
						{/if}

						{#if unlockedPaymentSummary.paymentStatus === 'Paid'}
							<p class="m-0 rounded-lg border border-emerald-200 bg-emerald-50 px-4 py-3 text-body-sm font-medium text-emerald-800">
								{t('tracking.payment.paidComplete')}
							</p>
						{:else if unlockedPaymentSummary.remainingBalance <= 0}
							<p class="m-0 text-body-sm text-secondary">{t('tracking.payment.noBalanceDue')}</p>
						{:else}
							<form
								method="POST"
								action="?/startCheckout"
								use:enhance={() => {
									startingCheckout = true;
									checkoutError = '';
									return async ({ result, update }) => {
										try {
											if (result.type === 'error') {
												checkoutError = formatEnhanceUnexpectedError(result.error, t);
												return;
											}
											await update({ reset: false });
										} finally {
											startingCheckout = false;
										}
									};
								}}
							>
								<input type="hidden" name="phoneLastFour" value={verifiedPhone || phoneLastFour} />
								<button
									type="submit"
									disabled={startingCheckout}
									class="w-full h-11 rounded-button bg-claytile text-white font-semibold hover:opacity-95 active:scale-[0.99] transition-all shadow-xs cursor-pointer inline-flex items-center justify-center gap-2 disabled:opacity-60"
								>
									<span>{startingCheckout ? t('tracking.payment.redirecting') : `${t('tracking.payment.payNow')} · ${formatCurrency(unlockedPaymentSummary.remainingBalance, data.locale, unlockedPaymentSummary.currencyCode)}`}</span>
								</button>
							</form>
						{/if}
					{/if}
				</section>
			{/if}

			<!-- Photo Evidence Gallery Section -->
			{#if (tracking.publicPhotoCount ?? 0) > 0 || unlockedAttachments.length > 0}
				<section class="bg-white border border-stone rounded-xl p-5 sm:p-8 shadow-2xs space-y-4 sm:space-y-5">
					<div class="flex flex-wrap items-center justify-between gap-2.5 border-b border-stone/70 pb-3.5">
						<div class="min-w-0 flex-1">
							<div class="flex items-center gap-2">
								<h2 class="m-0 font-subheading text-base sm:text-subheading font-bold text-primary leading-snug">{t('tracking.photoEvidence')}</h2>
								<span class="px-2 py-0.5 rounded-full bg-ash border border-stone text-caption font-mono font-medium text-secondary leading-none">
									{unlockedAttachments.length || tracking.publicPhotoCount}
								</span>
							</div>
							<p class="m-0 text-caption text-secondary mt-0.5">{t('tracking.photoEvidenceSubtitle')}</p>
						</div>
						{#if isUnlocked}
							<span class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-emerald-50 border border-emerald-200 text-caption font-medium text-emerald-700 shrink-0 leading-none">
								<svg class="w-3.5 h-3.5 text-emerald-600" viewBox="0 0 20 20" fill="currentColor">
									<path fill-rule="evenodd" d="M16.704 4.153a.75.75 0 01.143 1.052l-8 10.5a.75.75 0 01-1.127.075l-4.5-4.5a.75.75 0 011.06-1.06l3.894 3.893 7.48-9.817a.75.75 0 011.05-.143z" clip-rule="evenodd" />
								</svg>
								<span>{t('tracking.verifiedBadge')}</span>
							</span>
						{/if}
					</div>

					{#if !isUnlocked}
						<!-- Locked Verification State -->
						<div class="rounded-lg border border-stone bg-ash/30 p-4 sm:p-6 space-y-4">
							<div class="flex items-start gap-3">
								<div class="w-9 h-9 rounded-full bg-white border border-stone flex items-center justify-center text-secondary shrink-0 shadow-2xs mt-0.5">
									<svg class="w-4 h-4 text-secondary" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
										<rect x="3" y="11" width="18" height="11" rx="2" ry="2" />
										<path d="M7 11V7a5 5 0 0110 0v4" />
									</svg>
								</div>
								<div class="min-w-0 flex-1">
									<h3 class="m-0 text-body-sm font-semibold text-primary">{t('tracking.photoEvidence')}</h3>
									<p class="m-0 text-caption text-secondary mt-0.5">{t('tracking.photoLockedDescription')}</p>
								</div>
							</div>

							{#if verifyError || form?.errorKey}
								<div class="rounded-lg border border-error/30 bg-error/10 px-3.5 py-2 text-body-sm text-error flex items-center gap-2">
									<svg class="w-4 h-4 shrink-0 text-error" viewBox="0 0 20 20" fill="currentColor">
										<path fill-rule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-8-5a.75.75 0 01.75.75v4.5a.75.75 0 01-1.5 0v-4.5A.75.75 0 0110 5zm0 10a1 1 0 100-2 1 1 0 000 2z" clip-rule="evenodd" />
									</svg>
									<span>{verifyError || (form?.errorKey ? t(form.errorKey) : '')}</span>
								</div>
							{/if}

							<form
								method="POST"
								action="?/verifyPhotos"
								class="flex flex-col sm:flex-row items-stretch sm:items-end gap-3 pt-1"
								use:enhance={() => {
									verifying = true;
									verifyError = '';
									return async ({ result, update }) => {
										try {
											if (result.type === 'error') {
												verifyError = formatEnhanceUnexpectedError(result.error, t);
												return;
											}
											await update({ reset: false });
										} finally {
											verifying = false;
										}
									};
								}}
							>
								<input type="hidden" name="includePayment" value={onlinePaymentEnabled ? 'true' : 'false'} />
								<input type="hidden" name="includePhotos" value="true" />
								<label class="space-y-1.5 flex-1">
									<span class="block text-caption font-semibold uppercase text-secondary">{t('tracking.phoneLastFourLabel')}</span>
									<input
										name="phoneLastFour"
										type="text"
										inputmode="numeric"
										maxlength="4"
										bind:value={phoneLastFour}
										oninput={(e) => {
											phoneLastFour = e.currentTarget.value.replace(/\D/g, '').slice(0, 4);
										}}
										placeholder={t('tracking.phoneLastFourPlaceholder')}
										class="w-full rounded-input border border-stone bg-white px-4 py-2.5 font-mono text-center text-lg tracking-[0.25em] text-primary focus:border-ink focus:ring-1 focus:ring-ink/10 focus:outline-none transition-colors"
										required
									/>
								</label>
								<button
									type="submit"
									disabled={verifying || phoneLastFour.replace(/\D/g, '').length !== 4}
									class="px-button-x py-2.5 rounded-button bg-ink text-white font-medium hover:bg-ink/90 active:scale-95 transition-all disabled:opacity-50 disabled:cursor-not-allowed shadow-xs inline-flex items-center justify-center gap-2 cursor-pointer h-[42px]"
								>
									{#if verifying}
										<svg class="w-4 h-4 animate-spin text-white" viewBox="0 0 24 24" fill="none">
											<circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
											<path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
										</svg>
										<span>{t('tracking.unlockingPhotos')}</span>
									{:else}
										<svg class="w-4 h-4" viewBox="0 0 20 20" fill="currentColor">
											<path fill-rule="evenodd" d="M10 1a4.5 4.5 0 00-4.5 4.5V9H5a2 2 0 00-2 2v6a2 2 0 002 2h10a2 2 0 002-2v-6a2 2 0 00-2-2h-.5V5.5A4.5 4.5 0 0010 1zm3 8V5.5a3 3 0 10-6 0V9h6z" clip-rule="evenodd" />
										</svg>
										<span>{t('tracking.unlockPhotos')}</span>
									{/if}
								</button>
							</form>
						</div>
					{:else}
						<!-- Unlocked Photo Gallery Grid -->
						<div class="grid grid-cols-2 sm:grid-cols-3 gap-3">
							{#each unlockedAttachments as attachment, index}
								<button
									type="button"
									class="group relative aspect-video rounded-lg overflow-hidden border border-stone/80 bg-ash/40 hover:border-ink hover:shadow-md transition-all text-left cursor-pointer focus:outline-none focus:ring-2 focus:ring-ink"
									onclick={() => openLightbox(index)}
									aria-label={`${t('tracking.viewPhotoFull')}: ${attachment.fileName}`}
								>
									<img
										src={apiAssetUrl(attachment.url)}
										alt={attachment.fileName}
										class="w-full h-full object-cover group-hover:scale-105 transition-transform duration-300"
										loading="lazy"
									/>
									<!-- Hover overlay -->
									<div class="absolute inset-0 bg-ink/20 opacity-0 group-hover:opacity-100 transition-opacity flex items-center justify-center">
										<div class="w-8 h-8 rounded-full bg-white/90 text-ink flex items-center justify-center shadow-xs">
											<svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
												<path stroke-linecap="round" stroke-linejoin="round" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0zM10 7v6m3-3H7" />
											</svg>
										</div>
									</div>
									<!-- Caption footer -->
									<div class="absolute inset-x-0 bottom-0 bg-gradient-to-t from-black/80 via-black/40 to-transparent p-2 text-white pointer-events-none">
										<p class="m-0 text-[11px] font-medium truncate leading-tight drop-shadow-xs">{attachment.fileName}</p>
									</div>
								</button>
							{/each}
						</div>
					{/if}
				</section>
			{/if}

			<!-- Public Updates Timeline -->
			<section class="bg-white border border-stone rounded-xl p-5 sm:p-8 shadow-2xs space-y-4 sm:space-y-5">
				<div class="border-b border-stone/70 pb-3.5">
					<h2 class="m-0 font-subheading text-base sm:text-subheading font-bold text-primary leading-snug">{t('tracking.history')}</h2>
					<p class="m-0 text-caption text-secondary mt-0.5">{t('tracking.title')}</p>
				</div>

				<div class="space-y-3.5 pt-0.5">
					{#each tracking.history as item, index}
						{@const detailText = getHistoryDetail(item)}
						<div class="flex items-start gap-3">
							<div class="w-6 h-6 rounded-full {index === 0 ? 'bg-ink text-white' : 'bg-ash border border-stone text-secondary'} flex items-center justify-center text-[11px] font-bold shrink-0 mt-0.5 shadow-2xs tabular-nums">
								{index + 1}
							</div>
							<div class="flex-1 min-w-0 border-b border-stone/50 pb-3.5 {index === tracking.history.length - 1 ? 'border-b-0 pb-0' : ''}">
								<p class="m-0 font-semibold text-body-sm text-primary leading-snug">{formatEventType(item.eventType)}</p>
								{#if detailText}
									<p class="m-0 text-body-sm text-secondary mt-0.5 leading-snug">{detailText}</p>
								{/if}
								<p class="m-0 text-caption text-secondary/75 mt-1 leading-snug">{t('tracking.updated')} {formatDate(item.createdAt, data.locale, { dateStyle: 'medium', timeStyle: 'short' })}</p>
							</div>
						</div>
					{/each}

					{#if tracking.history.length === 0}
						<p class="m-0 text-body-sm text-secondary italic py-2">{t('tracking.empty')}</p>
					{/if}
				</div>
			</section>
		{/if}
	</div>
</main>

<!-- Customer Lightbox Modal -->
{#if currentPhoto !== null}
	<!-- svelte-ignore a11y_click_events_have_key_events -->
	<!-- svelte-ignore a11y_no_static_element_interactions -->
	<div
		class="fixed inset-0 z-50 bg-black/90 backdrop-blur-md flex flex-col justify-between p-4 sm:p-6 animate-in fade-in duration-200"
		onclick={closeLightbox}
	>
		<!-- Top Bar -->
		<div class="flex items-center justify-between text-white max-w-5xl w-full mx-auto" onclick={(e) => e.stopPropagation()}>
			<div class="flex items-center gap-3">
				<span class="text-caption font-mono uppercase tracking-wider text-white/70">
					{activePhotoIndex !== null ? `${activePhotoIndex + 1} / ${unlockedAttachments.length}` : ''}
				</span>
				<span class="text-body-sm font-medium text-white truncate max-w-xs sm:max-w-md">{currentPhoto.fileName}</span>
			</div>
			<button
				type="button"
				class="w-9 h-9 rounded-full bg-white/10 hover:bg-white/20 text-white flex items-center justify-center transition-colors cursor-pointer text-lg font-bold"
				onclick={closeLightbox}
				aria-label={t('tracking.closeLightbox')}
			>
				✕
			</button>
		</div>

		<!-- Main Image Container -->
		<div class="relative flex-1 flex items-center justify-center max-w-5xl w-full mx-auto my-2" onclick={(e) => e.stopPropagation()}>
			{#if unlockedAttachments.length > 1}
				<button
					type="button"
					class="absolute left-2 sm:left-4 z-10 w-11 h-11 rounded-full bg-black/50 hover:bg-black/80 text-white flex items-center justify-center transition-all cursor-pointer shadow-lg backdrop-blur-xs"
					onclick={prevPhoto}
					aria-label={t('tickets.detail.lightboxPrev')}
				>
					<svg class="w-6 h-6" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
						<path stroke-linecap="round" stroke-linejoin="round" d="M15 19l-7-7 7-7" />
					</svg>
				</button>
				<button
					type="button"
					class="absolute right-2 sm:right-4 z-10 w-11 h-11 rounded-full bg-black/50 hover:bg-black/80 text-white flex items-center justify-center transition-all cursor-pointer shadow-lg backdrop-blur-xs"
					onclick={nextPhoto}
					aria-label={t('tickets.detail.lightboxNext')}
				>
					<svg class="w-6 h-6" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
						<path stroke-linecap="round" stroke-linejoin="round" d="M9 5l7 7-7 7" />
					</svg>
				</button>
			{/if}

			<img
				src={apiAssetUrl(currentPhoto.url)}
				alt={currentPhoto.fileName}
				class="max-h-[75vh] max-w-full object-contain rounded-lg shadow-2xl select-none"
			/>
		</div>

		<!-- Bottom Caption / Metadata -->
		<div class="text-center text-white/70 text-caption max-w-5xl w-full mx-auto" onclick={(e) => e.stopPropagation()}>
			<span>{formatDate(currentPhoto.createdAt, data.locale, { dateStyle: 'medium', timeStyle: 'short' })}</span>
		</div>
	</div>
{/if}
