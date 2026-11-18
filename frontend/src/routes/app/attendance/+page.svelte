<script lang="ts">
	import { enhance } from '$app/forms';
	import PageHeader from '$lib/components/PageHeader.svelte';
	import DeleteConfirmButton from '$lib/components/DeleteConfirmButton.svelte';
	import DatePicker from '$lib/components/ui/date-picker/DatePicker.svelte';
	import TimePicker from '$lib/components/ui/time-picker/TimePicker.svelte';
	import Dropdown from '$lib/components/ui/dropdown/Dropdown.svelte';
	import StatusBadge from '$lib/components/ui/badge/StatusBadge.svelte';
	import { can, permissions } from '$lib/auth/permissions';
	import { createTranslator } from '$lib/i18n';
	import type { AttendanceItem } from '$lib/types/payroll';
	import type { ActionData, PageData } from './$types';

	let { data, form }: { data: PageData; form: ActionData } = $props();
	const t = $derived(createTranslator(data.locale));
	const summary = $derived(data.attendanceData.summary);
	const items = $derived(data.attendanceData.items ?? []);
	const canWrite = $derived(can(data.user.permissions, permissions.attendanceWrite));
	const canApprove = $derived(can(data.user.permissions, permissions.attendanceApprove));
	const canManage = $derived(data.canManage ?? (canWrite || canApprove));

	let activeView = $state<'table' | 'calendar'>('table');
	let selectedIds = $state<number[]>([]);
	let showModal = $state(false);

	// Shift Modal fields
	let modalId = $state(0);
	let modalUserId = $state<number>(0);
	let modalWorkDate = $state('');
	let modalCheckInTime = $state('09:00');
	let modalCheckOutTime = $state('18:00');
	let modalBreakMinutes = $state(60);
	let modalIsAdminOverride = $state(false);
	let modalOverrideReason = $state('');
	let modalNotes = $state('');
	let modalApproveImmediately = $state(true);

	const hasActiveFilters = $derived(
		Boolean(
			data.filters.from ||
			data.filters.to ||
			data.filters.userId ||
			data.filters.roleId ||
			data.filters.employmentType ||
			data.filters.status
		)
	);

	function toTimeInput(isoString: string | null | undefined, fallback: string) {
		if (!isoString) return fallback;
		// Extract HH:mm directly from the ISO timestamp string (e.g. 2026-11-06T09:00:00+07:00)
		// to anchor display strictly to shop operational time without browser timezone distortion.
		const match = isoString.match(/T(\d{2}):(\d{2})/);
		if (match) {
			return `${match[1]}:${match[2]}`;
		}
		const d = new Date(isoString);
		if (Number.isNaN(d.getTime())) return fallback;
		return `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`;
	}

	function openNewModal(defaultDate: string = '') {
		const today = new Date().toISOString().slice(0, 10);
		modalId = 0;
		modalUserId = data.staff[0]?.id ?? data.user.id;
		modalWorkDate = defaultDate || today;
		modalCheckInTime = summary.operationalOpenTime || '09:00';
		modalCheckOutTime = summary.operationalCloseTime || '18:00';
		modalBreakMinutes = summary.defaultBreakMinutes ?? 60;
		modalIsAdminOverride = false;
		modalOverrideReason = '';
		modalNotes = '';
		modalApproveImmediately = canApprove;
		showModal = true;
	}

	function openEditModal(item: AttendanceItem) {
		modalId = item.id;
		modalUserId = item.userId;
		modalWorkDate = item.workDate;
		modalCheckInTime = toTimeInput(item.checkInAt, '09:00');
		modalCheckOutTime = item.checkOutAt ? toTimeInput(item.checkOutAt, '18:00') : '';
		modalBreakMinutes = item.breakMinutes;
		modalIsAdminOverride = item.isAdminOverride;
		modalOverrideReason = item.overrideReason ?? '';
		modalNotes = item.notes ?? '';
		modalApproveImmediately = item.status === 'Approved';
		showModal = true;
	}

	function parseMinutes(hhmm: string): number | null {
		if (!hhmm || !hhmm.includes(':')) return null;
		const [h, m] = hhmm.split(':').map(Number);
		if (!Number.isFinite(h) || !Number.isFinite(m)) return null;
		return h * 60 + m;
	}

	const derivedWorkedHours = $derived.by(() => {
		const inMins = parseMinutes(modalCheckInTime);
		const outMins = parseMinutes(modalCheckOutTime);
		if (inMins === null || outMins === null || outMins <= inMins) return 0;
		const net = Math.max(0, outMins - inMins - Math.max(0, Number(modalBreakMinutes) || 0));
		return Math.round((net / 60) * 100) / 100;
	});

	const isOutsideOperatingHours = $derived.by(() => {
		const openMins = parseMinutes(summary.operationalOpenTime || '09:00') ?? 540;
		const closeMins = parseMinutes(summary.operationalCloseTime || '18:00') ?? 1080;
		const inMins = parseMinutes(modalCheckInTime);
		const outMins = parseMinutes(modalCheckOutTime);
		if (inMins !== null && (inMins < openMins || inMins > closeMins)) return true;
		if (outMins !== null && (outMins < openMins || outMins > closeMins)) return true;
		return false;
	});

	function toggleSelect(id: number) {
		if (selectedIds.includes(id)) {
			selectedIds = selectedIds.filter((item) => item !== id);
		} else {
			selectedIds = [...selectedIds, id];
		}
	}

	function toggleSelectAllPending() {
		const pendingIds = items.filter((i) => i.status === 'Pending').map((i) => i.id);
		if (pendingIds.length > 0 && pendingIds.every((id) => selectedIds.includes(id))) {
			selectedIds = [];
		} else {
			selectedIds = pendingIds;
		}
	}

	// Group items by date for Calendar view
	const groupedByDate = $derived.by(() => {
		const map = new Map<string, AttendanceItem[]>();
		for (const item of items) {
			const list = map.get(item.workDate) ?? [];
			list.push(item);
			map.set(item.workDate, list);
		}
		return Array.from(map.entries()).sort((a, b) => b[0].localeCompare(a[0]));
	});

	function formatDateLabel(dateStr: string) {
		try {
			const d = new Date(dateStr + 'T00:00:00');
			return d.toLocaleDateString(data.locale === 'id' ? 'id-ID' : 'en-US', {
				weekday: 'short',
				month: 'short',
				day: 'numeric',
				year: 'numeric'
			});
		} catch {
			return dateStr;
		}
	}
</script>

{#snippet statusBadge(status: string)}
	{@const variant = status === 'Approved' ? 'success' : status === 'Rejected' ? 'error' : 'warning'}
	<StatusBadge {variant} label={t(`attendance.status.${status}`)} />
{/snippet}

<div class="space-y-lg pb-xl">
	<PageHeader
		items={[{ label: t('common.console'), href: '/app' }, { label: t('attendance.title') }]}
		title={t('attendance.title')}
		subtitle={canManage ? t('attendance.subtitle') : t('attendance.mySubtitle')}
	>
		{#snippet actions()}
			<div class="flex flex-wrap items-center gap-sm">
				<span class="inline-flex items-center gap-1.5 rounded-full border border-stone bg-ash/60 px-3 py-1.5 text-caption font-medium text-secondary">
					<span class="material-symbols-outlined text-[15px] text-secondary">schedule</span>
					<span>{t('attendance.operatingWindowBadge')}</span>
					<strong class="font-semibold text-primary">{summary.operationalOpenTime} – {summary.operationalCloseTime}</strong>
					{#if summary.operationalTimeZone}
						<span class="text-[11px] text-secondary/80 font-mono">({summary.operationalTimeZone})</span>
					{/if}
				</span>
				{#if canWrite}
					<button
						type="button"
						onclick={() => openNewModal()}
						class="h-10 px-4 rounded-button font-medium bg-primary text-white hover:opacity-90 transition-opacity cursor-pointer inline-flex items-center gap-1.5 shadow-2xs text-body-sm whitespace-nowrap"
					>
						<span class="material-symbols-outlined text-[18px]">add</span>
						<span>{t('attendance.logShift')}</span>
					</button>
				{/if}
			</div>
		{/snippet}
	</PageHeader>

	<!-- Summary Cards -->
	<div class="grid grid-cols-2 lg:grid-cols-4 gap-md">
		<div data-bento class="rounded-xl border border-stone bg-white p-md shadow-2xs">
			<p class="font-label-caps text-label-caps text-secondary uppercase tracking-wider">{t('attendance.summaryTotalEntries')}</p>
			<p class="mt-1 font-display text-headline-sm font-bold text-primary">{summary.totalEntries}</p>
		</div>
		<div data-bento class="rounded-xl border border-stone bg-white p-md shadow-2xs">
			<div class="flex items-center justify-between">
				<p class="font-label-caps text-label-caps text-secondary uppercase tracking-wider">{t('attendance.summaryPending')}</p>
				{#if summary.pendingEntries > 0}
					<span class="w-2 h-2 rounded-full bg-amber-500"></span>
				{/if}
			</div>
			<p class="mt-1 font-display text-headline-sm font-bold text-amber-600">{summary.pendingEntries}</p>
		</div>
		<div data-bento class="rounded-xl border border-stone bg-white p-md shadow-2xs">
			<div class="flex items-center justify-between">
				<p class="font-label-caps text-label-caps text-secondary uppercase tracking-wider">{t('attendance.summaryApproved')}</p>
				{#if summary.approvedEntries > 0}
					<span class="w-2 h-2 rounded-full bg-emerald-500"></span>
				{/if}
			</div>
			<p class="mt-1 font-display text-headline-sm font-bold text-emerald-600">{summary.approvedEntries}</p>
		</div>
		<div data-bento class="rounded-xl border border-stone bg-white p-md shadow-2xs">
			<p class="font-label-caps text-label-caps text-secondary uppercase tracking-wider">{t('attendance.summaryApprovedHours')}</p>
			<p class="mt-1 font-display text-headline-sm font-bold text-primary">
				{summary.totalApprovedHours}<span class="text-subheading font-medium text-secondary ml-0.5">h</span>
			</p>
		</div>
	</div>

	<!-- Standardized Bento Filter Toolbar -->
	<form method="GET" class="rounded-xl border border-stone bg-white p-md shadow-2xs relative z-30 !overflow-visible">
		<div class="flex flex-col xl:flex-row xl:items-end gap-md !overflow-visible">
			<div class="grid grid-cols-1 sm:grid-cols-2 {canManage ? 'lg:grid-cols-3 xl:grid-cols-6' : 'lg:grid-cols-3'} gap-md flex-1 !overflow-visible">
				<div class="space-y-xs">
					<span class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block">{t('attendance.filterFrom')}</span>
					<DatePicker name="from" value={data.filters.from} clearable />
				</div>
				<div class="space-y-xs">
					<span class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block">{t('attendance.filterTo')}</span>
					<DatePicker name="to" value={data.filters.to} clearable />
				</div>
				{#if canManage}
					<div class="space-y-xs block">
						<span class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block">{t('attendance.filterWorker')}</span>
						<Dropdown
							name="userId"
							value={data.filters.userId ?? ''}
							options={[{ value: '', label: t('attendance.allWorkers') }, ...data.staff.map(worker => ({ value: String(worker.id), label: worker.fullName }))]}
						/>
					</div>
					<div class="space-y-xs block">
						<span class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block">{t('attendance.filterRole')}</span>
						<Dropdown
							name="roleId"
							value={data.filters.roleId ?? ''}
							options={[{ value: '', label: t('attendance.allRoles') }, ...data.roles.map(role => ({ value: String(role.id), label: role.name }))]}
						/>
					</div>
					<div class="space-y-xs block">
						<span class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block">{t('attendance.filterEmployment')}</span>
						<Dropdown
							name="employmentType"
							value={data.filters.employmentType ?? ''}
							options={[
								{ value: '', label: t('attendance.allEmploymentTypes') },
								{ value: 'Freelance', label: t('settings.employmentType.Freelance') },
								{ value: 'FullTime', label: t('settings.employmentType.FullTime') }
							]}
						/>
					</div>
				{/if}
				<div class="space-y-xs block">
					<span class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block">{t('attendance.filterStatus')}</span>
					<Dropdown
						name="status"
						value={data.filters.status ?? ''}
						options={[
							{ value: '', label: t('attendance.allStatuses') },
							{ value: 'Pending', label: t('attendance.status.Pending') },
							{ value: 'Approved', label: t('attendance.status.Approved') },
							{ value: 'Rejected', label: t('attendance.status.Rejected') }
						]}
						align="right"
					/>
				</div>
			</div>
			<div class="flex items-center gap-sm shrink-0">
				{#if hasActiveFilters}
					<a
						href="/app/attendance"
						class="h-10 px-4 rounded-button font-medium border border-stone bg-white text-secondary hover:text-primary hover:bg-ash transition-colors inline-flex items-center justify-center text-body-sm shadow-2xs"
					>
						{t('common.reset')}
					</a>
				{/if}
				<button
					type="submit"
					class="h-10 px-5 rounded-button font-medium bg-primary text-white hover:opacity-90 cursor-pointer inline-flex items-center justify-center text-body-sm shadow-2xs whitespace-nowrap"
				>
					{t('reports.apply')}
				</button>
			</div>
		</div>
	</form>

	<!-- View Switcher & Bulk Selection Bar -->
	<div class="flex flex-wrap items-center justify-between gap-md border-b border-stone pb-sm">
		<!-- Segmented Control Pill -->
		<div class="inline-flex items-center p-0.5 rounded-lg border border-stone bg-ash/60">
			<button
				type="button"
				onclick={() => (activeView = 'table')}
				class="px-3 py-1.5 rounded-md text-caption font-medium transition-all cursor-pointer flex items-center gap-1.5 {activeView === 'table' ? 'bg-white text-primary font-semibold shadow-2xs' : 'text-secondary hover:text-primary'}"
			>
				<span class="material-symbols-outlined text-[16px]">view_list</span>
				<span>{t('attendance.viewTable')}</span>
			</button>
			<button
				type="button"
				onclick={() => (activeView = 'calendar')}
				class="px-3 py-1.5 rounded-md text-caption font-medium transition-all cursor-pointer flex items-center gap-1.5 {activeView === 'calendar' ? 'bg-white text-primary font-semibold shadow-2xs' : 'text-secondary hover:text-primary'}"
			>
				<span class="material-symbols-outlined text-[16px]">calendar_month</span>
				<span>{t('attendance.viewCalendar')}</span>
			</button>
		</div>

		<!-- Bulk Actions Strip when rows selected -->
		{#if canApprove && selectedIds.length > 0}
			<div class="flex items-center gap-sm bg-white border border-stone rounded-xl px-3 py-1.5 shadow-2xs">
				<span class="text-caption font-medium text-secondary">
					<strong class="text-primary font-semibold">{selectedIds.length}</strong> {t('attendance.shiftsCount')}
				</span>
				<div class="h-4 w-px bg-stone"></div>
				<form
					method="POST"
					action="?/bulkStatus"
					class="flex items-center gap-xs"
					use:enhance={() => {
						return async ({ update }) => {
							await update();
							selectedIds = [];
						};
					}}
				>
					{#each selectedIds as id}
						<input type="hidden" name="attendanceIds" value={id} />
					{/each}
					<button
						type="submit"
						name="status"
						value="Approved"
						class="px-3 py-1 rounded-button bg-emerald-600 hover:bg-emerald-700 text-white text-caption font-medium cursor-pointer transition-colors inline-flex items-center gap-1 shadow-2xs"
					>
						<span class="material-symbols-outlined text-[14px]">check</span>
						<span>{t('attendance.approveSelected')}</span>
					</button>
					<button
						type="submit"
						name="status"
						value="Rejected"
						class="px-3 py-1 rounded-button border border-rose-200 bg-rose-50 hover:bg-rose-100 text-rose-700 text-caption font-medium cursor-pointer transition-colors inline-flex items-center gap-1 shadow-2xs"
					>
						<span class="material-symbols-outlined text-[14px]">close</span>
						<span>{t('attendance.rejectSelected')}</span>
					</button>
				</form>
				<button
					type="button"
					onclick={() => (selectedIds = [])}
					class="text-caption text-secondary hover:text-primary cursor-pointer px-1.5 py-0.5 rounded hover:bg-ash"
				>
					{t('attendance.deselectAll')}
				</button>
			</div>
		{/if}
	</div>

	{#if items.length === 0}
		<div class="rounded-xl border border-stone bg-white p-xl text-center shadow-2xs">
			<div class="w-12 h-12 mx-auto rounded-full bg-ash flex items-center justify-center text-secondary mb-3">
				<span class="material-symbols-outlined text-[24px]">history_toggle_off</span>
			</div>
			<h4 class="font-subheading text-body font-semibold text-primary">{t('attendance.empty')}</h4>
			<p class="text-body-sm text-secondary mt-1 max-w-md mx-auto">{t('inventory.noItemsCopy')}</p>
		</div>
	{:else if activeView === 'table'}
		<section data-bento class="rounded-xl border border-stone bg-white shadow-2xs overflow-x-auto">
			<table class="w-full {canManage ? 'min-w-[920px]' : 'min-w-[640px]'} border-collapse text-left">
				<thead class="bg-ash/50 border-b border-stone">
					<tr>
						{#if canApprove}
							<th class="px-4 py-3 w-10">
								<input
									type="checkbox"
									class="rounded border-stone text-primary focus:ring-primary cursor-pointer"
									onchange={toggleSelectAllPending}
									checked={selectedIds.length > 0 && items.filter((i) => i.status === 'Pending').every((i) => selectedIds.includes(i.id))}
								/>
							</th>
						{/if}
						<th class="px-4 py-3 font-label-caps text-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('attendance.colDate')}</th>
						{#if canManage}
							<th class="px-4 py-3 font-label-caps text-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('attendance.colWorker')}</th>
							<th class="px-4 py-3 font-label-caps text-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('attendance.colRole')}</th>
						{/if}
						<th class="px-4 py-3 font-label-caps text-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('attendance.colCheckIn')}</th>
						<th class="px-4 py-3 font-label-caps text-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('attendance.colCheckOut')}</th>
						<th class="px-4 py-3 font-label-caps text-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('attendance.colBreak')}</th>
						<th class="px-4 py-3 font-label-caps text-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('attendance.colHours')}</th>
						<th class="px-4 py-3 font-label-caps text-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('attendance.colStatus')}</th>
						<th class="px-4 py-3 font-label-caps text-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('attendance.colNotes')}</th>
						{#if canWrite || canApprove}
							<th class="px-4 py-3 font-label-caps text-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider text-right">{t('attendance.colActions')}</th>
						{/if}
					</tr>
				</thead>
				<tbody class="divide-y divide-stone text-body-sm">
					{#each items as item}
						<tr class="hover:bg-ash/40 transition-colors">
							{#if canApprove}
								<td class="px-4 py-3">
									<input
										type="checkbox"
										class="rounded border-stone text-primary focus:ring-primary cursor-pointer"
										checked={selectedIds.includes(item.id)}
										onchange={() => toggleSelect(item.id)}
									/>
								</td>
							{/if}
							<td class="px-4 py-3 font-medium text-primary whitespace-nowrap">{item.workDate}</td>
							{#if canManage}
								<td class="px-4 py-3">
									<div class="font-medium text-primary">{item.workerName}</div>
									<span class="text-[11px] text-secondary">{t(`settings.employmentType.${item.employmentType}`)}</span>
								</td>
								<td class="px-4 py-3 text-secondary">{item.roles.join(', ') || '-'}</td>
							{/if}
							<td class="px-4 py-3 font-mono text-primary">{toTimeInput(item.checkInAt, '-')}</td>
							<td class="px-4 py-3 font-mono text-primary">{item.checkOutAt ? toTimeInput(item.checkOutAt, '-') : '-'}</td>
							<td class="px-4 py-3 text-secondary">{item.breakMinutes}m</td>
							<td class="px-4 py-3 font-semibold text-primary">{item.totalHours}h</td>
							<td class="px-4 py-3 whitespace-nowrap">
								{@render statusBadge(item.status)}
							</td>
							<td class="px-4 py-3 text-secondary max-w-[240px] truncate">
								{#if item.isAdminOverride}
									<span class="inline-flex items-center gap-1 rounded px-1.5 py-0.5 text-[10px] font-semibold border border-amber-200 bg-amber-50 text-amber-700 mr-1">
										<span class="material-symbols-outlined text-[12px]">warning</span>
										{t('attendance.overrideBadge')}
									</span>
								{/if}
								<span>{item.overrideReason || item.notes || '-'}</span>
							</td>
							{#if canWrite || canApprove}
								<td class="px-4 py-3 text-right whitespace-nowrap">
									<div class="inline-flex items-center gap-xs">
										{#if canApprove && item.status !== 'Approved'}
											<form method="POST" action="?/bulkStatus" use:enhance>
												<input type="hidden" name="attendanceIds" value={item.id} />
												<input type="hidden" name="status" value="Approved" />
												<button
													type="submit"
													class="px-2.5 py-1 rounded-button border border-emerald-200 bg-emerald-50 hover:bg-emerald-100 text-emerald-700 text-caption font-medium cursor-pointer transition-colors inline-flex items-center gap-1 shadow-2xs"
												>
													<span class="material-symbols-outlined text-[13px]">check</span>
													<span>{t('tickets.detail.approve')}</span>
												</button>
											</form>
										{/if}
										{#if canWrite}
											<button
												type="button"
												onclick={() => openEditModal(item)}
												class="px-2.5 py-1 rounded-button border border-stone bg-white text-primary hover:bg-ash text-caption font-medium cursor-pointer transition-colors shadow-2xs"
											>
												{t('customers.detail.edit')}
											</button>
											<form method="POST" action="?/deleteAttendance" use:enhance>
												<input type="hidden" name="id" value={item.id} />
												<DeleteConfirmButton
													label={t('common.delete')}
													confirmTitle={t('common.deleteConfirmTitle')}
													confirmMessage={t('common.deleteConfirm')}
													cancelLabel={t('common.cancel')}
													class="px-2.5 py-1 rounded-button border border-rose-200 bg-rose-50 hover:bg-rose-100 text-rose-700 text-caption font-medium cursor-pointer transition-colors shadow-2xs"
												/>
											</form>
										{/if}
									</div>
								</td>
							{/if}
						</tr>
					{/each}
				</tbody>
			</table>
		</section>
	{:else}
		<!-- Operational Calendar / Daily Cards View -->
		<div class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-md">
			{#each groupedByDate as [dateStr, dayItems]}
				<div data-bento class="rounded-xl border border-stone bg-white p-md shadow-2xs space-y-sm flex flex-col justify-between">
					<div>
						<div class="flex items-center justify-between border-b border-stone pb-xs">
							<div>
								<h4 class="font-subheading text-body font-semibold text-primary">{formatDateLabel(dateStr)}</h4>
								<span class="text-caption text-secondary font-mono">{dateStr}</span>
							</div>
							<div class="flex items-center gap-xs">
								<span class="text-caption font-medium text-secondary bg-ash px-2 py-0.5 rounded-full border border-stone/50">
									{dayItems.length} {t('attendance.shiftsCount')}
								</span>
								{#if canWrite}
									<button
										type="button"
										onclick={() => openNewModal(dateStr)}
										class="p-1 rounded hover:bg-ash text-secondary hover:text-primary transition-colors cursor-pointer"
										title={t('attendance.logShift')}
									>
										<span class="material-symbols-outlined text-[18px]">add</span>
									</button>
								{/if}
							</div>
						</div>
						<div class="space-y-xs mt-sm">
							{#each dayItems as shift}
								<div class="rounded-lg border border-stone/70 bg-ash/30 p-sm flex items-center justify-between gap-sm text-body-sm hover:bg-ash/60 transition-colors">
									<div>
										<p class="font-semibold text-primary">{shift.workerName}</p>
										<p class="text-[12px] text-secondary font-mono">
											{toTimeInput(shift.checkInAt, '-')} – {shift.checkOutAt ? toTimeInput(shift.checkOutAt, '-') : '...'} ({shift.totalHours}h)
										</p>
									</div>
									<div class="flex items-center gap-xs">
										{@render statusBadge(shift.status)}
										{#if canWrite}
											<button
												type="button"
												onclick={() => openEditModal(shift)}
												class="p-1 text-secondary hover:text-primary cursor-pointer rounded hover:bg-white transition-colors"
											>
												<span class="material-symbols-outlined text-[16px]">edit</span>
											</button>
										{/if}
									</div>
								</div>
							{/each}
						</div>
					</div>
				</div>
			{/each}
		</div>
	{/if}
</div>

{#if showModal}
	<div class="fixed inset-0 z-[60] flex items-center justify-center bg-black/40 backdrop-blur-xs px-md py-6" role="presentation">
		<div class="w-[min(620px,calc(100vw-2rem))] max-h-[calc(100vh-3rem)] overflow-y-auto rounded-xl border border-stone bg-white p-md sm:p-xl shadow-xl space-y-lg" role="dialog" aria-modal="true">
			<div class="flex items-start justify-between gap-md border-b border-stone pb-md">
				<div>
					<h3 class="font-subheading text-subheading font-semibold text-primary">
						{modalId > 0 ? t('attendance.editShift') : t('attendance.logShift')}
					</h3>
					<p class="text-caption text-secondary mt-0.5">
						{t('attendance.subtitle')}
					</p>
				</div>
				<button
					type="button"
					onclick={() => (showModal = false)}
					class="p-1 rounded-md text-secondary hover:text-primary hover:bg-ash transition-colors cursor-pointer"
				>
					<span class="material-symbols-outlined text-[20px]">close</span>
				</button>
			</div>

			<form
				method="POST"
				action="?/saveAttendance"
				class="space-y-md"
				use:enhance={() => {
					return async ({ update, result }) => {
						await update();
						if (result.type === 'success') {
							showModal = false;
						}
					};
				}}
			>
				<input type="hidden" name="id" value={modalId} />

				<div class="grid grid-cols-1 sm:grid-cols-2 gap-md">
					<div class="space-y-1 block">
						<span class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block">{t('attendance.colWorker')}</span>
						<Dropdown
							name="userId"
							value={String(modalUserId)}
							onchange={(val) => { modalUserId = Number(val); }}
							options={data.staff.map((worker) => ({
								value: String(worker.id),
								label: `${worker.fullName} (${worker.employmentType ?? 'FullTime'})`
							}))}
						/>
					</div>

					<div class="space-y-1 block">
						<label for="attendance-work-date" class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block">{t('attendance.colDate')}</label>
						<DatePicker
							id="attendance-work-date"
							name="workDate"
							bind:value={modalWorkDate}
							required
						/>
					</div>
				</div>

				<div class="grid grid-cols-1 sm:grid-cols-3 gap-md">
					<div class="space-y-1 block">
						<label for="attendance-check-in" class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block">{t('attendance.colCheckIn')}</label>
						<TimePicker
							id="attendance-check-in"
							name="checkInTime"
							bind:value={modalCheckInTime}
							required
						/>
					</div>

					<div class="space-y-1 block">
						<label for="attendance-check-out" class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block">{t('attendance.colCheckOut')}</label>
						<TimePicker
							id="attendance-check-out"
							name="checkOutTime"
							bind:value={modalCheckOutTime}
							clearable
						/>
					</div>

					<label class="space-y-1 block">
						<span class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block">{t('attendance.colBreak')}</span>
						<input
							type="number"
							name="breakMinutes"
							min="0"
							max="720"
							bind:value={modalBreakMinutes}
							class="w-full h-10 rounded-input border border-stone bg-white px-3 py-2 text-body-sm text-primary focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary"
							required
						/>
					</label>
				</div>

				<div class="rounded-xl border border-stone bg-ash/40 p-md flex items-center justify-between">
					<span class="text-body-sm font-medium text-secondary">{t('attendance.derivedHours')}</span>
					<span class="font-display text-headline-sm font-bold text-primary">
						{derivedWorkedHours}<span class="text-subheading font-medium text-secondary ml-0.5">h</span>
					</span>
				</div>

				{#if isOutsideOperatingHours || modalIsAdminOverride}
					<div class="rounded-xl border border-amber-200 bg-amber-50/70 p-md space-y-sm">
						<div class="flex items-center gap-1.5 text-amber-800 text-caption font-semibold">
							<span class="material-symbols-outlined text-[16px]">warning</span>
							<span>{t('attendance.outsideHoursWarning')} ({summary.operationalOpenTime} – {summary.operationalCloseTime})</span>
						</div>
						<label class="flex items-center gap-sm text-body-sm text-primary cursor-pointer select-none">
							<input
								type="checkbox"
								name="isAdminOverride"
								bind:checked={modalIsAdminOverride}
								class="rounded border-stone text-primary focus:ring-primary"
							/>
							<span class="font-medium">{t('attendance.adminOverrideLabel')}</span>
						</label>
						{#if modalIsAdminOverride}
							<label class="space-y-1 block pt-1">
								<span class="text-caption font-medium text-primary block">{t('attendance.overrideReasonLabel')}</span>
								<input
									name="overrideReason"
									bind:value={modalOverrideReason}
									placeholder={t('attendance.overrideReasonPlaceholder')}
									class="w-full rounded-input border border-amber-300 bg-white px-3 py-2 text-body-sm focus:outline-none focus:ring-1 focus:ring-amber-500"
									required
								/>
							</label>
						{/if}
					</div>
				{/if}

				<label class="space-y-1 block">
					<span class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block">{t('attendance.colNotes')}</span>
					<textarea
						name="notes"
						rows="2"
						bind:value={modalNotes}
						placeholder={t('attendance.notesPlaceholder')}
						class="w-full rounded-input border border-stone bg-white px-3 py-2 text-body-sm focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary"
					></textarea>
				</label>

				{#if canApprove}
					<label class="flex items-center gap-sm text-body-sm text-secondary cursor-pointer select-none">
						<input
							type="checkbox"
							name="approveImmediately"
							bind:checked={modalApproveImmediately}
							class="rounded border-stone text-primary focus:ring-primary"
						/>
						<span>{t('attendance.approveImmediately')}</span>
					</label>
				{/if}

				<div class="flex justify-end gap-sm border-t border-stone pt-md">
					<button
						type="button"
						onclick={() => (showModal = false)}
						class="px-button-x py-button-y rounded-button border border-stone bg-white text-secondary hover:text-primary hover:bg-ash text-body-sm font-medium cursor-pointer shadow-2xs transition-colors"
					>
						{t('common.cancel')}
					</button>
					<button
						type="submit"
						class="h-10 px-5 rounded-button font-medium bg-primary text-white hover:opacity-90 cursor-pointer shadow-2xs text-body-sm transition-opacity"
					>
						{t('common.save')}
					</button>
				</div>
			</form>
		</div>
	</div>
{/if}
