<script lang="ts">
	import { createTranslator, formatCurrency, formatDate, resolveFeatureConfig, resolveTerminology } from '$lib/i18n';
	import { apiAssetUrl } from '$lib/media';
	import PatternLock from '$lib/components/PatternLock.svelte';
	import type { Locale } from '$lib/i18n';
	import type { TicketReceipt } from '$lib/types/service-tickets';

	let {
		receipt,
		publicTrackingUrl,
		locale,
		printable = false,
		paperFormat = 'a4'
	}: {
		receipt: TicketReceipt;
		publicTrackingUrl: string;
		locale: Locale;
		printable?: boolean;
		paperFormat?: 'a4' | 'thermal';
	} = $props();

	const t = $derived(createTranslator(locale));
	const terms = $derived(resolveTerminology(receipt.terminology, locale));
	const features = $derived(
		resolveFeatureConfig(
			receipt.featureConfig,
			receipt.isInternalWorkOrder ? 'InternalMaintenance' : undefined
		)
	);
	const qrUrl = $derived(`https://api.qrserver.com/v1/create-qr-code/?size=180x180&data=${encodeURIComponent(publicTrackingUrl)}`);
	const paymentQrUrl = $derived(
		receipt.activePaymentUrl
			? `https://api.qrserver.com/v1/create-qr-code/?size=180x180&data=${encodeURIComponent(receipt.activePaymentUrl)}`
			: ''
	);
	const canShowPaymentQr = $derived(
		Boolean(receipt.showPaymentQrCode && receipt.activePaymentUrl && !receipt.isInternalWorkOrder && features.showBillingAndPayments)
	);
	const logoUrl = $derived(apiAssetUrl(receipt.logoUrl));
	const currencyCode = $derived(receipt.currencyCode || 'IDR');
	const workItems = $derived(receipt.workItems ?? []);
	const consumedParts = $derived(receipt.consumedParts ?? []);
	const printCustomFields = $derived((receipt.printCustomFields ?? []).filter((f) => f.value !== null && f.value !== ''));
	const printInspections = $derived((receipt.printInspections ?? []).filter((i) => i.status !== 'Unchecked'));
	const rawSubtotal = $derived((receipt.workItemsSubtotal ?? 0) + (receipt.consumedPartsSubtotal ?? 0));
	const canShowSecurity = $derived(
		Boolean(receipt.securityPasscode || receipt.securityPattern) &&
			(receipt.featureConfig ? features.showSecurityCredentials && features.securityCredentialMode !== 'None' : true)
	);
	const canShowPattern = $derived(
		Boolean(receipt.securityPattern) &&
			(!receipt.featureConfig || features.securityCredentialMode === 'PasscodeAndPattern')
	);
	const hasFinancialSummary = $derived(
		receipt.showPaymentSummary &&
			!receipt.isInternalWorkOrder &&
			features.showBillingAndPayments &&
			(workItems.length > 0 ||
				consumedParts.length > 0 ||
				receipt.totalAmount != null ||
				(receipt.amountPaid ?? 0) > 0 ||
				(receipt.discountAmount ?? 0) > 0 ||
				receipt.paymentStatus === 'Waived')
	);

	function locationLabel(locationType: string | null | undefined) {
		if (locationType === 'OnSite') return t('fieldService.onSite');
		if (locationType === 'PickupDelivery') return t('fieldService.pickupDelivery');
		return t('fieldService.inShop');
	}

	function inspectionStatusLabel(status: string) {
		if (status === 'Pass') return t('tickets.inspection.pass');
		if (status === 'Issue') return t('tickets.inspection.fail');
		if (status === 'NotApplicable') return t('tickets.inspection.na');
		return t('tickets.inspection.untested');
	}

	function localizedPaymentStatus(status: string) {
		const key = `tickets.paymentStatus.${status}`;
		const translated = t(key);
		return translated === key ? status : translated;
	}

	function localizedWorkItemType(itemType: string) {
		const key = `tickets.workItems.itemType.${itemType}`;
		const translated = t(key);
		return translated === key ? itemType : translated;
	}
</script>

{#if paperFormat === 'thermal'}
	<!-- Thermal 80mm POS Receipt Layout -->
	<section class="receipt-print receipt-thermal mx-auto w-full max-w-[360px] bg-white border border-stone/80 rounded-xl p-5 shadow-md print:border-0 print:p-0 print:max-w-none print:shadow-none text-[12px] leading-relaxed text-primary">
		<!-- Header -->
		<div class="text-center space-y-1 pb-3 border-b border-dashed border-stone">
			{#if logoUrl}
				<img class="receipt-logo h-12 w-12 mx-auto object-contain mb-1" src={logoUrl} alt={receipt.shopName} />
			{/if}
			<h1 class="font-headline-sm text-body font-bold text-primary">{receipt.shopName}</h1>
			{#if receipt.address}
				<p class="text-[11px] text-secondary whitespace-pre-line leading-tight">{receipt.address}</p>
			{/if}
			<p class="text-[11px] text-secondary font-mono">
				{#if receipt.showPhoneNumber && receipt.phoneNumber}
					{receipt.phoneNumber} · 
				{/if}
				WA: {receipt.whatsAppNumber}
			</p>
		</div>

		<!-- Ticket & Date -->
		<div class="text-center py-2.5 border-b border-dashed border-stone space-y-0.5">
			<span class="text-[10px] font-label-caps uppercase tracking-widest text-secondary font-bold">
				{receipt.isInternalWorkOrder ? t('receipt.workOrder') : t('receipt.serviceReceipt')}
			</span>
			<div class="font-mono text-[18px] font-bold tracking-wider text-primary">{receipt.ticketNumber}</div>
			<div class="text-[11px] text-secondary">{formatDate(receipt.createdAt, locale, { dateStyle: 'medium', timeStyle: 'short' })}</div>
			{#if receipt.isInternalWorkOrder}
				<div class="text-[10px] font-medium text-secondary">{t('receipt.internalNoPayment')}</div>
			{/if}
		</div>

		<!-- Customer & Device Metadata -->
		<div class="py-2.5 border-b border-dashed border-stone space-y-1.5 text-[11px]">
			<div class="flex justify-between items-baseline gap-2">
				<span class="text-secondary font-medium">{terms.requesterSingular}:</span>
				<span class="font-semibold text-primary text-right">{receipt.customerName}</span>
			</div>
			{#if receipt.customerPhone}
				<div class="flex justify-between items-baseline gap-2 font-mono text-secondary">
					<span>{t('receipt.phoneLabel')}:</span>
					<span>{receipt.customerPhone}</span>
				</div>
			{/if}
			<div class="flex justify-between items-baseline gap-2 pt-1">
				<span class="text-secondary font-medium">{terms.assetSingular}:</span>
				<span class="font-semibold text-primary text-right">{receipt.device}</span>
			</div>
			<div class="flex justify-between items-baseline gap-2 text-secondary">
				<span>{t('receipt.statusLabel')}:</span>
				<span>{receipt.status} ({receipt.priority})</span>
			</div>
			{#if receipt.serviceLocationType && receipt.serviceLocationType !== 'InShop'}
				<div class="flex justify-between items-baseline gap-2 text-secondary">
					<span>{t('fieldService.locationType')}:</span>
					<span class="text-right font-medium text-primary">{locationLabel(receipt.serviceLocationType)}</span>
				</div>
			{/if}
			{#if receipt.siteAddressSnapshot}
				<div class="text-[10px] text-secondary">
					<span class="font-medium">{t('receipt.siteLocation')}:</span> {receipt.siteAddressSnapshot}
				</div>
			{/if}
			{#if receipt.scheduledVisitStart}
				<div class="flex justify-between items-baseline gap-2 text-secondary">
					<span>{t('receipt.scheduledVisit')}:</span>
					<span>{formatDate(receipt.scheduledVisitStart, locale, { dateStyle: 'short', timeStyle: 'short' })}</span>
				</div>
			{/if}
		</div>

		<!-- Problem & Diagnostic Details -->
		<div class="py-2.5 border-b border-dashed border-stone space-y-2 text-[11px]">
			<div>
				<span class="font-bold text-secondary uppercase text-[10px] tracking-wider block">{t('tickets.detail.complaint')}</span>
				<p class="text-primary whitespace-pre-line mt-0.5">{receipt.complaint}</p>
			</div>

			{#if receipt.initialDiagnosis}
				<div>
					<span class="font-bold text-secondary uppercase text-[10px] tracking-wider block">{t('tickets.detail.initialDiagnosis')}</span>
					<p class="text-primary whitespace-pre-line mt-0.5">{receipt.initialDiagnosis}</p>
				</div>
			{/if}

			{#if receipt.showPhysicalCondition && receipt.physicalConditionNotes}
				<div>
					<span class="font-bold text-secondary uppercase text-[10px] tracking-wider block">{t('tickets.detail.physicalConditionNotes')}</span>
					<p class="text-primary whitespace-pre-line mt-0.5">{receipt.physicalConditionNotes}</p>
				</div>
			{/if}

			{#if receipt.showAccessories && receipt.accessorySummary}
				<div>
					<span class="font-bold text-secondary uppercase text-[10px] tracking-wider block">{t('tickets.detail.accessorySummary')}</span>
					<p class="text-primary whitespace-pre-line mt-0.5">{receipt.accessorySummary}</p>
				</div>
			{/if}

			{#if receipt.showPhotoNotes && receipt.photoNotes}
				<div>
					<span class="font-bold text-secondary uppercase text-[10px] tracking-wider block">{t('tickets.detail.photoNotes')}</span>
					<p class="text-primary whitespace-pre-line mt-0.5">{receipt.photoNotes}</p>
				</div>
			{/if}

			{#if printCustomFields.length > 0}
				<div class="space-y-1">
					<span class="font-bold text-secondary uppercase text-[10px] tracking-wider block">{t('fieldService.customFieldsSection')}</span>
					{#each printCustomFields as field}
						<div class="flex justify-between gap-2 text-[11px]">
							<span class="text-secondary">{locale === 'id' ? field.labelId || field.labelEn : field.labelEn || field.labelId}:</span>
							<span class="font-medium text-primary text-right">{field.value}</span>
						</div>
					{/each}
				</div>
			{/if}

			{#if features.showPreRepairChecklist && printInspections.length > 0}
				<div class="space-y-1">
					<span class="font-bold text-secondary uppercase text-[10px] tracking-wider block">{t('receipt.inspectionChecklist')}</span>
					{#each printInspections as check}
						<div class="flex justify-between gap-2 text-[10px]">
							<span class="text-primary">{check.label}{check.notes ? ` (${check.notes})` : ''}</span>
							<span class="font-semibold shrink-0">{inspectionStatusLabel(check.status)}</span>
						</div>
					{/each}
				</div>
			{/if}

			{#if canShowSecurity}
				<div class="rounded border border-stone bg-ash/40 p-2 space-y-1">
					<span class="font-bold text-secondary uppercase text-[10px] tracking-wider block">{t('tickets.security.title')}</span>
					{#if receipt.securityPasscode}
						<p class="font-mono">{t('receipt.passcodeLabel')}: <strong class="font-bold text-primary">{receipt.securityPasscode}</strong></p>
					{/if}
					{#if canShowPattern}
						<div class="flex items-center gap-2 pt-1">
							<PatternLock value={receipt.securityPattern!} readonly={true} size={70} />
							<span class="font-mono text-[10px] text-secondary">{receipt.securityPattern}</span>
						</div>
					{/if}
				</div>
			{/if}
		</div>

		<!-- Billing & Financial Summary -->
		{#if hasFinancialSummary}
			<div class="py-2.5 border-b border-dashed border-stone space-y-2 text-[11px]">
				<span class="font-bold text-secondary uppercase text-[10px] tracking-wider block">{t('receipt.billingSummary')}</span>

				{#if workItems.length > 0}
					<div class="space-y-1">
						<span class="text-[10px] font-semibold text-secondary uppercase tracking-wider block">{t('receipt.workItems')}</span>
						{#each workItems as item}
							<div class="flex items-start justify-between gap-2">
								<div class="min-w-0">
									<div class="font-medium text-primary leading-tight">{item.title}</div>
									<div class="text-[10px] text-secondary font-mono">
										{item.quantity} × {formatCurrency(item.unitPrice, locale, currencyCode)}
									</div>
								</div>
								<span class="font-mono font-medium text-primary shrink-0 tabular-nums">{formatCurrency(item.lineTotal, locale, currencyCode)}</span>
							</div>
						{/each}
					</div>
				{/if}

				{#if consumedParts.length > 0}
					<div class="space-y-1 pt-1">
						<span class="text-[10px] font-semibold text-secondary uppercase tracking-wider block">{t('receipt.consumedParts')}</span>
						{#each consumedParts as part}
							<div class="flex items-start justify-between gap-2">
								<div class="min-w-0">
									<div class="font-medium text-primary leading-tight">{part.itemName}</div>
									<div class="text-[10px] text-secondary font-mono">
										{part.netQuantity} {part.unit} × {formatCurrency(part.unitPrice, locale, currencyCode)}
									</div>
								</div>
								<span class="font-mono font-medium text-primary shrink-0 tabular-nums">{formatCurrency(part.lineTotal, locale, currencyCode)}</span>
							</div>
						{/each}
					</div>
				{/if}

				<div class="border-t border-dashed border-stone pt-1.5 space-y-0.5 font-mono">
					{#if rawSubtotal > 0}
						<div class="flex justify-between gap-2 text-secondary">
							<span>{t('receipt.subtotal')}</span>
							<span class="tabular-nums">{formatCurrency(rawSubtotal, locale, currencyCode)}</span>
						</div>
					{/if}
					{#if (receipt.discountAmount ?? 0) > 0}
						<div class="flex justify-between gap-2 text-secondary">
							<span>{t('receipt.discount')}</span>
							<span class="tabular-nums">-{formatCurrency(receipt.discountAmount, locale, currencyCode)}</span>
						</div>
					{/if}
					<div class="flex justify-between gap-2 font-bold text-primary pt-0.5">
						<span>{t('receipt.totalBill')}</span>
						<span class="tabular-nums">{receipt.totalAmount != null ? formatCurrency(receipt.totalAmount, locale, currencyCode) : '—'}</span>
					</div>
					<div class="flex justify-between gap-2 text-secondary">
						<span>{t('receipt.amountPaid')}</span>
						<span class="tabular-nums">{formatCurrency(receipt.amountPaid ?? 0, locale, currencyCode)}</span>
					</div>
					{#if (receipt.overpaidAmount ?? 0) > 0}
						<div class="flex justify-between gap-2 font-semibold text-amber-800">
							<span>{t('receipt.refundDue')}</span>
							<span class="tabular-nums">{formatCurrency(receipt.overpaidAmount, locale, currencyCode)}</span>
						</div>
					{:else if receipt.paymentStatus !== 'Waived'}
						<div class="flex justify-between gap-2 font-semibold text-primary">
							<span>{t('receipt.remainingBalance')}</span>
							<span class="tabular-nums">{formatCurrency(receipt.remainingBalance ?? 0, locale, currencyCode)}</span>
						</div>
					{/if}
					<div class="flex justify-between gap-2 pt-0.5 text-[10px]">
						<span class="text-secondary font-sans">{t('receipt.paymentStatus')}</span>
						<span class="font-bold text-primary uppercase font-sans">{localizedPaymentStatus(receipt.paymentStatus)}</span>
					</div>
				</div>
			</div>
		{/if}

		<!-- QR Code Tracking -->
		{#if receipt.showQrCode && !receipt.isInternalWorkOrder}
			<div class="py-3 text-center border-b border-dashed border-stone space-y-1">
				<img class="receipt-qr mx-auto h-[120px] w-[120px]" src={qrUrl} alt={t('receipt.qrAlt')} />
				<span class="text-[10px] text-secondary font-medium block">{t('receipt.scanToTrack')}</span>
				<span class="text-[9px] text-secondary font-mono break-all block">{publicTrackingUrl}</span>
			</div>
		{/if}

		<!-- Online Payment QR Code -->
		{#if canShowPaymentQr}
			<div class="py-3 text-center border-b border-dashed border-stone space-y-1">
				<img class="receipt-qr mx-auto h-[120px] w-[120px]" src={paymentQrUrl} alt={t('receipt.scanToPay')} />
				<span class="text-[10px] text-primary font-semibold block">{t('receipt.scanToPay')}</span>
				<span class="text-[9px] text-secondary font-mono break-all block">{receipt.activePaymentUrl}</span>
			</div>
		{/if}

		<!-- Footer Terms -->
		<div class="pt-3 text-[10px] text-secondary text-center space-y-1.5 whitespace-pre-line leading-normal">
			{#if receipt.receiptFooter}
				<p class="font-medium text-primary">{receipt.receiptFooter}</p>
			{/if}
			{#if receipt.termsAndConditions}
				<p class="text-secondary/80 text-[9px]">{receipt.termsAndConditions}</p>
			{/if}
		</div>
	</section>
{:else}
	<!-- Standard A4 Sheet Document Layout -->
	<section class="receipt-print receipt-a4 mx-auto w-full max-w-[820px] bg-white border border-stone/80 rounded-xl p-6 sm:p-10 shadow-md print:border-0 print:p-0 print:max-w-none print:shadow-none text-body-sm leading-relaxed text-primary">
		<!-- Document Header -->
		<div class="receipt-header flex flex-col sm:flex-row items-start justify-between gap-md border-b border-stone pb-6">
			<div class="flex min-w-0 items-start gap-4">
				{#if logoUrl}
					<img class="receipt-logo h-16 w-16 shrink-0 object-contain rounded border border-stone/40 p-1" src={logoUrl} alt={receipt.shopName} />
				{/if}
				<div class="min-w-0">
					<h1 class="font-headline-sm text-headline-sm text-primary font-bold">{receipt.shopName}</h1>
					{#if receipt.address}
						<p class="text-body-sm text-secondary whitespace-pre-line mt-0.5">{receipt.address}</p>
					{/if}
					<p class="text-caption text-secondary mt-1 font-mono">
						{#if receipt.showPhoneNumber && receipt.phoneNumber}
							{receipt.phoneNumber} · 
						{/if}
						{t('receipt.whatsApp')}: {receipt.whatsAppNumber}
					</p>
				</div>
			</div>
			<div class="text-left sm:text-right shrink-0 pt-2 sm:pt-0">
				<span class="text-[11px] font-label-caps uppercase tracking-widest text-secondary font-bold block">
					{receipt.isInternalWorkOrder ? t('receipt.workOrder') : t('receipt.serviceReceipt')}
				</span>
				<div class="font-mono text-[22px] font-bold text-primary mt-0.5 tracking-wider">{receipt.ticketNumber}</div>
				<div class="text-caption text-secondary font-mono mt-0.5">{formatDate(receipt.createdAt, locale, { dateStyle: 'medium', timeStyle: 'short' })}</div>
				{#if receipt.isInternalWorkOrder}
					<div class="mt-1 inline-block rounded-full border border-stone bg-ash px-2.5 py-0.5 text-[10px] font-semibold text-secondary">
						{t('receipt.internalNoPayment')}
					</div>
				{/if}
			</div>
		</div>

		<!-- Customer & Device Metadata Grid -->
		<div class="receipt-meta grid grid-cols-1 sm:grid-cols-2 gap-6 py-5 border-b border-stone">
			<div class="rounded-lg border border-stone/60 bg-ash/30 p-3 space-y-1">
				<span class="text-[10px] font-label-caps uppercase tracking-wider text-secondary font-bold block">{terms.requesterSingular}</span>
				<p class="font-semibold text-primary text-body">{receipt.customerName}</p>
				{#if receipt.customerPhone}
					<p class="text-caption font-mono text-secondary">{receipt.customerPhone}</p>
				{/if}
				{#if receipt.siteAddressSnapshot}
					<p class="text-caption text-secondary pt-1">{t('receipt.siteLocation')}: {receipt.siteAddressSnapshot}</p>
				{/if}
			</div>
			<div class="rounded-lg border border-stone/60 bg-ash/30 p-3 space-y-1">
				<span class="text-[10px] font-label-caps uppercase tracking-wider text-secondary font-bold block">{terms.assetSingular}</span>
				<p class="font-semibold text-primary text-body">{receipt.device}</p>
				<p class="text-caption text-secondary font-medium">
					{t('receipt.statusLabel')}: <span class="text-primary font-semibold">{receipt.status}</span> · {t('tickets.detail.priority')}: <span class="text-primary font-semibold">{receipt.priority}</span>
				</p>
				{#if receipt.serviceLocationType && receipt.serviceLocationType !== 'InShop'}
					<p class="text-caption text-secondary">
						{t('fieldService.locationType')}: <span class="font-semibold text-primary">{locationLabel(receipt.serviceLocationType)}</span>
						{#if receipt.scheduledVisitStart}
							· {formatDate(receipt.scheduledVisitStart, locale, { dateStyle: 'medium', timeStyle: 'short' })}
						{/if}
					</p>
				{/if}
			</div>
		</div>

		<!-- Body & QR Code -->
		<div class="receipt-body grid grid-cols-1 {receipt.showQrCode && !receipt.isInternalWorkOrder ? 'md:grid-cols-[1fr_210px]' : ''} gap-8 py-6">
			<div class="space-y-4">
				<div>
					<span class="text-[11px] font-label-caps uppercase tracking-wider text-secondary font-bold block">{t('tickets.detail.complaint')}</span>
					<p class="text-primary whitespace-pre-line mt-1">{receipt.complaint}</p>
				</div>

				{#if receipt.initialDiagnosis}
					<div>
						<span class="text-[11px] font-label-caps uppercase tracking-wider text-secondary font-bold block">{t('tickets.detail.initialDiagnosis')}</span>
						<p class="text-primary whitespace-pre-line mt-1">{receipt.initialDiagnosis}</p>
					</div>
				{/if}

				{#if receipt.showPhysicalCondition && receipt.physicalConditionNotes}
					<div>
						<span class="text-[11px] font-label-caps uppercase tracking-wider text-secondary font-bold block">{t('tickets.detail.physicalConditionNotes')}</span>
						<p class="text-primary whitespace-pre-line mt-1">{receipt.physicalConditionNotes}</p>
					</div>
				{/if}

				{#if receipt.showAccessories && receipt.accessorySummary}
					<div>
						<span class="text-[11px] font-label-caps uppercase tracking-wider text-secondary font-bold block">{t('tickets.detail.accessorySummary')}</span>
						<p class="text-primary whitespace-pre-line mt-1">{receipt.accessorySummary}</p>
					</div>
				{/if}

				{#if receipt.showPhotoNotes && receipt.photoNotes}
					<div>
						<span class="text-[11px] font-label-caps uppercase tracking-wider text-secondary font-bold block">{t('tickets.detail.photoNotes')}</span>
						<p class="text-primary whitespace-pre-line mt-1">{receipt.photoNotes}</p>
					</div>
				{/if}

				{#if printCustomFields.length > 0}
					<div class="border border-stone/70 rounded-xl p-3.5 bg-ash/20">
						<span class="text-[10px] font-label-caps uppercase tracking-wider text-secondary font-bold block mb-2">{t('fieldService.customFieldsSection')}</span>
						<div class="grid grid-cols-1 sm:grid-cols-2 gap-2 text-caption">
							{#each printCustomFields as field}
								<div>
									<span class="text-secondary">{locale === 'id' ? field.labelId || field.labelEn : field.labelEn || field.labelId}:</span>
									<span class="font-semibold text-primary ml-1">{field.value}</span>
								</div>
							{/each}
						</div>
					</div>
				{/if}

				{#if features.showPreRepairChecklist && printInspections.length > 0}
					<div class="border border-stone/70 rounded-xl p-3.5 bg-ash/20">
						<span class="text-[10px] font-label-caps uppercase tracking-wider text-secondary font-bold block mb-2">{t('receipt.inspectionChecklist')}</span>
						<div class="grid grid-cols-1 sm:grid-cols-2 gap-1.5 text-caption">
							{#each printInspections as check}
								<div class="flex items-center justify-between gap-2 rounded border border-stone/40 bg-white px-2.5 py-1">
									<span class="text-primary">{check.label}{check.notes ? ` — ${check.notes}` : ''}</span>
									<span class="font-semibold text-xs shrink-0">{inspectionStatusLabel(check.status)}</span>
								</div>
							{/each}
						</div>
					</div>
				{/if}

				{#if canShowSecurity}
					<div class="border border-stone rounded-xl p-3.5 bg-ash/30 space-y-2">
						<span class="text-[10px] font-label-caps uppercase tracking-wider text-secondary font-bold block">{t('tickets.security.title')}</span>
						<div class="flex flex-wrap items-center gap-6">
							{#if receipt.securityPasscode}
								<div>
									<span class="text-caption text-secondary block">{t('tickets.security.passcode')}</span>
									<span class="font-mono font-bold text-body text-primary">{receipt.securityPasscode}</span>
								</div>
							{/if}
							{#if canShowPattern}
								<div class="flex items-center gap-3">
									<PatternLock value={receipt.securityPattern!} readonly={true} size={84} />
									<div class="text-caption text-secondary">
										<span class="font-medium text-primary block">{t('tickets.security.pattern')}</span>
										<span class="font-mono text-[11px]">{receipt.securityPattern}</span>
									</div>
								</div>
							{/if}
						</div>
					</div>
				{/if}

				{#if hasFinancialSummary}
					<div class="border border-stone rounded-xl p-4 bg-white space-y-3">
						<div class="flex flex-wrap items-center justify-between gap-2 border-b border-stone/70 pb-2">
							<span class="text-[11px] font-label-caps uppercase tracking-wider text-secondary font-bold">{t('receipt.billingSummary')}</span>
							<span class="inline-flex items-center rounded-full border border-stone bg-ash/60 px-2.5 py-0.5 text-[11px] font-semibold text-primary">
								{t('receipt.paymentStatus')}: {localizedPaymentStatus(receipt.paymentStatus)}
							</span>
						</div>

						{#if workItems.length > 0}
							<div class="space-y-1.5">
								<span class="text-[10px] font-label-caps uppercase tracking-wider text-secondary font-bold block">{t('receipt.workItems')}</span>
								<div class="divide-y divide-stone/50 border border-stone/60 rounded-lg overflow-hidden">
									{#each workItems as item}
										<div class="flex items-start justify-between gap-4 px-3 py-2 text-body-sm bg-white">
											<div class="min-w-0">
												<div class="font-medium text-primary">{item.title}</div>
												{#if item.description}
													<div class="text-caption text-secondary">{item.description}</div>
												{/if}
												<div class="text-caption font-mono text-secondary">
													{localizedWorkItemType(item.itemType)} · {item.quantity} × {formatCurrency(item.unitPrice, locale, currencyCode)}
												</div>
											</div>
											<span class="font-mono font-semibold text-primary shrink-0 tabular-nums">{formatCurrency(item.lineTotal, locale, currencyCode)}</span>
										</div>
									{/each}
								</div>
							</div>
						{/if}

						{#if consumedParts.length > 0}
							<div class="space-y-1.5">
								<span class="text-[10px] font-label-caps uppercase tracking-wider text-secondary font-bold block">{t('receipt.consumedParts')}</span>
								<div class="divide-y divide-stone/50 border border-stone/60 rounded-lg overflow-hidden">
									{#each consumedParts as part}
										<div class="flex items-start justify-between gap-4 px-3 py-2 text-body-sm bg-white">
											<div class="min-w-0">
												<div class="font-medium text-primary">{part.itemName}</div>
												<div class="text-caption font-mono text-secondary">
													{part.sku} · {part.netQuantity} {part.unit} × {formatCurrency(part.unitPrice, locale, currencyCode)}
												</div>
											</div>
											<span class="font-mono font-semibold text-primary shrink-0 tabular-nums">{formatCurrency(part.lineTotal, locale, currencyCode)}</span>
										</div>
									{/each}
								</div>
							</div>
						{/if}

						<div class="rounded-lg bg-ash/40 border border-stone/60 p-3 space-y-1.5 text-body-sm">
							{#if rawSubtotal > 0}
								<div class="flex justify-between gap-4 text-secondary">
									<span>{t('receipt.subtotal')}</span>
									<span class="font-mono tabular-nums">{formatCurrency(rawSubtotal, locale, currencyCode)}</span>
								</div>
							{/if}
							{#if (receipt.discountAmount ?? 0) > 0}
								<div class="flex justify-between gap-4 text-secondary">
									<span>{t('receipt.discount')}</span>
									<span class="font-mono tabular-nums">-{formatCurrency(receipt.discountAmount, locale, currencyCode)}</span>
								</div>
							{/if}
							<div class="flex justify-between gap-4 font-bold text-primary border-t border-stone/60 pt-1.5">
								<span>{t('receipt.totalBill')}</span>
								<span class="font-mono tabular-nums">{receipt.totalAmount != null ? formatCurrency(receipt.totalAmount, locale, currencyCode) : '—'}</span>
							</div>
							<div class="flex justify-between gap-4 text-secondary">
								<span>{t('receipt.amountPaid')}</span>
								<span class="font-mono tabular-nums">{formatCurrency(receipt.amountPaid ?? 0, locale, currencyCode)}</span>
							</div>
							{#if (receipt.overpaidAmount ?? 0) > 0}
								<div class="flex justify-between gap-4 font-semibold text-amber-800">
									<span>{t('receipt.refundDue')}</span>
									<span class="font-mono tabular-nums">{formatCurrency(receipt.overpaidAmount, locale, currencyCode)}</span>
								</div>
							{:else if receipt.paymentStatus !== 'Waived'}
								<div class="flex justify-between gap-4 font-semibold text-primary">
									<span>{t('receipt.remainingBalance')}</span>
									<span class="font-mono tabular-nums">{formatCurrency(receipt.remainingBalance ?? 0, locale, currencyCode)}</span>
								</div>
							{/if}
						</div>
					</div>
				{/if}

				{#if receipt.receiptFooter}
					<p class="text-body-sm text-secondary whitespace-pre-line pt-2">{receipt.receiptFooter}</p>
				{/if}

				{#if receipt.termsAndConditions}
					<div class="border-t border-stone pt-3">
						<span class="text-[10px] font-label-caps uppercase tracking-wider text-secondary font-bold block mb-1">{t('settings.termsAndConditions')}</span>
						<p class="text-caption text-secondary/80 whitespace-pre-line leading-relaxed font-mono text-[11px]">{receipt.termsAndConditions}</p>
					</div>
				{/if}
			</div>

			<!-- QR Code Block -->
			{#if (receipt.showQrCode && !receipt.isInternalWorkOrder) || canShowPaymentQr}
				<div class="space-y-3 h-fit self-start">
					{#if receipt.showQrCode && !receipt.isInternalWorkOrder}
						<div class="border border-stone rounded-xl p-4 text-center bg-ash/30 flex flex-col items-center justify-between space-y-2">
							<div>
								<img class="receipt-qr mx-auto h-[160px] w-[160px] rounded bg-white p-1 border border-stone/40" src={qrUrl} alt={t('receipt.qrAlt')} />
								<span class="text-caption font-semibold text-primary block mt-2">{t('receipt.scanForLiveStatus')}</span>
							</div>
							<p class="text-[10px] font-mono text-secondary break-all">{publicTrackingUrl}</p>
						</div>
					{/if}
					{#if canShowPaymentQr}
						<div class="border border-claytile/40 rounded-xl p-4 text-center bg-tertiary-container/30 flex flex-col items-center justify-between space-y-2">
							<div>
								<img class="receipt-qr mx-auto h-[160px] w-[160px] rounded bg-white p-1 border border-stone/40" src={paymentQrUrl} alt={t('receipt.scanToPay')} />
								<span class="text-caption font-semibold text-primary block mt-2">{t('receipt.scanToPay')}</span>
							</div>
							<p class="text-[10px] font-mono text-secondary break-all">{receipt.activePaymentUrl}</p>
						</div>
					{/if}
				</div>
			{/if}
		</div>
	</section>
{/if}

{#if printable}
	<style>
		@media print {
			:global(body) {
				background: #ffffff !important;
			}

			:global(body *) {
				visibility: hidden !important;
			}

			.receipt-print,
			.receipt-print * {
				visibility: visible !important;
			}

			:global(main) {
				margin: 0 !important;
				min-height: auto !important;
			}

			:global(main > div) {
				padding: 0 !important;
				background: #ffffff !important;
				overflow: visible !important;
			}

			:global(.receipt-page) {
				padding: 0 !important;
				margin: 0 !important;
				background: #ffffff !important;
			}

			/* A4 Print Layout */
			@page {
				size: A4 portrait;
				margin: 0;
			}

			.receipt-print.receipt-a4 {
				position: absolute;
				top: 0;
				left: 0;
				width: 210mm;
				min-height: 148mm;
				margin: 0 auto !important;
				padding: 10mm !important;
				box-shadow: none !important;
				border: none !important;
				font-size: 11px;
				line-height: 1.35;
				break-inside: avoid;
			}

			/* Thermal 80mm Print Layout */
			.receipt-print.receipt-thermal {
				position: absolute;
				top: 0;
				left: 0;
				width: 76mm !important;
				max-width: 76mm !important;
				margin: 0 !important;
				padding: 3mm !important;
				box-shadow: none !important;
				border: none !important;
				font-size: 10px !important;
				line-height: 1.25 !important;
			}

			.receipt-header {
				padding-bottom: 4mm !important;
			}

			.receipt-meta {
				padding-block: 4mm !important;
			}

			.receipt-body {
				padding-top: 4mm !important;
				gap: 6mm !important;
			}

			.receipt-qr {
				width: 36mm !important;
				height: 36mm !important;
			}

			.receipt-logo {
				width: 14mm !important;
				height: 14mm !important;
			}
		}
	</style>
{/if}
