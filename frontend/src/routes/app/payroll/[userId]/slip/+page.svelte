<script lang="ts">
	import PageHeader from '$lib/components/PageHeader.svelte';
	import { createTranslator, formatCurrency } from '$lib/i18n';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();
	const t = $derived(createTranslator(data.locale));
	const slip = $derived(data.slip);
	const worker = $derived(slip.worker);
	const currencyCode = $derived(slip.currencyCode || data.shopProfile?.currencyCode || 'IDR');

	const todayFormatted = $derived(
		new Intl.DateTimeFormat(data.locale === 'id' ? 'id-ID' : 'en-US', {
			year: 'numeric',
			month: 'short',
			day: 'numeric'
		}).format(new Date())
	);
</script>

<svelte:head>
	<title>{worker.fullName} · {t('payroll.slip.title')}</title>
</svelte:head>

<div class="space-y-lg pb-2xl">
	<!-- Screen-only Controls & Header -->
	<div class="print:hidden">
		<PageHeader
			items={[
				{ label: t('common.console'), href: '/app' },
				{ label: t('nav.payroll'), href: `/app/payroll?from=${slip.from}&to=${slip.to}&cycle=${slip.cycle}` },
				{ label: worker.fullName }
			]}
			title={t('payroll.slip.title')}
			subtitle={`${worker.fullName} · ${slip.from} – ${slip.to}`}
		>
			{#snippet actions()}
				<div class="flex flex-wrap items-center gap-2">
					<div class="hidden sm:inline-flex items-center gap-1.5 rounded-full border border-stone bg-ash/60 px-3 py-1.5 text-caption font-mono text-secondary shadow-2xs">
						<span class="material-symbols-outlined text-[14px]">description</span>
						<span>{t('payroll.slip.a4Badge')}</span>
					</div>
					<a
						class="inline-flex h-9 items-center rounded-button border border-stone bg-white px-3.5 text-body-sm font-medium text-ink hover:bg-ash transition-all shadow-2xs"
						href={`/app/payroll?from=${slip.from}&to=${slip.to}&cycle=${slip.cycle}`}
					>
						{t('payroll.slip.backToPayroll')}
					</a>
					<button
						type="button"
						class="inline-flex h-9 items-center gap-1.5 rounded-button bg-primary px-4 text-body-sm font-semibold text-white hover:opacity-90 active:scale-95 transition-all shadow-xs cursor-pointer"
						onclick={() => window.print()}
					>
						<span class="material-symbols-outlined text-[18px]">print</span>
						<span>{t('payroll.slip.printAction')}</span>
					</button>
				</div>
			{/snippet}
		</PageHeader>
	</div>

	<!-- Printable Standard A4 Paper Sheet Preview Container -->
	<div class="flex justify-center print:block">
		<article
			class="w-full max-w-[210mm] min-h-[297mm] rounded-sm border border-stone/80 bg-white p-8 sm:p-12 shadow-lg shadow-stone/20 text-ink space-y-6 print:border-none print:shadow-none print:p-0 print:max-w-none print:w-full print:min-h-0 print:m-0 print:space-y-5"
		>
			<!-- Letterhead: Shop Identity & Document Metadata -->
			<header class="flex flex-wrap items-start justify-between gap-6 border-b border-stone/90 pb-5">
				<div class="space-y-1">
					<h1 class="text-2xl font-bold tracking-tight text-ink mb-0">{slip.shopName}</h1>
					{#if slip.shopAddress}
						<p class="text-xs text-secondary mb-0 leading-relaxed max-w-sm">{slip.shopAddress}</p>
					{/if}
					{#if slip.shopPhone}
						<p class="text-xs font-mono text-secondary mb-0">{slip.shopPhone}</p>
					{/if}
				</div>
				<div class="text-right space-y-1.5 sm:min-w-[240px]">
					<div class="inline-block rounded-md border border-stone/80 bg-ash/70 px-3 py-1 text-[11px] font-bold uppercase tracking-wider text-ink font-label-caps">
						{t('payroll.slip.title')}
					</div>
					<p class="text-xs font-mono tabular-nums text-secondary mb-0">
						{t('payroll.slip.period')}: <strong class="text-ink font-semibold">{slip.from}</strong> – <strong class="text-ink font-semibold">{slip.to}</strong>
					</p>
					<div class="flex items-center justify-end gap-2 text-[11px] text-secondary">
						<span class="capitalize">{slip.cycle === 'weekly' ? t('payroll.cycleWeekly') : t('payroll.cycleMonthly')}</span>
						<span>·</span>
						<span>{t('payroll.slip.issueDate')}: {todayFormatted}</span>
					</div>
				</div>
			</header>

			<!-- Worker Summary Card -->
			<section class="grid grid-cols-1 sm:grid-cols-2 gap-4 rounded-lg border border-stone/80 bg-ash/30 p-4 text-xs">
				<div class="space-y-1.5">
					<p class="font-label-caps text-[10px] font-bold uppercase tracking-wider text-secondary">{t('payroll.slip.workerInfo')}</p>
					<div class="text-sm font-bold text-ink">{worker.fullName}</div>
					<div class="text-xs text-secondary font-mono">{worker.email}</div>
					<div class="flex flex-wrap items-center gap-1.5 pt-1">
						<span class="rounded border px-2 py-0.5 text-[10px] font-bold uppercase tracking-wider {worker.employmentType === 'Freelance' ? 'border-amber-300 bg-amber-50 text-amber-800' : 'border-stone bg-white text-secondary'}">
							{t(`settings.employmentType.${worker.employmentType}`)}
						</span>
						{#each worker.roles as role (role)}
							<span class="rounded border border-stone/80 bg-white px-2 py-0.5 text-[10px] font-medium text-secondary">
								{role}
							</span>
						{/each}
					</div>
				</div>
				<div class="space-y-1.5 sm:text-right flex flex-col sm:items-end justify-between">
					<div>
						<p class="font-label-caps text-[10px] font-bold uppercase tracking-wider text-secondary">{t('payroll.slip.compensationMode')}</p>
						<p class="text-xs font-semibold text-ink mt-0.5">
							{t(`settings.basePayMode.${worker.basePayMode}`)}
						</p>
					</div>
					<div class="rounded-md border border-stone/80 bg-white px-3 py-1.5 font-mono text-xs text-secondary tabular-nums inline-block">
						<strong class="text-ink font-bold">{worker.totalDaysWorked}</strong> {t('payroll.days')} · <strong class="text-ink font-bold">{worker.totalHoursWorked.toFixed(2)}</strong> {t('payroll.hours')}
					</div>
				</div>
			</section>

			<!-- 1. Base Wage Calculation Breakdown -->
			<section class="space-y-2">
				<div class="flex items-center justify-between">
					<h2 class="text-xs font-bold uppercase tracking-wider text-secondary font-label-caps">{t('payroll.slip.tierSection')}</h2>
				</div>
				<div class="rounded-lg border border-stone/80 overflow-hidden">
					<table class="w-full text-left border-collapse text-xs">
						<thead>
							<tr class="border-b border-stone/80 bg-ash/50 text-[11px] font-bold uppercase tracking-wider text-secondary font-label-caps">
								<th class="px-3.5 py-2.5">{t('payroll.colBaseWage')}</th>
								<th class="px-3.5 py-2.5 text-center w-28">{t('attendance.summaryTotalEntries')}</th>
								<th class="px-3.5 py-2.5 text-right w-32">{t('attendance.colHours')}</th>
								<th class="px-3.5 py-2.5 text-right w-36">{t('reports.total')}</th>
							</tr>
						</thead>
						<tbody class="divide-y divide-stone/60">
							{#each worker.tierBreakdown as tier, idx (`tier-${idx}`)}
								<tr class="hover:bg-ash/20">
									<td class="px-3.5 py-2.5 font-medium text-ink">{tier.tierLabel}</td>
									<td class="px-3.5 py-2.5 text-center font-mono tabular-nums text-secondary">{tier.shiftCount}</td>
									<td class="px-3.5 py-2.5 text-right font-mono tabular-nums text-secondary">{tier.totalHours.toFixed(2)} {t('payroll.hours')}</td>
									<td class="px-3.5 py-2.5 text-right font-mono tabular-nums font-bold text-ink">
										{formatCurrency(tier.subtotalAmount, data.locale, currencyCode)}
									</td>
								</tr>
							{:else}
								<tr>
									<td colspan="4" class="px-3.5 py-3 text-center text-secondary italic">{t('payroll.noTiers')}</td>
								</tr>
							{/each}
						</tbody>
						<tfoot>
							<tr class="border-t border-stone/80 bg-ash/30 font-bold text-ink">
								<td colspan="3" class="px-3.5 py-2.5 text-right">{t('payroll.totalBaseWages')}</td>
								<td class="px-3.5 py-2.5 text-right font-mono tabular-nums text-ink">
									{formatCurrency(worker.baseCalculatedWage, data.locale, currencyCode)}
								</td>
							</tr>
						</tfoot>
					</table>
				</div>
			</section>

			<!-- 2. Performance Bonus Breakdown (Ticket Numbers column removed for paper cleanliness) -->
			<section class="space-y-2">
				<div class="flex items-center justify-between">
					<h2 class="text-xs font-bold uppercase tracking-wider text-secondary font-label-caps">{t('payroll.slip.bonusSection')}</h2>
				</div>
				<div class="rounded-lg border border-stone/80 overflow-hidden">
					<table class="w-full text-left border-collapse text-xs">
						<thead>
							<tr class="border-b border-stone/80 bg-ash/50 text-[11px] font-bold uppercase tracking-wider text-secondary font-label-caps">
								<th class="px-3.5 py-2.5">{t('payroll.colBonuses')}</th>
								<th class="px-3.5 py-2.5 text-center w-28">{t('payroll.totalQualifyingUnits')}</th>
								<th class="px-3.5 py-2.5 text-right w-36">{t('reports.total')}</th>
							</tr>
						</thead>
						<tbody class="divide-y divide-stone/60">
							{#each worker.bonusBreakdown as bonus (bonus.bonusTypeCode)}
								<tr class="hover:bg-ash/20">
									<td class="px-3.5 py-2.5">
										<div class="font-semibold text-ink">{bonus.bonusName}</div>
										<div class="text-[11px] text-secondary leading-normal mt-0.5">{bonus.formulaSummary}</div>
									</td>
									<td class="px-3.5 py-2.5 text-center font-mono tabular-nums text-secondary">
										{bonus.qualifiedCount > 0 || bonus.referenceNumbers.length > 0 ? bonus.qualifiedCount : '—'}
									</td>
									<td class="px-3.5 py-2.5 text-right font-mono tabular-nums font-bold text-emerald-700">
										{formatCurrency(bonus.totalBonusAmount, data.locale, currencyCode)}
									</td>
								</tr>
							{:else}
								<tr>
									<td colspan="3" class="px-3.5 py-3 text-center text-secondary italic">{t('payroll.noBonuses')}</td>
								</tr>
							{/each}
						</tbody>
						<tfoot>
							<tr class="border-t border-stone/80 bg-ash/30 font-bold text-ink">
								<td colspan="2" class="px-3.5 py-2.5 text-right">{t('payroll.totalBonuses')}</td>
								<td class="px-3.5 py-2.5 text-right font-mono tabular-nums text-emerald-700">
									{formatCurrency(worker.totalBonusAmount, data.locale, currencyCode)}
								</td>
							</tr>
						</tfoot>
					</table>
				</div>
			</section>

			<!-- 3. Manual Adjustments & Deductions (if any) -->
			{#if worker.adjustments.length > 0}
				<section class="space-y-2">
					<div class="flex items-center justify-between">
						<h2 class="text-xs font-bold uppercase tracking-wider text-secondary font-label-caps">{t('payroll.slip.adjustmentSection')}</h2>
					</div>
					<div class="rounded-lg border border-stone/80 overflow-hidden">
						<table class="w-full text-left border-collapse text-xs">
							<thead>
								<tr class="border-b border-stone/80 bg-ash/50 text-[11px] font-bold uppercase tracking-wider text-secondary font-label-caps">
									<th class="px-3.5 py-2.5">{t('payroll.adjustmentReason')}</th>
									<th class="px-3.5 py-2.5 text-right w-36">{t('reports.total')}</th>
								</tr>
							</thead>
							<tbody class="divide-y divide-stone/60">
								{#each worker.adjustments as adj (adj.id)}
									<tr class="hover:bg-ash/20">
										<td class="px-3.5 py-2.5 text-ink">{adj.reason}</td>
										<td class="px-3.5 py-2.5 text-right font-mono tabular-nums font-bold {adj.amount < 0 ? 'text-error' : 'text-emerald-700'}">
											{adj.amount > 0 ? '+' : ''}{formatCurrency(adj.amount, data.locale, currencyCode)}
										</td>
									</tr>
								{/each}
							</tbody>
							<tfoot>
								<tr class="border-t border-stone/80 bg-ash/30 font-bold text-ink">
									<td class="px-3.5 py-2.5 text-right">{t('payroll.colAdjustments')}</td>
									<td class="px-3.5 py-2.5 text-right font-mono tabular-nums {worker.totalAdjustments < 0 ? 'text-error' : 'text-emerald-700'}">
										{worker.totalAdjustments > 0 ? '+' : ''}{formatCurrency(worker.totalAdjustments, data.locale, currencyCode)}
									</td>
								</tr>
							</tfoot>
						</table>
					</div>
				</section>
			{/if}

			<!-- Net Payable Highlight Banner -->
			<section class="rounded-lg border-2 border-ink bg-ash/40 px-5 py-3.5 flex items-center justify-between print:bg-white print:border-ink">
				<div>
					<span class="font-label-caps text-xs font-bold uppercase tracking-wider text-ink block">{t('payroll.slip.netPayable')}</span>
					<span class="text-[11px] text-secondary font-sans leading-tight block">{t('payroll.slip.computerGenerated')}</span>
				</div>
				<div class="text-2xl font-bold font-mono tabular-nums text-ink tracking-tight">
					{formatCurrency(worker.finalPayableAmount, data.locale, currencyCode)}
				</div>
			</section>

			<!-- Official Dual Signature Area -->
			<footer class="grid grid-cols-2 gap-12 pt-6 text-center text-xs print:pt-6" style="page-break-inside: avoid; break-inside: avoid;">
				<div class="space-y-12">
					<p class="font-semibold text-secondary uppercase text-[10px] tracking-wider mb-0 font-label-caps">{t('payroll.slip.preparedBy')}</p>
					<div class="mx-auto w-48 border-t border-stone pt-1 text-ink font-semibold">
						{slip.shopName}
					</div>
				</div>
				<div class="space-y-12">
					<p class="font-semibold text-secondary uppercase text-[10px] tracking-wider mb-0 font-label-caps">{t('payroll.slip.receivedBy')}</p>
					<div class="mx-auto w-48 border-t border-stone pt-1 text-ink font-semibold">
						{worker.fullName}
					</div>
				</div>
			</footer>
		</article>
	</div>
</div>

<style>
	@media print {
		@page {
			size: A4 portrait;
			margin: 12mm 15mm 12mm 15mm;
		}
		:global(body) {
			background: #ffffff !important;
			color: #1c1815 !important;
			-webkit-print-color-adjust: exact !important;
			print-color-adjust: exact !important;
		}
	}
</style>

