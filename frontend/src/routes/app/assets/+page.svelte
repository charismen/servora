<script lang="ts">
	import { enhance } from '$app/forms';
	import PageHeader from '$lib/components/PageHeader.svelte';
	import CurrencyInput from '$lib/components/CurrencyInput.svelte';
	import DatePicker from '$lib/components/ui/date-picker/DatePicker.svelte';
	import { can, permissions } from '$lib/auth/permissions';
	import { createTranslator, formatCurrency, formatDate } from '$lib/i18n';
	import type { AssetCategory, AssetListItem } from '$lib/types/assets';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();
	const t = $derived(createTranslator(data.locale));
	const items = $derived(data.assetsPage.items);
	const summary = $derived(data.assetsPage.summary);
	const currencyCode = $derived(data.shopProfile?.currencyCode ?? 'IDR');
	const currencySymbol = $derived(currencyCode === 'USD' ? '$' : 'Rp');

	// Modals
	let showNewModal = $state(false);
	let showCategoriesModal = $state(false);
	let editingCategory = $state<AssetCategory | null>(null);

	// Form auto-fill on category select
	let selectedCategoryId = $state<number | null>(null);
	let usefulLifeInput = $state(36);
	let salvagePercentInput = $state(0);
	let purchaseCostInput = $state<number | string>('');
	let salvageValueInput = $state<number | string>('');
	let estimatedSellingValueInput = $state<number | string>('');

	function onCategoryChange(catIdStr: string) {
		const catId = Number(catIdStr);
		selectedCategoryId = catId || null;
		const cat = data.categories.find((c) => c.id === catId);
		if (cat) {
			usefulLifeInput = cat.defaultUsefulLifeMonths;
			salvagePercentInput = cat.defaultSalvagePercent;
			if (purchaseCostInput) {
				salvageValueInput = Math.round(Number(purchaseCostInput) * (salvagePercentInput / 100));
			}
		}
	}

	function onPurchaseCostInput() {
		if (salvagePercentInput > 0 && purchaseCostInput) {
			salvageValueInput = Math.round(Number(purchaseCostInput) * (salvagePercentInput / 100));
		}
	}

	function handleWindowKeydown(e: KeyboardEvent) {
		if (e.key === 'Escape') {
			if (showNewModal) showNewModal = false;
			if (showCategoriesModal) {
				showCategoriesModal = false;
				editingCategory = null;
			}
		}
	}

	const hasActiveFilters = $derived(
		Boolean(
			data.filters.search ||
			data.filters.categoryId ||
			data.filters.status ||
			data.filters.condition ||
			data.filters.assignedUserId ||
			data.filters.companyName ||
			data.filters.eolOnly ||
			data.filters.sort
		)
	);
</script>

<svelte:window onkeydown={handleWindowKeydown} />

<svelte:head>
	<title>{t('assets.title')} — {t('brand.name')}</title>
</svelte:head>

<div class="space-y-md sm:space-y-lg pb-xl">
	<PageHeader
		items={[{ label: t('common.console'), href: '/app' }, { label: t('nav.assets') }]}
		title={t('assets.title')}
		subtitle={t('assets.subtitle')}
	>
		{#snippet actions()}
			<div class="flex flex-wrap items-center gap-2 sm:gap-sm">
				<a
					class="h-9 sm:h-10 px-3 sm:px-4 rounded-button border border-stone bg-white text-secondary hover:text-primary hover:bg-ash font-medium text-caption sm:text-body-sm shadow-2xs transition-colors inline-flex items-center gap-1.5"
					href="/app/assets/export"
					title={t('assets.exportCsv')}
				>
					<span class="material-symbols-outlined text-[18px]">download</span>
					<span class="hidden sm:inline">{t('assets.exportCsv')}</span>
				</a>

				<button
					type="button"
					class="h-9 sm:h-10 px-3 sm:px-4 rounded-button border border-stone bg-white text-secondary hover:text-primary hover:bg-ash font-medium text-caption sm:text-body-sm shadow-2xs transition-colors inline-flex items-center gap-1.5 cursor-pointer"
					onclick={() => (showCategoriesModal = true)}
				>
					<span class="material-symbols-outlined text-[18px]">category</span>
					<span class="hidden sm:inline">{t('assets.categories')}</span>
				</button>

				{#if can(data.user.permissions, permissions.assetsWrite)}
					<button
						type="button"
						class="h-9 sm:h-10 px-3.5 sm:px-4 rounded-button font-medium bg-primary text-white hover:opacity-90 shadow-2xs text-caption sm:text-body-sm cursor-pointer transition-opacity inline-flex items-center gap-1.5"
						onclick={() => (showNewModal = true)}
					>
						<span class="material-symbols-outlined text-[18px]">add</span>
						<span>{t('assets.newAsset')}</span>
					</button>
				{/if}
			</div>
		{/snippet}
	</PageHeader>

	<!-- KPI Summary Strip -->
	<section class="grid grid-cols-2 sm:grid-cols-3 xl:grid-cols-6 gap-3 sm:gap-4">
		<div class="bg-white border border-stone rounded-xl p-3 sm:p-4 shadow-2xs transition-all hover:border-slate/80 min-w-0">
			<span class="text-caption font-medium text-secondary block truncate">{t('assets.kpi.totalAssets')}</span>
			<strong class="text-base sm:text-lg xl:text-xl 2xl:text-headline-sm font-bold text-primary block mt-0.5 sm:mt-1 truncate">{summary.totalAssets}</strong>
			<span class="text-[11px] text-secondary mt-1 block truncate">
				{summary.activeCount} {t('assets.kpi.activeCount')} · {summary.availableCount} {t('assets.kpi.availableCount')}
			</span>
		</div>

		<div class="bg-white border border-stone rounded-xl p-3 sm:p-4 shadow-2xs transition-all hover:border-slate/80 min-w-0">
			<span class="text-caption font-medium text-secondary block truncate">{t('assets.kpi.netBookValue')}</span>
			<strong
				class="text-base sm:text-lg xl:text-xl 2xl:text-headline-sm font-bold font-mono tracking-tight text-primary block mt-0.5 sm:mt-1 truncate"
				title={formatCurrency(summary.totalNetBookValue, data.locale, currencyCode)}
			>
				{formatCurrency(summary.totalNetBookValue, data.locale, currencyCode)}
			</strong>
			<span class="text-[11px] text-secondary mt-1 block truncate" title={`Cost: ${formatCurrency(summary.totalPurchaseCost, data.locale, currencyCode)}`}>
				Cost: {formatCurrency(summary.totalPurchaseCost, data.locale, currencyCode)}
			</span>
		</div>

		<div class="bg-white border border-stone rounded-xl p-3 sm:p-4 shadow-2xs transition-all hover:border-slate/80 min-w-0">
			<span class="text-caption font-medium text-secondary block truncate">{t('assets.kpi.accumulatedDeprec')}</span>
			<strong
				class="text-base sm:text-lg xl:text-xl 2xl:text-headline-sm font-bold font-mono tracking-tight text-amber-700 block mt-0.5 sm:mt-1 truncate"
				title={formatCurrency(summary.totalAccumulatedDepreciation, data.locale, currencyCode)}
			>
				{formatCurrency(summary.totalAccumulatedDepreciation, data.locale, currencyCode)}
			</strong>
			<span class="text-[11px] text-secondary mt-1 block truncate" title={`-${formatCurrency(summary.monthlyDepreciationExpense, data.locale, currencyCode)} / mo`}>
				-{formatCurrency(summary.monthlyDepreciationExpense, data.locale, currencyCode)} / mo
			</span>
		</div>

		<div class="bg-white border border-stone rounded-xl p-3 sm:p-4 shadow-2xs transition-all hover:border-slate/80 min-w-0">
			<span class="text-caption font-medium text-secondary block truncate">{t('assets.kpi.sellingValue')}</span>
			<strong
				class="text-base sm:text-lg xl:text-xl 2xl:text-headline-sm font-bold font-mono tracking-tight text-primary block mt-0.5 sm:mt-1 truncate"
				title={formatCurrency(summary.totalEstimatedSellingValue, data.locale, currencyCode)}
			>
				{formatCurrency(summary.totalEstimatedSellingValue, data.locale, currencyCode)}
			</strong>
			<span class="text-[11px] text-emerald-700 font-medium mt-1 block truncate" title={`Gain: ${formatCurrency(summary.totalEstimatedSellingValue - summary.totalNetBookValue, data.locale, currencyCode)}`}>
				Gain: {formatCurrency(summary.totalEstimatedSellingValue - summary.totalNetBookValue, data.locale, currencyCode)}
			</span>
		</div>

		<div class="bg-white border border-stone rounded-xl p-3 sm:p-4 shadow-2xs transition-all hover:border-slate/80 min-w-0">
			<span class="text-caption font-medium text-secondary block truncate">{t('assets.kpi.maintenanceSpend')}</span>
			<strong
				class="text-base sm:text-lg xl:text-xl 2xl:text-headline-sm font-bold font-mono tracking-tight text-primary block mt-0.5 sm:mt-1 truncate"
				title={formatCurrency(summary.totalMaintenanceCost, data.locale, currencyCode)}
			>
				{formatCurrency(summary.totalMaintenanceCost, data.locale, currencyCode)}
			</strong>
			<span class="text-[11px] text-secondary mt-1 block truncate">
				Lifetime repair & TCO
			</span>
		</div>

		<div class="bg-white border border-stone rounded-xl p-3 sm:p-4 shadow-2xs transition-all hover:border-slate/80 min-w-0">
			<span class="text-caption font-medium text-secondary block truncate">Lifecycle Alerts</span>
			<div class="flex items-center gap-2 mt-0.5 sm:mt-1">
				<strong class="text-base sm:text-lg xl:text-xl 2xl:text-headline-sm font-bold {summary.underMaintenanceCount > 0 ? 'text-amber-700' : 'text-primary'}">
					{summary.underMaintenanceCount}
				</strong>
				<span class="text-caption text-secondary">service</span>
				<span class="text-stone-300">/</span>
				<strong class="text-base sm:text-lg xl:text-xl 2xl:text-headline-sm font-bold {summary.eolApproachingCount > 0 ? 'text-orange-600' : 'text-primary'}">
					{summary.eolApproachingCount}
				</strong>
				<span class="text-caption text-secondary">EOL</span>
			</div>
			<span class="text-[11px] text-secondary mt-1 block truncate">
				{summary.underMaintenanceCount} in repair · {summary.eolApproachingCount} near EOL
			</span>
		</div>
	</section>

	<!-- Filter & Search Bar -->
	<section class="bg-white border border-stone rounded-xl p-3 sm:p-4 shadow-2xs">
		<form method="GET" class="space-y-3 text-body-sm">
			<!-- Row 1: Search & Sort By -->
			<div class="flex flex-col sm:flex-row gap-3 items-stretch sm:items-end">
				<div class="flex-1 min-w-0">
					<label for="filter-search" class="block text-caption font-semibold text-secondary mb-1">Search</label>
					<div class="relative">
						<span class="material-symbols-outlined absolute left-3 top-2.5 text-[18px] text-secondary pointer-events-none">search</span>
						<input
							id="filter-search"
							type="search"
							name="search"
							value={data.filters.search ?? ''}
							placeholder="Tag, name, brand, model, S/N, location..."
							class="w-full border border-stone rounded-md pl-10 pr-3 py-2 bg-white text-body-sm focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary shadow-2xs"
						/>
					</div>
				</div>

				<div class="w-full sm:w-56 shrink-0">
					<label for="filter-sort" class="block text-caption font-semibold text-secondary mb-1">Sort By</label>
					<select id="filter-sort" name="sort" class="w-full border border-stone rounded-md px-3 py-2 bg-white text-body-sm focus:outline-none focus:border-primary shadow-2xs">
						<option value="">Asset Tag (Asc)</option>
						<option value="tag-desc" selected={data.filters.sort === 'tag-desc'}>Asset Tag (Desc)</option>
						<option value="name" selected={data.filters.sort === 'name'}>Name</option>
						<option value="cost-desc" selected={data.filters.sort === 'cost-desc'}>Purchase Cost (High)</option>
						<option value="bookvalue-desc" selected={data.filters.sort === 'bookvalue-desc'}>Book Value (High)</option>
						<option value="age-desc" selected={data.filters.sort === 'age-desc'}>Age (Oldest First)</option>
					</select>
				</div>
			</div>

			<!-- Row 2: Facet Dropdowns -->
			<div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3 pt-1">
				<div>
					<label for="filter-category" class="block text-caption font-semibold text-secondary mb-1">{t('assets.category')}</label>
					<select id="filter-category" name="categoryId" class="w-full border border-stone rounded-md px-3 py-2 bg-white text-body-sm focus:outline-none focus:border-primary shadow-2xs">
						<option value="">{t('assets.filter.allCategories')}</option>
						{#each data.categories as cat}
							<option value={cat.id} selected={data.filters.categoryId === String(cat.id)}>{cat.name}</option>
						{/each}
					</select>
				</div>

				<div>
					<label for="filter-status" class="block text-caption font-semibold text-secondary mb-1">{t('assets.status')}</label>
					<select id="filter-status" name="status" class="w-full border border-stone rounded-md px-3 py-2 bg-white text-body-sm focus:outline-none focus:border-primary shadow-2xs">
						<option value="">{t('assets.filter.allStatuses')}</option>
						{#each ['Active', 'Available', 'UnderMaintenance', 'Retired', 'Sold', 'WrittenOff'] as st}
							<option value={st} selected={data.filters.status === st}>{t(`assets.status.${st}`)}</option>
						{/each}
					</select>
				</div>

				<div>
					<label for="filter-condition" class="block text-caption font-semibold text-secondary mb-1">{t('assets.condition')}</label>
					<select id="filter-condition" name="condition" class="w-full border border-stone rounded-md px-3 py-2 bg-white text-body-sm focus:outline-none focus:border-primary shadow-2xs">
						<option value="">{t('assets.filter.allConditions')}</option>
						{#each ['New', 'Good', 'Fair', 'Poor', 'Damaged'] as cond}
							<option value={cond} selected={data.filters.condition === cond}>{t(`assets.condition.${cond}`)}</option>
						{/each}
					</select>
				</div>

				<div>
					<label for="filter-custodian" class="block text-caption font-semibold text-secondary mb-1">{t('assets.custodian')}</label>
					<select id="filter-custodian" name="assignedUserId" class="w-full border border-stone rounded-md px-3 py-2 bg-white text-body-sm focus:outline-none focus:border-primary shadow-2xs">
						<option value="">{t('assets.filter.allStaff')}</option>
						{#each data.staff as s}
							<option value={s.id} selected={data.filters.assignedUserId === String(s.id)}>{s.fullName}</option>
						{/each}
					</select>
				</div>
			</div>

			<!-- Row 3: Toggle & Actions -->
			<div class="flex flex-wrap items-center justify-between gap-3 pt-2.5 border-t border-stone/40">
				<label class="flex items-center gap-2 text-caption font-medium text-secondary cursor-pointer select-none">
					<input
						type="checkbox"
						name="eolOnly"
						value="true"
						checked={data.filters.eolOnly === 'true'}
						class="rounded text-primary focus:ring-primary w-4 h-4 cursor-pointer"
					/>
					<span>{t('assets.filter.eolOnly')}</span>
				</label>

				<div class="flex items-center gap-2">
					{#if hasActiveFilters}
						<a
							href="/app/assets"
							class="h-9 px-3 rounded-button border border-stone bg-white text-secondary hover:text-primary text-caption inline-flex items-center gap-1 transition-colors"
						>
							<span class="material-symbols-outlined text-[16px]">close</span>
							<span>{t('common.clear')}</span>
						</a>
					{/if}
					<button
						type="submit"
						class="h-9 px-4 rounded-button bg-primary text-white font-medium text-caption hover:opacity-90 shadow-2xs transition-opacity inline-flex items-center gap-1.5 cursor-pointer"
					>
						<span class="material-symbols-outlined text-[16px]">filter_list</span>
						<span>{t('reports.apply')}</span>
					</button>
				</div>
			</div>
		</form>
	</section>

	<!-- MOBILE CARD VIEW (< 768px) -->
	<section class="block md:hidden space-y-3">
		{#if items.length === 0}
			<div class="bg-white border border-stone rounded-xl p-8 text-center shadow-2xs">
				<div class="max-w-sm mx-auto flex flex-col items-center justify-center text-center">
					<div class="w-12 h-12 rounded-xl bg-ash border border-stone flex items-center justify-center text-secondary mb-3 shadow-2xs">
						<span class="material-symbols-outlined text-[26px] text-primary/70">
							{hasActiveFilters ? 'search_off' : 'precision_manufacturing'}
						</span>
					</div>
					<h4 class="font-subheading text-body font-bold text-primary mb-1">
						{hasActiveFilters ? t('assets.empty.filteredTitle') : t('assets.empty.title')}
					</h4>
					<p class="text-caption text-secondary mb-4 leading-relaxed">
						{hasActiveFilters ? t('assets.empty.filteredCopy') : t('assets.empty.copy')}
					</p>
					<div class="flex flex-wrap items-center justify-center gap-2">
						{#if hasActiveFilters}
							<a
								href="/app/assets"
								class="h-9 px-3 rounded-button font-medium text-caption inline-flex items-center gap-1 border border-stone bg-white text-secondary hover:text-primary transition-all shadow-2xs"
							>
								<span class="material-symbols-outlined text-[16px]">restart_alt</span>
								<span>{t('common.clear')}</span>
							</a>
						{/if}
						{#if can(data.user.permissions, permissions.assetsWrite)}
							<button
								type="button"
								class="h-9 px-4 rounded-button font-medium text-caption inline-flex items-center gap-1.5 bg-primary text-white hover:opacity-90 transition-all shadow-xs cursor-pointer"
								onclick={() => (showNewModal = true)}
							>
								<span class="material-symbols-outlined text-[16px]">add</span>
								<span>{hasActiveFilters ? t('assets.newAsset') : t('assets.empty.cta')}</span>
							</button>
						{/if}
					</div>
				</div>
			</div>
		{:else}
			{#each items as item}
				<div class="bg-white border border-stone rounded-xl p-4 shadow-2xs space-y-3">
					<div class="flex items-start justify-between gap-2 border-b border-stone/50 pb-2.5">
						<div>
							<a
								href={`/app/assets/${item.id}`}
								class="font-mono font-bold text-body text-primary hover:underline block leading-tight"
							>
								{item.assetTag}
							</a>
							<span class="text-caption text-secondary block mt-0.5 truncate max-w-[200px]">
								{item.companyName ?? t('brand.name')}
							</span>
						</div>
						<div class="text-right">
							<span class="inline-block px-2 py-0.5 rounded text-[11px] font-semibold {item.status === 'Active' ? 'bg-emerald-100 text-emerald-800' : item.status === 'Available' ? 'bg-blue-100 text-blue-800' : item.status === 'UnderMaintenance' ? 'bg-amber-100 text-amber-800' : 'bg-stone-200 text-stone-700'}">
								{t(`assets.status.${item.status}`)}
							</span>
							<span class="block text-[10px] text-secondary mt-0.5">{t(`assets.condition.${item.condition}`)}</span>
						</div>
					</div>

					<div class="space-y-1">
						<a href={`/app/assets/${item.id}`} class="font-semibold text-primary block leading-snug hover:underline line-clamp-2">
							{item.name}
						</a>
						<p class="text-caption text-secondary truncate">
							{[item.categoryName, item.brand, item.model].filter(Boolean).join(' · ') || '-'}
						</p>
					</div>

					<!-- Financial & Age Grid -->
					<div class="grid grid-cols-2 gap-2 bg-ash/40 p-2.5 rounded-lg border border-stone/40 text-caption">
						<div>
							<span class="text-secondary block text-[11px]">{t('assets.currentBookValue')}</span>
							<strong class="font-mono text-body-sm font-bold text-primary block">
								{formatCurrency(item.valuation.currentBookValue, data.locale, currencyCode)}
							</strong>
							<span class="text-[10px] text-secondary">
								Cost: {formatCurrency(item.purchaseCost, data.locale, currencyCode)}
							</span>
						</div>
						<div>
							<span class="text-secondary block text-[11px]">{t('assets.age')} & Life</span>
							<strong class="font-mono text-body-sm font-semibold text-primary block">
								{item.valuation.ageYearsPart}y {item.valuation.ageMonthsPart}m
							</strong>
							<div class="h-1.5 w-full bg-stone-200 rounded-full overflow-hidden mt-1">
								<div
									class="h-full {item.valuation.isFullyDepreciated ? 'bg-amber-600' : item.valuation.isEolApproaching ? 'bg-orange-500' : 'bg-primary'}"
									style={`width: ${Math.min(100, item.valuation.lifeConsumedPercent)}%;`}
								></div>
							</div>
						</div>
					</div>

					<div class="flex items-center justify-between text-caption text-secondary pt-1">
						<div class="truncate max-w-[200px]">
							<span class="material-symbols-outlined text-[15px] align-middle text-stone-500">person</span>
							<span class="font-medium text-primary">{item.assignedUserName ?? t('assets.unassigned')}</span>
							{#if item.location}
								<span class="text-stone-400">·</span>
								<span>{item.location}</span>
							{/if}
						</div>
						<div class="flex items-center gap-2">
							<a
								class="h-8 px-2.5 rounded-md border border-stone bg-white text-secondary hover:text-primary inline-flex items-center gap-1 text-caption font-medium"
								href={`/app/assets/${item.id}/label`}
								title={t('assets.printTag')}
							>
								<span class="material-symbols-outlined text-[16px]">qr_code</span>
								<span>Tag</span>
							</a>
							<a
								class="h-8 px-3 rounded-md bg-primary text-white inline-flex items-center gap-1 text-caption font-medium hover:opacity-90"
								href={`/app/assets/${item.id}`}
							>
								<span>Detail</span>
								<span class="material-symbols-outlined text-[16px]">chevron_right</span>
							</a>
						</div>
					</div>
				</div>
			{/each}
		{/if}
	</section>

	<!-- DESKTOP DATA TABLE (>= 768px) -->
	<section class="hidden md:block bg-white border border-stone rounded-xl shadow-2xs overflow-hidden">
		<div class="overflow-x-auto">
			<table class="w-full text-left text-body-sm">
				<thead>
					<tr class="border-b border-stone text-caption text-secondary uppercase font-semibold bg-ash/30">
						<th class="py-3 px-4">{t('assets.tag')}</th>
						<th class="py-3 px-4">{t('assets.name')}</th>
						<th class="py-3 px-4">{t('assets.category')}</th>
						<th class="py-3 px-4">{t('assets.custodian')} & {t('assets.location')}</th>
						<th class="py-3 px-4">{t('assets.age')} & {t('assets.lifeConsumed')}</th>
						<th class="py-3 px-4 text-right">{t('assets.purchaseCost')}</th>
						<th class="py-3 px-4 text-right">{t('assets.currentBookValue')}</th>
						<th class="py-3 px-4 text-right">{t('assets.estimatedSellingValue')}</th>
						<th class="py-3 px-4 text-center">{t('assets.status')}</th>
						<th class="py-3 px-4 text-right">Actions</th>
					</tr>
				</thead>
				<tbody class="divide-y divide-stone/40">
					{#if items.length === 0}
						<tr>
							<td colspan="10" class="px-6 py-16 text-center">
								<div class="max-w-md mx-auto flex flex-col items-center justify-center text-center">
									<div class="w-14 h-14 rounded-2xl bg-ash/70 border border-stone/80 flex items-center justify-center text-secondary mb-3.5 shadow-2xs">
										<span class="material-symbols-outlined text-[30px] text-primary/70">
											{hasActiveFilters ? 'search_off' : 'precision_manufacturing'}
										</span>
									</div>
									<h4 class="font-subheading text-body-lg font-bold text-primary mb-1.5">
										{hasActiveFilters ? t('assets.empty.filteredTitle') : t('assets.empty.title')}
									</h4>
									<p class="text-body-sm text-secondary mb-5 leading-relaxed max-w-sm">
										{hasActiveFilters ? t('assets.empty.filteredCopy') : t('assets.empty.copy')}
									</p>
									<div class="flex items-center justify-center gap-2.5">
										{#if hasActiveFilters}
											<a
												href="/app/assets"
												class="h-9 px-4 rounded-button font-medium text-body-sm inline-flex items-center gap-1.5 border border-stone bg-white text-secondary hover:text-primary hover:bg-ash transition-all shadow-2xs"
											>
												<span class="material-symbols-outlined text-[18px]">restart_alt</span>
												<span>{t('common.clear')}</span>
											</a>
										{/if}
										{#if can(data.user.permissions, permissions.assetsWrite)}
											<button
												type="button"
												class="h-9 px-4 rounded-button font-medium text-body-sm inline-flex items-center gap-1.5 bg-primary text-white hover:opacity-90 active:scale-95 transition-all shadow-xs cursor-pointer"
												onclick={() => (showNewModal = true)}
											>
												<span class="material-symbols-outlined text-[18px]">add</span>
												<span>{hasActiveFilters ? t('assets.newAsset') : t('assets.empty.cta')}</span>
											</button>
										{/if}
									</div>
								</div>
							</td>
						</tr>
					{:else}
						{#each items as item}
							<tr class="hover:bg-ash/20 transition-colors">
								<!-- Tag -->
								<td class="py-3 px-4 whitespace-nowrap">
									<a
										class="font-mono font-bold text-primary hover:underline inline-flex items-center gap-1"
										href={`/app/assets/${item.id}`}
									>
										<span>{item.assetTag}</span>
									</a>
									{#if item.companyName}
										<span class="block text-[11px] text-secondary truncate max-w-[130px]">{item.companyName}</span>
									{/if}
								</td>

								<!-- Name & Specs -->
								<td class="py-3 px-4 max-w-[220px]">
									<a class="font-medium text-primary hover:underline line-clamp-1 block" href={`/app/assets/${item.id}`}>
										{item.name}
									</a>
									<span class="text-[11px] text-secondary truncate block">
										{[item.brand, item.model].filter(Boolean).join(' ') || item.serialNumber || '-'}
									</span>
								</td>

								<!-- Category -->
								<td class="py-3 px-4 whitespace-nowrap text-caption text-secondary">
									{item.categoryName ?? '-'}
								</td>

								<!-- Custodian & Location -->
								<td class="py-3 px-4 text-caption whitespace-nowrap">
									<strong class="block text-primary font-medium">{item.assignedUserName ?? t('assets.unassigned')}</strong>
									<span class="text-secondary">{item.location || '-'}</span>
								</td>

								<!-- Age & Progress -->
								<td class="py-3 px-4 min-w-[140px]">
									<div class="flex justify-between items-center text-[11px] mb-1">
										<span class="font-medium text-primary">{item.valuation.ageYearsPart}y {item.valuation.ageMonthsPart}m</span>
										<span class="text-secondary">{item.valuation.lifeConsumedPercent}%</span>
									</div>
									<div class="h-1.5 w-full bg-ash rounded-full overflow-hidden border border-stone/40">
										<div
											class="h-full {item.valuation.isFullyDepreciated ? 'bg-amber-600' : item.valuation.isEolApproaching ? 'bg-orange-500' : 'bg-primary'}"
											style={`width: ${Math.min(100, item.valuation.lifeConsumedPercent)}%;`}
										></div>
									</div>
								</td>

								<!-- Purchase Cost -->
								<td class="py-3 px-4 text-right font-mono whitespace-nowrap tabular-nums">
									{formatCurrency(item.purchaseCost, data.locale, currencyCode)}
								</td>

								<!-- Book Value -->
								<td class="py-3 px-4 text-right font-mono font-bold text-primary whitespace-nowrap tabular-nums">
									{formatCurrency(item.valuation.currentBookValue, data.locale, currencyCode)}
								</td>

								<!-- Est. Selling Value -->
								<td class="py-3 px-4 text-right font-mono whitespace-nowrap tabular-nums">
									<span class="block">{formatCurrency(item.valuation.effectiveSellingValue, data.locale, currencyCode)}</span>
									<span class="text-[10px] font-medium {item.valuation.estimatedGainLoss >= 0 ? 'text-emerald-700' : 'text-rose-700'}">
										{item.valuation.estimatedGainLoss >= 0 ? '+' : ''}{formatCurrency(item.valuation.estimatedGainLoss, data.locale, currencyCode)}
									</span>
								</td>

								<!-- Status & Condition Badges -->
								<td class="py-3 px-4 text-center whitespace-nowrap">
									<span class="inline-block px-2 py-0.5 rounded text-[11px] font-semibold {item.status === 'Active' ? 'bg-emerald-100 text-emerald-800' : item.status === 'Available' ? 'bg-blue-100 text-blue-800' : item.status === 'UnderMaintenance' ? 'bg-amber-100 text-amber-800' : 'bg-stone-200 text-stone-700'}">
										{t(`assets.status.${item.status}`)}
									</span>
									<span class="block text-[10px] text-secondary mt-0.5">{t(`assets.condition.${item.condition}`)}</span>
								</td>

								<!-- Actions -->
								<td class="py-3 px-4 text-right whitespace-nowrap">
									<div class="inline-flex items-center gap-1">
										<a
											class="p-1 rounded text-secondary hover:text-primary hover:bg-ash transition-colors"
											href={`/app/assets/${item.id}/label`}
											title={t('assets.printTag')}
										>
											<span class="material-symbols-outlined text-[18px]">qr_code</span>
										</a>
										<a
											class="p-1 rounded text-secondary hover:text-primary hover:bg-ash transition-colors"
											href={`/app/assets/${item.id}`}
											title="View Details"
										>
											<span class="material-symbols-outlined text-[18px]">visibility</span>
										</a>
									</div>
								</td>
							</tr>
						{/each}
					{/if}
				</tbody>
			</table>
		</div>

		<!-- Pagination Footer -->
		{#if data.assetsPage.totalCount > 0}
			<div class="p-3 border-t border-stone flex items-center justify-between text-caption text-secondary bg-ash/20">
				<span>
					Showing {items.length} of {data.assetsPage.totalCount} assets
				</span>
				<span>Page {data.assetsPage.page}</span>
			</div>
		{/if}
	</section>
</div>

<!-- ================= MODALS ================= -->

<!-- 1. New Asset Modal -->
{#if showNewModal}
	<div
		class="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-xs p-3 sm:p-4"
		onclick={(e) => { if (e.target === e.currentTarget) showNewModal = false; }}
		role="presentation"
	>
		<div
			class="w-full max-w-2xl bg-white rounded-xl shadow-2xl border border-stone flex flex-col max-h-[90vh] overflow-hidden animate-in fade-in zoom-in-95 duration-150"
			role="dialog"
			aria-modal="true"
			aria-labelledby="modal-new-asset-title"
		>
			<!-- Sticky Modal Header -->
			<div class="flex items-center justify-between border-b border-stone px-5 py-3.5 bg-white sticky top-0 z-10 shrink-0">
				<div>
					<h3 id="modal-new-asset-title" class="font-subheading text-subheading font-bold text-primary">{t('assets.newAsset')}</h3>
					<p class="text-caption text-secondary">Register company equipment and configure straight-line depreciation</p>
				</div>
				<button
					type="button"
					class="p-1 rounded text-secondary hover:text-primary hover:bg-ash cursor-pointer transition-colors"
					onclick={() => (showNewModal = false)}
					aria-label={t('common.close')}
				>
					<span class="material-symbols-outlined text-[20px]">close</span>
				</button>
			</div>

			<!-- Scrollable Form Body -->
			<form
				id="new-asset-form"
				method="POST"
				action="?/saveAsset"
				use:enhance={() => {
					return async ({ update }) => {
						showNewModal = false;
						await update();
					};
				}}
				class="p-5 overflow-y-auto space-y-5 text-body-sm flex-1"
			>
				<!-- Section 1: Identification & Physical Specs -->
				<div class="space-y-3">
					<h4 class="text-caption font-bold uppercase tracking-wider text-secondary border-b border-stone/50 pb-1">
						Identification & Location
					</h4>
					<div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
						<div>
							<label for="new-asset-tag" class="block text-caption font-semibold text-secondary mb-1">
								{t('assets.tag')} <span class="text-secondary font-normal">(optional)</span>
							</label>
							<input
								id="new-asset-tag"
								name="assetTag"
								type="text"
								placeholder="Auto-generate (AST-XXXX)"
								class="w-full border border-stone rounded-md px-3 py-2 font-mono text-body-sm focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary"
							/>
						</div>

						<div>
							<label for="new-asset-name" class="block text-caption font-semibold text-secondary mb-1">
								{t('assets.name')} <span class="text-rose-600">*</span>
							</label>
							<input
								id="new-asset-name"
								name="name"
								type="text"
								required
								placeholder="e.g. Fluke 87V Multimeter, Laptop Dell XPS"
								class="w-full border border-stone rounded-md px-3 py-2 text-body-sm focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary"
							/>
						</div>

						<div>
							<label for="new-asset-category" class="block text-caption font-semibold text-secondary mb-1">{t('assets.category')}</label>
							<select
								id="new-asset-category"
								name="categoryId"
								class="w-full border border-stone rounded-md px-3 py-2 bg-white text-body-sm focus:outline-none focus:border-primary"
								onchange={(e) => onCategoryChange(e.currentTarget.value)}
							>
								<option value="">-- {t('assets.filter.allCategories')} --</option>
								{#each data.categories as cat}
									<option value={cat.id}>{cat.name}</option>
								{/each}
							</select>
						</div>

						<div>
							<label for="new-asset-company" class="block text-caption font-semibold text-secondary mb-1">{t('assets.companyTag')}</label>
							<input
								id="new-asset-company"
								name="companyName"
								type="text"
								placeholder={t('assets.companyPlaceholder')}
								class="w-full border border-stone rounded-md px-3 py-2 text-body-sm focus:outline-none focus:border-primary"
							/>
						</div>

						<div>
							<label for="new-asset-department" class="block text-caption font-semibold text-secondary mb-1">{t('assets.department')}</label>
							<input
								id="new-asset-department"
								name="department"
								type="text"
								placeholder="e.g. Repair Bench, IT Support, Logistics"
								class="w-full border border-stone rounded-md px-3 py-2 text-body-sm focus:outline-none focus:border-primary"
							/>
						</div>

						<div>
							<label for="new-asset-location" class="block text-caption font-semibold text-secondary mb-1">{t('assets.location')}</label>
							<input
								id="new-asset-location"
								name="location"
								type="text"
								placeholder="e.g. Storage Shelf A2, Bench 1"
								class="w-full border border-stone rounded-md px-3 py-2 text-body-sm focus:outline-none focus:border-primary"
							/>
						</div>

						<div>
							<label for="new-asset-brand" class="block text-caption font-semibold text-secondary mb-1">{t('assets.brand')}</label>
							<input
								id="new-asset-brand"
								name="brand"
								type="text"
								placeholder="e.g. Apple, Fluke, Rigol, Toyota"
								class="w-full border border-stone rounded-md px-3 py-2 text-body-sm focus:outline-none focus:border-primary"
							/>
						</div>

						<div>
							<label for="new-asset-model" class="block text-caption font-semibold text-secondary mb-1">{t('assets.model')}</label>
							<input
								id="new-asset-model"
								name="model"
								type="text"
								placeholder="e.g. DS1054Z, ThinkPad T14"
								class="w-full border border-stone rounded-md px-3 py-2 text-body-sm focus:outline-none focus:border-primary"
							/>
						</div>

						<div>
							<label for="new-asset-serial" class="block text-caption font-semibold text-secondary mb-1">{t('assets.serialNumber')}</label>
							<input
								id="new-asset-serial"
								name="serialNumber"
								type="text"
								placeholder="Serial or IMEI"
								class="w-full border border-stone rounded-md px-3 py-2 font-mono text-body-sm focus:outline-none focus:border-primary"
							/>
						</div>

						<div>
							<label for="new-asset-condition" class="block text-caption font-semibold text-secondary mb-1">{t('assets.condition')}</label>
							<select
								id="new-asset-condition"
								name="condition"
								class="w-full border border-stone rounded-md px-3 py-2 bg-white text-body-sm focus:outline-none focus:border-primary"
							>
								{#each ['New', 'Good', 'Fair', 'Poor', 'Damaged'] as cond}
									<option value={cond}>{t(`assets.condition.${cond}`)}</option>
								{/each}
							</select>
						</div>

						<div class="sm:col-span-2">
							<label for="new-asset-custodian" class="block text-caption font-semibold text-secondary mb-1">{t('assets.custodian')}</label>
							<select
								id="new-asset-custodian"
								name="assignedUserId"
								class="w-full border border-stone rounded-md px-3 py-2 bg-white text-body-sm focus:outline-none focus:border-primary"
							>
								<option value="">-- {t('assets.unassigned')} --</option>
								{#each data.staff as s}
									<option value={s.id}>{s.fullName} ({s.email})</option>
								{/each}
							</select>
						</div>
					</div>
				</div>

				<!-- Section 2: Valuation & Depreciation -->
				<div class="space-y-3 pt-1">
					<h4 class="text-caption font-bold uppercase tracking-wider text-secondary border-b border-stone/50 pb-1">
						Financial Valuation & Depreciation
					</h4>
					<div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
						<div>
							<span class="block text-caption font-semibold text-secondary mb-1">
								{t('assets.purchaseCost')} <span class="text-rose-600">*</span>
							</span>
							<CurrencyInput
								name="purchaseCost"
								required
								prefix={currencySymbol}
								locale={data.locale}
								currency={currencyCode}
								bind:value={purchaseCostInput}
								oninput={onPurchaseCostInput}
							/>
						</div>

						<div>
							<span class="block text-caption font-semibold text-secondary mb-1">{t('assets.salvageValue')}</span>
							<CurrencyInput
								name="salvageValue"
								prefix={currencySymbol}
								locale={data.locale}
								currency={currencyCode}
								bind:value={salvageValueInput}
							/>
						</div>

						<div>
							<label for="new-asset-useful-life" class="block text-caption font-semibold text-secondary mb-1">{t('assets.usefulLifeMonths')}</label>
							<input
								id="new-asset-useful-life"
								name="usefulLifeMonths"
								type="number"
								min="1"
								bind:value={usefulLifeInput}
								class="w-full border border-stone rounded-md px-3 py-2 font-mono text-body-sm focus:outline-none focus:border-primary"
							/>
						</div>

						<div>
							<label for="new-asset-deprec-method" class="block text-caption font-semibold text-secondary mb-1">{t('assets.depreciationMethod')}</label>
							<select
								id="new-asset-deprec-method"
								name="depreciationMethod"
								class="w-full border border-stone rounded-md px-3 py-2 bg-white text-body-sm focus:outline-none focus:border-primary"
							>
								<option value="StraightLine">{t('assets.depreciationMethod.StraightLine')}</option>
								<option value="None">{t('assets.depreciationMethod.None')}</option>
							</select>
						</div>

						<div class="sm:col-span-2">
							<span class="block text-caption font-semibold text-secondary mb-1">{t('assets.estimatedSellingValue')}</span>
							<CurrencyInput
								name="estimatedSellingValue"
								prefix={currencySymbol}
								locale={data.locale}
								currency={currencyCode}
								bind:value={estimatedSellingValueInput}
							/>
						</div>
					</div>
				</div>

				<!-- Section 3: Acquisition & Warranty -->
				<div class="space-y-3 pt-1">
					<h4 class="text-caption font-bold uppercase tracking-wider text-secondary border-b border-stone/50 pb-1">
						Acquisition & Warranty
					</h4>
					<div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
						<div>
							<label for="new-asset-purchase-date" class="block text-caption font-semibold text-secondary mb-1">{t('assets.purchaseDate')}</label>
							<DatePicker
								id="new-asset-purchase-date"
								name="purchaseDate"
								value={new Date().toISOString().slice(0, 10)}
							/>
						</div>

						<div>
							<label for="new-asset-warranty-date" class="block text-caption font-semibold text-secondary mb-1">{t('assets.warrantyExpiresAt')}</label>
							<DatePicker
								id="new-asset-warranty-date"
								name="warrantyExpiresAt"
								clearable
							/>
						</div>

						<div>
							<label for="new-asset-supplier" class="block text-caption font-semibold text-secondary mb-1">{t('assets.supplier')}</label>
							<input
								id="new-asset-supplier"
								name="supplierName"
								type="text"
								placeholder="Vendor / Store name"
								class="w-full border border-stone rounded-md px-3 py-2 text-body-sm focus:outline-none focus:border-primary"
							/>
						</div>

						<div>
							<label for="new-asset-invoice" class="block text-caption font-semibold text-secondary mb-1">{t('assets.invoiceNumber')}</label>
							<input
								id="new-asset-invoice"
								name="invoiceNumber"
								type="text"
								placeholder="PO or Invoice Ref"
								class="w-full border border-stone rounded-md px-3 py-2 text-body-sm focus:outline-none focus:border-primary"
							/>
						</div>

						<div class="sm:col-span-2">
							<label for="new-asset-desc" class="block text-caption font-semibold text-secondary mb-1">{t('assets.description')}</label>
							<textarea
								id="new-asset-desc"
								name="description"
								rows="2"
								placeholder="Hardware specifications, accessories included..."
								class="w-full border border-stone rounded-md px-3 py-2 text-body-sm focus:outline-none focus:border-primary"
							></textarea>
						</div>
					</div>
				</div>
			</form>

			<!-- Sticky Modal Footer -->
			<div class="flex items-center justify-end gap-2 border-t border-stone px-5 py-3 bg-ash/30 sticky bottom-0 z-10 shrink-0">
				<button
					type="button"
					class="px-4 py-2 border border-stone rounded-md text-secondary hover:text-primary hover:bg-white text-body-sm font-medium transition-colors cursor-pointer"
					onclick={() => (showNewModal = false)}
				>
					{t('common.cancel')}
				</button>
				<button
					type="submit"
					form="new-asset-form"
					class="px-5 py-2 bg-primary text-white rounded-md font-semibold text-body-sm hover:opacity-90 transition-opacity shadow-2xs cursor-pointer"
				>
					{t('common.save')}
				</button>
			</div>
		</div>
	</div>
{/if}

<!-- 2. Categories Modal -->
{#if showCategoriesModal}
	<div
		class="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-xs p-3 sm:p-4"
		onclick={(e) => { if (e.target === e.currentTarget) { showCategoriesModal = false; editingCategory = null; } }}
		role="presentation"
	>
		<div
			class="w-full max-w-xl bg-white rounded-xl shadow-2xl border border-stone flex flex-col max-h-[90vh] overflow-hidden animate-in fade-in zoom-in-95 duration-150"
			role="dialog"
			aria-modal="true"
			aria-labelledby="modal-categories-title"
		>
			<div class="flex items-center justify-between border-b border-stone px-5 py-3.5 bg-white sticky top-0 z-10 shrink-0">
				<div>
					<h3 id="modal-categories-title" class="font-subheading text-subheading font-bold text-primary">{t('assets.categories')}</h3>
					<p class="text-caption text-secondary">Manage default useful life and salvage presets for asset categories</p>
				</div>
				<button
					type="button"
					class="p-1 rounded text-secondary hover:text-primary hover:bg-ash cursor-pointer transition-colors"
					onclick={() => { showCategoriesModal = false; editingCategory = null; }}
					aria-label={t('common.close')}
				>
					<span class="material-symbols-outlined text-[20px]">close</span>
				</button>
			</div>

			<div class="p-5 overflow-y-auto space-y-4 flex-1">
				<!-- Add / Edit Category Form -->
				<form
					method="POST"
					action="?/saveCategory"
					use:enhance={() => {
						return async ({ update }) => {
							editingCategory = null;
							await update();
						};
					}}
					class="bg-ash/40 p-4 rounded-xl border border-stone/60 space-y-3 text-body-sm"
				>
					<h4 class="font-bold text-primary text-body-sm">
						{editingCategory ? `Edit: ${editingCategory.name}` : 'Add New Category'}
					</h4>
					{#if editingCategory}
						<input type="hidden" name="id" value={editingCategory.id} />
					{/if}

					<div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
						<div>
							<label for="cat-name-input" class="block text-caption font-semibold text-secondary mb-1">
								{t('assets.categoryName')} <span class="text-rose-600">*</span>
							</label>
							<input
								id="cat-name-input"
								name="name"
								type="text"
								value={editingCategory?.name ?? ''}
								required
								class="w-full border border-stone rounded-md px-3 py-1.5 bg-white text-body-sm focus:outline-none focus:border-primary"
							/>
						</div>
						<div>
							<label for="cat-life-input" class="block text-caption font-semibold text-secondary mb-1">{t('assets.categoryUsefulLife')}</label>
							<input
								id="cat-life-input"
								name="defaultUsefulLifeMonths"
								type="number"
								min="1"
								value={editingCategory?.defaultUsefulLifeMonths ?? 36}
								class="w-full border border-stone rounded-md px-3 py-1.5 bg-white font-mono text-body-sm focus:outline-none focus:border-primary"
							/>
						</div>
						<div>
							<label for="cat-salvage-input" class="block text-caption font-semibold text-secondary mb-1">{t('assets.categorySalvagePercent')}</label>
							<input
								id="cat-salvage-input"
								name="defaultSalvagePercent"
								type="number"
								step="0.1"
								min="0"
								max="100"
								value={editingCategory?.defaultSalvagePercent ?? 0}
								class="w-full border border-stone rounded-md px-3 py-1.5 bg-white font-mono text-body-sm focus:outline-none focus:border-primary"
							/>
						</div>
						<div>
							<label for="cat-desc-input" class="block text-caption font-semibold text-secondary mb-1">{t('assets.categoryDesc')}</label>
							<input
								id="cat-desc-input"
								name="description"
								type="text"
								value={editingCategory?.description ?? ''}
								class="w-full border border-stone rounded-md px-3 py-1.5 bg-white text-body-sm focus:outline-none focus:border-primary"
							/>
						</div>
					</div>

					<div class="flex items-center justify-end gap-2 pt-2">
						{#if editingCategory}
							<button
								type="button"
								class="px-3 py-1.5 text-caption text-secondary hover:text-primary cursor-pointer"
								onclick={() => (editingCategory = null)}
							>
								Cancel Edit
							</button>
						{/if}
						<button
							type="submit"
							class="px-4 py-1.5 bg-primary text-white text-caption font-semibold rounded-md hover:opacity-90 transition-opacity cursor-pointer shadow-2xs"
						>
							{editingCategory ? 'Update Category' : 'Add Category'}
						</button>
					</div>
				</form>

				<!-- Categories List -->
				<div class="divide-y divide-stone/40 border border-stone rounded-xl overflow-hidden bg-white">
					{#each data.categories as cat}
						<div class="p-3.5 flex items-center justify-between hover:bg-ash/20 text-body-sm transition-colors">
							<div>
								<strong class="text-primary font-semibold block">{cat.name}</strong>
								<span class="text-caption text-secondary">
									{cat.defaultUsefulLifeMonths} mos useful life · {cat.defaultSalvagePercent}% salvage · {cat.assetCount} assets
								</span>
							</div>
							<button
								type="button"
								class="h-8 px-2.5 rounded border border-stone text-caption font-medium text-primary hover:bg-ash transition-colors cursor-pointer inline-flex items-center gap-1"
								onclick={() => (editingCategory = cat)}
							>
								<span class="material-symbols-outlined text-[15px]">edit</span>
								<span>Edit</span>
							</button>
						</div>
					{/each}
				</div>
			</div>

			<div class="flex justify-end p-4 border-t border-stone bg-ash/20 sticky bottom-0 z-10 shrink-0">
				<button
					type="button"
					class="px-4 py-2 bg-white border border-stone rounded-md text-primary font-medium text-body-sm hover:bg-ash transition-colors cursor-pointer"
					onclick={() => { showCategoriesModal = false; editingCategory = null; }}
				>
					{t('common.close')}
				</button>
			</div>
		</div>
	</div>
{/if}
