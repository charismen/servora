<script lang="ts">
	import { enhance } from '$app/forms';
	import PageHeader from '$lib/components/PageHeader.svelte';
	import ReceiptView from '$lib/components/ReceiptView.svelte';
	import { createTranslator } from '$lib/i18n';
	import type { TicketReceipt } from '$lib/types/service-tickets';
	import type { PrintTemplateSettings } from '$lib/types/settings';
	import type { ActionData, PageData } from './$types';

	let { data, form }: { data: PageData; form: ActionData } = $props();
	const t = $derived(createTranslator(data.locale));
	
	let settings = $state<PrintTemplateSettings>({
		termsAndConditions: '',
		showPhysicalCondition: true,
		showAccessories: true,
		showPhotoNotes: true,
		showQrCode: true,
		showPhoneNumber: true,
		showPaymentSummary: true
	});

	let activePaperFormat = $state<'a4' | 'thermal'>('a4');
	const previewCurrency = $derived(data.profile.currencyCode ?? 'IDR');
	const sampleWorkPrice = $derived(previewCurrency === 'USD' ? 45 : 250000);
	const samplePartPrice = $derived(previewCurrency === 'USD' ? 95 : 500000);
	const sampleTotalAmount = $derived(sampleWorkPrice + samplePartPrice);

	const previewReceipt: TicketReceipt = $derived({
		id: 0,
		ticketNumber: `${data.profile.ticketPrefix}-20260701-0001`,
		customerName: t('settings.sampleCustomer'),
		customerPhone: '+62 812 3456 7890',
		device: t('settings.sampleDevice'),
		complaint: t('settings.sampleComplaint'),
		initialDiagnosis: t('settings.sampleInitialDiagnosis'),
		physicalConditionNotes: t('settings.samplePhysicalCondition'),
		accessorySummary: t('settings.sampleAccessories'),
		photoNotes: t('settings.samplePhotoNotes'),
		priority: t('settings.samplePriority'),
		status: t('settings.sampleStatus'),
		createdAt: '2026-07-01T09:00:00.000Z',
		publicTrackingToken: 'sample-token',
		shopName: data.profile.shopName,
		logoUrl: data.profile.logoUrl,
		phoneNumber: data.profile.phoneNumber,
		whatsAppNumber: data.profile.whatsAppNumber,
		address: data.profile.address,
		receiptFooter: data.profile.receiptFooter,
		termsAndConditions: settings.termsAndConditions,
		showPhysicalCondition: settings.showPhysicalCondition,
		showAccessories: settings.showAccessories,
		showPhotoNotes: settings.showPhotoNotes,
		showQrCode: settings.showQrCode,
		showPhoneNumber: settings.showPhoneNumber,
		showPaymentSummary: settings.showPaymentSummary,
		paymentStatus: 'Partial',
		currencyCode: previewCurrency,
		workItemsSubtotal: sampleWorkPrice,
		consumedPartsSubtotal: samplePartPrice,
		discountAmount: 0,
		totalAmount: sampleTotalAmount,
		amountPaid: sampleWorkPrice,
		remainingBalance: samplePartPrice,
		overpaidAmount: 0,
		workItems: [
			{
				itemType: 'Service',
				title: t('settings.sampleWorkItem'),
				quantity: 1,
				unitPrice: sampleWorkPrice,
				lineTotal: sampleWorkPrice
			}
		],
		consumedParts: [
			{
				sku: 'PRT-001',
				itemName: t('settings.samplePartItem'),
				unit: 'pcs',
				netQuantity: 1,
				unitPrice: samplePartPrice,
				lineTotal: samplePartPrice
			}
		],
		isInternalWorkOrder: data.user?.workspace?.operatingMode === 'InternalMaintenance',
		terminology: data.user?.workspace?.terminology,
		featureConfig: data.user?.workspace?.featureConfig
	});

	$effect(() => {
		settings = { ...(form?.settings ?? data.settings) };
	});
</script>

<div class="space-y-lg pb-xl">
	<PageHeader
		items={[{ label: t('common.console'), href: '/app' }, { label: t('nav.settings'), href: '/app/settings' }, { label: t('settings.printTemplates') }]}
		title={t('settings.printTemplates')}
		subtitle={t('settings.printTemplatesSubtitle')}
	/>

	<!-- Flexible Workspace: Settings Panel + Spacious Document Stage -->
	<div class="grid grid-cols-1 xl:grid-cols-[400px_1fr] gap-lg xl:gap-xl items-start">
		<!-- Left: Template Configuration Panel -->
		<div class="space-y-sm">
			<form method="POST" class="rounded-xl border border-stone bg-white p-md sm:p-lg shadow-2xs space-y-lg" use:enhance>
				<div class="border-b border-stone pb-md">
					<h3 class="font-subheading text-subheading font-semibold text-primary">{t('settings.printTemplates')}</h3>
					<p class="text-caption text-secondary mt-0.5">{t('settings.printConfigSubtitle')}</p>
				</div>

				<div class="space-y-1 block">
					<label class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block" for="termsAndConditions">
						{t('settings.termsAndConditions')}
					</label>
					<textarea
						bind:value={settings.termsAndConditions}
						id="termsAndConditions"
						name="termsAndConditions"
						rows="6"
						class="w-full rounded-input border border-stone bg-white px-3 py-2 font-mono text-body-sm text-primary focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary shadow-2xs"
						placeholder={t('settings.printTermsPlaceholder')}
					></textarea>
				</div>

				<!-- Section Visibility Toggles -->
				<div class="space-y-2 pt-1">
					<span class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block">
						{t('settings.printSectionsLabel')}
					</span>
					<div class="space-y-sm">
						<label class="flex items-start gap-sm rounded-xl border border-stone bg-ash/30 p-sm sm:p-md text-body-sm text-secondary hover:bg-ash/60 transition-colors cursor-pointer select-none">
							<input type="checkbox" name="showPhysicalCondition" bind:checked={settings.showPhysicalCondition} class="mt-0.5 rounded border-stone text-primary focus:ring-primary" />
							<div>
								<span class="font-semibold text-primary text-body-sm block">{t('settings.showPhysicalCondition')}</span>
								<span class="text-[11px] text-secondary block mt-0.5">{t('settings.printPhysicalConditionHint')}</span>
							</div>
						</label>

						<label class="flex items-start gap-sm rounded-xl border border-stone bg-ash/30 p-sm sm:p-md text-body-sm text-secondary hover:bg-ash/60 transition-colors cursor-pointer select-none">
							<input type="checkbox" name="showAccessories" bind:checked={settings.showAccessories} class="mt-0.5 rounded border-stone text-primary focus:ring-primary" />
							<div>
								<span class="font-semibold text-primary text-body-sm block">{t('settings.showAccessories')}</span>
								<span class="text-[11px] text-secondary block mt-0.5">{t('settings.printAccessoriesHint')}</span>
							</div>
						</label>

						<label class="flex items-start gap-sm rounded-xl border border-stone bg-ash/30 p-sm sm:p-md text-body-sm text-secondary hover:bg-ash/60 transition-colors cursor-pointer select-none">
							<input type="checkbox" name="showPhotoNotes" bind:checked={settings.showPhotoNotes} class="mt-0.5 rounded border-stone text-primary focus:ring-primary" />
							<div>
								<span class="font-semibold text-primary text-body-sm block">{t('settings.showPhotoNotes')}</span>
								<span class="text-[11px] text-secondary block mt-0.5">{t('settings.printPhotoNotesHint')}</span>
							</div>
						</label>

						<label class="flex items-start gap-sm rounded-xl border border-stone bg-ash/30 p-sm sm:p-md text-body-sm text-secondary hover:bg-ash/60 transition-colors cursor-pointer select-none">
							<input type="checkbox" name="showQrCode" bind:checked={settings.showQrCode} class="mt-0.5 rounded border-stone text-primary focus:ring-primary" />
							<div>
								<span class="font-semibold text-primary text-body-sm block">{t('settings.showQrCode')}</span>
								<span class="text-[11px] text-secondary block mt-0.5">{t('settings.printQrCodeHint')}</span>
							</div>
						</label>

						<label class="flex items-start gap-sm rounded-xl border border-stone bg-ash/30 p-sm sm:p-md text-body-sm text-secondary hover:bg-ash/60 transition-colors cursor-pointer select-none">
							<input type="checkbox" name="showPhoneNumber" bind:checked={settings.showPhoneNumber} class="mt-0.5 rounded border-stone text-primary focus:ring-primary" />
							<div>
								<span class="font-semibold text-primary text-body-sm block">{t('settings.showPhoneNumber')}</span>
								<span class="text-[11px] text-secondary block mt-0.5">{t('settings.printPhoneNumberHint')}</span>
							</div>
						</label>

						<label class="flex items-start gap-sm rounded-xl border border-stone bg-ash/30 p-sm sm:p-md text-body-sm text-secondary hover:bg-ash/60 transition-colors cursor-pointer select-none">
							<input type="checkbox" name="showPaymentSummary" bind:checked={settings.showPaymentSummary} class="mt-0.5 rounded border-stone text-primary focus:ring-primary" />
							<div>
								<span class="font-semibold text-primary text-body-sm block">{t('settings.showPaymentSummary')}</span>
								<span class="text-[11px] text-secondary block mt-0.5">{t('settings.showPaymentSummaryHint')}</span>
							</div>
						</label>
					</div>
				</div>

				<div class="flex justify-end gap-sm border-t border-stone pt-md">
					<a
						class="px-button-x py-button-y rounded-button border border-stone bg-white text-secondary hover:text-primary hover:bg-ash text-body-sm font-medium shadow-2xs transition-colors flex items-center justify-center text-center"
						href="/app/settings"
					>
						{t('settings.cancel')}
					</a>
					<button
						class="h-10 px-5 rounded-button font-medium bg-primary text-white hover:opacity-90 shadow-2xs text-body-sm cursor-pointer transition-opacity"
						type="submit"
					>
						{t('settings.save')}
					</button>
				</div>
			</form>
		</div>

		<!-- Right: Dominant Document Stage with Paper Format Switcher -->
		<div class="space-y-sm min-w-0">
			<!-- Stage Toolbar -->
			<div class="flex flex-wrap items-center justify-between gap-sm bg-white border border-stone rounded-xl px-4 py-2.5 shadow-2xs">
				<div class="flex items-center gap-2">
					<span class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block">
						{t('settings.receiptPaperFormat')}
					</span>
					<!-- Segmented Paper Format Pill -->
					<div class="inline-flex items-center p-0.5 rounded-lg border border-stone bg-ash/60">
						<button
							type="button"
							onclick={() => (activePaperFormat = 'a4')}
							class="px-3 py-1 rounded-md text-caption font-medium transition-all cursor-pointer flex items-center gap-1.5 {activePaperFormat === 'a4' ? 'bg-white text-primary font-semibold shadow-2xs' : 'text-secondary hover:text-primary'}"
						>
							<span class="material-symbols-outlined text-[15px]">description</span>
							<span>{t('settings.receiptPaperA4')}</span>
						</button>
						<button
							type="button"
							onclick={() => (activePaperFormat = 'thermal')}
							class="px-3 py-1 rounded-md text-caption font-medium transition-all cursor-pointer flex items-center gap-1.5 {activePaperFormat === 'thermal' ? 'bg-white text-primary font-semibold shadow-2xs' : 'text-secondary hover:text-primary'}"
						>
							<span class="material-symbols-outlined text-[15px]">receipt_long</span>
							<span>{t('settings.receiptPaperThermal')}</span>
						</button>
					</div>
				</div>

				<div class="flex items-center gap-2">
					<span class="inline-flex items-center gap-1.5 text-caption font-medium text-secondary bg-ash/60 px-2.5 py-1 rounded-full border border-stone/60">
						<span class="w-1.5 h-1.5 rounded-full bg-emerald-500"></span>
						<span>{activePaperFormat === 'a4' ? t('settings.printPaperA4Spec') : t('settings.printPaperThermalSpec')}</span>
					</span>
				</div>
			</div>

			<!-- Document Canvas Background -->
			<div class="bg-ash/40 border border-stone/80 rounded-2xl p-4 sm:p-8 flex flex-col items-center justify-start min-h-[700px] overflow-x-auto shadow-inner">
				<ReceiptView
					receipt={previewReceipt}
					publicTrackingUrl="https://example.com/track/sample-token"
					locale={data.locale}
					paperFormat={activePaperFormat}
				/>
			</div>
		</div>
	</div>
</div>
