<script lang="ts">
	import PageHeader from '$lib/components/PageHeader.svelte';
	import DeleteConfirmButton from '$lib/components/DeleteConfirmButton.svelte';
	import GlowingBadge from '$lib/components/ui/glowing-badge/GlowingBadge.svelte';
	import Dropdown from '$lib/components/ui/dropdown/Dropdown.svelte';
	import { can, permissions } from '$lib/auth/permissions';
	import type { ActionData, PageData } from './$types';
	import { createTranslator, formatDate } from '$lib/i18n';

	let { data, form }: { data: PageData; form: ActionData } = $props();
	const t = $derived(createTranslator(data.locale));
	const canDeleteTicket = $derived(can(data.user.permissions, permissions.serviceTicketsDelete));

	const priorities = ['Low', 'Normal', 'High', 'Urgent'];
	const pageSizeOptions = [10, 20, 50, 100];

	const hasActiveFilters = $derived(
		Boolean(data.filters.search || data.filters.status || data.filters.priority || (data.filters.sort && data.filters.sort !== 'newest'))
	);

	function priorityClass(priority: string) {
		switch (priority?.toLowerCase()) {
			case 'urgent':
				return 'bg-red-500/10 text-red-700 border-red-200/80';
			case 'high':
				return 'bg-amber-500/10 text-amber-800 border-amber-200/80';
			case 'normal':
				return 'bg-ash text-ink/80 border-stone/80';
			case 'low':
			default:
				return 'bg-ash/60 text-secondary border-stone/50';
		}
	}

	function pageHref(page: number) {
		const params = new URLSearchParams();
		params.set('page', String(page));
		params.set('pageSize', String(data.tickets.pageSize));
		for (const key of ['search', 'status', 'priority', 'sort'] as const) {
			const value = data.filters[key];
			if (value && value !== 'newest') params.set(key, value);
		}
		if (data.filters.myTickets) params.set('myTickets', 'true');
		return `?${params.toString()}`;
	}
</script>

<div class="space-y-lg pb-xl">
	<PageHeader items={[{ label: t('common.console'), href: '/app' }, { label: t('nav.tickets') }]} title={t('tickets.list.title')} subtitle={t('tickets.list.subtitle')}>
		{#snippet actions()}
		{#if can(data.user.permissions, permissions.serviceTicketsWrite)}
			<a class="px-button-x py-button-y rounded-button font-medium font-body-sm text-body-sm inline-flex items-center gap-2 bg-ink text-white hover:bg-ink/90 active:scale-95 transition-all shadow-xs shrink-0" href="/app/tickets/new">
				<span class="material-symbols-outlined text-[18px]">add</span>
				<span>{t('tickets.list.newTicket')}</span>
			</a>
		{/if}
		{/snippet}
	</PageHeader>

	<!-- View & Queue Bar -->
	<div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
		<!-- Queue Switcher Tabs -->
		<div class="inline-flex rounded-input border border-stone/80 bg-ash/40 p-1 text-caption font-medium shadow-2xs">
			<a
				href="?myTickets=false"
				class="inline-flex items-center gap-1.5 px-3.5 py-1.5 rounded-button text-body-sm font-medium transition-all shrink-0 {!data.filters.myTickets ? 'bg-white text-ink font-semibold shadow-xs' : 'text-secondary hover:text-ink'}"
			>
				<span class="material-symbols-outlined text-[18px]">list_alt</span>
				<span>{t('tickets.list.allTickets')}</span>
			</a>
			<a
				href="?myTickets=true"
				class="inline-flex items-center gap-1.5 px-3.5 py-1.5 rounded-button text-body-sm font-medium transition-all shrink-0 {data.filters.myTickets ? 'bg-white text-ink font-semibold shadow-xs' : 'text-secondary hover:text-ink'}"
			>
				<span class="material-symbols-outlined text-[18px]">engineering</span>
				<span>{t('tickets.list.myTickets')}</span>
			</a>
		</div>

		<!-- View Switcher (List vs Kanban) -->
		<div class="inline-flex rounded-input border border-stone/80 bg-ash/40 p-1 text-caption font-medium shadow-2xs self-start sm:self-auto">
			<span
				class="inline-flex items-center gap-1.5 px-3.5 py-1.5 rounded-button text-body-sm font-semibold transition-all shrink-0 bg-white text-ink shadow-xs"
			>
				<span class="material-symbols-outlined text-[18px]">table_rows</span>
				<span>{t('tickets.list.viewList')}</span>
			</span>
			<a
				href="/app/kanban{data.filters.myTickets ? '?myTickets=true' : ''}"
				class="inline-flex items-center gap-1.5 px-3.5 py-1.5 rounded-button text-body-sm font-medium transition-all shrink-0 text-secondary hover:text-ink"
			>
				<span class="material-symbols-outlined text-[18px]">view_kanban</span>
				<span>{t('tickets.list.viewKanban')}</span>
			</a>
		</div>
	</div>

	<!-- Filters Bar -->
	<form data-bento class="bg-surface-container-lowest border border-stone rounded-xl p-md shadow-2xs !overflow-visible relative z-30" method="GET">
		<input type="hidden" name="page" value="1" />
		{#if data.filters.myTickets}
			<input type="hidden" name="myTickets" value="true" />
		{/if}
		<div class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 xl:grid-cols-[1fr_160px_160px_150px_110px_auto] gap-md items-end">
			<label class="space-y-1 sm:col-span-2 md:col-span-1">
				<span class="block font-label-caps text-label-caps text-secondary uppercase text-[11px] font-bold tracking-wider">{t('tickets.list.search')}</span>
				<div class="relative">
					<span class="material-symbols-outlined absolute left-3 top-1/2 -translate-y-1/2 text-secondary text-[18px]">search</span>
					<input name="search" value={data.filters.search} class="w-full pl-9 pr-3 py-2 border border-stone rounded-input font-body-sm text-body-sm focus:border-ink focus:ring-1 focus:ring-ink/10 focus:outline-none bg-white text-ink transition-colors" placeholder={t('tickets.list.searchPlaceholder')} type="text" />
				</div>
			</label>
			<div class="space-y-1">
				<span class="block font-label-caps text-label-caps text-secondary uppercase text-[11px] font-bold tracking-wider">{t('tickets.detail.status')}</span>
				<Dropdown
					name="status"
					value={data.filters.status ?? ''}
					options={[{ value: '', label: t('tickets.list.allStatuses') }, ...data.statuses.map(status => ({ value: status.name, label: status.name }))]}
				/>
			</div>
			<div class="space-y-1">
				<span class="block font-label-caps text-label-caps text-secondary uppercase text-[11px] font-bold tracking-wider">{t('tickets.detail.priority')}</span>
				<Dropdown
					name="priority"
					value={data.filters.priority ?? ''}
					options={[{ value: '', label: t('tickets.list.allPriorities') }, ...priorities.map(p => ({ value: p, label: t(`tickets.priority.${p}`) || p }))]}
				/>
			</div>
			<div class="space-y-1">
				<span class="block font-label-caps text-label-caps text-secondary uppercase text-[11px] font-bold tracking-wider">{t('tickets.list.sort')}</span>
				<Dropdown
					name="sort"
					value={data.filters.sort ?? 'newest'}
					options={[
						{ value: 'newest', label: t('tickets.list.sort.newest') },
						{ value: 'oldest', label: t('tickets.list.sort.oldest') },
						{ value: 'ticket-asc', label: t('tickets.list.sort.ticketAsc') },
						{ value: 'priority-desc', label: t('tickets.list.sort.priorityDesc') }
					]}
				/>
			</div>
			<div class="space-y-1">
				<span class="block font-label-caps text-label-caps text-secondary uppercase text-[11px] font-bold tracking-wider">{t('tickets.list.pageSize')}</span>
				<Dropdown
					name="pageSize"
					value={String(data.tickets.pageSize)}
					options={pageSizeOptions.map(size => ({ value: String(size), label: String(size) }))}
					align="right"
				/>
			</div>
			<div class="flex items-center gap-2">
				<button class="w-full sm:w-auto h-10 px-5 rounded-button bg-ink text-white font-medium text-body-sm hover:bg-ink/90 active:scale-95 transition-all cursor-pointer flex items-center justify-center gap-1.5 shrink-0 shadow-xs" type="submit">
					<span class="material-symbols-outlined text-[18px]">filter_alt</span>
					<span>{t('reports.apply')}</span>
				</button>
				{#if hasActiveFilters}
					<a
						href="?page=1{data.filters.myTickets ? '&myTickets=true' : ''}"
						class="h-10 px-3 rounded-button border border-stone bg-white text-secondary hover:text-ink hover:bg-ash font-medium text-body-sm transition-all flex items-center justify-center gap-1 shrink-0 shadow-2xs"
						title={t('tickets.list.clearFilters')}
					>
						<span class="material-symbols-outlined text-[18px]">restart_alt</span>
						<span class="hidden sm:inline">{t('tickets.list.clearFilters')}</span>
					</a>
				{/if}
			</div>
		</div>
	</form>

	<!-- Tickets Table -->
	<div data-bento class="bg-surface-container-lowest border border-stone rounded-xl overflow-hidden shadow-2xs">
		<div class="overflow-x-auto custom-scrollbar">
			<table class="w-full text-left border-collapse">
				<thead>
					<tr class="bg-surface-container-low border-b border-stone">
						<th class="px-md py-4 font-label-caps text-label-caps text-secondary uppercase tracking-wider text-[11px] font-bold">{t('tickets.list.ticketNumber')}</th>
						<th class="px-md py-4 font-label-caps text-label-caps text-secondary uppercase tracking-wider text-[11px] font-bold">{t('tickets.detail.customer')}</th>
						<th class="px-md py-4 font-label-caps text-label-caps text-secondary uppercase tracking-wider text-[11px] font-bold hidden md:table-cell">{t('tickets.detail.device')}</th>
						<th class="px-md py-4 font-label-caps text-label-caps text-secondary uppercase tracking-wider text-[11px] font-bold">{t('tickets.detail.status')}</th>
						<th class="px-md py-4 font-label-caps text-label-caps text-secondary uppercase tracking-wider text-[11px] font-bold hidden sm:table-cell">{t('tickets.detail.priority')}</th>
						<th class="px-md py-4 font-label-caps text-label-caps text-secondary uppercase tracking-wider text-[11px] font-bold hidden lg:table-cell">{t('tickets.detail.assignedTechnician')}</th>
						<th class="px-md py-4 font-label-caps text-label-caps text-secondary uppercase tracking-wider text-[11px] font-bold text-right hidden lg:table-cell">{t('tickets.list.created')}</th>
						<th class="px-md py-4 text-right font-label-caps text-label-caps text-secondary uppercase tracking-wider text-[11px] font-bold">{t('tickets.list.actions')}</th>
					</tr>
				</thead>
				<tbody class="divide-y divide-stone">
					{#if data.tickets.items.length === 0}
						<tr>
							<td class="px-md py-16 text-center" colspan="8">
								<div class="max-w-md mx-auto flex flex-col items-center justify-center text-center">
									<div class="w-12 h-12 rounded-xl bg-ash border border-stone flex items-center justify-center text-secondary mb-3 shadow-2xs">
										<span class="material-symbols-outlined text-[24px]">inbox</span>
									</div>
									<h4 class="font-bold text-ink text-body-base mb-1">{t('tickets.list.empty')}</h4>
									<p class="text-body-sm text-secondary mb-4">{t('tickets.list.noTicketsCopy')}</p>
									<div class="flex items-center gap-2">
										{#if hasActiveFilters}
											<a
												href="?page=1{data.filters.myTickets ? '&myTickets=true' : ''}"
												class="px-button-x py-button-y rounded-button font-medium font-body-sm text-body-sm inline-flex items-center gap-1.5 border border-stone bg-white text-ink hover:bg-ash transition-all shadow-2xs"
											>
												<span class="material-symbols-outlined text-[18px]">restart_alt</span>
												<span>{t('tickets.list.clearFilters')}</span>
											</a>
										{/if}
										{#if can(data.user.permissions, permissions.serviceTicketsWrite)}
											<a
												class="px-button-x py-button-y rounded-button font-medium font-body-sm text-body-sm inline-flex items-center gap-1.5 bg-ink text-white hover:bg-ink/90 active:scale-95 transition-all shadow-xs"
												href="/app/tickets/new"
											>
												<span class="material-symbols-outlined text-[18px]">add</span>
												<span>{t('tickets.list.newTicket')}</span>
											</a>
										{/if}
									</div>
								</div>
							</td>
						</tr>
					{:else}
						{#each data.tickets.items as ticket}
							<tr class="group hover:bg-ash/50 transition-colors">
								<td class="px-md py-4">
									<a class="font-mono font-bold text-ink text-body-sm hover:underline hover:text-primary transition-colors inline-flex items-center gap-1.5" href="/app/tickets/{ticket.id}">
										<span class="material-symbols-outlined text-[16px] text-secondary">confirmation_number</span>
										<span>{ticket.ticketNumber}</span>
									</a>
								</td>
								<td class="px-md py-4">
									<div class="font-body-sm text-body-sm font-semibold text-primary">{ticket.customerName}</div>
									{#if ticket.customerPhone}
										<div class="text-[12px] font-mono text-secondary tracking-tight">{ticket.customerPhone}</div>
									{/if}
								</td>
								<td class="px-md py-4 font-body-sm text-body-sm font-medium text-primary hidden md:table-cell">{ticket.device}</td>
								<td class="px-md py-4">
									<GlowingBadge status={ticket.status}>{ticket.status}</GlowingBadge>
								</td>
								<td class="px-md py-4 hidden sm:table-cell">
									<span class="inline-flex items-center px-2 py-0.5 rounded border text-[11px] font-bold uppercase tracking-wider {priorityClass(ticket.priority)}">
										{t(`tickets.priority.${ticket.priority}`) || ticket.priority}
									</span>
								</td>
								<td class="px-md py-4 hidden lg:table-cell">
									{#if ticket.assignedTechnician}
										<div class="flex items-center gap-1.5 text-body-sm text-secondary">
											<div class="h-6 w-6 rounded-full border border-stone bg-ash flex items-center justify-center text-[10px] font-bold text-ink shrink-0">
												{ticket.assignedTechnician.slice(0, 2).toUpperCase()}
											</div>
											<span class="truncate max-w-[130px] font-medium text-ink">{ticket.assignedTechnician}</span>
										</div>
									{:else}
										<span class="text-body-sm text-secondary/60 italic">{t('tickets.detail.unassigned')}</span>
									{/if}
								</td>
								<td class="px-md py-4 text-right text-body-sm text-secondary font-mono hidden lg:table-cell">{formatDate(ticket.createdAt, data.locale, { dateStyle: 'medium' })}</td>
								<td class="px-md py-4">
									<div class="flex items-center justify-end gap-xs">
										<a class="inline-flex h-8 items-center rounded-button border border-stone bg-white px-3 text-body-sm font-medium text-primary hover:bg-ash transition-colors shadow-2xs" href="/app/tickets/{ticket.id}">{t('tickets.list.open')}</a>
										{#if canDeleteTicket}
											<form method="POST" action="?/deleteTicket">
												<input type="hidden" name="id" value={ticket.id} />
												<DeleteConfirmButton
													label={t('tickets.list.delete')}
													confirmTitle={t('common.deleteConfirmTitle')}
													confirmMessage={t('common.deleteConfirm')}
													cancelLabel={t('common.cancel')}
													class="inline-flex h-8 items-center rounded-button border border-error/30 bg-error/10 px-3 text-body-sm font-medium text-error hover:bg-error/15 transition-colors"
												/>
											</form>
										{/if}
									</div>
								</td>
							</tr>
						{/each}
					{/if}
				</tbody>
			</table>
		</div>
		<div class="flex flex-col sm:flex-row items-center justify-between gap-sm px-md py-4 bg-surface-container-low border-t border-stone">
			<p class="font-body-sm text-body-sm text-secondary">
				{t('common.pagination.showing')}
				<span class="font-bold text-primary">{data.tickets.items.length}</span>
				{t('common.pagination.of')}
				<span class="font-bold text-primary">{data.tickets.totalCount}</span>
			</p>
			<div class="flex items-center gap-sm text-body-sm">
				<a
					class="px-3 py-1.5 rounded-button border border-stone bg-white text-primary hover:bg-ash transition-colors inline-flex items-center gap-1 shadow-2xs {data.tickets.page <= 1 ? 'pointer-events-none opacity-40' : ''}"
					href={pageHref(Math.max(1, data.tickets.page - 1))}
				>
					<span class="material-symbols-outlined text-[16px]">chevron_left</span>
					<span>{t('common.previous')}</span>
				</a>
				<span class="text-secondary font-mono px-2">{data.tickets.page} / {Math.max(1, Math.ceil(data.tickets.totalCount / data.tickets.pageSize))}</span>
				<a
					class="px-3 py-1.5 rounded-button border border-stone bg-white text-primary hover:bg-ash transition-colors inline-flex items-center gap-1 shadow-2xs {data.tickets.page * data.tickets.pageSize >= data.tickets.totalCount ? 'pointer-events-none opacity-40' : ''}"
					href={pageHref(data.tickets.page + 1)}
				>
					<span>{t('common.next')}</span>
					<span class="material-symbols-outlined text-[16px]">chevron_right</span>
				</a>
			</div>
		</div>
	</div>
</div>
