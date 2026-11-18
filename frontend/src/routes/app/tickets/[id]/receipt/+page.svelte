<script lang="ts">
	import { enhance } from '$app/forms';
	import PageHeader from '$lib/components/PageHeader.svelte';
	import ReceiptView from '$lib/components/ReceiptView.svelte';
	import { createTranslator } from '$lib/i18n';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();
	const t = $derived(createTranslator(data.locale));
	const receipt = $derived(data.receipt);

	let activePaperFormat = $state<'a4' | 'thermal'>('a4');
</script>

<svelte:head>
	<title>{receipt.ticketNumber} · {t('receipt.title')}</title>
</svelte:head>

<div class="receipt-page space-y-lg pb-xl">
	<!-- Top Navigation Header (Hidden on Print) -->
	<div class="print:hidden">
		<PageHeader
			items={[
				{ label: t('common.console'), href: '/app' },
				{ label: t('nav.tickets'), href: '/app/tickets' },
				{ label: receipt.ticketNumber, href: `/app/tickets/${receipt.id}` },
				{ label: t('receipt.title') }
			]}
			title={t('receipt.title')}
			subtitle={`${receipt.ticketNumber} · ${receipt.customerName} (${receipt.device})`}
		>
			{#snippet actions()}
				<div class="flex items-center gap-sm">
					<a
						class="h-10 px-4 rounded-button border border-stone bg-white text-secondary hover:text-primary hover:bg-ash font-medium text-body-sm shadow-2xs transition-colors inline-flex items-center gap-1.5"
						href={`/app/tickets/${receipt.id}`}
					>
						<span class="material-symbols-outlined text-[18px]">arrow_back</span>
						<span>{t('receipt.backToTicket')}</span>
					</a>
					<form
						method="POST"
						action="?/printLog"
						use:enhance={() => {
							return async () => window.print();
						}}
					>
						<button
							class="h-10 px-5 rounded-button font-medium bg-primary text-white hover:opacity-90 shadow-2xs text-body-sm cursor-pointer transition-opacity inline-flex items-center gap-1.5"
							type="submit"
						>
							<span class="material-symbols-outlined text-[18px]">print</span>
							<span>{t('receipt.printTicket')}</span>
						</button>
					</form>
				</div>
			{/snippet}
		</PageHeader>
	</div>

	<!-- Document Stage Control Bar (Hidden on Print) -->
	<div class="print:hidden flex flex-wrap items-center justify-between gap-sm bg-white border border-stone rounded-xl px-4 py-2.5 shadow-2xs">
		<div class="flex items-center gap-2">
			<span class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block">
				{t('settings.receiptPaperFormat')}
			</span>
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
				<span>{activePaperFormat === 'a4' ? '210mm x 297mm (A4 Portrait)' : '80mm Continuous Roll'}</span>
			</span>
		</div>
	</div>

	<!-- Canvas Stage Wrapper -->
	<div class="bg-ash/40 border border-stone/80 rounded-2xl p-4 sm:p-8 flex flex-col items-center justify-start min-h-[700px] overflow-x-auto shadow-inner print:p-0 print:border-0 print:bg-white print:shadow-none print:min-h-0">
		<ReceiptView
			receipt={receipt}
			publicTrackingUrl={data.publicTrackingUrl}
			locale={data.locale}
			printable
			paperFormat={activePaperFormat}
		/>
	</div>
</div>
