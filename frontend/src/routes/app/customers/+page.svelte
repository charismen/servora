<script lang="ts">
	import { goto } from '$app/navigation';
	import PageHeader from '$lib/components/PageHeader.svelte';
	import { createTranslator, formatDate, resolveTerminology } from '$lib/i18n';
	import type { PageData } from './$types';
	import DatePicker from '$lib/components/ui/date-picker/DatePicker.svelte';
	import Dropdown from '$lib/components/ui/dropdown/Dropdown.svelte';
	import Pagination from '$lib/components/ui/pagination/Pagination.svelte';
	import StatusBadge from '$lib/components/ui/badge/StatusBadge.svelte';

	let { data }: { data: PageData } = $props();
	const t = $derived(createTranslator(data.locale));
	const terms = $derived(resolveTerminology(data.user?.workspace?.terminology, data.locale));
	let customers = $derived(data.customers);
	const pageSizeOptions = [10, 20, 50, 100];

	// Filter States
	let searchVal = $state('');
	let statusFilter = $state<'all' | 'active' | 'banned' | 'star'>('all');
	let startDate = $state('');
	let endDate = $state('');
	let copiedPhoneId = $state<number | null>(null);

	$effect(() => {
		searchVal = data.search;
		statusFilter = data.status ?? 'all';
		startDate = data.startDate ?? '';
		endDate = data.endDate ?? '';
	});

	const hasActiveFilters = $derived(
		Boolean(searchVal || (statusFilter && statusFilter !== 'all') || startDate || endDate || (data.sort && data.sort !== 'default'))
	);

	function buildQuery(overrides: Partial<{ search: string; status: string; startDate: string; endDate: string; sort: string; pageSize: number; page: number }> = {}) {
		const params = new URLSearchParams();
		const s = overrides.search ?? searchVal;
		const st = overrides.status ?? statusFilter;
		const sd = overrides.startDate ?? startDate;
		const ed = overrides.endDate ?? endDate;
		const so = overrides.sort ?? data.sort;
		const ps = overrides.pageSize ?? data.pageSize;
		const pg = overrides.page ?? 1;

		if (s) params.set('search', s);
		if (st && st !== 'all') params.set('status', st);
		if (sd) params.set('startDate', sd);
		if (ed) params.set('endDate', ed);
		if (so && so !== 'default') params.set('sort', so);
		params.set('pageSize', String(ps));
		params.set('page', String(pg));
		return `?${params.toString()}`;
	}

	function applyServerFilters(overrides: Partial<{ status: 'all' | 'active' | 'banned' | 'star'; search?: string; startDate?: string; endDate?: string }> = {}) {
		if (overrides.status !== undefined) statusFilter = overrides.status;
		void goto(buildQuery({ ...overrides, page: 1 }), { keepFocus: true, noScroll: true });
	}

	function resetFilters() {
		searchVal = '';
		statusFilter = 'all';
		startDate = '';
		endDate = '';
		void goto(`?pageSize=${data.pageSize}&page=1`, { keepFocus: true, noScroll: true });
	}

	function changeSort(sortValue: string) {
		void goto(buildQuery({ sort: sortValue, page: 1 }), { noScroll: true });
	}

	function changePageSize(size: number) {
		void goto(buildQuery({ pageSize: size, page: 1 }), { noScroll: true });
	}

	function getWhatsAppUrl(phone: string): string {
		let clean = phone.replace(/\D/g, '');
		if (clean.startsWith('0')) {
			clean = '62' + clean.slice(1);
		}
		return `https://wa.me/${clean}`;
	}

	function copyPhoneNumber(id: number, phone: string) {
		if (navigator?.clipboard) {
			void navigator.clipboard.writeText(phone);
			copiedPhoneId = id;
			setTimeout(() => {
				if (copiedPhoneId === id) copiedPhoneId = null;
			}, 2000);
		}
	}

	const statusOptions = $derived([
		{ value: 'all', label: t('customers.list.status.all') },
		{ value: 'active', label: t('customers.list.status.active') },
		{ value: 'star', label: t('customers.list.status.star') },
		{ value: 'banned', label: t('customers.list.status.banned') }
	]);

	const sortOptions = $derived([
		{ value: 'default', label: t('customers.list.sort.default') },
		{ value: 'name-asc', label: t('customers.list.sort.nameAsc') },
		{ value: 'name-desc', label: t('customers.list.sort.nameDesc') },
		{ value: 'oldest', label: t('customers.list.sort.oldest') },
		{ value: 'tickets-desc', label: t('customers.list.sort.ticketsDesc') }
	]);
</script>

<div class="space-y-lg">
	<PageHeader
		items={[
			{ label: t('common.console'), href: '/app' },
			{ label: terms.requesterPlural }
		]}
		title={terms.requesterPlural}
		subtitle={t('customers.list.subtitle')}
	>
		{#snippet actions()}
			<a
				href="/app/customers/new"
				class="inline-flex h-9 items-center gap-1.5 rounded-button bg-ink px-3.5 text-body-sm font-medium text-white shadow-xs hover:bg-ink/90 active:scale-95 transition-all cursor-pointer shrink-0"
			>
				<span class="material-symbols-outlined text-[18px]">person_add</span>
				<span>+ {terms.requesterSingular}</span>
			</a>
		{/snippet}
	</PageHeader>

	<!-- Standardized Filters Bar -->
	<form
		data-bento
		class="bg-surface-container-lowest border border-stone rounded-xl p-md shadow-2xs !overflow-visible relative z-30"
		method="GET"
	>
		<input type="hidden" name="page" value="1" />
		<div class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 xl:grid-cols-[1fr_160px_270px_160px_100px_auto] gap-md items-end">
			<!-- Search -->
			<label class="space-y-1 sm:col-span-2 md:col-span-1">
				<span class="block font-label-caps text-secondary uppercase text-[11px] font-bold tracking-wider">{t('customers.list.search')}</span>
				<div class="relative">
					<span class="material-symbols-outlined absolute left-3 top-1/2 -translate-y-1/2 text-secondary text-[18px]">search</span>
					<input
						name="search"
						value={data.search ?? ''}
						class="w-full pl-9 pr-3 py-2 border border-stone rounded-input font-body-sm text-body-sm focus:border-ink focus:ring-1 focus:ring-ink/10 focus:outline-none bg-white text-ink transition-colors"
						placeholder={t('customers.list.searchPlaceholder')}
						type="search"
					/>
				</div>
			</label>

			<!-- Status Filter -->
			<div class="space-y-1">
				<span class="block font-label-caps text-secondary uppercase text-[11px] font-bold tracking-wider">{t('customers.list.statusFilter')}</span>
				<Dropdown
					name="status"
					value={data.status ?? 'all'}
					options={statusOptions}
				/>
			</div>

			<!-- Date Joined Range -->
			<div class="space-y-1">
				<span class="block font-label-caps text-secondary uppercase text-[11px] font-bold tracking-wider">{t('customers.list.dateJoinedRange')}</span>
				<div class="grid grid-cols-2 gap-1.5 items-center">
					<DatePicker bind:value={startDate} name="startDate" placeholder={t('reports.from')} clearable />
					<DatePicker bind:value={endDate} name="endDate" placeholder={t('reports.to')} clearable />
				</div>
			</div>

			<!-- Sort -->
			<div class="space-y-1">
				<span class="block font-label-caps text-secondary uppercase text-[11px] font-bold tracking-wider">{t('customers.list.sort')}</span>
				<Dropdown
					name="sort"
					value={data.sort ?? 'default'}
					options={sortOptions}
				/>
			</div>

			<!-- Page Size -->
			<div class="space-y-1">
				<span class="block font-label-caps text-secondary uppercase text-[11px] font-bold tracking-wider">{t('customers.list.pageSize')}</span>
				<Dropdown
					name="pageSize"
					align="right"
					value={String(data.pageSize)}
					options={pageSizeOptions.map((s) => ({ value: String(s), label: String(s) }))}
				/>
			</div>

			<!-- Actions -->
			<div class="flex items-center gap-2">
				<button
					class="w-full sm:w-auto h-10 px-5 rounded-button bg-ink text-white font-medium text-body-sm hover:bg-ink/90 active:scale-95 transition-all cursor-pointer flex items-center justify-center gap-1.5 shrink-0 shadow-xs"
					type="submit"
				>
					<span class="material-symbols-outlined text-[18px]">filter_alt</span>
					<span>{t('reports.apply')}</span>
				</button>
				{#if hasActiveFilters}
					<a
						href="?page=1&pageSize={data.pageSize}"
						class="h-10 px-3 rounded-button border border-stone bg-white text-secondary hover:text-ink hover:bg-ash font-medium text-body-sm transition-all flex items-center justify-center gap-1 shrink-0 shadow-2xs"
						title={t('customers.list.clearFilters')}
					>
						<span class="material-symbols-outlined text-[18px]">restart_alt</span>
						<span class="hidden sm:inline">{t('customers.list.clearFilters')}</span>
					</a>
				{/if}
			</div>
		</div>
	</form>

	<!-- Customers Table Section -->
	<div class="rounded-xl border border-stone bg-white shadow-2xs overflow-hidden">
		{#if customers.items.length === 0}
			<div class="px-md py-16 text-center">
				<div class="max-w-md mx-auto flex flex-col items-center justify-center text-center">
					<div class="w-12 h-12 rounded-xl bg-ash border border-stone flex items-center justify-center text-secondary mb-3 shadow-2xs">
						<span class="material-symbols-outlined text-[24px]">
							{hasActiveFilters ? 'search_off' : 'group_off'}
						</span>
					</div>
					<h4 class="font-bold text-ink text-body-base mb-1">
						{hasActiveFilters ? t('customers.list.emptySearch') : t('customers.list.empty')}
					</h4>
					<p class="text-body-sm text-secondary mb-4 leading-relaxed">
						{hasActiveFilters ? t('customers.list.empty') : t('customers.list.subtitle')}
					</p>
					{#if hasActiveFilters}
						<button
							type="button"
							onclick={resetFilters}
							class="px-button-x py-button-y rounded-button font-medium font-body-sm text-body-sm inline-flex items-center gap-1.5 border border-stone bg-white text-ink hover:bg-ash transition-all shadow-2xs cursor-pointer"
						>
							<span class="material-symbols-outlined text-[18px]">restart_alt</span>
							<span>{t('customers.list.clearFilters')}</span>
						</button>
					{:else}
						<a
							href="/app/customers/new"
							class="px-button-x py-button-y rounded-button font-medium font-body-sm text-body-sm inline-flex items-center gap-1.5 bg-ink text-white hover:bg-ink/90 transition-all shadow-xs"
						>
							<span class="material-symbols-outlined text-[18px]">person_add</span>
							<span>{t('customers.list.newCustomer')}</span>
						</a>
					{/if}
				</div>
			</div>
		{:else}
			<div class="overflow-x-auto">
				<table class="w-full text-left border-collapse">
					<thead>
						<tr class="border-b border-stone bg-surface-container-low text-[11px] font-bold uppercase tracking-wider text-secondary">
							<th class="px-md py-3.5 font-label-caps">{terms.requesterSingular}</th>
							<th class="px-md py-3.5 font-label-caps">{t('customers.list.phoneNumber')}</th>
							<th class="px-md py-3.5 font-label-caps hidden md:table-cell">{t('customers.list.emailAddress')}</th>
							<th class="px-md py-3.5 font-label-caps hidden lg:table-cell">{t('customers.list.totalTickets')}</th>
							<th class="px-md py-3.5 font-label-caps">{t('customers.list.status')}</th>
							<th class="px-md py-3.5 font-label-caps hidden md:table-cell">{t('customers.list.lastService')}</th>
							<th class="px-md py-3.5 font-label-caps text-right">{t('customers.list.actions')}</th>
						</tr>
					</thead>
					<tbody class="divide-y divide-stone font-body-sm text-body-sm">
						{#each customers.items as customer (customer.id)}
							<tr class="align-middle hover:bg-ash/40 transition-colors">
								<!-- Customer Name & ID -->
								<td class="px-md py-3.5">
									<div class="flex items-center gap-3">
										<div class="w-9 h-9 rounded-full border border-stone bg-ash flex items-center justify-center font-bold text-xs text-ink shrink-0">
											{(customer.fullName || 'C').slice(0, 2).toUpperCase()}
										</div>
										<div class="min-w-0">
											<a
												href="/app/customers/{customer.id}"
												class="font-semibold text-ink leading-snug hover:underline block truncate {customer.isBanned ? 'opacity-60' : ''}"
											>
												{customer.fullName}
											</a>
											<div class="text-[11px] text-secondary font-mono tracking-tight">
												ID: C-{customer.id}
											</div>
										</div>
									</div>
								</td>

								<!-- Phone Number & WhatsApp Shortcut -->
								<td class="px-md py-3.5 whitespace-nowrap">
									<div class="flex items-center gap-2">
										<span class="font-mono text-ink text-xs {customer.isBanned ? 'opacity-60' : ''}">
											{customer.phoneNumber}
										</span>
										<button
											type="button"
											onclick={() => copyPhoneNumber(customer.id, customer.phoneNumber)}
											class="text-secondary hover:text-ink transition-colors cursor-pointer p-0.5"
											title={copiedPhoneId === customer.id ? t('customers.list.phoneCopied') : t('customers.list.copyPhone')}
										>
											<span class="material-symbols-outlined text-[15px]">
												{copiedPhoneId === customer.id ? 'check' : 'content_copy'}
											</span>
										</button>
										<a
											href={getWhatsAppUrl(customer.phoneNumber)}
											target="_blank"
											rel="noopener noreferrer"
											class="text-emerald-700 hover:text-emerald-800 transition-colors inline-flex items-center p-0.5"
											title={t('customers.list.openWhatsApp')}
										>
											<span class="material-symbols-outlined text-[15px]">chat</span>
										</a>
									</div>
								</td>

								<!-- Email Address -->
								<td class="px-md py-3.5 text-secondary hidden md:table-cell text-xs font-mono {customer.isBanned ? 'opacity-60' : ''}">
									{customer.email || '—'}
								</td>

								<!-- Total Tickets Badge -->
								<td class="px-md py-3.5 hidden lg:table-cell whitespace-nowrap">
									{#if customer.totalTickets > 0}
										<span class="rounded-full border border-stone/80 bg-white px-2.5 py-0.5 text-[11px] font-semibold text-ink shadow-2xs">
											{customer.totalTickets} {customer.totalTickets === 1 ? t('customers.list.ticketSingular') : t('customers.list.ticketPlural')}
										</span>
									{:else}
										<span class="text-xs text-secondary/60 italic">—</span>
									{/if}
								</td>

								<!-- Status Badge -->
								<td class="px-md py-3.5 whitespace-nowrap">
									{#if customer.isBanned}
										<StatusBadge
											variant="error"
											label={t('customers.list.status.banned')}
										/>
									{:else if customer.isStarred}
										<StatusBadge
											variant="warning"
											label={t('customers.list.status.starred')}
										/>
									{:else}
										<StatusBadge
											variant="success"
											label={t('customers.list.status.active')}
										/>
									{/if}
								</td>

								<!-- Date Joined -->
								<td class="px-md py-3.5 font-mono text-xs text-secondary hidden md:table-cell whitespace-nowrap {customer.isBanned ? 'opacity-60' : ''}">
									{formatDate(customer.createdAt, data.locale)}
								</td>

								<!-- Actions -->
								<td class="px-md py-3.5 text-right whitespace-nowrap">
									<div class="inline-flex items-center gap-1.5 justify-end">
										<a
											href="/app/customers/{customer.id}"
											class="inline-flex h-8 items-center gap-1 rounded-button border border-stone bg-white px-2.5 text-xs font-semibold text-ink hover:bg-ash transition-colors shadow-2xs"
										>
											<span class="material-symbols-outlined text-[15px]">visibility</span>
											<span>{t('customers.list.viewProfile')}</span>
										</a>
										<a
											href="/app/customers/{customer.id}/edit"
											class="inline-flex h-8 items-center gap-1 rounded-button border border-stone bg-white px-2.5 text-xs font-semibold text-secondary hover:text-ink hover:bg-ash transition-colors shadow-2xs"
										>
											<span class="material-symbols-outlined text-[15px]">edit</span>
											<span>{t('customers.list.editProfile')}</span>
										</a>
									</div>
								</td>
							</tr>
						{/each}
					</tbody>
				</table>
			</div>

			<!-- Table Pagination -->
			{#if customers.totalCount > 0}
				<Pagination
					page={customers.page}
					pageSize={customers.pageSize}
					totalCount={customers.totalCount}
					hrefBuilder={(p) => buildQuery({ page: p })}
					showingLabel={t('common.pagination.showing')}
					toLabel={t('common.pagination.to')}
					ofLabel={t('common.pagination.of')}
					itemsLabel={t('common.pagination.items')}
					previousLabel={t('common.previous')}
					nextLabel={t('common.next')}
				/>
			{/if}
		{/if}
	</div>
</div>
