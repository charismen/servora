<script lang="ts">
	import { enhance } from '$app/forms';
	import PageHeader from '$lib/components/PageHeader.svelte';
	import CurrencyInput from '$lib/components/CurrencyInput.svelte';
	import DatePicker from '$lib/components/ui/date-picker/DatePicker.svelte';
	import { can, permissions } from '$lib/auth/permissions';
	import { createTranslator, formatCurrency, formatDate } from '$lib/i18n';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();
	const t = $derived(createTranslator(data.locale));
	const asset = $derived(data.asset);
	const valuation = $derived(asset.valuation);
	const currencyCode = $derived(data.shopProfile?.currencyCode ?? 'IDR');
	const currencySymbol = $derived(currencyCode === 'USD' ? '$' : 'Rp');

	let activeTab = $state<'schedule' | 'custody' | 'maintenance'>('schedule');

	// Modals
	let showEditModal = $state(false);
	let showCustodyModal = $state(false);
	let showMaintenanceModal = $state(false);
	let showDisposeModal = $state(false);
	let showDeleteModal = $state(false);

	let custodyActionType = $state<'CheckOut' | 'CheckIn' | 'Transfer'>('CheckOut');
</script>

<svelte:window
	onkeydown={(e) => {
		if (e.key === 'Escape') {
			showEditModal = false;
			showCustodyModal = false;
			showMaintenanceModal = false;
			showDisposeModal = false;
			showDeleteModal = false;
		}
	}}
/>

<svelte:head>
	<title>{asset.assetTag} · {asset.name} — {t('assets.detail.title')}</title>
</svelte:head>

<div class="space-y-lg pb-xl">
	<PageHeader
		items={[
			{ label: t('common.console'), href: '/app' },
			{ label: t('nav.assets'), href: '/app/assets' },
			{ label: asset.assetTag }
		]}
		title={asset.name}
		subtitle={`${asset.assetTag} · ${asset.companyName ?? t('brand.name')} · ${asset.categoryName ?? '-'} · ${t(`assets.status.${asset.status}`)}`}
	>
		{#snippet actions()}
			<div class="flex flex-wrap items-center gap-1.5 sm:gap-2">
				<a
					class="h-9 px-3 rounded-button border border-stone bg-white text-secondary hover:text-primary hover:bg-ash font-medium text-xs sm:text-body-sm shadow-2xs transition-colors inline-flex items-center gap-1.5"
					href={`/app/assets/${asset.id}/label`}
				>
					<span class="material-symbols-outlined text-[18px]">qr_code</span>
					<span>{t('assets.printTag')}</span>
				</a>

				{#if can(data.user.permissions, permissions.assetsWrite)}
					<button
						type="button"
						class="h-9 px-3 rounded-button border border-stone bg-white text-secondary hover:text-primary hover:bg-ash font-medium text-xs sm:text-body-sm shadow-2xs transition-colors inline-flex items-center gap-1.5 cursor-pointer"
						onclick={() => (showCustodyModal = true)}
					>
						<span class="material-symbols-outlined text-[18px]">badge</span>
						<span>{t('assets.custody.button')}</span>
					</button>

					<button
						type="button"
						class="h-9 px-3 rounded-button border border-stone bg-white text-secondary hover:text-primary hover:bg-ash font-medium text-xs sm:text-body-sm shadow-2xs transition-colors inline-flex items-center gap-1.5 cursor-pointer"
						onclick={() => (showMaintenanceModal = true)}
					>
						<span class="material-symbols-outlined text-[18px]">build</span>
						<span>{t('assets.maintenance.button')}</span>
					</button>

					<button
						type="button"
						class="h-9 px-3 rounded-button border border-stone bg-white text-secondary hover:text-primary hover:bg-ash font-medium text-xs sm:text-body-sm shadow-2xs transition-colors inline-flex items-center gap-1.5 cursor-pointer"
						onclick={() => (showDisposeModal = true)}
					>
						<span class="material-symbols-outlined text-[18px]">sell</span>
						<span>{t('assets.dispose.button')}</span>
					</button>

					<button
						type="button"
						class="h-9 px-3.5 rounded-button font-medium bg-primary text-white hover:opacity-90 shadow-2xs text-xs sm:text-body-sm cursor-pointer transition-opacity inline-flex items-center gap-1.5"
						onclick={() => (showEditModal = true)}
					>
						<span class="material-symbols-outlined text-[18px]">edit</span>
						<span>{t('assets.editAsset')}</span>
					</button>
				{/if}

				{#if can(data.user.permissions, permissions.assetsDelete)}
					<button
						type="button"
						class="h-9 px-2.5 rounded-button border border-rose-200 bg-white text-rose-700 hover:bg-rose-50 font-medium text-xs sm:text-body-sm shadow-2xs transition-colors inline-flex items-center gap-1 cursor-pointer"
						onclick={() => (showDeleteModal = true)}
						title={t('assets.deleteAsset')}
						aria-label={t('assets.deleteAsset')}
					>
						<span class="material-symbols-outlined text-[18px]">delete</span>
					</button>
				{/if}
			</div>
		{/snippet}
	</PageHeader>

	<!-- Top Grid: Valuation Bento + Asset Lifecycle & Info -->
	<div class="grid grid-cols-1 lg:grid-cols-3 gap-md lg:gap-lg">
		<!-- Valuation & Financial Status (Col span 2) -->
		<section class="lg:col-span-2 bg-white border border-stone rounded-xl p-4 sm:p-lg shadow-2xs space-y-md">
			<div class="flex flex-wrap items-center justify-between gap-2 border-b border-stone/60 pb-sm">
				<div>
					<h2 class="font-subheading text-subheading font-bold text-primary">{t('assets.tabs.overview')}</h2>
					<p class="text-caption text-secondary">
						{t('assets.depreciationMethod')}: <strong class="text-primary font-semibold">{t(`assets.depreciationMethod.${asset.depreciationMethod}`)}</strong>
					</p>
				</div>
				<div class="flex items-center gap-2">
					<span class="inline-flex items-center px-2.5 py-1 rounded-full text-caption font-semibold {valuation.isFullyDepreciated ? 'bg-amber-100 text-amber-800' : valuation.isEolApproaching ? 'bg-orange-100 text-orange-800' : 'bg-emerald-100 text-emerald-800'}">
						{valuation.isFullyDepreciated ? t('assets.kpi.eolApproaching') : valuation.isEolApproaching ? `${valuation.lifeConsumedPercent}% EOL` : t('assets.status.Active')}
					</span>
				</div>
			</div>

			<!-- 4-Metric Grid with Responsive Typography -->
			<div class="grid grid-cols-2 xl:grid-cols-4 gap-2.5 sm:gap-md">
				<div class="p-3 bg-ash/40 rounded-lg border border-stone/50 min-w-0">
					<span class="text-caption text-secondary block truncate">{t('assets.currentBookValue')}</span>
					<strong class="text-base sm:text-lg lg:text-[20px] font-bold font-mono tracking-tight text-primary block mt-0.5 truncate" title={formatCurrency(valuation.currentBookValue, data.locale, currencyCode)}>
						{formatCurrency(valuation.currentBookValue, data.locale, currencyCode)}
					</strong>
					<span class="text-[11px] text-secondary truncate block mt-0.5" title={`Orig: ${formatCurrency(valuation.purchaseCost, data.locale, currencyCode)}`}>
						Orig: {formatCurrency(valuation.purchaseCost, data.locale, currencyCode)}
					</span>
				</div>

				<div class="p-3 bg-ash/40 rounded-lg border border-stone/50 min-w-0">
					<span class="text-caption text-secondary block truncate">{t('assets.kpi.accumulatedDeprec')}</span>
					<strong class="text-base sm:text-lg lg:text-[20px] font-bold font-mono tracking-tight text-amber-700 block mt-0.5 truncate" title={formatCurrency(valuation.accumulatedDepreciation, data.locale, currencyCode)}>
						{formatCurrency(valuation.accumulatedDepreciation, data.locale, currencyCode)}
					</strong>
					<span class="text-[11px] text-secondary truncate block mt-0.5" title={`Monthly: ${formatCurrency(valuation.monthlyDepreciation, data.locale, currencyCode)}`}>
						Monthly: {formatCurrency(valuation.monthlyDepreciation, data.locale, currencyCode)}
					</span>
				</div>

				<div class="p-3 bg-ash/40 rounded-lg border border-stone/50 min-w-0">
					<span class="text-caption text-secondary block truncate">{t('assets.estimatedSellingValue')}</span>
					<strong class="text-base sm:text-lg lg:text-[20px] font-bold font-mono tracking-tight text-primary block mt-0.5 truncate" title={formatCurrency(valuation.effectiveSellingValue, data.locale, currencyCode)}>
						{formatCurrency(valuation.effectiveSellingValue, data.locale, currencyCode)}
					</strong>
					<span class="text-[11px] font-medium truncate block mt-0.5 {valuation.estimatedGainLoss >= 0 ? 'text-emerald-700' : 'text-rose-700'}" title={`Gain: ${formatCurrency(valuation.estimatedGainLoss, data.locale, currencyCode)}`}>
						{valuation.estimatedGainLoss >= 0 ? '+' : ''}{formatCurrency(valuation.estimatedGainLoss, data.locale, currencyCode)}
					</span>
				</div>

				<div class="p-3 bg-ash/40 rounded-lg border border-stone/50 min-w-0">
					<span class="text-caption text-secondary block truncate">{t('assets.netTco')}</span>
					<strong class="text-base sm:text-lg lg:text-[20px] font-bold font-mono tracking-tight text-primary block mt-0.5 truncate" title={formatCurrency(valuation.netTco, data.locale, currencyCode)}>
						{formatCurrency(valuation.netTco, data.locale, currencyCode)}
					</strong>
					<span class="text-[11px] text-secondary truncate block mt-0.5" title={`Maint: ${formatCurrency(valuation.totalMaintenanceCost, data.locale, currencyCode)}`}>
						Maint: {formatCurrency(valuation.totalMaintenanceCost, data.locale, currencyCode)}
					</span>
				</div>
			</div>

			<!-- Useful Life Progress -->
			<div class="pt-2">
				<div class="flex flex-wrap justify-between items-center text-caption font-medium mb-1.5 gap-1">
					<span class="text-secondary">
						{t('assets.age')}: <strong class="text-primary">{valuation.ageYearsPart}y {valuation.ageMonthsPart}m</strong> ({valuation.ageMonths} mos)
					</span>
					<span class="text-secondary">
						{t('assets.remainingLife')}: <strong class="text-primary">{valuation.remainingLifeMonths} mos</strong> of {valuation.usefulLifeMonths} mos
					</span>
				</div>
				<div class="h-2.5 w-full bg-ash rounded-full overflow-hidden border border-stone/60">
					<div
						class="h-full transition-all duration-300 {valuation.isFullyDepreciated ? 'bg-amber-600' : valuation.isEolApproaching ? 'bg-orange-500' : 'bg-primary'}"
						style={`width: ${Math.min(100, valuation.lifeConsumedPercent)}%;`}
					></div>
				</div>
			</div>
		</section>

		<!-- Asset Identity, Custody & Location Card -->
		<section class="bg-white border border-stone rounded-xl p-4 sm:p-lg shadow-2xs space-y-sm text-body-sm">
			<h2 class="font-subheading text-subheading font-bold text-primary border-b border-stone/60 pb-sm">
				{t('assets.detail.title')}
			</h2>

			<div class="space-y-2 text-caption">
				<div class="flex items-center justify-between py-1 border-b border-stone/30">
					<span class="text-secondary">{t('assets.status')}:</span>
					<span class="inline-block px-2 py-0.5 rounded text-[11px] font-semibold {asset.status === 'Active' ? 'bg-emerald-100 text-emerald-800' : asset.status === 'UnderMaintenance' ? 'bg-amber-100 text-amber-800' : asset.status === 'Sold' ? 'bg-blue-100 text-blue-800' : 'bg-stone-100 text-stone-700'}">
						{t(`assets.status.${asset.status}`)}
					</span>
				</div>
				<div class="flex items-center justify-between py-1 border-b border-stone/30">
					<span class="text-secondary">{t('assets.condition')}:</span>
					<span class="font-semibold text-primary">{t(`assets.condition.${asset.condition}`)}</span>
				</div>
				<div class="flex items-center justify-between py-1 border-b border-stone/30">
					<span class="text-secondary">{t('assets.custodian')}:</span>
					<span class="font-semibold text-primary inline-flex items-center gap-1">
						<span class="material-symbols-outlined text-[15px] text-secondary">person</span>
						<span>{asset.assignedUserName ?? t('assets.unassigned')}</span>
					</span>
				</div>
				<div class="flex items-center justify-between py-1 border-b border-stone/30">
					<span class="text-secondary">{t('assets.location')}:</span>
					<span class="font-semibold text-primary">{asset.location || '-'}</span>
				</div>
				<div class="flex items-center justify-between py-1 border-b border-stone/30">
					<span class="text-secondary">{t('assets.companyTag')}:</span>
					<span class="font-semibold text-primary">{asset.companyName || '-'}</span>
				</div>
				<div class="flex items-center justify-between py-1 border-b border-stone/30">
					<span class="text-secondary">{t('assets.department')}:</span>
					<span class="font-semibold text-primary">{asset.department || '-'}</span>
				</div>
				<div class="flex items-center justify-between py-1 border-b border-stone/30">
					<span class="text-secondary">{t('assets.brand')} / {t('assets.model')}:</span>
					<span class="font-semibold text-primary">{[asset.brand, asset.model].filter(Boolean).join(' ') || '-'}</span>
				</div>
				<div class="flex items-center justify-between py-1 border-b border-stone/30">
					<span class="text-secondary">{t('assets.serialNumber')}:</span>
					<span class="font-mono font-semibold text-primary">{asset.serialNumber || '-'}</span>
				</div>
				<div class="flex items-center justify-between py-1 border-b border-stone/30">
					<span class="text-secondary">{t('assets.purchaseDate')}:</span>
					<span class="font-semibold text-primary">{formatDate(asset.purchaseDate, data.locale)}</span>
				</div>
				<div class="flex items-center justify-between py-1 border-b border-stone/30">
					<span class="text-secondary">{t('assets.warrantyExpiresAt')}:</span>
					<span class="font-semibold {valuation.isWarrantyActive ? 'text-emerald-700' : 'text-secondary'}">
						{asset.warrantyExpiresAt ? formatDate(asset.warrantyExpiresAt, data.locale) : '-'}
						{#if asset.warrantyExpiresAt}
							<span class="text-[10px] ml-1 px-1.5 py-0.5 rounded {valuation.isWarrantyActive ? 'bg-emerald-50 text-emerald-700 font-bold' : 'bg-stone-100 text-stone-600'}">
								{valuation.isWarrantyActive ? t('assets.warrantyActive') : t('assets.warrantyExpired')}
							</span>
						{/if}
					</span>
				</div>
				{#if asset.supplierName || asset.invoiceNumber}
					<div class="flex items-center justify-between py-1">
						<span class="text-secondary">{t('assets.supplier')}:</span>
						<span class="text-primary font-medium">{asset.supplierName || '-'} {asset.invoiceNumber ? `(${asset.invoiceNumber})` : ''}</span>
					</div>
				{/if}
			</div>
		</section>
	</div>

	<!-- Tabbed Section: Schedule / Custody / Maintenance -->
	<section class="bg-white border border-stone rounded-xl shadow-2xs overflow-hidden">
		<!-- Tabs Control Header with Horizontal Scroll for Mobile -->
		<div class="flex border-b border-stone bg-ash/30 px-3 sm:px-lg pt-1 sm:pt-2 gap-1 sm:gap-2 overflow-x-auto no-scrollbar whitespace-nowrap">
			<button
				type="button"
				class="px-3 sm:px-4 py-2.5 font-medium text-xs sm:text-body-sm transition-colors border-b-2 cursor-pointer inline-flex items-center gap-1.5 shrink-0 {activeTab === 'schedule' ? 'border-primary text-primary font-semibold' : 'border-transparent text-secondary hover:text-primary'}"
				onclick={() => (activeTab = 'schedule')}
			>
				<span class="material-symbols-outlined text-[18px]">calendar_month</span>
				<span>{t('assets.tabs.schedule')}</span>
			</button>

			<button
				type="button"
				class="px-3 sm:px-4 py-2.5 font-medium text-xs sm:text-body-sm transition-colors border-b-2 cursor-pointer inline-flex items-center gap-1.5 shrink-0 {activeTab === 'custody' ? 'border-primary text-primary font-semibold' : 'border-transparent text-secondary hover:text-primary'}"
				onclick={() => (activeTab = 'custody')}
			>
				<span class="material-symbols-outlined text-[18px]">badge</span>
				<span>{t('assets.tabs.custody')}</span>
				<span class="ml-1 px-1.5 py-0.2 rounded-full text-[10px] font-bold {activeTab === 'custody' ? 'bg-primary text-white' : 'bg-stone/30 text-secondary'}">
					{asset.custodyLogs.length}
				</span>
			</button>

			<button
				type="button"
				class="px-3 sm:px-4 py-2.5 font-medium text-xs sm:text-body-sm transition-colors border-b-2 cursor-pointer inline-flex items-center gap-1.5 shrink-0 {activeTab === 'maintenance' ? 'border-primary text-primary font-semibold' : 'border-transparent text-secondary hover:text-primary'}"
				onclick={() => (activeTab = 'maintenance')}
			>
				<span class="material-symbols-outlined text-[18px]">build</span>
				<span>{t('assets.tabs.maintenance')}</span>
				<span class="ml-1 px-1.5 py-0.2 rounded-full text-[10px] font-bold {activeTab === 'maintenance' ? 'bg-primary text-white' : 'bg-stone/30 text-secondary'}">
					{asset.maintenanceLogs.length}
				</span>
			</button>
		</div>

		<!-- Tab Content -->
		<div class="p-3 sm:p-lg">
			<!-- TAB 1: SCHEDULE -->
			{#if activeTab === 'schedule'}
				<div class="overflow-x-auto">
					<table class="w-full text-left text-body-sm min-w-[560px]">
						<thead>
							<tr class="border-b border-stone text-caption text-secondary uppercase font-semibold">
								<th class="py-2.5 px-3">{t('assets.schedule.year')}</th>
								<th class="py-2.5 px-3">{t('assets.schedule.period')}</th>
								<th class="py-2.5 px-3 text-right">{t('assets.schedule.opening')}</th>
								<th class="py-2.5 px-3 text-right">{t('assets.schedule.expense')}</th>
								<th class="py-2.5 px-3 text-right">{t('assets.schedule.accumulated')}</th>
								<th class="py-2.5 px-3 text-right">{t('assets.schedule.closing')}</th>
							</tr>
						</thead>
						<tbody class="divide-y divide-stone/40">
							{#each asset.depreciationSchedule as row}
								<tr class={row.isCurrentPeriod ? 'bg-primary/5 font-semibold' : 'hover:bg-ash/20'}>
									<td class="py-3 px-3">
										<span class="inline-flex items-center gap-1.5">
											<span>{t('assets.schedule.year')} {row.yearNumber}</span>
											{#if row.isCurrentPeriod}
												<span class="text-[10px] bg-primary text-white px-2 py-0.5 rounded-full font-bold uppercase tracking-wider">
													{t('assets.schedule.current')}
												</span>
											{/if}
										</span>
									</td>
									<td class="py-3 px-3 text-secondary text-caption whitespace-nowrap">
										{formatDate(row.periodStart, data.locale)} — {formatDate(row.periodEnd, data.locale)}
										<span class="font-mono text-stone-500">({row.startMonth}–{row.endMonth}m)</span>
									</td>
									<td class="py-3 px-3 text-right font-mono">
										{formatCurrency(row.openingBookValue, data.locale, currencyCode)}
									</td>
									<td class="py-3 px-3 text-right font-mono text-amber-700">
										{formatCurrency(row.depreciationExpense, data.locale, currencyCode)}
									</td>
									<td class="py-3 px-3 text-right font-mono text-amber-800">
										{formatCurrency(row.accumulatedDepreciation, data.locale, currencyCode)}
									</td>
									<td class="py-3 px-3 text-right font-mono font-bold text-primary">
										{formatCurrency(row.closingBookValue, data.locale, currencyCode)}
									</td>
								</tr>
							{/each}
						</tbody>
					</table>
				</div>

			<!-- TAB 2: CUSTODY -->
			{:else if activeTab === 'custody'}
				{#if asset.custodyLogs.length === 0}
					<div class="py-14 text-center">
						<div class="max-w-sm mx-auto flex flex-col items-center justify-center text-center">
							<div class="w-12 h-12 rounded-xl bg-ash border border-stone flex items-center justify-center text-secondary mb-3 shadow-2xs">
								<span class="material-symbols-outlined text-[24px]">badge</span>
							</div>
							<h4 class="font-subheading text-body font-bold text-primary mb-1">{t('assets.custody.history')}</h4>
							<p class="text-caption text-secondary leading-relaxed">{t('assets.custody.emptySubtitle')}</p>
						</div>
					</div>
				{:else}
					<!-- Mobile Card View (< 768px) -->
					<div class="block md:hidden space-y-3">
						{#each asset.custodyLogs as log}
							<div class="p-3 bg-white border border-stone rounded-lg shadow-2xs space-y-2 text-caption">
								<div class="flex items-center justify-between">
									<span class="inline-block px-2 py-0.5 rounded text-[11px] font-bold {log.actionType === 'CheckOut' ? 'bg-blue-100 text-blue-800' : log.actionType === 'CheckIn' ? 'bg-stone-100 text-stone-800' : 'bg-purple-100 text-purple-800'}">
										{t(`assets.custody.actionType.${log.actionType}`)}
									</span>
									<span class="text-secondary text-[11px]">
										{formatDate(log.createdAt, data.locale, { dateStyle: 'medium', timeStyle: 'short' })}
									</span>
								</div>

								<div class="flex items-center justify-between pt-1">
									<div class="flex items-center gap-1.5 font-medium text-primary">
										<span class="material-symbols-outlined text-[16px] text-secondary">person</span>
										<span>{log.userName ?? t('assets.unassigned')}</span>
									</div>
									<span class="text-secondary">
										{log.location || '-'}
									</span>
								</div>

								<div class="flex items-center justify-between text-[11px] text-secondary border-t border-stone/30 pt-1.5">
									<span>{t('assets.condition')}: <strong class="text-primary">{t(`assets.condition.${log.condition}`)}</strong></span>
									<span>By: {log.recordedByUserName ?? '-'}</span>
								</div>

								{#if log.notes}
									<p class="text-[11px] text-secondary bg-ash/40 p-1.5 rounded italic">
										"{log.notes}"
									</p>
								{/if}
							</div>
						{/each}
					</div>

					<!-- Desktop Table View (>= 768px) -->
					<div class="hidden md:block overflow-x-auto">
						<table class="w-full text-left text-body-sm">
							<thead>
								<tr class="border-b border-stone text-caption text-secondary uppercase font-semibold">
									<th class="py-2.5 px-3">Date</th>
									<th class="py-2.5 px-3">Action</th>
									<th class="py-2.5 px-3">{t('assets.custodian')}</th>
									<th class="py-2.5 px-3">{t('assets.location')}</th>
									<th class="py-2.5 px-3">{t('assets.condition')}</th>
									<th class="py-2.5 px-3">Notes</th>
									<th class="py-2.5 px-3">Recorded By</th>
								</tr>
							</thead>
							<tbody class="divide-y divide-stone/40">
								{#each asset.custodyLogs as log}
									<tr class="hover:bg-ash/20 transition-colors">
										<td class="py-3 px-3 text-secondary text-caption whitespace-nowrap">
											{formatDate(log.createdAt, data.locale, { dateStyle: 'medium', timeStyle: 'short' })}
										</td>
										<td class="py-3 px-3 font-semibold">
											<span class="inline-block px-2 py-0.5 rounded text-caption {log.actionType === 'CheckOut' ? 'bg-blue-100 text-blue-800' : log.actionType === 'CheckIn' ? 'bg-stone-100 text-stone-800' : 'bg-purple-100 text-purple-800'}">
												{t(`assets.custody.actionType.${log.actionType}`)}
											</span>
										</td>
										<td class="py-3 px-3 font-medium text-primary">
											{log.userName ?? '-'}
										</td>
										<td class="py-3 px-3 text-secondary">
											{log.location ?? '-'}
										</td>
										<td class="py-3 px-3">
											<span class="text-caption font-medium">{t(`assets.condition.${log.condition}`)}</span>
										</td>
										<td class="py-3 px-3 text-secondary text-caption max-w-xs truncate" title={log.notes ?? ''}>
											{log.notes ?? '-'}
										</td>
										<td class="py-3 px-3 text-secondary text-caption">
											{log.recordedByUserName ?? '-'}
										</td>
									</tr>
								{/each}
							</tbody>
						</table>
					</div>
				{/if}

			<!-- TAB 3: MAINTENANCE -->
			{:else if activeTab === 'maintenance'}
				{#if asset.maintenanceLogs.length === 0}
					<div class="py-14 text-center">
						<div class="max-w-sm mx-auto flex flex-col items-center justify-center text-center">
							<div class="w-12 h-12 rounded-xl bg-ash border border-stone flex items-center justify-center text-secondary mb-3 shadow-2xs">
								<span class="material-symbols-outlined text-[24px]">build</span>
							</div>
							<h4 class="font-subheading text-body font-bold text-primary mb-1">{t('assets.maintenance.history')}</h4>
							<p class="text-caption text-secondary leading-relaxed">{t('assets.maintenance.emptySubtitle')}</p>
						</div>
					</div>
				{:else}
					<!-- Mobile Card View (< 768px) -->
					<div class="block md:hidden space-y-3">
						{#each asset.maintenanceLogs as log}
							<div class="p-3 bg-white border border-stone rounded-lg shadow-2xs space-y-2 text-caption">
								<div class="flex items-center justify-between">
									<span class="inline-block px-2 py-0.5 rounded text-[11px] bg-ash font-semibold text-primary">
										{t(`assets.maintenance.type.${log.maintenanceType}`)}
									</span>
									<span class="text-secondary text-[11px]">
										{formatDate(log.performedAt, data.locale)}
									</span>
								</div>

								<div>
									<strong class="block text-primary font-medium text-body-sm">{log.title}</strong>
									{#if log.description}
										<p class="text-[11px] text-secondary line-clamp-2 mt-0.5">{log.description}</p>
									{/if}
								</div>

								<div class="flex items-center justify-between border-t border-stone/30 pt-1.5">
									<span class="text-secondary">
										Cost: <strong class="text-primary font-mono font-bold">{formatCurrency(log.cost, data.locale, currencyCode)}</strong>
									</span>
									{#if log.serviceTicketId}
										<a class="text-primary font-mono underline hover:text-primary/80 text-[11px]" href={`/app/tickets/${log.serviceTicketId}`}>
											Ticket {log.ticketNumber || `#${log.serviceTicketId}`}
										</a>
									{/if}
								</div>

								<div class="flex items-center justify-between text-[11px] text-secondary">
									<span>By: {log.performedBy ?? '-'}</span>
									{#if log.nextDueAt}
										<span>Next: {formatDate(log.nextDueAt, data.locale)}</span>
									{/if}
								</div>
							</div>
						{/each}
					</div>

					<!-- Desktop Table View (>= 768px) -->
					<div class="hidden md:block overflow-x-auto">
						<table class="w-full text-left text-body-sm">
							<thead>
								<tr class="border-b border-stone text-caption text-secondary uppercase font-semibold">
									<th class="py-2.5 px-3">Date</th>
									<th class="py-2.5 px-3">{t('assets.maintenance.type')}</th>
									<th class="py-2.5 px-3">Title & Description</th>
									<th class="py-2.5 px-3 text-right">{t('assets.maintenance.cost')}</th>
									<th class="py-2.5 px-3">{t('assets.maintenance.performedBy')}</th>
									<th class="py-2.5 px-3">Ticket</th>
									<th class="py-2.5 px-3">{t('assets.maintenance.nextDueAt')}</th>
								</tr>
							</thead>
							<tbody class="divide-y divide-stone/40">
								{#each asset.maintenanceLogs as log}
									<tr class="hover:bg-ash/20 transition-colors">
										<td class="py-3 px-3 text-secondary text-caption whitespace-nowrap">
											{formatDate(log.performedAt, data.locale)}
										</td>
										<td class="py-3 px-3">
											<span class="inline-block px-2 py-0.5 rounded text-caption bg-ash font-medium text-primary">
												{t(`assets.maintenance.type.${log.maintenanceType}`)}
											</span>
										</td>
										<td class="py-3 px-3">
											<strong class="block text-primary font-medium">{log.title}</strong>
											{#if log.description}
												<p class="text-caption text-secondary line-clamp-1">{log.description}</p>
											{/if}
										</td>
										<td class="py-3 px-3 text-right font-mono font-bold text-primary whitespace-nowrap">
											{formatCurrency(log.cost, data.locale, currencyCode)}
										</td>
										<td class="py-3 px-3 text-secondary text-caption">
											{log.performedBy ?? '-'}
										</td>
										<td class="py-3 px-3 text-caption">
											{#if log.serviceTicketId}
												<a class="text-primary font-mono underline hover:text-primary/80" href={`/app/tickets/${log.serviceTicketId}`}>
													{log.ticketNumber || `#${log.serviceTicketId}`}
												</a>
											{:else}
												-
											{/if}
										</td>
										<td class="py-3 px-3 text-secondary text-caption whitespace-nowrap">
											{log.nextDueAt ? formatDate(log.nextDueAt, data.locale) : '-'}
										</td>
									</tr>
								{/each}
							</tbody>
						</table>
					</div>
				{/if}
			{/if}
		</div>
	</section>
</div>

<!-- ================= MODALS ================= -->

<!-- 1. Edit Asset Modal -->
{#if showEditModal}
	<div
		class="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-xs p-3 sm:p-4"
		onclick={(e) => { if (e.target === e.currentTarget) showEditModal = false; }}
		role="presentation"
	>
		<div
			class="w-full max-w-2xl bg-white rounded-xl shadow-2xl border border-stone flex flex-col max-h-[90vh] overflow-hidden"
			role="dialog"
			aria-modal="true"
			aria-labelledby="modal-edit-asset-title"
		>
			<div class="flex items-center justify-between border-b border-stone px-5 py-3.5 bg-white sticky top-0 z-10 shrink-0">
				<div>
					<h3 id="modal-edit-asset-title" class="font-subheading text-subheading font-bold text-primary">{t('assets.editAsset')}</h3>
					<p class="text-caption text-secondary">Update asset details, location, and depreciation parameters</p>
				</div>
				<button
					type="button"
					class="p-1 rounded text-secondary hover:text-primary hover:bg-ash cursor-pointer transition-colors"
					onclick={() => (showEditModal = false)}
					aria-label={t('common.close')}
				>
					<span class="material-symbols-outlined text-[20px]">close</span>
				</button>
			</div>

			<form
				id="edit-asset-form"
				method="POST"
				action="?/updateAsset"
				use:enhance={() => {
					return async ({ update }) => {
						showEditModal = false;
						await update();
					};
				}}
				class="p-5 overflow-y-auto space-y-4 text-body-sm flex-1"
			>
				<div class="space-y-3">
					<h4 class="text-caption font-bold uppercase tracking-wider text-secondary border-b border-stone/50 pb-1">
						Identification & Location
					</h4>
					<div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
						<div>
							<label for="edit-asset-tag" class="block text-caption font-semibold text-secondary mb-1">{t('assets.tag')} *</label>
							<input id="edit-asset-tag" name="assetTag" type="text" value={asset.assetTag} required class="w-full border border-stone rounded-md px-3 py-1.5 font-mono text-body-sm bg-white focus:outline-none focus:ring-1 focus:ring-primary" />
						</div>
						<div>
							<label for="edit-asset-name" class="block text-caption font-semibold text-secondary mb-1">{t('assets.name')} *</label>
							<input id="edit-asset-name" name="name" type="text" value={asset.name} required class="w-full border border-stone rounded-md px-3 py-1.5 text-body-sm bg-white focus:outline-none focus:ring-1 focus:ring-primary" />
						</div>

						<div>
							<label for="edit-asset-category" class="block text-caption font-semibold text-secondary mb-1">{t('assets.category')}</label>
							<select id="edit-asset-category" name="categoryId" class="w-full border border-stone rounded-md px-3 py-1.5 text-body-sm bg-white focus:outline-none focus:ring-1 focus:ring-primary">
								<option value="">-- {t('assets.filter.allCategories')} --</option>
								{#each data.categories as cat}
									<option value={cat.id} selected={asset.categoryId === cat.id}>{cat.name}</option>
								{/each}
							</select>
						</div>

						<div>
							<label for="edit-asset-company" class="block text-caption font-semibold text-secondary mb-1">{t('assets.companyTag')}</label>
							<input id="edit-asset-company" name="companyName" type="text" value={asset.companyName ?? ''} class="w-full border border-stone rounded-md px-3 py-1.5 text-body-sm bg-white focus:outline-none focus:ring-1 focus:ring-primary" />
						</div>

						<div>
							<label for="edit-asset-dept" class="block text-caption font-semibold text-secondary mb-1">{t('assets.department')}</label>
							<input id="edit-asset-dept" name="department" type="text" value={asset.department ?? ''} class="w-full border border-stone rounded-md px-3 py-1.5 text-body-sm bg-white focus:outline-none focus:ring-1 focus:ring-primary" />
						</div>

						<div>
							<label for="edit-asset-location" class="block text-caption font-semibold text-secondary mb-1">{t('assets.location')}</label>
							<input id="edit-asset-location" name="location" type="text" value={asset.location ?? ''} class="w-full border border-stone rounded-md px-3 py-1.5 text-body-sm bg-white focus:outline-none focus:ring-1 focus:ring-primary" />
						</div>
					</div>
				</div>

				<div class="space-y-3 pt-2">
					<h4 class="text-caption font-bold uppercase tracking-wider text-secondary border-b border-stone/50 pb-1">
						Physical Specifications
					</h4>
					<div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
						<div>
							<label for="edit-asset-brand" class="block text-caption font-semibold text-secondary mb-1">{t('assets.brand')}</label>
							<input id="edit-asset-brand" name="brand" type="text" value={asset.brand ?? ''} class="w-full border border-stone rounded-md px-3 py-1.5 text-body-sm bg-white focus:outline-none focus:ring-1 focus:ring-primary" />
						</div>

						<div>
							<label for="edit-asset-model" class="block text-caption font-semibold text-secondary mb-1">{t('assets.model')}</label>
							<input id="edit-asset-model" name="model" type="text" value={asset.model ?? ''} class="w-full border border-stone rounded-md px-3 py-1.5 text-body-sm bg-white focus:outline-none focus:ring-1 focus:ring-primary" />
						</div>

						<div>
							<label for="edit-asset-serial" class="block text-caption font-semibold text-secondary mb-1">{t('assets.serialNumber')}</label>
							<input id="edit-asset-serial" name="serialNumber" type="text" value={asset.serialNumber ?? ''} class="w-full border border-stone rounded-md px-3 py-1.5 font-mono text-body-sm bg-white focus:outline-none focus:ring-1 focus:ring-primary" />
						</div>

						<div>
							<label for="edit-asset-condition" class="block text-caption font-semibold text-secondary mb-1">{t('assets.condition')}</label>
							<select id="edit-asset-condition" name="condition" class="w-full border border-stone rounded-md px-3 py-1.5 text-body-sm bg-white focus:outline-none focus:ring-1 focus:ring-primary">
								{#each ['New', 'Good', 'Fair', 'Poor', 'Damaged'] as cond}
									<option value={cond} selected={asset.condition === cond}>{t(`assets.condition.${cond}`)}</option>
								{/each}
							</select>
						</div>
					</div>
				</div>

				<div class="space-y-3 pt-2">
					<h4 class="text-caption font-bold uppercase tracking-wider text-secondary border-b border-stone/50 pb-1">
						Valuation & Depreciation
					</h4>
					<div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
						<div>
							<label for="edit-asset-cost" class="block text-caption font-semibold text-secondary mb-1">{t('assets.purchaseCost')} *</label>
							<CurrencyInput
								id="edit-asset-cost"
								name="purchaseCost"
								required
								prefix={currencySymbol}
								locale={data.locale}
								currency={currencyCode}
								value={asset.purchaseCost}
							/>
						</div>

						<div>
							<label for="edit-asset-salvage" class="block text-caption font-semibold text-secondary mb-1">{t('assets.salvageValue')}</label>
							<CurrencyInput
								id="edit-asset-salvage"
								name="salvageValue"
								prefix={currencySymbol}
								locale={data.locale}
								currency={currencyCode}
								value={asset.salvageValue}
							/>
						</div>

						<div>
							<label for="edit-asset-useful-life" class="block text-caption font-semibold text-secondary mb-1">{t('assets.usefulLifeMonths')}</label>
							<input id="edit-asset-useful-life" name="usefulLifeMonths" type="number" min="1" value={asset.usefulLifeMonths} class="w-full border border-stone rounded-md px-3 py-1.5 font-mono text-body-sm bg-white focus:outline-none focus:ring-1 focus:ring-primary" />
						</div>

						<div>
							<label for="edit-asset-selling" class="block text-caption font-semibold text-secondary mb-1">{t('assets.estimatedSellingValue')}</label>
							<CurrencyInput
								id="edit-asset-selling"
								name="estimatedSellingValue"
								prefix={currencySymbol}
								locale={data.locale}
								currency={currencyCode}
								value={asset.estimatedSellingValue ?? ''}
							/>
						</div>
					</div>
				</div>

				<div class="space-y-3 pt-2">
					<h4 class="text-caption font-bold uppercase tracking-wider text-secondary border-b border-stone/50 pb-1">
						Dates & Notes
					</h4>
					<div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
						<div>
							<label for="edit-asset-purchase-date" class="block text-caption font-semibold text-secondary mb-1">{t('assets.purchaseDate')}</label>
							<DatePicker id="edit-asset-purchase-date" name="purchaseDate" value={asset.purchaseDate.slice(0, 10)} />
						</div>

						<div>
							<label for="edit-asset-warranty" class="block text-caption font-semibold text-secondary mb-1">{t('assets.warrantyExpiresAt')}</label>
							<DatePicker id="edit-asset-warranty" name="warrantyExpiresAt" value={asset.warrantyExpiresAt?.slice(0, 10) ?? ''} clearable />
						</div>

						<div class="sm:col-span-2">
							<label for="edit-asset-desc" class="block text-caption font-semibold text-secondary mb-1">{t('assets.description')}</label>
							<textarea id="edit-asset-desc" name="description" rows="2" class="w-full border border-stone rounded-md px-3 py-1.5 text-body-sm bg-white focus:outline-none focus:ring-1 focus:ring-primary">{asset.description ?? ''}</textarea>
						</div>
					</div>
				</div>
			</form>

			<!-- Sticky Modal Footer -->
			<div class="border-t border-stone px-5 py-3 bg-white sticky bottom-0 z-10 flex items-center justify-end gap-2 shrink-0">
				<button
					type="button"
					class="px-4 py-2 border border-stone rounded-md text-secondary hover:text-primary hover:bg-ash cursor-pointer text-body-sm font-medium transition-colors"
					onclick={() => (showEditModal = false)}
				>
					{t('common.cancel')}
				</button>
				<button
					type="submit"
					form="edit-asset-form"
					class="px-5 py-2 bg-primary text-white rounded-md font-semibold text-body-sm hover:opacity-90 shadow-2xs cursor-pointer transition-opacity"
				>
					{t('common.save')}
				</button>
			</div>
		</div>
	</div>
{/if}

<!-- 2. Custody Modal -->
{#if showCustodyModal}
	<div
		class="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-xs p-3 sm:p-4"
		onclick={(e) => { if (e.target === e.currentTarget) showCustodyModal = false; }}
		role="presentation"
	>
		<div
			class="w-full max-w-lg bg-white rounded-xl shadow-2xl border border-stone flex flex-col max-h-[90vh] overflow-hidden"
			role="dialog"
			aria-modal="true"
			aria-labelledby="modal-custody-title"
		>
			<div class="flex items-center justify-between border-b border-stone px-5 py-3.5 bg-white sticky top-0 z-10 shrink-0">
				<div>
					<h3 id="modal-custody-title" class="font-subheading text-subheading font-bold text-primary">{t('assets.custody.title')}</h3>
					<p class="text-caption text-secondary">Record equipment handover, check-in, or location transfer</p>
				</div>
				<button
					type="button"
					class="p-1 rounded text-secondary hover:text-primary hover:bg-ash cursor-pointer transition-colors"
					onclick={() => (showCustodyModal = false)}
					aria-label={t('common.close')}
				>
					<span class="material-symbols-outlined text-[20px]">close</span>
				</button>
			</div>

			<form
				id="custody-form"
				method="POST"
				action="?/updateCustody"
				use:enhance={() => {
					return async ({ update }) => {
						showCustodyModal = false;
						await update();
					};
				}}
				class="p-5 overflow-y-auto space-y-4 text-body-sm flex-1"
			>
				<div>
					<span class="block text-caption font-semibold text-secondary mb-1">Action Type</span>
					<div class="grid grid-cols-3 gap-2">
						<button
							type="button"
							class="py-2 px-3 text-caption font-bold rounded-md border text-center transition-all cursor-pointer {custodyActionType === 'CheckOut' ? 'bg-primary text-white border-primary' : 'bg-ash text-secondary border-stone hover:text-primary'}"
							onclick={() => (custodyActionType = 'CheckOut')}
						>
							{t('assets.custody.checkOut')}
						</button>
						<button
							type="button"
							class="py-2 px-3 text-caption font-bold rounded-md border text-center transition-all cursor-pointer {custodyActionType === 'CheckIn' ? 'bg-primary text-white border-primary' : 'bg-ash text-secondary border-stone hover:text-primary'}"
							onclick={() => (custodyActionType = 'CheckIn')}
						>
							{t('assets.custody.checkIn')}
						</button>
						<button
							type="button"
							class="py-2 px-3 text-caption font-bold rounded-md border text-center transition-all cursor-pointer {custodyActionType === 'Transfer' ? 'bg-primary text-white border-primary' : 'bg-ash text-secondary border-stone hover:text-primary'}"
							onclick={() => (custodyActionType = 'Transfer')}
						>
							{t('assets.custody.transfer')}
						</button>
					</div>
					<input type="hidden" name="actionType" value={custodyActionType} />
				</div>

				{#if custodyActionType !== 'CheckIn'}
					<div>
						<label for="custody-user-id" class="block text-caption font-semibold text-secondary mb-1">{t('assets.custody.selectStaff')}</label>
						<select id="custody-user-id" name="userId" class="w-full border border-stone rounded-md px-3 py-2 text-body-sm bg-white focus:outline-none focus:ring-1 focus:ring-primary">
							<option value="">-- {t('assets.unassigned')} --</option>
							{#each data.staff as s}
								<option value={s.id} selected={asset.assignedUserId === s.id}>{s.fullName} ({s.email})</option>
							{/each}
						</select>
					</div>
				{/if}

				<div>
					<label for="custody-location" class="block text-caption font-semibold text-secondary mb-1">{t('assets.custody.targetLocation')}</label>
					<input id="custody-location" name="location" type="text" value={asset.location ?? ''} placeholder="e.g. Storage Shelf B, Main Bench, Lab" class="w-full border border-stone rounded-md px-3 py-2 text-body-sm bg-white focus:outline-none focus:ring-1 focus:ring-primary" />
				</div>

				<div>
					<label for="custody-condition" class="block text-caption font-semibold text-secondary mb-1">{t('assets.custody.conditionAtHandover')}</label>
					<select id="custody-condition" name="condition" class="w-full border border-stone rounded-md px-3 py-2 text-body-sm bg-white focus:outline-none focus:ring-1 focus:ring-primary">
						{#each ['New', 'Good', 'Fair', 'Poor', 'Damaged'] as cond}
							<option value={cond} selected={asset.condition === cond}>{t(`assets.condition.${cond}`)}</option>
						{/each}
					</select>
				</div>

				<div>
					<label for="custody-notes" class="block text-caption font-semibold text-secondary mb-1">{t('assets.custody.notes')}</label>
					<textarea id="custody-notes" name="notes" rows="2" placeholder="Handover reason, equipment check results..." class="w-full border border-stone rounded-md px-3 py-2 text-body-sm bg-white focus:outline-none focus:ring-1 focus:ring-primary"></textarea>
				</div>
			</form>

			<div class="border-t border-stone px-5 py-3 bg-white sticky bottom-0 z-10 flex items-center justify-end gap-2 shrink-0">
				<button
					type="button"
					class="px-4 py-2 border border-stone rounded-md text-secondary hover:text-primary hover:bg-ash cursor-pointer text-body-sm font-medium transition-colors"
					onclick={() => (showCustodyModal = false)}
				>
					{t('common.cancel')}
				</button>
				<button
					type="submit"
					form="custody-form"
					class="px-5 py-2 bg-primary text-white rounded-md font-semibold text-body-sm hover:opacity-90 shadow-2xs cursor-pointer transition-opacity"
				>
					{t('common.save')}
				</button>
			</div>
		</div>
	</div>
{/if}

<!-- 3. Maintenance Modal -->
{#if showMaintenanceModal}
	<div
		class="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-xs p-3 sm:p-4"
		onclick={(e) => { if (e.target === e.currentTarget) showMaintenanceModal = false; }}
		role="presentation"
	>
		<div
			class="w-full max-w-lg bg-white rounded-xl shadow-2xl border border-stone flex flex-col max-h-[90vh] overflow-hidden"
			role="dialog"
			aria-modal="true"
			aria-labelledby="modal-maint-title"
		>
			<div class="flex items-center justify-between border-b border-stone px-5 py-3.5 bg-white sticky top-0 z-10 shrink-0">
				<div>
					<h3 id="modal-maint-title" class="font-subheading text-subheading font-bold text-primary">{t('assets.maintenance.logMaintenance')}</h3>
					<p class="text-caption text-secondary">Log repairs, calibrations, or scheduled servicing</p>
				</div>
				<button
					type="button"
					class="p-1 rounded text-secondary hover:text-primary hover:bg-ash cursor-pointer transition-colors"
					onclick={() => (showMaintenanceModal = false)}
					aria-label={t('common.close')}
				>
					<span class="material-symbols-outlined text-[20px]">close</span>
				</button>
			</div>

			<form
				id="maintenance-form"
				method="POST"
				action="?/addMaintenance"
				use:enhance={() => {
					return async ({ update }) => {
						showMaintenanceModal = false;
						await update();
					};
				}}
				class="p-5 overflow-y-auto space-y-4 text-body-sm flex-1"
			>
				<div>
					<label for="maint-type" class="block text-caption font-semibold text-secondary mb-1">{t('assets.maintenance.type')}</label>
					<select id="maint-type" name="maintenanceType" class="w-full border border-stone rounded-md px-3 py-2 text-body-sm bg-white focus:outline-none focus:ring-1 focus:ring-primary">
						<option value="Preventive">{t('assets.maintenance.type.Preventive')}</option>
						<option value="Corrective">{t('assets.maintenance.type.Corrective')}</option>
						<option value="Calibration">{t('assets.maintenance.type.Calibration')}</option>
						<option value="Upgrade">{t('assets.maintenance.type.Upgrade')}</option>
					</select>
				</div>

				<div>
					<label for="maint-title-input" class="block text-caption font-semibold text-secondary mb-1">Title / Summary *</label>
					<input id="maint-title-input" name="title" type="text" required placeholder="e.g. Periodic calibration & probe replacement" class="w-full border border-stone rounded-md px-3 py-2 text-body-sm bg-white focus:outline-none focus:ring-1 focus:ring-primary" />
				</div>

				<div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
					<div>
						<label for="maint-cost" class="block text-caption font-semibold text-secondary mb-1">{t('assets.maintenance.cost')} *</label>
						<CurrencyInput
							id="maint-cost"
							name="cost"
							required
							prefix={currencySymbol}
							locale={data.locale}
							currency={currencyCode}
							value={0}
						/>
					</div>
					<div>
						<label for="maint-performed-by" class="block text-caption font-semibold text-secondary mb-1">{t('assets.maintenance.performedBy')}</label>
						<input id="maint-performed-by" name="performedBy" type="text" placeholder="Internal or Vendor name" class="w-full border border-stone rounded-md px-3 py-2 text-body-sm bg-white focus:outline-none focus:ring-1 focus:ring-primary" />
					</div>
				</div>

				<div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
					<div>
						<label for="maint-performed-at" class="block text-caption font-semibold text-secondary mb-1">{t('assets.maintenance.performedAt')}</label>
						<DatePicker id="maint-performed-at" name="performedAt" value={new Date().toISOString().slice(0, 10)} />
					</div>
					<div>
						<label for="maint-next-due-at" class="block text-caption font-semibold text-secondary mb-1">{t('assets.maintenance.nextDueAt')}</label>
						<DatePicker id="maint-next-due-at" name="nextDueAt" clearable />
					</div>
				</div>

				{#if data.tickets.length > 0}
					<div>
						<label for="maint-ticket-id" class="block text-caption font-semibold text-secondary mb-1">{t('assets.maintenance.linkTicket')}</label>
						<select id="maint-ticket-id" name="serviceTicketId" class="w-full border border-stone rounded-md px-3 py-2 text-body-sm bg-white focus:outline-none focus:ring-1 focus:ring-primary">
							<option value="">-- No Linked Ticket --</option>
							{#each data.tickets as tick}
								<option value={tick.id}>{tick.ticketNumber} · {tick.device} ({tick.status})</option>
							{/each}
						</select>
					</div>
				{/if}

				<div>
					<label for="maint-desc" class="block text-caption font-semibold text-secondary mb-1">Description / Details</label>
					<textarea id="maint-desc" name="description" rows="2" placeholder="Parts replaced, calibration results..." class="w-full border border-stone rounded-md px-3 py-2 text-body-sm bg-white focus:outline-none focus:ring-1 focus:ring-primary"></textarea>
				</div>

				<label class="flex items-center gap-2 cursor-pointer pt-1">
					<input type="checkbox" name="setUnderMaintenance" value="true" class="rounded text-primary focus:ring-primary" />
					<span class="text-caption text-secondary">{t('assets.maintenance.setUnderMaintenance')}</span>
				</label>
			</form>

			<div class="border-t border-stone px-5 py-3 bg-white sticky bottom-0 z-10 flex items-center justify-end gap-2 shrink-0">
				<button
					type="button"
					class="px-4 py-2 border border-stone rounded-md text-secondary hover:text-primary hover:bg-ash cursor-pointer text-body-sm font-medium transition-colors"
					onclick={() => (showMaintenanceModal = false)}
				>
					{t('common.cancel')}
				</button>
				<button
					type="submit"
					form="maintenance-form"
					class="px-5 py-2 bg-primary text-white rounded-md font-semibold text-body-sm hover:opacity-90 shadow-2xs cursor-pointer transition-opacity"
				>
					{t('common.save')}
				</button>
			</div>
		</div>
	</div>
{/if}

<!-- 4. Dispose / Sell Modal -->
{#if showDisposeModal}
	<div
		class="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-xs p-3 sm:p-4"
		onclick={(e) => { if (e.target === e.currentTarget) showDisposeModal = false; }}
		role="presentation"
	>
		<div
			class="w-full max-w-lg bg-white rounded-xl shadow-2xl border border-stone flex flex-col max-h-[90vh] overflow-hidden"
			role="dialog"
			aria-modal="true"
			aria-labelledby="modal-dispose-title"
		>
			<div class="flex items-center justify-between border-b border-stone px-5 py-3.5 bg-white sticky top-0 z-10 shrink-0">
				<div>
					<h3 id="modal-dispose-title" class="font-subheading text-subheading font-bold text-primary">{t('assets.dispose.title')}</h3>
					<p class="text-caption text-secondary">Record asset liquidation, retirement, or write-off</p>
				</div>
				<button
					type="button"
					class="p-1 rounded text-secondary hover:text-primary hover:bg-ash cursor-pointer transition-colors"
					onclick={() => (showDisposeModal = false)}
					aria-label={t('common.close')}
				>
					<span class="material-symbols-outlined text-[20px]">close</span>
				</button>
			</div>

			<form
				id="dispose-form"
				method="POST"
				action="?/disposeAsset"
				use:enhance={() => {
					return async ({ update }) => {
						showDisposeModal = false;
						await update();
					};
				}}
				class="p-5 overflow-y-auto space-y-4 text-body-sm flex-1"
			>
				<div>
					<label for="dispose-status" class="block text-caption font-semibold text-secondary mb-1">Disposal Action</label>
					<select id="dispose-status" name="status" class="w-full border border-stone rounded-md px-3 py-2 font-semibold text-body-sm bg-white focus:outline-none focus:ring-1 focus:ring-primary">
						<option value="Sold">{t('assets.dispose.sold')}</option>
						<option value="Retired">{t('assets.dispose.retire')}</option>
						<option value="WrittenOff">{t('assets.dispose.writeOff')}</option>
					</select>
				</div>

				<div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
					<div>
						<label for="dispose-date" class="block text-caption font-semibold text-secondary mb-1">{t('assets.dispose.date')}</label>
						<DatePicker id="dispose-date" name="disposedAt" value={new Date().toISOString().slice(0, 10)} />
					</div>
					<div>
						<label for="dispose-price" class="block text-caption font-semibold text-secondary mb-1">{t('assets.actualSoldPrice')} (if Sold)</label>
						<CurrencyInput
							id="dispose-price"
							name="actualSoldPrice"
							prefix={currencySymbol}
							locale={data.locale}
							currency={currencyCode}
							value=""
						/>
					</div>
				</div>

				<div>
					<label for="dispose-notes" class="block text-caption font-semibold text-secondary mb-1">{t('assets.dispose.notes')}</label>
					<textarea id="dispose-notes" name="disposalNotes" rows="2" placeholder="Buyer company, receipt reference, or write-off cause..." class="w-full border border-stone rounded-md px-3 py-2 text-body-sm bg-white focus:outline-none focus:ring-1 focus:ring-primary"></textarea>
				</div>
			</form>

			<div class="border-t border-stone px-5 py-3 bg-white sticky bottom-0 z-10 flex items-center justify-end gap-2 shrink-0">
				<button
					type="button"
					class="px-4 py-2 border border-stone rounded-md text-secondary hover:text-primary hover:bg-ash cursor-pointer text-body-sm font-medium transition-colors"
					onclick={() => (showDisposeModal = false)}
				>
					{t('common.cancel')}
				</button>
				<button
					type="submit"
					form="dispose-form"
					class="px-5 py-2 bg-rose-700 text-white rounded-md font-semibold text-body-sm hover:opacity-90 shadow-2xs cursor-pointer transition-opacity"
				>
					Confirm Disposal
				</button>
			</div>
		</div>
	</div>
{/if}

<!-- 5. Delete Asset Modal -->
{#if showDeleteModal}
	<div
		class="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-xs p-3 sm:p-4"
		onclick={(e) => { if (e.target === e.currentTarget) showDeleteModal = false; }}
		role="presentation"
	>
		<div
			class="w-full max-w-md bg-white rounded-xl shadow-2xl border border-stone p-5 space-y-4"
			role="dialog"
			aria-modal="true"
			aria-labelledby="modal-delete-title"
		>
			<div class="flex items-center gap-3">
				<div class="h-10 w-10 rounded-full bg-rose-100 text-rose-700 flex items-center justify-center shrink-0">
					<span class="material-symbols-outlined text-[24px]">warning</span>
				</div>
				<div>
					<h3 id="modal-delete-title" class="font-subheading text-subheading font-bold text-primary">{t('assets.deleteAsset')}</h3>
					<p class="text-caption text-secondary">{asset.assetTag} · {asset.name}</p>
				</div>
			</div>

			<p class="text-body-sm text-secondary">
				{t('assets.deleteConfirm')}
			</p>

			<form
				method="POST"
				action="?/deleteAsset"
				use:enhance={() => {
					return async ({ update }) => {
						showDeleteModal = false;
						await update();
					};
				}}
				class="flex items-center justify-end gap-2 pt-2 border-t border-stone"
			>
				<button
					type="button"
					class="px-4 py-2 border border-stone rounded-md text-secondary hover:text-primary hover:bg-ash cursor-pointer text-body-sm font-medium transition-colors"
					onclick={() => (showDeleteModal = false)}
				>
					{t('common.cancel')}
				</button>
				<button
					type="submit"
					class="px-4 py-2 bg-rose-700 text-white rounded-md font-semibold text-body-sm hover:opacity-90 shadow-2xs cursor-pointer transition-opacity inline-flex items-center gap-1.5"
				>
					<span class="material-symbols-outlined text-[18px]">delete</span>
					<span>{t('assets.deleteAsset')}</span>
				</button>
			</form>
		</div>
	</div>
{/if}
