<script lang="ts">
	import PageHeader from '$lib/components/PageHeader.svelte';
	import StatusBadge from '$lib/components/ui/badge/StatusBadge.svelte';
	import { createTranslator, formatCurrency } from '$lib/i18n';
	import type { PageData } from './$types';
	import DatePicker from '$lib/components/ui/date-picker/DatePicker.svelte';
	import Dropdown from '$lib/components/ui/dropdown/Dropdown.svelte';

	let { data }: { data: PageData } = $props();

	const report = $derived(data.report);
	const filters = $derived(data.filters);
	const t = $derived(createTranslator(data.locale));
	const currencyCode = $derived(data.shopProfile?.currencyCode ?? 'IDR');
	const exportUrl = $derived(`/app/reports/export?${new URLSearchParams(filters).toString()}`);
	const pageSizeOptions = [10, 20, 50, 100];
	let pageSize = $state(10);
	let lowStockGroupPage = $state(1);
	let valuationPage = $state(1);
	let paymentPage = $state(1);
	let lowStockPage = $state(1);
	let movementPage = $state(1);
	let lowStockGroupSort = $state('available-asc');
	let valuationSort = $state('value-desc');
	let paymentSort = $state('balance-desc');
	let lowStockSort = $state('available-asc');
	let movementSort = $state('period-desc');

	const hasActiveFilters = $derived(
		Boolean(filters.from || filters.to || (filters.groupBy && filters.groupBy !== 'daily') || pageSize !== 10)
	);

	const sortedLowStockGroups = $derived(sortRows(report.inventoryLowStockGroups, lowStockGroupSort, {
		category: (row) => row.categoryName,
		items: (row) => row.itemCount,
		available: (row) => row.availableQuantity
	}));
	const sortedValuationRows = $derived(sortRows(report.inventoryValuationRows, valuationSort, {
		category: (row) => row.categoryName,
		items: (row) => row.itemCount,
		value: (row) => row.inventoryValue ?? 0
	}));
	const sortedPaymentRows = $derived(sortRows(report.paymentRows, paymentSort, {
		ticket: (row) => row.ticketNumber,
		customer: (row) => row.customerName,
		balance: (row) => (row.totalAmount ?? 0) - row.amountPaid
	}));
	const sortedLowStockRows = $derived(sortRows(report.inventoryRows, lowStockSort, {
		sku: (row) => row.sku,
		item: (row) => row.name,
		available: (row) => row.availableQuantity
	}));
	const sortedMovementRows = $derived(sortRows(report.inventoryMovementRows, movementSort, {
		period: (row) => row.period,
		type: (row) => row.type,
		quantity: (row) => row.quantity,
		cost: (row) => row.cost ?? 0
	}));

	function formatMoney(value: number | null | undefined) {
		return formatCurrency(value, data.locale, currencyCode);
	}

	function pageCount(rows: unknown[]) {
		return Math.max(1, Math.ceil(rows.length / pageSize));
	}

	function pageRows<T>(rows: T[], page: number) {
		return rows.slice((page - 1) * pageSize, page * pageSize);
	}

	function sortRows<T>(rows: T[], sort: string, selectors: Record<string, (row: T) => string | number>) {
		const desc = sort.endsWith('-desc');
		const key = sort.replace(/-(asc|desc)$/, '');
		const selector = selectors[key] ?? Object.values(selectors)[0];
		return [...rows].sort((a, b) => {
			const left = selector(a);
			const right = selector(b);
			const result = typeof left === 'number' && typeof right === 'number'
				? left - right
				: String(left).localeCompare(String(right));
			return desc ? -result : result;
		});
	}

	function resetPages() {
		lowStockGroupPage = 1;
		valuationPage = 1;
		paymentPage = 1;
		lowStockPage = 1;
		movementPage = 1;
	}

	const ticketTotals = $derived([
		{ label: t('reports.received'), value: report.totals.receivedTickets, tone: 'text-primary' },
		{ label: t('reports.completed'), value: report.totals.completedTickets, tone: 'text-primary' },
		{ label: t('reports.cancelled'), value: report.totals.cancelledTickets, tone: 'text-rose-600' },
		{ label: t('reports.unpaid'), value: report.totals.unpaidTickets + report.totals.partialPaidTickets, tone: 'text-amber-600' }
	]);

	const financialTotals = $derived([
		{ label: t('reports.revenue'), value: formatMoney(report.totals.totalRevenue), tone: 'text-primary' },
		{ label: t('reports.inventoryValue'), value: formatMoney(report.totals.totalInventoryValue), tone: 'text-primary' },
		{
			label: report.canViewCost ? t('reports.grossProfit') : t('reports.lowStock'),
			value: report.canViewCost ? formatMoney(report.totals.grossProfit) : report.totals.lowStockItems,
			tone: 'text-primary'
		}
	]);
</script>

{#snippet paymentStatusBadge(status: string)}
	<StatusBadge
		variant={status === 'Paid' ? 'success' : status === 'Partial' ? 'warning' : 'error'}
		label={t(`tickets.paymentStatus.${status}`) !== `tickets.paymentStatus.${status}` ? t(`tickets.paymentStatus.${status}`) : status}
		dot={true}
	/>
{/snippet}

{#snippet emptyTableRow(colspan: number)}
	<tr class="border-t border-stone">
		<td colspan={colspan} class="px-card-padding py-8 text-center text-body-sm text-secondary">
			<div class="flex flex-col items-center justify-center gap-1">
				<span class="material-symbols-outlined text-[24px] text-secondary/60">inbox</span>
				<span class="text-caption font-medium text-secondary">{t('reports.empty')}</span>
			</div>
		</td>
	</tr>
{/snippet}

{#snippet paginationBar(currentPage: number, total: number, onPrev: () => void, onNext: () => void)}
	<div class="flex items-center justify-between border-t border-stone px-card-padding py-2.5 text-body-sm text-secondary bg-ash/20">
		<span class="text-caption font-medium text-secondary">{currentPage} / {total}</span>
		<div class="flex items-center gap-xs">
			<button
				type="button"
				class="rounded-button border border-stone bg-white px-3 py-1 text-caption font-medium text-secondary hover:text-primary hover:bg-ash disabled:opacity-40 disabled:cursor-not-allowed cursor-pointer shadow-2xs transition-colors"
				disabled={currentPage <= 1}
				onclick={onPrev}
			>
				{t('common.previous')}
			</button>
			<button
				type="button"
				class="rounded-button border border-stone bg-white px-3 py-1 text-caption font-medium text-secondary hover:text-primary hover:bg-ash disabled:opacity-40 disabled:cursor-not-allowed cursor-pointer shadow-2xs transition-colors"
				disabled={currentPage >= total}
				onclick={onNext}
			>
				{t('common.next')}
			</button>
		</div>
	</div>
{/snippet}

<div class="space-y-lg pb-xl">
	<PageHeader
		items={[{ label: t('common.console'), href: '/app' }, { label: t('reports.title') }]}
		title={t('reports.title')}
		subtitle={t('reports.subtitle')}
	>
		{#snippet actions()}
			<a
				class="h-10 px-4 rounded-button font-medium bg-primary text-white hover:opacity-90 transition-opacity cursor-pointer inline-flex items-center justify-center gap-1.5 text-body-sm shadow-2xs whitespace-nowrap"
				href={exportUrl}
			>
				<span class="material-symbols-outlined text-[18px]">download</span>
				<span>{t('reports.exportCsv')}</span>
			</a>
		{/snippet}
	</PageHeader>

	<!-- Standardized Bento Filters Card -->
	<form
		method="GET"
		data-bento
		class="rounded-xl border border-stone bg-white p-md shadow-2xs relative z-30 !overflow-visible"
	>
		<div class="flex flex-col lg:flex-row lg:items-end gap-md !overflow-visible">
			<div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-md flex-1 !overflow-visible">
				<div class="flex flex-col space-y-1">
					<span class="font-label-caps text-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('reports.from')}</span>
					<DatePicker name="from" value={filters.from} clearable />
				</div>
				<div class="flex flex-col space-y-1">
					<span class="font-label-caps text-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('reports.to')}</span>
					<DatePicker name="to" value={filters.to} clearable />
				</div>
				<div class="flex flex-col space-y-1">
					<span class="font-label-caps text-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('reports.group')}</span>
					<Dropdown
						name="groupBy"
						value={filters.groupBy}
						options={[
							{ value: 'daily', label: t('reports.daily') },
							{ value: 'weekly', label: t('reports.weekly') },
							{ value: 'monthly', label: t('reports.monthly') }
						]}
					/>
				</div>
				<div class="flex flex-col space-y-1">
					<span class="font-label-caps text-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('reports.pageSize')}</span>
					<Dropdown
						name="pageSize"
						value={String(pageSize)}
						options={pageSizeOptions.map(s => ({ value: String(s), label: String(s) }))}
						onchange={(val) => {
							pageSize = Number(val);
							resetPages();
						}}
						align="right"
					/>
				</div>
			</div>

			<div class="w-full lg:w-auto flex items-center gap-sm shrink-0">
				{#if hasActiveFilters}
					<a
						href="/app/reports"
						class="w-full sm:w-auto h-10 px-4 rounded-button font-medium border border-stone bg-white text-secondary hover:text-primary hover:bg-ash transition-colors inline-flex items-center justify-center text-body-sm shadow-2xs"
					>
						{t('common.reset')}
					</a>
				{/if}
				<button
					class="w-full sm:w-auto h-10 px-5 rounded-button font-medium bg-primary text-white hover:opacity-90 transition-opacity cursor-pointer inline-flex items-center justify-center text-body-sm shadow-2xs whitespace-nowrap"
					type="submit"
				>
					{t('reports.apply')}
				</button>
			</div>
		</div>
	</form>

	<!-- Ticket count metrics row -->
	<div class="grid grid-cols-2 lg:grid-cols-4 gap-md">
		{#each ticketTotals as card}
			<div data-bento class="rounded-xl border border-stone bg-white p-md shadow-2xs hover:border-ink/20 transition-colors">
				<div class="flex items-center justify-between">
					<p class="font-label-caps text-label-caps text-secondary uppercase tracking-wider">{card.label}</p>
					{#if card.tone === 'text-rose-600'}
						<span class="w-2 h-2 rounded-full bg-rose-500"></span>
					{:else if card.tone === 'text-amber-600'}
						<span class="w-2 h-2 rounded-full bg-amber-500"></span>
					{/if}
				</div>
				<p class={`mt-1 font-display text-headline-sm font-bold font-mono tabular-nums ${card.tone}`}>{card.value}</p>
			</div>
		{/each}
	</div>

	<!-- Financial metrics row -->
	<div class="grid grid-cols-1 sm:grid-cols-3 gap-md">
		{#each financialTotals as card}
			<div data-bento class="rounded-xl border border-stone bg-white p-md shadow-2xs hover:border-ink/20 transition-colors">
				<p class="font-label-caps text-label-caps text-secondary uppercase tracking-wider">{card.label}</p>
				<p class={`mt-1 font-display text-headline-sm font-bold font-mono tabular-nums ${card.tone}`}>{card.value}</p>
			</div>
		{/each}
	</div>

	<div class="grid grid-cols-1 gap-lg xl:grid-cols-2">
		<!-- Low Stock By Category & Type -->
		<section data-bento class="rounded-xl border border-stone bg-white shadow-2xs !overflow-visible relative z-20">
			<div class="flex flex-wrap items-center justify-between gap-sm border-b border-stone px-card-padding py-3 bg-ash/30 relative z-10">
				<h3 class="font-subheading text-body font-semibold text-primary">{t('reports.lowStockByCategoryType')}</h3>
				<div class="w-48">
					<Dropdown
						size="sm"
						align="right"
						value={lowStockGroupSort}
						onchange={(val) => { lowStockGroupSort = val; lowStockGroupPage = 1; }}
						options={[
							{ value: 'available-asc', label: t('reports.sort.availableAsc') },
							{ value: 'available-desc', label: t('reports.sort.availableDesc') },
							{ value: 'items-desc', label: t('reports.sort.itemsDesc') },
							{ value: 'category-asc', label: t('reports.sort.categoryAsc') }
						]}
					/>
				</div>
			</div>
			<div class="overflow-x-auto">
				<table class="w-full min-w-[620px] border-collapse text-left">
					<thead class="bg-ash/50 border-b border-stone">
						<tr>
							<th class="px-card-padding py-3 font-label-caps text-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('reports.category')}</th>
							<th class="px-card-padding py-3 font-label-caps text-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('reports.type')}</th>
							<th class="px-card-padding py-3 text-right font-label-caps text-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('reports.items')}</th>
							<th class="px-card-padding py-3 text-right font-label-caps text-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('reports.available')}</th>
							<th class="px-card-padding py-3 text-right font-label-caps text-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('reports.reorder')}</th>
						</tr>
					</thead>
					<tbody class="divide-y divide-stone">
						{#each pageRows(sortedLowStockGroups, lowStockGroupPage) as row}
							<tr class="hover:bg-ash/40 transition-colors">
								<td class="px-card-padding py-3 text-body-sm font-medium text-primary">{row.categoryName}</td>
								<td class="px-card-padding py-3 text-body-sm text-secondary">{row.itemType}</td>
								<td class="px-card-padding py-3 text-right text-body-sm font-mono tabular-nums text-primary">{row.itemCount}</td>
								<td class="px-card-padding py-3 text-right text-body-sm font-semibold font-mono tabular-nums text-primary">{row.availableQuantity}</td>
								<td class="px-card-padding py-3 text-right text-body-sm font-mono tabular-nums text-secondary">{row.reorderLevel}</td>
							</tr>
						{:else}
							{@render emptyTableRow(5)}
						{/each}
					</tbody>
				</table>
			</div>
			{#if sortedLowStockGroups.length > pageSize}
				{@render paginationBar(
					lowStockGroupPage,
					pageCount(sortedLowStockGroups),
					() => (lowStockGroupPage = Math.max(1, lowStockGroupPage - 1)),
					() => (lowStockGroupPage = Math.min(pageCount(sortedLowStockGroups), lowStockGroupPage + 1))
				)}
			{/if}
		</section>

		{#if report.canViewCost}
			<!-- Inventory Valuation -->
			<section data-bento class="rounded-xl border border-stone bg-white shadow-2xs !overflow-visible relative z-10">
				<div class="flex flex-wrap items-center justify-between gap-sm border-b border-stone px-card-padding py-3 bg-ash/30 relative z-10">
					<h3 class="font-subheading text-body font-semibold text-primary">{t('reports.inventoryValuation')}</h3>
					<div class="w-48">
						<Dropdown
							size="sm"
							align="right"
							value={valuationSort}
							onchange={(val) => { valuationSort = val; valuationPage = 1; }}
							options={[
								{ value: 'value-desc', label: t('reports.sort.valueDesc') },
								{ value: 'items-desc', label: t('reports.sort.itemsDesc') },
								{ value: 'category-asc', label: t('reports.sort.categoryAsc') }
							]}
						/>
					</div>
				</div>
				<div class="overflow-x-auto">
					<table class="w-full min-w-[620px] border-collapse text-left">
						<thead class="bg-ash/50 border-b border-stone">
							<tr>
								<th class="px-card-padding py-3 font-label-caps text-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('reports.category')}</th>
								<th class="px-card-padding py-3 font-label-caps text-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('reports.type')}</th>
								<th class="px-card-padding py-3 text-right font-label-caps text-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('reports.items')}</th>
								<th class="px-card-padding py-3 text-right font-label-caps text-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('reports.onHand')}</th>
								<th class="px-card-padding py-3 text-right font-label-caps text-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('reports.value')}</th>
							</tr>
						</thead>
						<tbody class="divide-y divide-stone">
							{#each pageRows(sortedValuationRows, valuationPage) as row}
								<tr class="hover:bg-ash/40 transition-colors">
									<td class="px-card-padding py-3 text-body-sm font-medium text-primary">{row.categoryName}</td>
									<td class="px-card-padding py-3 text-body-sm text-secondary">{row.itemType}</td>
									<td class="px-card-padding py-3 text-right text-body-sm font-mono tabular-nums text-primary">{row.itemCount}</td>
									<td class="px-card-padding py-3 text-right text-body-sm font-mono tabular-nums text-primary">{row.onHandQuantity}</td>
									<td class="px-card-padding py-3 text-right text-body-sm font-semibold font-mono tabular-nums text-primary">{formatMoney(row.inventoryValue)}</td>
								</tr>
							{:else}
								{@render emptyTableRow(5)}
							{/each}
						</tbody>
					</table>
				</div>
				{#if sortedValuationRows.length > pageSize}
					{@render paginationBar(
						valuationPage,
						pageCount(sortedValuationRows),
						() => (valuationPage = Math.max(1, valuationPage - 1)),
						() => (valuationPage = Math.min(pageCount(sortedValuationRows), valuationPage + 1))
					)}
				{/if}
			</section>
		{/if}
	</div>

	<!-- Service Activity -->
	<section data-bento class="rounded-xl border border-stone bg-white shadow-2xs overflow-hidden">
		<div class="border-b border-stone px-card-padding py-3 bg-ash/30">
			<h3 class="font-subheading text-body font-semibold text-primary">{t('reports.serviceActivity')}</h3>
		</div>
		<div class="overflow-x-auto">
			<table class="w-full min-w-[620px] border-collapse text-left">
				<thead class="bg-ash/50 border-b border-stone">
					<tr>
						<th class="px-card-padding py-3 font-label-caps text-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('reports.period')}</th>
						<th class="px-card-padding py-3 text-right font-label-caps text-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('reports.received')}</th>
						<th class="px-card-padding py-3 text-right font-label-caps text-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('reports.completed')}</th>
						<th class="px-card-padding py-3 text-right font-label-caps text-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('reports.cancelled')}</th>
					</tr>
				</thead>
				<tbody class="divide-y divide-stone">
					{#each report.serviceRows as row}
						<tr class="hover:bg-ash/40 transition-colors">
							<td class="px-card-padding py-3 text-body-sm font-mono font-medium text-primary">{row.period}</td>
							<td class="px-card-padding py-3 text-right text-body-sm font-mono tabular-nums text-secondary">{row.received}</td>
							<td class="px-card-padding py-3 text-right text-body-sm font-mono tabular-nums font-medium text-emerald-600">{row.completed}</td>
							<td class="px-card-padding py-3 text-right text-body-sm font-mono tabular-nums text-rose-600">{row.cancelled}</td>
						</tr>
					{:else}
						{@render emptyTableRow(4)}
					{/each}
				</tbody>
			</table>
		</div>
	</section>

	<div class="grid grid-cols-1 gap-lg xl:grid-cols-2">
		<!-- Unpaid and Partial Payments -->
		<section data-bento class="rounded-xl border border-stone bg-white shadow-2xs !overflow-visible relative z-20">
			<div class="flex flex-wrap items-center justify-between gap-sm border-b border-stone px-card-padding py-3 bg-ash/30 relative z-10">
				<h3 class="font-subheading text-body font-semibold text-primary">{t('reports.unpaidPartial')}</h3>
				<div class="w-48">
					<Dropdown
						size="sm"
						align="right"
						value={paymentSort}
						onchange={(val) => { paymentSort = val; paymentPage = 1; }}
						options={[
							{ value: 'balance-desc', label: t('reports.sort.balanceDesc') },
							{ value: 'ticket-asc', label: t('reports.sort.ticketAsc') },
							{ value: 'customer-asc', label: t('reports.sort.customerAsc') }
						]}
					/>
				</div>
			</div>
			<div class="overflow-x-auto">
				<table class="w-full min-w-[680px] border-collapse text-left">
					<thead class="bg-ash/50 border-b border-stone">
						<tr>
							<th class="px-card-padding py-3 font-label-caps text-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('reports.ticket')}</th>
							<th class="px-card-padding py-3 font-label-caps text-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('reports.customer')}</th>
							<th class="px-card-padding py-3 font-label-caps text-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('reports.status')}</th>
							<th class="px-card-padding py-3 text-right font-label-caps text-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('reports.paid')}</th>
							<th class="px-card-padding py-3 text-right font-label-caps text-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('reports.total')}</th>
						</tr>
					</thead>
					<tbody class="divide-y divide-stone">
						{#each pageRows(sortedPaymentRows, paymentPage) as row}
							<tr class="hover:bg-ash/40 transition-colors">
								<td class="px-card-padding py-3 font-mono text-body-sm font-semibold">
									<a class="text-primary hover:underline" href={`/app/tickets/${row.ticketId}`}>{row.ticketNumber}</a>
								</td>
								<td class="px-card-padding py-3 text-body-sm text-secondary">{row.customerName}</td>
								<td class="px-card-padding py-3 whitespace-nowrap">
									{@render paymentStatusBadge(row.paymentStatus)}
								</td>
								<td class="px-card-padding py-3 text-right text-body-sm font-mono tabular-nums text-secondary">{formatMoney(row.amountPaid)}</td>
								<td class="px-card-padding py-3 text-right text-body-sm font-semibold font-mono tabular-nums text-primary">{formatMoney(row.totalAmount)}</td>
							</tr>
						{:else}
							{@render emptyTableRow(5)}
						{/each}
					</tbody>
				</table>
			</div>
			{#if sortedPaymentRows.length > pageSize}
				{@render paginationBar(
					paymentPage,
					pageCount(sortedPaymentRows),
					() => (paymentPage = Math.max(1, paymentPage - 1)),
					() => (paymentPage = Math.min(pageCount(sortedPaymentRows), paymentPage + 1))
				)}
			{/if}
		</section>

		<!-- Low Stock Items -->
		<section data-bento class="rounded-xl border border-stone bg-white shadow-2xs !overflow-visible relative z-10">
			<div class="flex flex-wrap items-center justify-between gap-sm border-b border-stone px-card-padding py-3 bg-ash/30 relative z-10">
				<h3 class="font-subheading text-body font-semibold text-primary">{t('reports.lowStock')}</h3>
				<div class="w-48">
					<Dropdown
						size="sm"
						align="right"
						value={lowStockSort}
						onchange={(val) => { lowStockSort = val; lowStockPage = 1; }}
						options={[
							{ value: 'available-asc', label: t('reports.sort.availableAsc') },
							{ value: 'available-desc', label: t('reports.sort.availableDesc') },
							{ value: 'sku-asc', label: t('reports.sort.skuAsc') },
							{ value: 'item-asc', label: t('reports.sort.itemAsc') }
						]}
					/>
				</div>
			</div>
			<div class="overflow-x-auto">
				<table class="w-full min-w-[620px] border-collapse text-left">
					<thead class="bg-ash/50 border-b border-stone">
						<tr>
							<th class="px-card-padding py-3 font-label-caps text-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('reports.sku')}</th>
							<th class="px-card-padding py-3 font-label-caps text-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('reports.item')}</th>
							<th class="px-card-padding py-3 text-right font-label-caps text-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('reports.available')}</th>
							<th class="px-card-padding py-3 text-right font-label-caps text-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('reports.reorder')}</th>
							{#if report.canViewCost}
								<th class="px-card-padding py-3 text-right font-label-caps text-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('reports.cost')}</th>
							{/if}
						</tr>
					</thead>
					<tbody class="divide-y divide-stone">
						{#each pageRows(sortedLowStockRows, lowStockPage) as row}
							<tr class="hover:bg-ash/40 transition-colors">
								<td class="px-card-padding py-3 font-mono text-body-sm font-semibold">
									<a class="text-primary hover:underline" href={`/app/inventory/${row.itemId}`}>{row.sku}</a>
								</td>
								<td class="px-card-padding py-3 text-body-sm">
									<a class="text-secondary hover:text-primary transition-colors hover:underline" href={`/app/inventory/${row.itemId}`}>{row.name}</a>
								</td>
								<td class="px-card-padding py-3 text-right text-body-sm font-semibold font-mono tabular-nums text-rose-600">{row.availableQuantity}</td>
								<td class="px-card-padding py-3 text-right text-body-sm font-mono tabular-nums text-secondary">{row.reorderLevel}</td>
								{#if report.canViewCost}
									<td class="px-card-padding py-3 text-right text-body-sm font-mono tabular-nums text-primary">{formatMoney(row.costPrice)}</td>
								{/if}
							</tr>
						{:else}
							{@render emptyTableRow(report.canViewCost ? 5 : 4)}
						{/each}
					</tbody>
				</table>
			</div>
			{#if sortedLowStockRows.length > pageSize}
				{@render paginationBar(
					lowStockPage,
					pageCount(sortedLowStockRows),
					() => (lowStockPage = Math.max(1, lowStockPage - 1)),
					() => (lowStockPage = Math.min(pageCount(sortedLowStockRows), lowStockPage + 1))
				)}
			{/if}
		</section>
	</div>

	<div class="grid grid-cols-1 gap-lg xl:grid-cols-3">
		<!-- Inventory Movement -->
		<section data-bento class="rounded-xl border border-stone bg-white shadow-2xs xl:col-span-2 !overflow-visible relative z-20">
			<div class="flex flex-wrap items-center justify-between gap-sm border-b border-stone px-card-padding py-3 bg-ash/30 relative z-10">
				<h3 class="font-subheading text-body font-semibold text-primary">{t('reports.inventoryMovement')}</h3>
				<div class="w-48">
					<Dropdown
						size="sm"
						align="right"
						value={movementSort}
						onchange={(val) => { movementSort = val; movementPage = 1; }}
						options={[
							{ value: 'period-desc', label: t('reports.sort.periodDesc') },
							{ value: 'period-asc', label: t('reports.sort.periodAsc') },
							{ value: 'quantity-desc', label: t('reports.sort.quantityDesc') },
							{ value: 'type-asc', label: t('reports.sort.typeAsc') },
							...(report.canViewCost ? [{ value: 'cost-desc', label: t('reports.sort.costDesc') }] : [])
						]}
					/>
				</div>
			</div>
			<div class="overflow-x-auto">
				<table class="w-full min-w-[620px] border-collapse text-left">
					<thead class="bg-ash/50 border-b border-stone">
						<tr>
							<th class="px-card-padding py-3 font-label-caps text-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('reports.period')}</th>
							<th class="px-card-padding py-3 font-label-caps text-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('reports.type')}</th>
							<th class="px-card-padding py-3 text-right font-label-caps text-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('reports.quantity')}</th>
							{#if report.canViewCost}
								<th class="px-card-padding py-3 text-right font-label-caps text-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('reports.cost')}</th>
							{/if}
						</tr>
					</thead>
					<tbody class="divide-y divide-stone">
						{#each pageRows(sortedMovementRows, movementPage) as row}
							<tr class="hover:bg-ash/40 transition-colors">
								<td class="px-card-padding py-3 text-body-sm font-mono text-primary">{row.period}</td>
								<td class="px-card-padding py-3 text-body-sm text-secondary">{row.type}</td>
								<td class="px-card-padding py-3 text-right text-body-sm font-semibold font-mono tabular-nums text-primary">{row.quantity}</td>
								{#if report.canViewCost}
									<td class="px-card-padding py-3 text-right text-body-sm font-mono tabular-nums text-primary">{formatMoney(row.cost)}</td>
								{/if}
							</tr>
						{:else}
							{@render emptyTableRow(report.canViewCost ? 4 : 3)}
						{/each}
					</tbody>
				</table>
			</div>
			{#if sortedMovementRows.length > pageSize}
				{@render paginationBar(
					movementPage,
					pageCount(sortedMovementRows),
					() => (movementPage = Math.max(1, movementPage - 1)),
					() => (movementPage = Math.min(pageCount(sortedMovementRows), movementPage + 1))
				)}
			{/if}
		</section>

		<!-- Aging & Approvals Column -->
		<section class="space-y-lg">
			<!-- Ticket Aging -->
			<div data-bento class="rounded-xl border border-stone bg-white shadow-2xs overflow-hidden">
				<div class="border-b border-stone px-card-padding py-3 bg-ash/30">
					<h3 class="font-subheading text-body font-semibold text-primary">{t('reports.ticketAging')}</h3>
				</div>
				<div class="overflow-x-auto">
					<table class="w-full border-collapse text-left">
						<tbody class="divide-y divide-stone">
							{#each report.agingRows as row}
								<tr class="hover:bg-ash/40 transition-colors">
									<td class="px-card-padding py-3 text-body-sm font-medium text-primary">{row.bucket}</td>
									<td class="px-card-padding py-3 text-right text-body-sm font-semibold font-mono tabular-nums text-primary">{row.count}</td>
								</tr>
							{:else}
								{@render emptyTableRow(2)}
							{/each}
						</tbody>
					</table>
				</div>
			</div>

			<!-- Approvals -->
			<div data-bento class="rounded-xl border border-stone bg-white shadow-2xs overflow-hidden">
				<div class="border-b border-stone px-card-padding py-3 bg-ash/30">
					<h3 class="font-subheading text-body font-semibold text-primary">{t('reports.approvals')}</h3>
				</div>
				<div class="overflow-x-auto">
					<table class="w-full border-collapse text-left">
						<tbody class="divide-y divide-stone">
							{#each report.approvalRows as row}
								<tr class="hover:bg-ash/40 transition-colors">
									<td class="px-card-padding py-3 text-body-sm font-medium text-primary">{row.status}</td>
									<td class="px-card-padding py-3 text-right text-body-sm font-mono tabular-nums text-secondary">{row.count}</td>
									<td class="px-card-padding py-3 text-right text-body-sm font-semibold font-mono tabular-nums text-primary">{formatMoney(row.estimatedAmount)}</td>
								</tr>
							{:else}
								{@render emptyTableRow(3)}
							{/each}
						</tbody>
					</table>
				</div>
			</div>
		</section>
	</div>
</div>
