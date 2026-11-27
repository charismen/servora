<script lang="ts">
	import { enhance } from '$app/forms';
	import PageHeader from '$lib/components/PageHeader.svelte';
	import DeleteConfirmButton from '$lib/components/DeleteConfirmButton.svelte';
	import CurrencyInput from '$lib/components/CurrencyInput.svelte';
	import InventoryItemModal from '$lib/components/InventoryItemModal.svelte';
	import Dropdown from '$lib/components/ui/dropdown/Dropdown.svelte';
	import StatusBadge from '$lib/components/ui/badge/StatusBadge.svelte';
	import { can, permissions } from '$lib/auth/permissions';
	import { createTranslator, formatCurrency, formatDate } from '$lib/i18n';
	import { fade } from 'svelte/transition';
	import type { ActionData, PageData } from './$types';
	import type { InventoryItem } from '$lib/types/inventory';

	let { data, form }: { data: PageData; form: ActionData } = $props();
	const t = $derived(createTranslator(data.locale));
	const canWrite = $derived(can(data.user.permissions, permissions.inventoryWrite));
	const canSeeCost = $derived(can(data.user.permissions, permissions.inventoryCostRead));
	const canManageItems = $derived(canWrite && canSeeCost);
	const canDeleteInventory = $derived(can(data.user.permissions, permissions.inventoryDelete));
	const currencyCode = $derived(data.shopProfile?.currencyCode ?? 'IDR');
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
	const totalValue = $derived(data.items.reduce((sum, item) => sum + item.onHandQuantity * (item.costPrice ?? 0), 0));
	const lowStockCount = $derived(data.items.filter((item) => item.availableQuantity <= item.reorderLevel).length);
	const reservedUnits = $derived(data.items.reduce((sum, item) => sum + item.reservedQuantity, 0));
	const itemTypes = ['SparePart', 'Accessory', 'ServiceConsumable', 'Equipment', 'ToolOrSupply', 'RefurbishedUnit', 'SecondhandPart', 'SecondhandPcBuild', 'SecondhandLaptop'];
	const conditions = ['New', 'Used', 'Refurbished', 'ForParts'];
	const pageSizeOptions = [10, 20, 50, 100];
	const currentPage = $derived(data.inventoryPage.page);
	const totalPages = $derived(Math.max(1, Math.ceil(data.inventoryPage.totalCount / data.inventoryPage.pageSize)));
	const queryBase = $derived(new URLSearchParams(Object.entries(data.filters).filter(([key, value]) => value && key !== 'page').map(([key, value]) => [key, String(value)])));

	const hasActiveFilters = $derived(
		Boolean(
			data.filters.search ||
			data.filters.categoryId ||
			data.filters.itemType ||
			data.filters.condition ||
			data.filters.isActive ||
			(data.filters.sort && data.filters.sort !== 'name')
		)
	);

	// Modal States
	let showAddModal = $state(false);
	let editingItem = $state<InventoryItem | null>(null);
	let deletingItemId = $state<number | null>(null);
	let showCategoriesModal = $state(false);
	let showImportModal = $state(false);
	let stockInItem = $state<InventoryItem | null>(null);
	let adjustStockItem = $state<InventoryItem | null>(null);
	const itemModalOpen = $derived(showAddModal || editingItem !== null);

	// Auto-open import modal if import preview or results arrive
	$effect(() => {
		if (form?.importPreview) {
			showImportModal = true;
		}
	});

	const defaultTaxPercent = $derived(data.shopProfile?.inventoryDefaultTaxPercent ?? 11);
	const defaultTaxMode = $derived(data.shopProfile?.inventoryDefaultTaxMode === 'Multiplier' ? 'Multiplier' : 'Percentage');
	const roundingIncrement = $derived(Math.max(1, data.shopProfile?.inventoryRoundingIncrement ?? 1000));
	const defaultUnit = $derived(data.shopProfile?.inventoryDefaultUnit ?? 'pcs');

	function pageHref(page: number) {
		const query = new URLSearchParams(queryBase);
		query.set('page', String(page));
		return `?${query.toString()}`;
	}
</script>

<div class="space-y-lg pb-xl">
	<!-- Page Header with Top Actions Slot -->
	<PageHeader items={[{ label: t('common.console'), href: '/app' }, { label: t('nav.inventory') }]} title={t('nav.inventory')} subtitle={t('inventory.subtitle')}>
		{#snippet actions()}
		<div class="flex items-center gap-2">
			{#if canManageItems}
				<button
					type="button"
					class="px-button-x py-button-y rounded-button border border-stone bg-white text-ink hover:bg-ash font-medium text-body-sm transition-all shadow-2xs flex items-center gap-1.5 cursor-pointer"
					onclick={() => showCategoriesModal = true}
				>
					<span class="material-symbols-outlined text-[18px]">category</span>
					<span class="hidden sm:inline">{t('inventory.manageCategories')}</span>
				</button>
				<button
					type="button"
					class="px-button-x py-button-y rounded-button border border-stone bg-white text-ink hover:bg-ash font-medium text-body-sm transition-all shadow-2xs flex items-center gap-1.5 cursor-pointer"
					onclick={() => showImportModal = true}
				>
					<span class="material-symbols-outlined text-[18px]">upload_file</span>
					<span class="hidden sm:inline">{t('inventory.importExport')}</span>
				</button>
				<button
					type="button"
					class="px-button-x py-button-y rounded-button font-medium font-body-sm text-body-sm inline-flex items-center gap-1.5 bg-ink text-white hover:bg-ink/90 active:scale-95 transition-all shadow-xs cursor-pointer"
					onclick={() => showAddModal = true}
				>
					<span class="material-symbols-outlined text-[18px]">add</span>
					<span>{t('inventory.addItem')}</span>
				</button>
			{/if}
		</div>
		{/snippet}
	</PageHeader>

	<!-- Overview Metrics -->
	<div class="grid grid-cols-1 sm:grid-cols-3 gap-md">
		<div data-bento class="bg-surface-container-lowest border border-stone rounded-xl p-md flex items-center justify-between shadow-2xs">
			<div>
				<p class="font-label-caps text-secondary text-[11px] font-bold tracking-wider uppercase mb-1">{t('inventory.totalValue')}</p>
				<h3 class="font-headline-sm text-[22px] text-ink font-bold font-mono tracking-tight">{canSeeCost ? formatCurrency(totalValue, data.locale, currencyCode) : '-'}</h3>
			</div>
			<div class="w-10 h-10 rounded-xl bg-ash border border-stone flex items-center justify-center text-ink shrink-0">
				<span class="material-symbols-outlined text-[20px]">payments</span>
			</div>
		</div>

		<div data-bento class="bg-surface-container-lowest border border-stone rounded-xl p-md flex items-center justify-between shadow-2xs {lowStockCount > 0 ? 'border-amber-300/80 bg-amber-50/15' : ''}">
			<div>
				<p class="font-label-caps text-secondary text-[11px] font-bold tracking-wider uppercase mb-1">{t('inventory.lowStockItems')}</p>
				<div class="flex items-center gap-2">
					<h3 class="font-headline-sm text-[22px] text-ink font-bold font-mono tracking-tight">{lowStockCount}</h3>
					{#if lowStockCount > 0}
						<span class="px-1.5 py-0.5 rounded text-[10px] font-bold uppercase tracking-wider bg-amber-500/10 text-amber-800 border border-amber-300">
							{t('inventory.lowStock')}
						</span>
					{/if}
				</div>
			</div>
			<div class="w-10 h-10 rounded-xl {lowStockCount > 0 ? 'bg-amber-100/60 text-amber-800 border border-amber-200' : 'bg-ash text-ink border border-stone'} flex items-center justify-center shrink-0">
				<span class="material-symbols-outlined text-[20px]">{lowStockCount > 0 ? 'warning' : 'inventory_2'}</span>
			</div>
		</div>

		<div data-bento class="bg-surface-container-lowest border border-stone rounded-xl p-md flex items-center justify-between shadow-2xs">
			<div>
				<p class="font-label-caps text-secondary text-[11px] font-bold tracking-wider uppercase mb-1">{t('inventory.reservedUnits')}</p>
				<h3 class="font-headline-sm text-[22px] text-ink font-bold font-mono tracking-tight">{reservedUnits}</h3>
			</div>
			<div class="w-10 h-10 rounded-xl bg-ash border border-stone flex items-center justify-center text-ink shrink-0">
				<span class="material-symbols-outlined text-[20px]">build_circle</span>
			</div>
		</div>
	</div>

	<!-- Standardized Filter Bar -->
	<section data-bento class="bg-surface-container-lowest border border-stone rounded-xl p-md shadow-2xs !overflow-visible relative z-30">
		<form method="GET" class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 xl:grid-cols-[1fr_170px_160px_150px_130px_130px_auto] gap-md items-end">
			<label class="space-y-1 sm:col-span-2 md:col-span-1">
				<span class="block font-label-caps text-secondary uppercase text-[11px] font-bold tracking-wider">{t('inventory.search')}</span>
				<div class="relative">
					<span class="material-symbols-outlined absolute left-3 top-1/2 -translate-y-1/2 text-secondary text-[18px]">search</span>
					<input name="search" value={data.filters.search ?? ''} class="w-full pl-9 pr-3 py-2 border border-stone rounded-input font-body-sm text-body-sm focus:border-ink focus:ring-1 focus:ring-ink/10 focus:outline-none bg-white text-ink transition-colors" placeholder={t('inventory.searchPlaceholder')} type="search" />
				</div>
			</label>
			<div class="space-y-1">
				<span class="block font-label-caps text-secondary uppercase text-[11px] font-bold tracking-wider">{t('inventory.category')}</span>
				<Dropdown
					name="categoryId"
					value={data.filters.categoryId ?? ''}
					options={[
						{ value: '', label: t('inventory.allCategories') },
						...data.categories.map((category) => ({ value: String(category.id), label: category.name }))
					]}
				/>
			</div>
			<div class="space-y-1">
				<span class="block font-label-caps text-secondary uppercase text-[11px] font-bold tracking-wider">{t('inventory.itemType')}</span>
				<Dropdown
					name="itemType"
					value={data.filters.itemType ?? ''}
					options={[
						{ value: '', label: t('inventory.allTypes') },
						...itemTypes.map((type) => ({ value: type, label: t(`inventory.itemType.${type}`) }))
					]}
				/>
			</div>
			<div class="space-y-1">
				<span class="block font-label-caps text-secondary uppercase text-[11px] font-bold tracking-wider">{t('inventory.condition')}</span>
				<Dropdown
					name="condition"
					value={data.filters.condition ?? ''}
					options={[
						{ value: '', label: t('inventory.allConditions') },
						...conditions.map((condition) => ({ value: condition, label: t(`inventory.condition.${condition}`) }))
					]}
				/>
			</div>
			<div class="space-y-1">
				<span class="block font-label-caps text-secondary uppercase text-[11px] font-bold tracking-wider">{t('inventory.status')}</span>
				<Dropdown
					name="isActive"
					value={data.filters.isActive ?? ''}
					options={[
						{ value: '', label: t('inventory.allStatuses') },
						{ value: 'true', label: t('settings.active') },
						{ value: 'false', label: t('inventory.inactive') }
					]}
				/>
			</div>
			<div class="space-y-1">
				<span class="block font-label-caps text-secondary uppercase text-[11px] font-bold tracking-wider">{t('inventory.sort')}</span>
				<Dropdown
					name="sort"
					value={data.filters.sort ?? 'name'}
					options={[
						{ value: 'name', label: t('inventory.sort.name') },
						{ value: 'sku', label: t('inventory.sort.sku') },
						{ value: 'available-asc', label: t('inventory.sort.availableAsc') },
						{ value: 'available-desc', label: t('inventory.sort.availableDesc') }
					]}
				/>
			</div>
			<div class="flex items-center gap-2">
				<button class="w-full sm:w-auto h-10 px-4 rounded-button bg-ink text-white font-medium text-body-sm hover:bg-ink/90 active:scale-95 transition-all shadow-xs cursor-pointer flex items-center justify-center gap-1.5 shrink-0" type="submit">
					<span class="material-symbols-outlined text-[18px]">filter_alt</span>
					<span>{t('inventory.search')}</span>
				</button>
				{#if hasActiveFilters}
					<a
						href="?page=1"
						class="h-10 px-3 rounded-button border border-stone bg-white text-secondary hover:text-ink hover:bg-ash font-medium text-body-sm transition-all flex items-center justify-center gap-1 shrink-0 shadow-2xs"
						title={t('inventory.clearFilters')}
					>
						<span class="material-symbols-outlined text-[18px]">restart_alt</span>
						<span class="hidden sm:inline">{t('inventory.clearFilters')}</span>
					</a>
				{/if}
			</div>
		</form>
	</section>

	<!-- High-Scanability Inventory Items Table -->
	<div data-bento class="bg-surface-container-lowest border border-stone rounded-xl overflow-hidden shadow-2xs">
		<!-- Desktop Table View -->
		<div class="hidden lg:block overflow-x-auto custom-scrollbar">
			<table class="w-full text-left border-collapse min-w-[760px]">
				<thead>
					<tr class="bg-surface-container-low border-b border-stone">
						<th class="px-md py-4 font-label-caps text-secondary uppercase tracking-wider text-[11px] font-bold">{t('inventory.sku')}</th>
						<th class="px-md py-4 font-label-caps text-secondary uppercase tracking-wider text-[11px] font-bold">{t('inventory.item')}</th>
						<th class="px-md py-4 font-label-caps text-secondary uppercase tracking-wider text-[11px] font-bold">{t('inventory.stock')}</th>
						<th class="px-md py-4 text-right font-label-caps text-secondary uppercase tracking-wider text-[11px] font-bold">{t('inventory.price')}</th>
						<th class="px-md py-4 font-label-caps text-secondary uppercase tracking-wider text-[11px] font-bold hidden xl:table-cell">{t('inventory.location')}</th>
						<th class="px-md py-4 text-right font-label-caps text-secondary uppercase tracking-wider text-[11px] font-bold">{t('tickets.list.actions')}</th>
					</tr>
				</thead>
				<tbody class="divide-y divide-stone font-body-sm text-body-sm">
					{#each data.items as item}
						{@const isLowStock = item.availableQuantity <= item.reorderLevel}
						<tr class="group hover:bg-ash/50 transition-colors">
							<td class="px-md py-4 align-top">
								<span class="font-mono font-bold text-xs text-ink bg-ash/80 px-2 py-1 rounded border border-stone/80 inline-block">
									{item.sku}
								</span>
							</td>
							<td class="px-md py-4 align-top">
								<div class="space-y-1">
									<div class="flex items-center gap-2">
										<a class="font-bold text-primary font-body-sm text-body-sm hover:underline hover:text-ink transition-colors" href={`/app/inventory/${item.id}`}>
											{item.name}
										</a>
										{#if !item.isActive}
											<StatusBadge variant="neutral" label={t('inventory.inactive')} />
										{/if}
									</div>
									<div class="flex flex-wrap items-center gap-1.5 text-xs">
										<span class="px-2 py-0.5 rounded bg-ash text-secondary text-[11px] font-medium border border-stone/60">
											{item.categoryName ?? t('inventory.noCategory')}
										</span>
										<span class="text-secondary/60">·</span>
										<span class="text-secondary text-[11px]">{t(`inventory.itemType.${item.itemType}`) || item.itemType}</span>
										<span class="text-secondary/60">·</span>
										<span class="text-secondary text-[11px]">{t(`inventory.condition.${item.condition}`) || item.condition}</span>
									</div>
									{#if item.description}
										<p class="text-xs text-secondary/80 line-clamp-1">{item.description}</p>
									{/if}
								</div>
							</td>
							<td class="px-md py-4 align-top">
								<div class="space-y-1">
									<div class="flex items-center gap-2">
										<span class="font-bold font-mono text-[14px] tabular-nums {isLowStock ? 'text-amber-800' : 'text-emerald-700'}">
											{item.availableQuantity} {item.unit} {t('inventory.availableShort')}
										</span>
										{#if isLowStock}
											<StatusBadge variant="warning" label={t('inventory.lowStock')} />
										{/if}
									</div>
									<div class="text-[12px] text-secondary font-mono tabular-nums flex items-center gap-2">
										<span>{item.onHandQuantity} {t('inventory.onHand')}</span>
										{#if item.reservedQuantity > 0}
											<span class="text-amber-700">({item.reservedQuantity} {t('inventory.reserved')})</span>
										{/if}
									</div>
								</div>
							</td>
							<td class="px-md py-4 align-top text-right">
								<div class="space-y-0.5 font-mono tabular-nums">
									<div class="font-bold text-ink text-body-sm">
										{formatCurrency(item.fixedSalePrice ?? item.salePrice, data.locale, currencyCode)}
									</div>
									{#if canSeeCost}
										<div class="text-[11px] text-secondary">
											{t('inventory.cost')}: {formatCurrency(item.purchasePrice ?? item.costPrice ?? 0, data.locale, currencyCode)}
										</div>
									{/if}
								</div>
							</td>
							<td class="px-md py-4 align-top hidden xl:table-cell">
								<div class="text-xs text-secondary space-y-0.5">
									{#if item.locationNote}
										<div class="flex items-center gap-1">
											<span class="material-symbols-outlined text-[15px] text-secondary">location_on</span>
											<span>{item.locationNote}</span>
										</div>
									{/if}
									{#if item.barcodeValue}
										<div class="font-mono text-[11px] text-secondary/80">
											{item.barcodeValue}
										</div>
									{/if}
								</div>
							</td>
							<td class="px-md py-4 align-top">
								<div class="flex items-center justify-end gap-1.5">
									{#if canWrite}
										<button
											type="button"
											class="inline-flex h-8 items-center gap-1 rounded-button border border-stone bg-white px-2.5 text-xs font-semibold text-ink hover:bg-ash transition-colors shadow-2xs cursor-pointer"
											onclick={() => stockInItem = item}
											title={t('inventory.stockIn')}
										>
											<span class="material-symbols-outlined text-[15px]">add_box</span>
											<span>{t('inventory.stockIn')}</span>
										</button>
										<button
											type="button"
											class="inline-flex h-8 items-center gap-1 rounded-button border border-stone bg-white px-2.5 text-xs font-medium text-secondary hover:text-ink hover:bg-ash transition-colors shadow-2xs cursor-pointer"
											onclick={() => adjustStockItem = item}
											title={t('inventory.adjust')}
										>
											<span class="material-symbols-outlined text-[15px]">tune</span>
										</button>
									{/if}
									{#if canManageItems}
										<button
											type="button"
											class="inline-flex h-8 items-center rounded-button border border-stone bg-white px-2.5 text-xs font-medium text-secondary hover:text-ink hover:bg-ash transition-colors shadow-2xs cursor-pointer"
											onclick={() => editingItem = item}
											title={t('inventory.editItem')}
											aria-label={t('inventory.editItem')}
										>
											<span class="material-symbols-outlined text-[15px]" aria-hidden="true">edit</span>
										</button>
									{/if}
									<a
										class="inline-flex h-8 items-center rounded-button border border-stone bg-white px-2.5 text-xs font-medium text-secondary hover:text-ink hover:bg-ash transition-colors shadow-2xs"
										href={`/app/inventory/${item.id}/label?from=list`}
										title={t('inventory.printLabel')}
										aria-label={t('inventory.printLabel')}
									>
										<span class="material-symbols-outlined text-[15px]" aria-hidden="true">print</span>
									</a>
									<a
										class="inline-flex h-8 items-center rounded-button border border-stone bg-white px-2.5 text-xs font-medium text-primary hover:bg-ash transition-colors shadow-2xs"
										href={`/app/inventory/${item.id}`}
									>
										{t('tickets.list.open')}
									</a>
									{#if canDeleteInventory}
										<form
											method="POST"
											action="?/deleteItem"
											use:enhance={() => {
												deletingItemId = item.id;
												return async ({ update }) => {
													await update();
													deletingItemId = null;
												};
											}}
										>
											<input type="hidden" name="id" value={item.id} />
											<DeleteConfirmButton
												label={t('common.delete')}
												confirmLabel={t('common.delete')}
												confirmTitle={t('inventory.deleteItemTitle')}
												confirmMessage={t('inventory.deleteItemConfirm')}
												cancelLabel={t('common.cancel')}
												icon="delete"
												iconOnly
												disabled={item.reservedQuantity > 0}
												submitting={deletingItemId === item.id}
												title={item.reservedQuantity > 0 ? t('inventory.deleteBlockedReserved') : t('inventory.deleteOrDeactivate')}
												ariaLabel={item.reservedQuantity > 0 ? t('inventory.deleteBlockedReserved') : t('inventory.deleteOrDeactivate')}
												class="inline-flex h-8 items-center justify-center rounded-button border border-error/30 bg-error/10 px-2.5 text-xs font-medium text-error hover:bg-error/15 transition-colors shadow-2xs cursor-pointer disabled:opacity-40 disabled:cursor-not-allowed disabled:hover:bg-error/10"
											/>
										</form>
									{/if}
								</div>
							</td>
						</tr>
					{/each}
					{#if data.items.length === 0}
						<tr>
							<td colspan="6" class="px-md py-16 text-center">
								<div class="max-w-md mx-auto flex flex-col items-center justify-center text-center">
									<div class="w-12 h-12 rounded-xl bg-ash border border-stone flex items-center justify-center text-secondary mb-3 shadow-2xs">
										<span class="material-symbols-outlined text-[24px]">inbox</span>
									</div>
									<h4 class="font-bold text-ink text-body-base mb-1">{t('inventory.empty')}</h4>
									<p class="text-body-sm text-secondary mb-4">{t('inventory.noItemsCopy')}</p>
									<div class="flex items-center gap-2">
										{#if hasActiveFilters}
											<a
												href="?page=1"
												class="px-button-x py-button-y rounded-button font-medium font-body-sm text-body-sm inline-flex items-center gap-1.5 border border-stone bg-white text-ink hover:bg-ash transition-all shadow-2xs"
											>
												<span class="material-symbols-outlined text-[18px]">restart_alt</span>
												<span>{t('inventory.clearFilters')}</span>
											</a>
										{/if}
										{#if canManageItems}
											<button
												type="button"
												class="px-button-x py-button-y rounded-button font-medium font-body-sm text-body-sm inline-flex items-center gap-1.5 bg-ink text-white hover:bg-ink/90 active:scale-95 transition-all shadow-xs cursor-pointer"
												onclick={() => showAddModal = true}
											>
												<span class="material-symbols-outlined text-[18px]">add</span>
												<span>{t('inventory.addItem')}</span>
											</button>
										{/if}
									</div>
								</div>
							</td>
						</tr>
					{/if}
				</tbody>
			</table>
		</div>

		<!-- Mobile & Tablet Card View (lg:hidden) -->
		<div class="lg:hidden divide-y divide-stone">
			{#each data.items as item}
				{@const isLowStock = item.availableQuantity <= item.reorderLevel}
				<div class="p-md hover:bg-ash/30 transition-colors space-y-2">
					<div class="flex items-start justify-between gap-2">
						<div>
							<div class="flex items-center gap-2">
								<span class="font-mono font-bold text-xs text-ink bg-ash/80 px-1.5 py-0.5 rounded border border-stone">
									{item.sku}
								</span>
								{#if !item.isActive}
									<StatusBadge variant="neutral" label={t('inventory.inactive')} />
								{/if}
								{#if isLowStock}
									<span class="px-1.5 py-0.5 rounded text-[10px] font-bold uppercase tracking-wider bg-amber-500/10 text-amber-800 border border-amber-300">
										{t('inventory.lowStock')}
									</span>
								{/if}
							</div>
							<a class="font-semibold text-ink text-body-sm hover:underline block mt-1" href={`/app/inventory/${item.id}`}>
								{item.name}
							</a>
						</div>
						<div class="text-right font-mono">
							<div class="font-bold text-ink text-body-sm">{formatCurrency(item.fixedSalePrice ?? item.salePrice, data.locale, currencyCode)}</div>
							<div class="text-xs {isLowStock ? 'text-amber-800 font-bold' : 'text-emerald-700 font-semibold'}">{item.availableQuantity} {item.unit} {t('inventory.availableShort')}</div>
						</div>
					</div>

					<div class="flex flex-wrap items-center justify-between gap-2 pt-1 border-t border-stone/50">
						<span class="text-xs text-secondary">
							{item.categoryName ?? t('inventory.noCategory')} · {t(`inventory.itemType.${item.itemType}`) || item.itemType}
						</span>
						<div class="flex items-center gap-1.5">
							{#if canWrite}
								<button
									type="button"
									class="inline-flex h-7 items-center gap-1 rounded-button border border-stone bg-white px-2 text-xs font-semibold text-ink hover:bg-ash cursor-pointer shadow-2xs"
									onclick={() => stockInItem = item}
								>
									<span class="material-symbols-outlined text-[14px]">add_box</span>
									<span>{t('inventory.stockIn')}</span>
								</button>
								<button
									type="button"
									class="inline-flex h-7 items-center rounded-button border border-stone bg-white px-2 text-xs text-secondary hover:text-ink hover:bg-ash cursor-pointer shadow-2xs"
									onclick={() => adjustStockItem = item}
								>
									<span class="material-symbols-outlined text-[14px]">tune</span>
								</button>
							{/if}
							{#if canManageItems}
								<button
									type="button"
									class="inline-flex h-7 items-center rounded-button border border-stone bg-white px-2 text-xs text-secondary hover:text-ink hover:bg-ash cursor-pointer shadow-2xs"
									onclick={() => editingItem = item}
									title={t('inventory.editItem')}
									aria-label={t('inventory.editItem')}
								>
									<span class="material-symbols-outlined text-[14px]" aria-hidden="true">edit</span>
								</button>
							{/if}
							<a
								class="inline-flex h-7 items-center rounded-button border border-stone bg-white px-2 text-xs font-medium text-primary hover:bg-ash shadow-2xs"
								href={`/app/inventory/${item.id}`}
							>
								{t('tickets.list.open')}
							</a>
							{#if canDeleteInventory}
								<form
									method="POST"
									action="?/deleteItem"
									use:enhance={() => {
										deletingItemId = item.id;
										return async ({ update }) => {
											await update();
											deletingItemId = null;
										};
									}}
								>
									<input type="hidden" name="id" value={item.id} />
									<DeleteConfirmButton
										label={t('common.delete')}
										confirmLabel={t('common.delete')}
										confirmTitle={t('inventory.deleteItemTitle')}
										confirmMessage={t('inventory.deleteItemConfirm')}
										cancelLabel={t('common.cancel')}
										icon="delete"
										iconOnly
										disabled={item.reservedQuantity > 0}
										submitting={deletingItemId === item.id}
										title={item.reservedQuantity > 0 ? t('inventory.deleteBlockedReserved') : t('inventory.deleteOrDeactivate')}
										ariaLabel={item.reservedQuantity > 0 ? t('inventory.deleteBlockedReserved') : t('inventory.deleteOrDeactivate')}
										class="inline-flex h-7 items-center justify-center rounded-button border border-error/30 bg-error/10 px-2 text-xs text-error hover:bg-error/15 cursor-pointer shadow-2xs disabled:opacity-40 disabled:cursor-not-allowed"
									/>
								</form>
							{/if}
						</div>
					</div>
				</div>
			{/each}
			{#if data.items.length === 0}
				<div class="p-8 text-center text-secondary text-body-sm">{t('inventory.empty')}</div>
			{/if}
		</div>

		<!-- Pagination Footer -->
		<div class="flex flex-col sm:flex-row items-center justify-between gap-sm px-md py-4 bg-surface-container-low border-t border-stone">
			<p class="font-body-sm text-body-sm text-secondary">
				{t('common.pagination.showing')}
				<span class="font-bold text-primary font-mono">{data.items.length}</span>
				{t('common.pagination.of')}
				<span class="font-bold text-primary font-mono">{data.inventoryPage.totalCount}</span>
			</p>
			<div class="flex items-center gap-sm text-body-sm">
				<a
					class="px-3 py-1.5 rounded-button border border-stone bg-white text-primary hover:bg-ash transition-colors inline-flex items-center gap-1 shadow-2xs {currentPage <= 1 ? 'pointer-events-none opacity-40' : ''}"
					href={pageHref(currentPage - 1)}
				>
					<span class="material-symbols-outlined text-[16px]">chevron_left</span>
					<span>{t('common.previous')}</span>
				</a>
				<span class="text-secondary font-mono px-2">{currentPage} / {totalPages}</span>
				<a
					class="px-3 py-1.5 rounded-button border border-stone bg-white text-primary hover:bg-ash transition-colors inline-flex items-center gap-1 shadow-2xs {currentPage >= totalPages ? 'pointer-events-none opacity-40' : ''}"
					href={pageHref(currentPage + 1)}
				>
					<span>{t('common.next')}</span>
					<span class="material-symbols-outlined text-[16px]">chevron_right</span>
				</a>
			</div>
		</div>
	</div>

	<!-- Recent Movements Audit Log -->
	<section data-bento class="bg-surface-container-lowest border border-stone rounded-xl overflow-hidden shadow-2xs">
		<div class="px-md py-3.5 border-b border-stone flex items-center justify-between bg-surface-container-low">
			<h3 class="font-subheading text-[15px] font-bold text-ink">{t('inventory.recentMovements')}</h3>
			<span class="text-xs text-secondary font-mono">{data.movements.length} {t('inventory.items')}</span>
		</div>
		<div class="overflow-x-auto custom-scrollbar">
			<table class="w-full text-left border-collapse min-w-[640px]">
				<tbody class="divide-y divide-stone font-body-sm text-body-sm">
					{#each data.movements as movement}
						<tr class="hover:bg-ash/40 transition-colors">
							<td class="px-md py-3 font-semibold text-primary">
								<span class="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-bold uppercase tracking-wider {movement.type === 'StockIn' ? 'bg-emerald-500/10 text-emerald-800 border border-emerald-300' : movement.type === 'Adjustment' ? 'bg-blue-500/10 text-blue-800 border border-blue-200' : 'bg-ash text-secondary border border-stone'}">
									{movement.type}
								</span>
							</td>
							<td class="px-md py-3 text-secondary">
								<a class="font-medium text-ink hover:underline" href={`/app/inventory/${movement.inventoryItemId}`}>{movement.itemName}</a>
							</td>
							<td class="px-md py-3 text-ink font-mono font-bold">
								{movement.quantity > 0 ? `+${movement.quantity}` : movement.quantity}
							</td>
							<td class="px-md py-3 text-secondary hidden sm:table-cell">
								{#if movement.serviceTicketId && movement.ticketNumber}
									<a class="font-mono text-xs text-primary hover:underline font-semibold" href={`/app/tickets/${movement.serviceTicketId}`}>{movement.ticketNumber}</a>
								{:else}
									<span class="text-secondary/60">-</span>
								{/if}
							</td>
							<td class="px-md py-3 text-secondary text-xs hidden md:table-cell">{movement.reason ?? '-'}</td>
							<td class="px-md py-3 text-right text-secondary font-mono text-xs">{formatDate(movement.createdAt, data.locale, { dateStyle: 'medium', timeStyle: 'short' })}</td>
						</tr>
					{/each}
					{#if data.movements.length === 0}
						<tr>
							<td class="px-md py-8 text-center text-secondary" colspan="6">{t('inventory.noMovement')}</td>
						</tr>
					{/if}
				</tbody>
			</table>
		</div>
	</section>
</div>

<!-- ================= MODALS & DIALOGS ================= -->

<!-- 1. Add / Edit Item Modal -->
<InventoryItemModal
	open={itemModalOpen}
	item={editingItem}
	categories={data.categories}
	locale={data.locale}
	{currencyCode}
	{defaultTaxPercent}
	{defaultTaxMode}
	{roundingIncrement}
	{defaultUnit}
	onclose={() => {
		showAddModal = false;
		editingItem = null;
	}}
/>

<!-- 2. Quick Stock In Modal -->
{#if stockInItem}
	<div transition:fade={{ duration: 150 }} class="fixed inset-0 z-50 bg-black/60 backdrop-blur-xs flex items-center justify-center p-4">
		<div class="bg-white border border-stone rounded-2xl shadow-2xl max-w-md w-full overflow-hidden">
			<div class="px-6 py-4 border-b border-stone flex items-center justify-between bg-surface-container-low">
				<div>
					<h3 class="font-subheading text-[16px] font-bold text-ink">{t('inventory.stockIn')}</h3>
					<p class="text-xs text-secondary mt-0.5 font-mono">{stockInItem.sku} · {stockInItem.name}</p>
				</div>
				<button type="button" class="w-8 h-8 rounded-lg hover:bg-ash text-secondary hover:text-ink flex items-center justify-center cursor-pointer transition-colors" onclick={() => stockInItem = null}>
					<span class="material-symbols-outlined text-[20px]">close</span>
				</button>
			</div>

			<form method="POST" action="?/stockIn" class="p-6 space-y-4">
				<input type="hidden" name="id" value={stockInItem.id} />

				<div class="bg-ash/50 border border-stone rounded-xl p-3 flex items-center justify-between text-body-sm">
					<span class="text-secondary">{t('inventory.detail.available')}:</span>
					<strong class="font-mono font-bold text-emerald-700">{stockInItem.availableQuantity} {stockInItem.unit}</strong>
				</div>

				<label class="space-y-1 block">
					<span class="font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('inventory.qty')} *</span>
					<input name="quantity" type="number" min="0.01" step="0.01" required class="w-full border border-stone rounded-input px-3 py-2 text-body-sm font-mono text-ink focus:border-ink focus:ring-1 focus:ring-ink/10 focus:outline-none" placeholder="Quantity to add" />
				</label>

				{#if canSeeCost}
					<div class="space-y-1">
						<span class="font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('inventory.unitCost')}</span>
						<CurrencyInput
							name="unitCost"
							prefix={currencySymbol}
							locale={data.locale}
							currency={currencyCode}
							placeholder={t('inventory.unitCost')}
						/>
					</div>
				{/if}

				<label class="space-y-1 block">
					<span class="font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('inventory.reason')}</span>
					<input name="reason" class="w-full border border-stone rounded-input px-3 py-2 text-body-sm text-ink focus:border-ink focus:ring-1 focus:ring-ink/10 focus:outline-none" placeholder="e.g. Restock PO #1042" />
				</label>

				<div class="pt-2 flex items-center justify-end gap-2">
					<button type="button" class="px-4 py-2 rounded-button border border-stone bg-white text-secondary hover:text-ink hover:bg-ash font-medium text-body-sm transition-all cursor-pointer" onclick={() => stockInItem = null}>
						{t('common.cancel')}
					</button>
					<button type="submit" class="px-5 py-2 rounded-button bg-ink text-white font-medium text-body-sm hover:bg-ink/90 active:scale-95 transition-all shadow-xs cursor-pointer">
						{t('inventory.stockIn')}
					</button>
				</div>
			</form>
		</div>
	</div>
{/if}

<!-- 3. Quick Adjust Stock Modal -->
{#if adjustStockItem}
	<div transition:fade={{ duration: 150 }} class="fixed inset-0 z-50 bg-black/60 backdrop-blur-xs flex items-center justify-center p-4">
		<div class="bg-white border border-stone rounded-2xl shadow-2xl max-w-md w-full overflow-hidden">
			<div class="px-6 py-4 border-b border-stone flex items-center justify-between bg-surface-container-low">
				<div>
					<h3 class="font-subheading text-[16px] font-bold text-ink">{t('inventory.adjust')}</h3>
					<p class="text-xs text-secondary mt-0.5 font-mono">{adjustStockItem.sku} · {adjustStockItem.name}</p>
				</div>
				<button type="button" class="w-8 h-8 rounded-lg hover:bg-ash text-secondary hover:text-ink flex items-center justify-center cursor-pointer transition-colors" onclick={() => adjustStockItem = null}>
					<span class="material-symbols-outlined text-[20px]">close</span>
				</button>
			</div>

			<form method="POST" action="?/adjustStock" class="p-6 space-y-4">
				<input type="hidden" name="id" value={adjustStockItem.id} />

				<div class="bg-ash/50 border border-stone rounded-xl p-3 flex items-center justify-between text-body-sm">
					<span class="text-secondary">{t('inventory.detail.onHand')}:</span>
					<strong class="font-mono font-bold text-ink">{adjustStockItem.onHandQuantity} {adjustStockItem.unit}</strong>
				</div>

				<label class="space-y-1 block">
					<span class="font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('inventory.qtyDelta')} (+ / -) *</span>
					<input name="quantityDelta" type="number" step="0.01" required class="w-full border border-stone rounded-input px-3 py-2 text-body-sm font-mono text-ink focus:border-ink focus:ring-1 focus:ring-ink/10 focus:outline-none" placeholder="e.g. -1 for damaged, +2 for recount" />
				</label>

				<label class="space-y-1 block">
					<span class="font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('inventory.requiredReason')} *</span>
					<input name="reason" required class="w-full border border-stone rounded-input px-3 py-2 text-body-sm text-ink focus:border-ink focus:ring-1 focus:ring-ink/10 focus:outline-none" placeholder="e.g. Audit variance, broken during bench test" />
				</label>

				<div class="pt-2 flex items-center justify-end gap-2">
					<button type="button" class="px-4 py-2 rounded-button border border-stone bg-white text-secondary hover:text-ink hover:bg-ash font-medium text-body-sm transition-all cursor-pointer" onclick={() => adjustStockItem = null}>
						{t('common.cancel')}
					</button>
					<button type="submit" class="px-5 py-2 rounded-button bg-ink text-white font-medium text-body-sm hover:bg-ink/90 active:scale-95 transition-all shadow-xs cursor-pointer">
						{t('inventory.adjust')}
					</button>
				</div>
			</form>
		</div>
	</div>
{/if}

<!-- 4. Categories Management Modal -->
{#if showCategoriesModal}
	<div transition:fade={{ duration: 150 }} class="fixed inset-0 z-50 bg-black/60 backdrop-blur-xs flex items-center justify-center p-4">
		<div class="bg-white border border-stone rounded-2xl shadow-2xl max-w-2xl w-full max-h-[88vh] flex flex-col overflow-hidden">
			<div class="px-6 py-4 border-b border-stone flex items-center justify-between bg-surface-container-low shrink-0">
				<div>
					<h3 class="font-subheading text-[16px] font-bold text-ink">{t('inventory.categories')}</h3>
					<p class="text-xs text-secondary mt-0.5">{t('inventory.categoryDescription')}</p>
				</div>
				<button type="button" class="w-8 h-8 rounded-lg hover:bg-ash text-secondary hover:text-ink flex items-center justify-center cursor-pointer transition-colors" onclick={() => showCategoriesModal = false}>
					<span class="material-symbols-outlined text-[20px]">close</span>
				</button>
			</div>

			<div class="p-6 overflow-y-auto space-y-6 custom-scrollbar flex-1">
				<!-- Add Category Form -->
				<form method="POST" action="?/saveCategory" class="p-4 bg-ash/40 border border-stone rounded-xl space-y-3">
					<input type="hidden" name="isActive" value="on" />
					<div class="font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('inventory.addCategory')}</div>
					<div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
						<input name="name" required class="border border-stone rounded-input px-3 py-2 text-body-sm text-ink focus:border-ink bg-white" placeholder={t('inventory.categoryName')} />
						<input name="description" class="border border-stone rounded-input px-3 py-2 text-body-sm text-ink focus:border-ink bg-white" placeholder={t('inventory.categoryDescription')} />
					</div>
					<div class="flex justify-end">
						<button class="px-4 py-2 rounded-button bg-ink text-white font-medium text-body-sm hover:bg-ink/90 active:scale-95 transition-all shadow-xs cursor-pointer" type="submit">
							{t('inventory.addCategory')}
						</button>
					</div>
				</form>

				<!-- Existing Categories List -->
				<div class="space-y-2">
					<div class="font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('inventory.categories')} ({data.categories.length})</div>
					<div class="divide-y divide-stone border border-stone rounded-xl overflow-hidden bg-white">
						{#each data.categories as category}
							<form method="POST" class="p-3 flex flex-wrap items-center justify-between gap-3 hover:bg-ash/20 transition-colors">
								<input type="hidden" name="id" value={category.id} />
								<div class="flex-1 min-w-[200px] grid grid-cols-1 sm:grid-cols-2 gap-2">
									<input name="name" class="border border-stone rounded-input px-2.5 py-1.5 text-body-sm text-ink focus:border-ink font-semibold" value={category.name} required formaction="?/saveCategory" />
									<input name="description" class="border border-stone rounded-input px-2.5 py-1.5 text-body-sm text-ink focus:border-ink" value={category.description ?? ''} placeholder={t('inventory.categoryDescription')} formaction="?/saveCategory" />
								</div>
								<div class="flex items-center gap-3 shrink-0">
									<label class="flex items-center gap-1.5 text-xs text-secondary font-medium">
										<input name="isActive" type="checkbox" checked={category.isActive} formaction="?/saveCategory" class="rounded text-ink focus:ring-ink" />
										{t('settings.active')}
									</label>
									<button class="px-3 py-1.5 rounded-button border border-stone text-xs font-semibold text-ink bg-white hover:bg-ash transition-colors cursor-pointer shadow-2xs" formaction="?/saveCategory" type="submit">
										{t('settings.save')}
									</button>
									{#if canDeleteInventory}
										<DeleteConfirmButton label={t('common.delete')} confirmTitle={t('common.deleteConfirmTitle')} confirmMessage={t('common.deleteConfirm')} cancelLabel={t('common.cancel')} formaction="?/deleteCategory" class="px-3 py-1.5 rounded-button border border-error/30 bg-error/10 text-error text-xs font-semibold hover:bg-error/15 transition-colors cursor-pointer" />
									{/if}
								</div>
							</form>
						{/each}
						{#if data.categories.length === 0}
							<div class="p-6 text-center text-secondary text-body-sm">{t('inventory.noCategory')}</div>
						{/if}
					</div>
				</div>
			</div>
		</div>
	</div>
{/if}

<!-- 5. Import / Export Modal -->
{#if showImportModal}
	<div transition:fade={{ duration: 150 }} class="fixed inset-0 z-50 bg-black/60 backdrop-blur-xs flex items-center justify-center p-4 overflow-y-auto">
		<div class="bg-white border border-stone rounded-2xl shadow-2xl max-w-3xl w-full max-h-[90vh] flex flex-col overflow-hidden my-auto">
			<div class="px-6 py-4 border-b border-stone flex items-center justify-between bg-surface-container-low shrink-0">
				<div>
					<h3 class="font-subheading text-[17px] font-bold text-ink">{t('inventory.import.title')}</h3>
					<p class="text-xs text-secondary mt-0.5">{t('inventory.import.subtitle')}</p>
				</div>
				<button type="button" class="w-8 h-8 rounded-lg hover:bg-ash text-secondary hover:text-ink flex items-center justify-center cursor-pointer transition-colors" onclick={() => showImportModal = false}>
					<span class="material-symbols-outlined text-[20px]">close</span>
				</button>
			</div>

			<div class="p-6 overflow-y-auto space-y-6 custom-scrollbar flex-1">
				<!-- Step 1: Download Templates -->
				<div class="p-4 bg-ash/40 border border-stone rounded-xl flex flex-col sm:flex-row sm:items-center justify-between gap-3">
					<div>
						<h4 class="font-label-caps text-ink text-[12px] font-bold uppercase tracking-wider">{t('inventory.import.downloadTemplate')}</h4>
						<p class="text-xs text-secondary mt-0.5">Start with a pre-formatted Excel or CSV template.</p>
					</div>
					<div class="flex items-center gap-2">
						<a class="inline-flex items-center gap-1.5 px-3 py-2 rounded-button border border-stone bg-white text-xs font-semibold text-ink hover:bg-ash transition-colors shadow-2xs" href="/app/inventory/import-template?format=xlsx">
							<span class="material-symbols-outlined text-[16px]">table_view</span>
							<span>{t('inventory.import.excelTemplate')}</span>
						</a>
						<a class="inline-flex items-center gap-1.5 px-3 py-2 rounded-button border border-stone bg-white text-xs font-semibold text-ink hover:bg-ash transition-colors shadow-2xs" href="/app/inventory/import-template?format=csv">
							<span class="material-symbols-outlined text-[16px]">description</span>
							<span>{t('inventory.import.csvTemplate')}</span>
						</a>
					</div>
				</div>

				<!-- Step 2: Upload File & Preview Dry Run -->
				<form method="POST" action="?/importDryRun" enctype="multipart/form-data" class="p-4 border border-stone rounded-xl space-y-4">
					<h4 class="font-label-caps text-ink text-[12px] font-bold uppercase tracking-wider">{t('inventory.import.file')}</h4>
					<div class="flex flex-col sm:flex-row sm:items-end gap-3">
						<label class="space-y-1 flex-1 block">
							<input name="file" type="file" accept=".xlsx,.csv" required class="w-full border border-stone rounded-input p-2 text-body-sm text-ink focus:border-ink" />
						</label>
						<button class="px-5 py-2.5 rounded-button bg-ink text-white font-medium text-body-sm hover:bg-ink/90 active:scale-95 transition-all shadow-xs cursor-pointer shrink-0" type="submit">
							{t('inventory.import.preview')}
						</button>
					</div>
				</form>

				<!-- Step 3: Dry Run Preview Table (if available) -->
				{#if form?.importPreview}
					<div class="rounded-xl border border-stone overflow-hidden">
						<div class="p-4 bg-ash/60 flex flex-wrap items-center justify-between gap-3 text-body-sm border-b border-stone">
							<div class="font-mono text-xs">
								{t('inventory.import.total')}: <strong class="text-ink">{form.importPreview.totalRows}</strong> ·
								{t('inventory.import.valid')}: <strong class="text-emerald-700">{form.importPreview.validRows}</strong> ·
								{t('inventory.import.errors')}: <strong class="text-error">{form.importPreview.errorRows}</strong>
							</div>
							<form method="POST" action="?/applyImport" class="flex gap-2 items-center">
								<input type="hidden" name="batchId" value={form.importPreview.batchId} />
								<div class="w-36">
									<Dropdown
										size="sm"
										name="mode"
										value="upsert"
										options={[
											{ value: 'upsert', label: t('inventory.import.mode.upsert') },
											{ value: 'create', label: t('inventory.import.mode.create') },
											{ value: 'update', label: t('inventory.import.mode.update') }
										]}
									/>
								</div>
								<button class="px-4 py-1.5 rounded-button bg-ink text-white text-xs font-semibold hover:bg-ink/90 active:scale-95 transition-all shadow-xs disabled:opacity-50 cursor-pointer" type="submit" disabled={form.importPreview.validRows === 0}>
									{t('inventory.import.apply')}
								</button>
							</form>
						</div>
						<div class="overflow-x-auto max-h-64 custom-scrollbar">
							<table class="w-full text-left text-xs">
								<thead class="bg-surface-container-low border-b border-stone font-label-caps text-secondary font-bold uppercase">
									<tr>
										<th class="px-3 py-2">{t('inventory.import.row')}</th>
										<th class="px-3 py-2">{t('inventory.sku')}</th>
										<th class="px-3 py-2">{t('inventory.name')}</th>
										<th class="px-3 py-2">{t('inventory.category')}</th>
										<th class="px-3 py-2">{t('inventory.import.action')}</th>
										<th class="px-3 py-2">{t('inventory.import.status')}</th>
									</tr>
								</thead>
								<tbody class="divide-y divide-stone font-mono">
									{#each form.importPreview.rows as row}
										<tr class="hover:bg-ash/20">
											<td class="px-3 py-2 text-secondary">{row.rowNumber}</td>
											<td class="px-3 py-2 font-bold text-ink">{row.sku}</td>
											<td class="px-3 py-2 font-sans text-ink">{row.name}</td>
											<td class="px-3 py-2 text-secondary font-sans">{row.categoryName ?? '-'}</td>
											<td class="px-3 py-2 text-secondary">{row.action ?? '-'}</td>
											<td class="px-3 py-2 {row.isValid ? 'text-emerald-700 font-bold' : 'text-error font-semibold'}">{row.isValid ? t('inventory.import.valid') : row.errorMessage}</td>
										</tr>
									{/each}
								</tbody>
							</table>
						</div>
					</div>
				{/if}
			</div>
		</div>
	</div>
{/if}
