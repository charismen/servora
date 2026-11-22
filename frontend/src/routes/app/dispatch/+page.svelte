<script lang="ts">
	import { enhance } from '$app/forms';
	import { goto } from '$app/navigation';
	import PageHeader from '$lib/components/PageHeader.svelte';
	import { createTranslator, formatDate, resolveTerminology } from '$lib/i18n';
	import { can, permissions } from '$lib/auth/permissions';
	import { toast } from '$lib/stores/toast.svelte';
	import { formatEnhanceUnexpectedError } from '$lib/utils/http';
	import type { DispatchConflict, DispatchDueMaintenance, DispatchTicket, ServiceResource } from '$lib/types/dispatch';
	import type { ActionData, PageData } from './$types';

	let { data, form }: { data: PageData; form: ActionData } = $props();
	const t = $derived(createTranslator(data.locale));
	const terms = $derived(resolveTerminology(data.user?.workspace?.terminology, data.locale));
	const canWriteTickets = $derived(can(data.user?.permissions ?? [], permissions.serviceTicketsWrite));
	const canManageResources = $derived(can(data.user?.permissions ?? [], permissions.settingsWrite));

	const board = $derived(data.board);
	const filters = $derived(data.filters);
	const technicians = $derived(board.technicians ?? []);
	const activeResources = $derived(board.resources ?? []);
	const allResources = $derived(data.allResources ?? []);

	// Backlog search
	let backlogQuery = $state('');
	const filteredBacklog = $derived.by(() => {
		const q = backlogQuery.trim().toLowerCase();
		if (!q) return board.unscheduledTickets;
		return board.unscheduledTickets.filter(
			(item) =>
				item.ticketNumber.toLowerCase().includes(q) ||
				item.customerName.toLowerCase().includes(q) ||
				item.deviceSnapshot.toLowerCase().includes(q) ||
				(item.siteAddressSnapshot ?? '').toLowerCase().includes(q)
		);
	});

	// Date helpers
	function parseYmdLocal(ymd: string): Date {
		const [y, m, d] = ymd.split('-').map(Number);
		return new Date(y, m - 1, d);
	}

	function toYmdLocal(date: Date): string {
		const y = date.getFullYear();
		const m = String(date.getMonth() + 1).padStart(2, '0');
		const d = String(date.getDate()).padStart(2, '0');
		return `${y}-${m}-${d}`;
	}

	function toHmLocal(date: Date): string {
		return `${String(date.getHours()).padStart(2, '0')}:${String(date.getMinutes()).padStart(2, '0')}`;
	}

	function combineLocalIso(ymd: string, hm: string): string {
		const [y, m, d] = ymd.split('-').map(Number);
		const [hh, mm] = hm.split(':').map(Number);
		return new Date(y, m - 1, d, hh || 0, mm || 0, 0, 0).toISOString();
	}

	function addMinutesToHm(hm: string, minutes: number): string {
		const [hh, mm] = hm.split(':').map(Number);
		const total = Math.min(23 * 60 + 59, Math.max(0, (hh || 0) * 60 + (mm || 0) + minutes));
		return `${String(Math.floor(total / 60)).padStart(2, '0')}:${String(total % 60).padStart(2, '0')}`;
	}

	const selectedDateObj = $derived(parseYmdLocal(filters.date));
	const weekDays = $derived.by(() => {
		const start = parseYmdLocal(filters.weekStart);
		return Array.from({ length: 7 }, (_, i) => {
			const d = new Date(start);
			d.setDate(d.getDate() + i);
			return { date: d, ymd: toYmdLocal(d) };
		});
	});

	const todayYmd = toYmdLocal(new Date());

	// Timeline hour bounds (derived from workspace open/close hours + auto-expand for out-of-hours visits)
	const timelineHours = $derived.by(() => {
		const openH = Number((board.operationalHours?.openTime ?? '08:00').split(':')[0]);
		const closeH = Number((board.operationalHours?.closeTime ?? '18:00').split(':')[0]);
		let startHour = Number.isFinite(openH) ? Math.min(Math.max(openH, 0), 20) : 8;
		let endHour = Number.isFinite(closeH) ? Math.min(Math.max(closeH, startHour + 4), 23) : 18;

		for (const ticket of dayTickets) {
			if (!ticket.scheduledVisitStart) continue;
			const s = new Date(ticket.scheduledVisitStart);
			const e = ticket.scheduledVisitEnd ? new Date(ticket.scheduledVisitEnd) : new Date(s.getTime() + 60 * 60000);
			startHour = Math.min(startHour, s.getHours());
			endHour = Math.max(endHour, Math.min(23, e.getHours() + (e.getMinutes() > 0 ? 1 : 0)));
		}

		return Array.from({ length: Math.max(1, endHour - startHour + 1) }, (_, i) => startHour + i);
	});

	const dayTickets = $derived.by(() => {
		const dayStart = parseYmdLocal(filters.date);
		const dayEnd = new Date(dayStart);
		dayEnd.setDate(dayEnd.getDate() + 1);
		return board.scheduledTickets.filter((ticket) => {
			if (!ticket.scheduledVisitStart) return false;
			const start = new Date(ticket.scheduledVisitStart);
			const end = ticket.scheduledVisitEnd ? new Date(ticket.scheduledVisitEnd) : new Date(start.getTime() + 60 * 60000);
			return start < dayEnd && end > dayStart;
		});
	});

	type Lane = { id: number | null; label: string; sublabel?: string | null; color?: string };
	const lanes = $derived.by<Lane[]>(() => {
		if (filters.groupBy === 'resource') {
			return [
				{ id: null, label: t('dispatch.unassignedLane'), color: '#94a3b8' },
				...activeResources.map((r) => ({
					id: r.id,
					label: r.name,
					sublabel: r.code || t(`dispatch.resources.type.${r.resourceType}`),
					color: r.color
				}))
			];
		}

		return [
			{ id: null, label: t('dispatch.unassignedLane'), color: '#94a3b8' },
			...technicians.map((tech) => ({
				id: tech.userId,
				label: tech.fullName,
				sublabel: tech.email,
				color: '#1c1815'
			}))
		];
	});

	function ticketMatchesLane(ticket: DispatchTicket, laneId: number | null): boolean {
		const actual = filters.groupBy === 'resource' ? (ticket.serviceResourceId ?? null) : (ticket.assignedTechnicianUserId ?? null);
		return actual === laneId;
	}

	function ticketMatchesDay(ticket: DispatchTicket, ymd: string): boolean {
		if (!ticket.scheduledVisitStart) return false;
		const d = new Date(ticket.scheduledVisitStart);
		return toYmdLocal(d) === ymd;
	}

	function blockStyle(ticket: DispatchTicket): string {
		if (!ticket.scheduledVisitStart || timelineHours.length === 0) return '';
		const firstHour = timelineHours[0];
		const totalMinutes = timelineHours.length * 60;
		const start = new Date(ticket.scheduledVisitStart);
		const end = ticket.scheduledVisitEnd ? new Date(ticket.scheduledVisitEnd) : new Date(start.getTime() + 60 * 60000);
		const startMins = Math.max(0, (start.getHours() - firstHour) * 60 + start.getMinutes());
		const endMins = Math.min(totalMinutes, Math.max(startMins + 30, (end.getHours() - firstHour) * 60 + end.getMinutes()));
		const leftPct = (startMins / totalMinutes) * 100;
		const widthPct = Math.max(4, ((endMins - startMins) / totalMinutes) * 100);
		return `left: ${leftPct}%; width: ${widthPct}%;`;
	}

	function navigateBoard(overrides: Partial<typeof filters>) {
		const next = { ...filters, ...overrides };
		const params = new URLSearchParams();
		params.set('view', next.view);
		params.set('date', next.date);
		params.set('groupBy', next.groupBy);
		if (next.locationType) params.set('locationType', next.locationType);
		goto(`/app/dispatch?${params.toString()}`, { keepFocus: true, noScroll: true });
	}

	function shiftDate(deltaDays: number) {
		const d = parseYmdLocal(filters.date);
		d.setDate(d.getDate() + deltaDays);
		navigateBoard({ date: toYmdLocal(d) });
	}

	// Schedule / Edit / PM Modal state
	let scheduleModalOpen = $state(false);
	let modalMode = $state<'ticket' | 'pm'>('ticket');
	let modalTicket = $state<DispatchTicket | null>(null);
	let modalPm = $state<DispatchDueMaintenance | null>(null);
	let draftTechId = $state('');
	let draftResourceId = $state('');
	let draftDate = $state('');
	let draftStartHm = $state('09:00');
	let draftEndHm = $state('10:00');
	let draftLocationType = $state('OnSite');
	let draftSiteAddress = $state('');
	let draftNotes = $state('');
	let submittingSchedule = $state(false);
	let submittingStatus = $state(false);
	let modalConflicts = $state<DispatchConflict[]>([]);
	let modalErrorMessage = $state('');

	function openTicketModal(
		ticket: DispatchTicket,
		preset: { date?: string; startHm?: string; laneId?: number | null } | undefined = undefined
	) {
		modalMode = 'ticket';
		modalTicket = ticket;
		modalPm = null;
		modalConflicts = [];
		modalErrorMessage = '';

		const start = ticket.scheduledVisitStart ? new Date(ticket.scheduledVisitStart) : null;
		const end = ticket.scheduledVisitEnd ? new Date(ticket.scheduledVisitEnd) : null;

		draftDate = preset?.date ?? (start ? toYmdLocal(start) : filters.date);
		draftStartHm = preset?.startHm ?? (start ? toHmLocal(start) : '09:00');
		if (preset?.startHm) {
			const origDuration = start && end ? Math.max(30, Math.round((end.getTime() - start.getTime()) / 60000)) : 60;
			draftEndHm = addMinutesToHm(preset.startHm, origDuration);
		} else {
			draftEndHm = end ? toHmLocal(end) : addMinutesToHm(draftStartHm, 60);
		}

		if (preset && 'laneId' in preset) {
			if (filters.groupBy === 'resource') {
				draftResourceId = preset.laneId ? String(preset.laneId) : '';
				draftTechId = ticket.assignedTechnicianUserId ? String(ticket.assignedTechnicianUserId) : '';
			} else {
				draftTechId = preset.laneId ? String(preset.laneId) : '';
				draftResourceId = ticket.serviceResourceId ? String(ticket.serviceResourceId) : '';
			}
		} else {
			draftTechId = ticket.assignedTechnicianUserId ? String(ticket.assignedTechnicianUserId) : '';
			draftResourceId = ticket.serviceResourceId ? String(ticket.serviceResourceId) : '';
		}

		draftLocationType = ticket.serviceLocationType || 'InShop';
		draftSiteAddress = ticket.siteAddressSnapshot ?? '';
		draftNotes = ticket.dispatchNotes ?? '';
		scheduleModalOpen = true;
	}

	function openPmModal(pm: DispatchDueMaintenance) {
		modalMode = 'pm';
		modalTicket = null;
		modalPm = pm;
		modalConflicts = [];
		modalErrorMessage = '';
		draftDate = pm.nextDueAt ? toYmdLocal(new Date(pm.nextDueAt)) : filters.date;
		draftStartHm = '09:00';
		draftEndHm = '10:00';
		draftTechId = pm.assignedTechnicianUserId ? String(pm.assignedTechnicianUserId) : '';
		draftResourceId = '';
		draftLocationType = pm.defaultLocationType || 'OnSite';
		draftSiteAddress = '';
		draftNotes = '';
		scheduleModalOpen = true;
	}

	function closeScheduleModal() {
		scheduleModalOpen = false;
		modalConflicts = [];
		modalErrorMessage = '';
	}

	// Drag-and-drop state
	let draggedTicket = $state<DispatchTicket | null>(null);
	let dropTargetKey = $state<string | null>(null);

	function handleDragStart(event: DragEvent, ticket: DispatchTicket) {
		if (!canWriteTickets) return;
		draggedTicket = ticket;
		event.dataTransfer?.setData('text/plain', String(ticket.id));
	}

	function handleSlotDrop(event: DragEvent, ymd: string, hour: number, laneId: number | null) {
		event.preventDefault();
		dropTargetKey = null;
		if (!draggedTicket || !canWriteTickets) return;
		const hm = `${String(hour).padStart(2, '0')}:00`;
		const target = draggedTicket;
		draggedTicket = null;
		openTicketModal(target, { date: ymd, startHm: hm, laneId });
	}

	// Manage Resources Modal state
	let resourceModalOpen = $state(false);
	let editingResourceId = $state(0);
	let resName = $state('');
	let resCode = $state('');
	let resType = $state('Bay');
	let resColor = $state('#b45039');
	let resNotes = $state('');
	let resIsActive = $state(true);
	let savingResource = $state(false);

	function openResourceModal(resource: ServiceResource | undefined = undefined) {
		if (resource) {
			editingResourceId = resource.id;
			resName = resource.name;
			resCode = resource.code ?? '';
			resType = resource.resourceType;
			resColor = resource.color || '#b45039';
			resNotes = resource.notes ?? '';
			resIsActive = resource.isActive;
		} else {
			editingResourceId = 0;
			resName = '';
			resCode = '';
			resType = 'Bay';
			resColor = '#b45039';
			resNotes = '';
			resIsActive = true;
		}
		resourceModalOpen = true;
	}

	function dispatchStatusBadgeClass(status: string): string {
		switch (status) {
			case 'EnRoute':
				return 'bg-sky-50 text-sky-800 border-sky-200';
			case 'ArrivedOnSite':
				return 'bg-violet-50 text-violet-800 border-violet-200';
			case 'VisitCompleted':
				return 'bg-emerald-50 text-emerald-800 border-emerald-200';
			case 'Scheduled':
				return 'bg-amber-50 text-amber-800 border-amber-200';
			default:
				return 'bg-ash text-secondary border-stone';
		}
	}

	function locationLabel(locationType: string): string {
		if (locationType === 'OnSite') return t('fieldService.onSite');
		if (locationType === 'PickupDelivery') return t('fieldService.pickupDelivery');
		return t('fieldService.inShop');
	}

	function mapsUrl(address: string): string {
		return `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(address)}`;
	}

	const rangeHeadline = $derived.by(() => {
		if (filters.view === 'week') {
			const start = weekDays[0].date;
			const end = weekDays[6].date;
			return `${formatDate(start.toISOString(), data.locale, { month: 'short', day: 'numeric' })} – ${formatDate(end.toISOString(), data.locale, { month: 'short', day: 'numeric', year: 'numeric' })}`;
		}
		return formatDate(selectedDateObj.toISOString(), data.locale, { weekday: 'long', month: 'long', day: 'numeric', year: 'numeric' });
	});

	$effect(() => {
		if (!form) return;
		const f = form as Record<string, unknown>;
		if (f.conflict && Array.isArray(f.conflicts)) {
			modalConflicts = f.conflicts as DispatchConflict[];
			modalErrorMessage = typeof f.message === 'string' ? f.message : '';
		} else if (f.ok) {
			if (f.action === 'scheduleTicket' || f.action === 'unscheduleTicket' || f.action === 'updateDispatchStatus' || f.action === 'scheduleMaintenance') {
				closeScheduleModal();
			}
			if (f.action === 'saveResource') {
				editingResourceId = 0;
				resName = '';
				resCode = '';
				resNotes = '';
			}
		} else if (typeof f.message === 'string' && (f.action === 'scheduleTicket' || f.action === 'scheduleMaintenance')) {
			modalErrorMessage = f.message;
		}
	});
</script>

<div class="space-y-lg pb-xl">
	<PageHeader
		items={[{ label: t('common.console'), href: '/app' }, { label: t('nav.dispatch') }]}
		title={t('dispatch.title')}
		subtitle={t('dispatch.subtitle')}
	>
		{#snippet actions()}
			{#if canManageResources}
				<button
					type="button"
					onclick={() => openResourceModal()}
					class="h-10 px-4 rounded-button border border-stone bg-white text-primary hover:bg-ash font-body-sm font-medium inline-flex items-center gap-1.5 shadow-2xs cursor-pointer"
				>
					<span class="material-symbols-outlined text-[18px]">garage</span>
					<span>{t('dispatch.manageResources')}</span>
				</button>
			{/if}
		{/snippet}
	</PageHeader>

	<!-- KPI Summary Strip -->
	<div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-sm">
		<div class="rounded-xl border border-stone bg-white px-4 py-3 shadow-2xs">
			<span class="text-[11px] font-bold uppercase tracking-wider text-secondary block">{t('dispatch.kpi.scheduled')}</span>
			<span class="text-xl font-bold font-mono tabular-nums text-primary">{board.summary.scheduledCount}</span>
		</div>
		<div class="rounded-xl border border-stone bg-white px-4 py-3 shadow-2xs">
			<span class="text-[11px] font-bold uppercase tracking-wider text-secondary block">{t('dispatch.kpi.unscheduled')}</span>
			<span class="text-xl font-bold font-mono tabular-nums text-primary">{board.summary.unscheduledCount}</span>
		</div>
		<div class="rounded-xl border border-stone bg-white px-4 py-3 shadow-2xs">
			<span class="text-[11px] font-bold uppercase tracking-wider text-secondary block">{t('dispatch.kpi.activeVisits')}</span>
			<span class="text-xl font-bold font-mono tabular-nums text-sky-700">{board.summary.enRouteOrOnSiteCount}</span>
		</div>
		<div class="rounded-xl border {board.summary.conflictCount > 0 ? 'border-rose-300 bg-rose-50/40' : 'border-stone bg-white'} px-4 py-3 shadow-2xs">
			<span class="text-[11px] font-bold uppercase tracking-wider text-secondary block">{t('dispatch.kpi.conflicts')}</span>
			<span class="text-xl font-bold font-mono tabular-nums {board.summary.conflictCount > 0 ? 'text-rose-700' : 'text-primary'}">{board.summary.conflictCount}</span>
		</div>
		<div class="rounded-xl border border-stone bg-white px-4 py-3 shadow-2xs">
			<span class="text-[11px] font-bold uppercase tracking-wider text-secondary block">{t('dispatch.kpi.slaAtRisk')}</span>
			<span class="text-xl font-bold font-mono tabular-nums {board.summary.slaAtRiskCount > 0 ? 'text-amber-700' : 'text-primary'}">{board.summary.slaAtRiskCount}</span>
		</div>
		<div class="rounded-xl border border-stone bg-white px-4 py-3 shadow-2xs">
			<span class="text-[11px] font-bold uppercase tracking-wider text-secondary block">{t('dispatch.kpi.duePm')}</span>
			<span class="text-xl font-bold font-mono tabular-nums text-claytile">{board.summary.duePmCount}</span>
		</div>
	</div>

	<!-- Toolbar Controls -->
	<div class="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-stone bg-white px-4 py-3 shadow-2xs">
		<div class="flex flex-wrap items-center gap-2">
			<div class="inline-flex items-center rounded-lg border border-stone bg-ash/50 p-0.5">
				<button
					type="button"
					onclick={() => shiftDate(filters.view === 'week' ? -7 : -1)}
					class="h-8 px-2.5 rounded-md text-caption font-medium text-secondary hover:text-primary hover:bg-white cursor-pointer inline-flex items-center"
					aria-label={t('dispatch.prevRange')}
				>
					<span class="material-symbols-outlined text-[18px]">chevron_left</span>
				</button>
				<button
					type="button"
					onclick={() => navigateBoard({ date: todayYmd })}
					class="h-8 px-3 rounded-md text-caption font-semibold text-primary hover:bg-white cursor-pointer"
				>
					{t('dispatch.today')}
				</button>
				<button
					type="button"
					onclick={() => shiftDate(filters.view === 'week' ? 7 : 1)}
					class="h-8 px-2.5 rounded-md text-caption font-medium text-secondary hover:text-primary hover:bg-white cursor-pointer inline-flex items-center"
					aria-label={t('dispatch.nextRange')}
				>
					<span class="material-symbols-outlined text-[18px]">chevron_right</span>
				</button>
			</div>
			<h2 class="text-body font-bold text-primary m-0 ml-1">{rangeHeadline}</h2>
		</div>

		<div class="flex flex-wrap items-center gap-2">
			<!-- Day / Week View Switch -->
			<div class="inline-flex items-center rounded-lg border border-stone bg-ash/50 p-0.5 text-caption">
				<button
					type="button"
					onclick={() => navigateBoard({ view: 'day' })}
					class="px-3 py-1 rounded-md font-semibold cursor-pointer transition-all {filters.view === 'day' ? 'bg-ink text-white shadow-2xs' : 'text-secondary hover:text-primary'}"
				>
					{t('dispatch.viewDay')}
				</button>
				<button
					type="button"
					onclick={() => navigateBoard({ view: 'week' })}
					class="px-3 py-1 rounded-md font-semibold cursor-pointer transition-all {filters.view === 'week' ? 'bg-ink text-white shadow-2xs' : 'text-secondary hover:text-primary'}"
				>
					{t('dispatch.viewWeek')}
				</button>
			</div>

			<!-- Group By Technician / Resource -->
			<div class="inline-flex items-center rounded-lg border border-stone bg-ash/50 p-0.5 text-caption">
				<button
					type="button"
					onclick={() => navigateBoard({ groupBy: 'technician' })}
					class="px-3 py-1 rounded-md font-semibold cursor-pointer transition-all {filters.groupBy === 'technician' ? 'bg-white text-primary shadow-2xs' : 'text-secondary hover:text-primary'}"
				>
					{t('dispatch.groupByTechnician')}
				</button>
				<button
					type="button"
					onclick={() => navigateBoard({ groupBy: 'resource' })}
					class="px-3 py-1 rounded-md font-semibold cursor-pointer transition-all {filters.groupBy === 'resource' ? 'bg-white text-primary shadow-2xs' : 'text-secondary hover:text-primary'}"
				>
					{t('dispatch.groupByResource')}
				</button>
			</div>

			<!-- Location Filter -->
			<select
				value={filters.locationType}
				onchange={(e) => navigateBoard({ locationType: e.currentTarget.value })}
				class="h-9 rounded-input border border-stone bg-white px-2.5 text-caption font-medium text-primary"
			>
				<option value="">{t('dispatch.filterAllLocations')}</option>
				<option value="InShop">{t('fieldService.inShop')}</option>
				<option value="OnSite">{t('fieldService.onSite')}</option>
				<option value="PickupDelivery">{t('fieldService.pickupDelivery')}</option>
			</select>
		</div>
	</div>

	{#if filters.groupBy === 'resource' && activeResources.length === 0}
		<div class="rounded-xl border border-stone bg-ash/50 px-4 py-3 text-body-sm text-secondary flex flex-wrap items-center justify-between gap-2">
			<span>{t('dispatch.noResourcesHint')}</span>
			{#if canManageResources}
				<button
					type="button"
					onclick={() => openResourceModal()}
					class="text-caption font-semibold text-claytile hover:text-ink underline underline-offset-2 cursor-pointer"
				>
					{t('dispatch.manageResources')}
				</button>
			{/if}
		</div>
	{/if}

	<!-- Main Dispatch Grid + Backlog Sidebar -->
	<div class="grid grid-cols-1 xl:grid-cols-[1fr_340px] gap-lg items-start">
		<!-- Calendar Canvas -->
		<section class="rounded-xl border border-stone bg-white shadow-2xs overflow-hidden min-w-0">
			{#if filters.view === 'day'}
				<!-- DAY TIMELINE VIEW -->
				<div class="overflow-x-auto">
					<div class="min-w-[820px]">
						<!-- Hour Header Row -->
						<div class="grid grid-cols-[180px_1fr] border-b border-stone bg-ash/40">
							<div class="px-3 py-2.5 text-[11px] font-bold uppercase tracking-wider text-secondary border-r border-stone">
								{filters.groupBy === 'resource' ? t('dispatch.groupByResource') : t('dispatch.groupByTechnician')}
							</div>
							<div class="grid" style={`grid-template-columns: repeat(${timelineHours.length}, minmax(0, 1fr));`}>
								{#each timelineHours as hour}
									<div class="px-1.5 py-2.5 text-[11px] font-mono font-semibold text-secondary text-center border-r border-stone/60 last:border-r-0">
										{String(hour).padStart(2, '0')}:00
									</div>
								{/each}
							</div>
						</div>

						<!-- Swimlanes -->
						{#each lanes as lane (lane.id ?? 'unassigned')}
							{@const laneTickets = dayTickets.filter((tk) => ticketMatchesLane(tk, lane.id))}
							<div class="grid grid-cols-[180px_1fr] border-b border-stone last:border-b-0 min-h-[84px]">
								<!-- Lane Header -->
								<div class="px-3 py-3 border-r border-stone bg-ash/15 flex flex-col justify-center gap-0.5">
									<div class="flex items-center gap-2 min-w-0">
										<span class="w-2.5 h-2.5 rounded-full shrink-0" style={`background-color: ${lane.color ?? '#1c1815'};`}></span>
										<span class="text-body-sm font-semibold text-primary truncate">{lane.label}</span>
									</div>
									{#if lane.sublabel}
										<span class="text-[11px] text-secondary truncate pl-4">{lane.sublabel}</span>
									{/if}
								</div>

								<!-- Interactive Hourly Track -->
								<div class="relative min-h-[84px]">
									<!-- Background Drop Cells -->
									<div
										class="grid h-full min-h-[84px]"
										style={`grid-template-columns: repeat(${timelineHours.length}, minmax(0, 1fr));`}
									>
										{#each timelineHours as hour}
											{@const cellKey = `${lane.id ?? 'u'}-${hour}`}
											<div
												role="button"
												tabindex="0"
												aria-label={`${lane.label} ${String(hour).padStart(2, '0')}:00`}
												class="border-r border-stone/40 last:border-r-0 transition-colors {dropTargetKey === cellKey ? 'bg-claytile/15' : 'hover:bg-ash/30'}"
												ondragover={(e) => {
													e.preventDefault();
													dropTargetKey = cellKey;
												}}
												ondragleave={() => {
													if (dropTargetKey === cellKey) dropTargetKey = null;
												}}
												ondrop={(e) => handleSlotDrop(e, filters.date, hour, lane.id)}
												onclick={() => {
													if (canWriteTickets && board.unscheduledTickets.length > 0) {
														openTicketModal(board.unscheduledTickets[0], {
															date: filters.date,
															startHm: `${String(hour).padStart(2, '0')}:00`,
															laneId: lane.id
														});
													}
												}}
												onkeydown={(e) => {
													if (e.key === 'Enter' && canWriteTickets && board.unscheduledTickets.length > 0) {
														openTicketModal(board.unscheduledTickets[0], {
															date: filters.date,
															startHm: `${String(hour).padStart(2, '0')}:00`,
															laneId: lane.id
														});
													}
												}}
											></div>
										{/each}
									</div>

									<!-- Positioned Ticket Blocks -->
									{#each laneTickets as ticket, idx (ticket.id)}
										<button
											type="button"
											draggable={canWriteTickets}
											ondragstart={(e) => handleDragStart(e, ticket)}
											onclick={() => openTicketModal(ticket)}
											style={`${blockStyle(ticket)} top: ${6 + (idx % 2) * 34}px; border-left-color: ${ticket.hasConflict ? '#dc2626' : ticket.statusColor};`}
											class="absolute z-10 min-h-[56px] max-h-[72px] rounded-lg border border-l-4 px-2 py-1.5 text-left shadow-xs transition-all cursor-pointer overflow-hidden {ticket.hasConflict ? 'bg-rose-50 border-rose-300' : 'bg-white border-stone hover:border-ink'}"
										>
											<div class="flex items-center justify-between gap-1">
												<span class="font-mono text-[11px] font-bold text-primary truncate">{ticket.ticketNumber}</span>
												<div class="flex items-center gap-1 shrink-0">
													{#if ticket.hasConflict}
														<span class="px-1 py-0.2 rounded bg-rose-600 text-white text-[9px] font-bold uppercase">
															{t('dispatch.conflictBadge')}
														</span>
													{/if}
													<span class="px-1.5 py-0.2 rounded border text-[9px] font-semibold {dispatchStatusBadgeClass(ticket.dispatchStatus)}">
														{t(`dispatch.status.${ticket.dispatchStatus}`)}
													</span>
												</div>
											</div>
											<p class="text-[11px] font-medium text-primary truncate m-0">{ticket.customerName}</p>
											<p class="text-[10px] text-secondary truncate m-0">
												{ticket.deviceSnapshot} · {locationLabel(ticket.serviceLocationType)}
												{#if ticket.serviceResourceName && filters.groupBy === 'technician'}
													· {ticket.serviceResourceName}
												{/if}
											</p>
										</button>
									{/each}
								</div>
							</div>
						{/each}
					</div>
				</div>
			{:else}
				<!-- WEEK GRID VIEW -->
				<div class="overflow-x-auto">
					<div class="min-w-[900px]">
						<div class="grid grid-cols-[160px_repeat(7,minmax(0,1fr))] border-b border-stone bg-ash/40">
							<div class="px-3 py-2.5 text-[11px] font-bold uppercase tracking-wider text-secondary border-r border-stone">
								{filters.groupBy === 'resource' ? t('dispatch.groupByResource') : t('dispatch.groupByTechnician')}
							</div>
							{#each weekDays as wd (wd.ymd)}
								<button
									type="button"
									onclick={() => navigateBoard({ view: 'day', date: wd.ymd })}
									class="px-2 py-2 text-center border-r border-stone/60 last:border-r-0 cursor-pointer hover:bg-white/70 transition-colors {wd.ymd === todayYmd ? 'bg-tertiary-container/40' : ''}"
								>
									<span class="block text-[10px] font-bold uppercase text-secondary">
										{formatDate(wd.date.toISOString(), data.locale, { weekday: 'short' })}
									</span>
									<span class="block text-body-sm font-mono font-bold {wd.ymd === todayYmd ? 'text-claytile' : 'text-primary'}">
										{wd.date.getDate()}
									</span>
								</button>
							{/each}
						</div>

						{#each lanes as lane (lane.id ?? 'unassigned')}
							<div class="grid grid-cols-[160px_repeat(7,minmax(0,1fr))] border-b border-stone last:border-b-0 min-h-[104px]">
								<div class="px-3 py-3 border-r border-stone bg-ash/15 flex flex-col justify-start gap-0.5">
									<div class="flex items-center gap-2 min-w-0">
										<span class="w-2.5 h-2.5 rounded-full shrink-0" style={`background-color: ${lane.color ?? '#1c1815'};`}></span>
										<span class="text-body-sm font-semibold text-primary truncate">{lane.label}</span>
									</div>
									{#if lane.sublabel}
										<span class="text-[11px] text-secondary truncate pl-4">{lane.sublabel}</span>
									{/if}
								</div>

								{#each weekDays as wd (wd.ymd)}
									{@const cellKey = `wk-${lane.id ?? 'u'}-${wd.ymd}`}
									{@const cellTickets = board.scheduledTickets.filter(
										(tk) => ticketMatchesLane(tk, lane.id) && ticketMatchesDay(tk, wd.ymd)
									)}
									<div
										role="region"
										aria-label={`${lane.label} ${wd.ymd}`}
										class="p-1.5 border-r border-stone/50 last:border-r-0 space-y-1.5 transition-colors {dropTargetKey === cellKey ? 'bg-claytile/15' : wd.ymd === todayYmd ? 'bg-tertiary-container/10' : ''}"
										ondragover={(e) => {
											e.preventDefault();
											dropTargetKey = cellKey;
										}}
										ondragleave={() => {
											if (dropTargetKey === cellKey) dropTargetKey = null;
										}}
										ondrop={(e) => handleSlotDrop(e, wd.ymd, 9, lane.id)}
									>
										{#each cellTickets as ticket (ticket.id)}
											<button
												type="button"
												draggable={canWriteTickets}
												ondragstart={(e) => handleDragStart(e, ticket)}
												onclick={() => openTicketModal(ticket)}
												style={`border-left-color: ${ticket.hasConflict ? '#dc2626' : ticket.statusColor};`}
												class="w-full rounded-md border border-l-4 px-2 py-1.5 text-left text-[11px] transition-all cursor-pointer {ticket.hasConflict ? 'bg-rose-50 border-rose-300' : 'bg-white border-stone hover:border-ink shadow-2xs'}"
											>
												<div class="flex items-center justify-between gap-1">
													<span class="font-mono font-bold text-primary truncate">
														{ticket.scheduledVisitStart ? toHmLocal(new Date(ticket.scheduledVisitStart)) : ''}
													</span>
													{#if ticket.hasConflict}
														<span class="w-2 h-2 rounded-full bg-rose-600 shrink-0" title={t('dispatch.conflictBadge')}></span>
													{/if}
												</div>
												<p class="font-semibold text-primary truncate m-0">{ticket.ticketNumber}</p>
												<p class="text-[10px] text-secondary truncate m-0">{ticket.customerName}</p>
											</button>
										{/each}
									</div>
								{/each}
							</div>
						{/each}
					</div>
				</div>
			{/if}
		</section>

		<!-- Right Sidebar: Unscheduled Backlog + Due PM -->
		<aside class="space-y-md">
			<!-- Unscheduled Backlog -->
			<section class="rounded-xl border border-stone bg-white p-4 shadow-2xs space-y-3">
				<div class="flex items-center justify-between gap-2">
					<h3 class="text-body-sm font-bold text-primary m-0">{t('dispatch.backlogTitle')}</h3>
					<span class="px-2 py-0.5 rounded-full bg-ash border border-stone font-mono text-[11px] font-semibold text-secondary">
						{board.unscheduledTickets.length}
					</span>
				</div>
				<p class="text-[11px] text-secondary m-0">{t('dispatch.backlogDragHint')}</p>
				<input
					type="search"
					bind:value={backlogQuery}
					placeholder={t('dispatch.backlogSearchPlaceholder')}
					class="w-full h-9 rounded-input border border-stone bg-white px-3 text-caption text-primary focus:outline-none focus:ring-1 focus:ring-primary"
				/>

				{#if filteredBacklog.length === 0}
					<div class="rounded-lg border border-dashed border-stone bg-ash/30 px-3 py-6 text-center text-caption text-secondary">
						{t('dispatch.backlogEmpty')}
					</div>
				{:else}
					<div class="space-y-2 max-h-[380px] overflow-y-auto pr-1">
						{#each filteredBacklog as ticket (ticket.id)}
							<div
								role="listitem"
								draggable={canWriteTickets}
								ondragstart={(e) => handleDragStart(e, ticket)}
								class="rounded-lg border border-stone bg-ash/20 hover:bg-ash/40 p-2.5 space-y-1.5 transition-colors {canWriteTickets ? 'cursor-grab active:cursor-grabbing' : ''}"
							>
								<div class="flex items-center justify-between gap-2">
									<a href={`/app/tickets/${ticket.id}`} class="font-mono text-caption font-bold text-primary hover:underline truncate">
										{ticket.ticketNumber}
									</a>
									<span class="text-[10px] font-semibold px-1.5 py-0.5 rounded bg-white border border-stone text-secondary">
										{locationLabel(ticket.serviceLocationType)}
									</span>
								</div>
								<p class="text-caption font-medium text-primary truncate m-0">{ticket.customerName}</p>
								<p class="text-[11px] text-secondary truncate m-0">{ticket.deviceSnapshot}</p>
								{#if ticket.slaDueAt}
									<p class="text-[10px] font-semibold text-amber-800 m-0">
										{t('dispatch.slaDue')}: {formatDate(ticket.slaDueAt, data.locale, { dateStyle: 'short', timeStyle: 'short' })}
									</p>
								{/if}
								{#if canWriteTickets}
									<div class="pt-1 flex justify-end">
										<button
											type="button"
											onclick={() => openTicketModal(ticket)}
											class="px-2.5 py-1 rounded border border-ink bg-white text-ink hover:bg-ink hover:text-white text-[11px] font-semibold transition-colors cursor-pointer"
										>
											{t('dispatch.scheduleButton')}
										</button>
									</div>
								{/if}
							</div>
						{/each}
					</div>
				{/if}
			</section>

			<!-- Due Preventive Maintenance -->
			<section class="rounded-xl border border-stone bg-white p-4 shadow-2xs space-y-3">
				<div class="flex items-center justify-between gap-2">
					<h3 class="text-body-sm font-bold text-primary m-0">{t('dispatch.duePmTitle')}</h3>
					<span class="px-2 py-0.5 rounded-full bg-tertiary-container text-on-tertiary-fixed-variant font-mono text-[11px] font-semibold">
						{board.dueMaintenanceSchedules.length}
					</span>
				</div>

				{#if board.dueMaintenanceSchedules.length === 0}
					<p class="text-caption text-secondary m-0">{t('dispatch.duePmEmpty')}</p>
				{:else}
					<div class="space-y-2 max-h-[260px] overflow-y-auto pr-1">
						{#each board.dueMaintenanceSchedules as pm (pm.id)}
							<div class="rounded-lg border {pm.isOverdue ? 'border-amber-300 bg-amber-50/40' : 'border-stone bg-ash/20'} p-2.5 space-y-1">
								<div class="flex items-center justify-between gap-1.5">
									<span class="text-caption font-semibold text-primary truncate">{pm.title}</span>
									{#if pm.isOverdue}
										<span class="px-1.5 py-0.5 rounded bg-amber-600 text-white text-[9px] font-bold uppercase shrink-0">
											{t('dispatch.duePmOverdue')}
										</span>
									{/if}
								</div>
								<p class="text-[11px] text-secondary truncate m-0">
									{pm.assetName}{pm.assetIdentifier ? ` (${pm.assetIdentifier})` : ''}
								</p>
								{#if pm.nextDueAt}
									<p class="text-[10px] font-mono text-secondary m-0">
										{formatDate(pm.nextDueAt, data.locale, { dateStyle: 'medium' })}
									</p>
								{/if}
								{#if canWriteTickets}
									<div class="pt-1 flex justify-end">
										<button
											type="button"
											onclick={() => openPmModal(pm)}
											class="px-2.5 py-1 rounded bg-claytile text-white hover:opacity-90 text-[11px] font-semibold cursor-pointer"
										>
											{t('dispatch.duePmGenerateSchedule')}
										</button>
									</div>
								{/if}
							</div>
						{/each}
					</div>
				{/if}
			</section>
		</aside>
	</div>
</div>

<!-- Schedule / Edit Dispatch Modal -->
{#if scheduleModalOpen && (modalTicket || modalPm)}
	<!-- svelte-ignore a11y_click_events_have_key_events -->
	<!-- svelte-ignore a11y_no_static_element_interactions -->
	<div
		class="fixed inset-0 z-50 bg-ink/60 backdrop-blur-xs flex items-center justify-center p-4"
		onclick={closeScheduleModal}
	>
		<div
			class="w-full max-w-lg rounded-xl border border-stone bg-white p-6 shadow-xl space-y-4 max-h-[90vh] overflow-y-auto"
			onclick={(e) => e.stopPropagation()}
		>
			<div class="flex items-start justify-between gap-2 border-b border-stone pb-3">
				<div>
					<h3 class="text-subheading font-bold text-primary m-0">
						{#if modalMode === 'pm'}
							{t('dispatch.modal.pmTitle')}
						{:else if modalTicket?.scheduledVisitStart}
							{t('dispatch.modal.editTitle')}
						{:else}
							{t('dispatch.modal.scheduleTitle')}
						{/if}
					</h3>
					{#if modalTicket}
						<p class="text-caption text-secondary mt-0.5 m-0">
							<span class="font-mono font-bold text-primary">{modalTicket.ticketNumber}</span>
							· {modalTicket.customerName} · {modalTicket.deviceSnapshot}
						</p>
					{:else if modalPm}
						<p class="text-caption text-secondary mt-0.5 m-0">
							{modalPm.title} · {modalPm.assetName}
						</p>
					{/if}
				</div>
				<button
					type="button"
					onclick={closeScheduleModal}
					class="w-8 h-8 rounded-lg hover:bg-ash text-secondary hover:text-primary flex items-center justify-center cursor-pointer"
					aria-label={t('common.close')}
				>
					✕
				</button>
			</div>

			<!-- Live Visit Execution Bar (only for already-scheduled tickets) -->
			{#if modalMode === 'ticket' && modalTicket?.scheduledVisitStart && canWriteTickets}
				<div class="rounded-lg border border-stone bg-ash/30 p-3 space-y-2">
					<div class="flex items-center justify-between gap-2">
						<span class="text-[11px] font-bold uppercase tracking-wider text-secondary">{t('dispatch.modal.executionTitle')}</span>
						<span class="px-2 py-0.5 rounded border text-[11px] font-semibold {dispatchStatusBadgeClass(modalTicket.dispatchStatus)}">
							{t(`dispatch.status.${modalTicket.dispatchStatus}`)}
						</span>
					</div>
					<div class="flex flex-wrap gap-1.5">
						{#each [
							{ status: 'EnRoute', label: t('dispatch.action.markEnRoute') },
							{ status: 'ArrivedOnSite', label: t('dispatch.action.markArrived') },
							{ status: 'VisitCompleted', label: t('dispatch.action.markCompleted') }
						] as step}
							<form
								method="POST"
								action="?/updateDispatchStatus"
								class="inline"
								use:enhance={() => {
									submittingStatus = true;
									return async ({ result, update }) => {
										try {
											if (result.type === 'error') {
												toast.error(formatEnhanceUnexpectedError(result.error, t));
												return;
											}
											await update({ reset: false });
										} finally {
											submittingStatus = false;
										}
									};
								}}
							>
								<input type="hidden" name="ticketId" value={modalTicket.id} />
								<input type="hidden" name="dispatchStatus" value={step.status} />
								<button
									type="submit"
									disabled={submittingStatus || modalTicket.dispatchStatus === step.status}
									class="h-8 px-3 rounded-button border border-stone bg-white hover:bg-ink hover:text-white text-caption font-semibold text-primary transition-colors cursor-pointer disabled:opacity-45"
								>
									{step.label}
								</button>
							</form>
						{/each}
					</div>
				</div>
			{/if}

			{#if modalConflicts.length > 0}
				<div class="rounded-lg border border-rose-300 bg-rose-50 p-3 space-y-2 text-caption text-rose-900">
					<div class="font-bold flex items-center gap-1.5">
						<span class="material-symbols-outlined text-[18px] text-rose-700">warning</span>
						<span>{t('dispatch.conflictTitle')}</span>
					</div>
					<ul class="m-0 pl-5 space-y-1">
						{#each modalConflicts as c (c.ticketId)}
							<li>
								<span class="font-mono font-bold">{c.ticketNumber}</span> ({c.customerName}) —
								{formatDate(c.scheduledVisitStart, data.locale, { dateStyle: 'short', timeStyle: 'short' })}
								· {c.conflictType}
							</li>
						{/each}
					</ul>
				</div>
			{:else if modalErrorMessage}
				<div class="rounded-lg border border-rose-200 bg-rose-50 px-3 py-2 text-caption text-rose-800">
					{modalErrorMessage}
				</div>
			{/if}

			<form
				method="POST"
				action={modalMode === 'pm' ? '?/scheduleMaintenance' : '?/scheduleTicket'}
				class="space-y-3"
				use:enhance={() => {
					submittingSchedule = true;
					return async ({ result, update }) => {
						try {
							if (result.type === 'error') {
								toast.error(formatEnhanceUnexpectedError(result.error, t));
								return;
							}
							await update({ reset: false });
						} finally {
							submittingSchedule = false;
						}
					};
				}}
			>
				{#if modalMode === 'pm' && modalPm}
					<input type="hidden" name="scheduleId" value={modalPm.id} />
				{:else if modalTicket}
					<input type="hidden" name="ticketId" value={modalTicket.id} />
				{/if}
				<input type="hidden" name="scheduledVisitStart" value={combineLocalIso(draftDate, draftStartHm)} />
				<input type="hidden" name="scheduledVisitEnd" value={combineLocalIso(draftDate, draftEndHm)} />

				<div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
					<label class="space-y-1 block">
						<span class="text-[11px] font-bold uppercase tracking-wider text-secondary block">{t('dispatch.modal.technician')}</span>
						<select
							name="assignedTechnicianUserId"
							bind:value={draftTechId}
							class="w-full h-10 rounded-input border border-stone bg-white px-2.5 text-body-sm text-primary"
						>
							<option value="">{t('dispatch.modal.unassignedOption')}</option>
							{#each technicians as tech (tech.userId)}
								<option value={String(tech.userId)}>{tech.fullName}</option>
							{/each}
						</select>
					</label>

					<label class="space-y-1 block">
						<span class="text-[11px] font-bold uppercase tracking-wider text-secondary block">{t('dispatch.modal.resource')}</span>
						<select
							name="serviceResourceId"
							bind:value={draftResourceId}
							class="w-full h-10 rounded-input border border-stone bg-white px-2.5 text-body-sm text-primary"
						>
							<option value="">{t('dispatch.modal.noResourceOption')}</option>
							{#each activeResources as res (res.id)}
								<option value={String(res.id)}>{res.name}{res.code ? ` (${res.code})` : ''}</option>
							{/each}
						</select>
					</label>
				</div>

				<div class="grid grid-cols-1 sm:grid-cols-3 gap-3">
					<label class="space-y-1 block">
						<span class="text-[11px] font-bold uppercase tracking-wider text-secondary block">{t('dispatch.modal.date')}</span>
						<input
							type="date"
							bind:value={draftDate}
							required
							class="w-full h-10 rounded-input border border-stone bg-white px-2.5 text-body-sm font-mono text-primary"
						/>
					</label>
					<label class="space-y-1 block">
						<span class="text-[11px] font-bold uppercase tracking-wider text-secondary block">{t('dispatch.modal.startTime')}</span>
						<input
							type="time"
							bind:value={draftStartHm}
							required
							class="w-full h-10 rounded-input border border-stone bg-white px-2.5 text-body-sm font-mono text-primary"
						/>
					</label>
					<label class="space-y-1 block">
						<span class="text-[11px] font-bold uppercase tracking-wider text-secondary block">{t('dispatch.modal.endTime')}</span>
						<input
							type="time"
							bind:value={draftEndHm}
							required
							class="w-full h-10 rounded-input border border-stone bg-white px-2.5 text-body-sm font-mono text-primary"
						/>
					</label>
				</div>

				<!-- Quick Duration Chips -->
				<div class="flex flex-wrap items-center gap-1.5">
					<span class="text-[11px] font-semibold text-secondary mr-1">{t('dispatch.modal.duration')}:</span>
					{#each [
						{ mins: 30, label: '30m' },
						{ mins: 60, label: '1h' },
						{ mins: 120, label: '2h' },
						{ mins: 240, label: '4h' }
					] as preset}
						<button
							type="button"
							onclick={() => (draftEndHm = addMinutesToHm(draftStartHm, preset.mins))}
							class="px-2 py-0.5 rounded border border-stone bg-ash/40 hover:bg-ash text-[11px] font-mono font-semibold text-primary cursor-pointer"
						>
							{preset.label}
						</button>
					{/each}
				</div>

				<div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
					<label class="space-y-1 block">
						<span class="text-[11px] font-bold uppercase tracking-wider text-secondary block">{t('dispatch.modal.locationType')}</span>
						<select
							name="serviceLocationType"
							bind:value={draftLocationType}
							class="w-full h-10 rounded-input border border-stone bg-white px-2.5 text-body-sm text-primary"
						>
							<option value="InShop">{t('fieldService.inShop')}</option>
							<option value="OnSite">{t('fieldService.onSite')}</option>
							<option value="PickupDelivery">{t('fieldService.pickupDelivery')}</option>
						</select>
					</label>

					<label class="space-y-1 block">
						<span class="text-[11px] font-bold uppercase tracking-wider text-secondary block">{t('dispatch.modal.siteAddress')}</span>
						<input
							type="text"
							name="siteAddressSnapshot"
							bind:value={draftSiteAddress}
							class="w-full h-10 rounded-input border border-stone bg-white px-2.5 text-body-sm text-primary"
						/>
					</label>
				</div>

				<label class="space-y-1 block">
					<span class="text-[11px] font-bold uppercase tracking-wider text-secondary block">{t('dispatch.modal.dispatchNotes')}</span>
					<textarea
						name="dispatchNotes"
						rows="2"
						bind:value={draftNotes}
						placeholder={t('dispatch.modal.dispatchNotesPlaceholder')}
						class="w-full rounded-input border border-stone bg-white p-2.5 text-body-sm text-primary"
					></textarea>
				</label>

				<div class="flex flex-wrap items-center justify-between gap-2 pt-2 border-t border-stone">
					<div class="flex flex-wrap items-center gap-2">
						{#if modalMode === 'ticket' && modalTicket}
							<a
								href={`/app/tickets/${modalTicket.id}`}
								class="h-9 px-3 rounded-button border border-stone bg-white hover:bg-ash text-caption font-semibold text-primary inline-flex items-center gap-1"
							>
								<span class="material-symbols-outlined text-[15px]">open_in_new</span>
								<span>{t('dispatch.modal.openTicket')}</span>
							</a>
						{/if}
						{#if draftSiteAddress.trim()}
							<a
								href={mapsUrl(draftSiteAddress.trim())}
								target="_blank"
								rel="noopener noreferrer"
								class="h-9 px-3 rounded-button border border-stone bg-white hover:bg-ash text-caption font-semibold text-primary inline-flex items-center gap-1"
							>
								<span class="material-symbols-outlined text-[15px]">map</span>
								<span>{t('dispatch.modal.openMaps')}</span>
							</a>
						{/if}
					</div>

					<div class="flex flex-wrap items-center gap-2">
						{#if modalConflicts.length > 0}
							<button
								type="submit"
								name="allowConflict"
								value="true"
								disabled={submittingSchedule}
								class="h-9 px-4 rounded-button bg-rose-700 text-white hover:bg-rose-800 text-caption font-semibold cursor-pointer disabled:opacity-60"
							>
								{t('dispatch.conflictOverrideButton')}
							</button>
						{:else}
							<button
								type="submit"
								name="allowConflict"
								value="false"
								disabled={submittingSchedule}
								class="h-9 px-4 rounded-button bg-ink text-white hover:bg-ink/90 text-caption font-semibold cursor-pointer disabled:opacity-60"
							>
								{t('dispatch.modal.saveSchedule')}
							</button>
						{/if}
					</div>
				</div>
			</form>

			{#if modalMode === 'ticket' && modalTicket?.scheduledVisitStart && canWriteTickets}
				<form
					method="POST"
					action="?/unscheduleTicket"
					class="pt-2 border-t border-stone/70 flex justify-end"
					use:enhance={() => {
						submittingSchedule = true;
						return async ({ result, update }) => {
							try {
								if (result.type === 'error') {
									toast.error(formatEnhanceUnexpectedError(result.error, t));
									return;
								}
								await update({ reset: false });
							} finally {
								submittingSchedule = false;
							}
						};
					}}
				>
					<input type="hidden" name="ticketId" value={modalTicket.id} />
					<button
						type="submit"
						disabled={submittingSchedule}
						class="text-caption font-semibold text-rose-700 hover:underline cursor-pointer"
					>
						{t('dispatch.modal.unschedule')}
					</button>
				</form>
			{/if}
		</div>
	</div>
{/if}

<!-- Manage Service Resources Modal -->
{#if resourceModalOpen}
	<!-- svelte-ignore a11y_click_events_have_key_events -->
	<!-- svelte-ignore a11y_no_static_element_interactions -->
	<div
		class="fixed inset-0 z-50 bg-ink/60 backdrop-blur-xs flex items-center justify-center p-4"
		onclick={() => (resourceModalOpen = false)}
	>
		<div
			class="w-full max-w-xl rounded-xl border border-stone bg-white p-6 shadow-xl space-y-4 max-h-[90vh] overflow-y-auto"
			onclick={(e) => e.stopPropagation()}
		>
			<div class="flex items-start justify-between gap-2 border-b border-stone pb-3">
				<div>
					<h3 class="text-subheading font-bold text-primary m-0">{t('dispatch.resources.title')}</h3>
					<p class="text-caption text-secondary mt-0.5 m-0">{t('dispatch.resources.subtitle')}</p>
				</div>
				<button
					type="button"
					onclick={() => (resourceModalOpen = false)}
					class="w-8 h-8 rounded-lg hover:bg-ash text-secondary hover:text-primary flex items-center justify-center cursor-pointer"
					aria-label={t('common.close')}
				>
					✕
				</button>
			</div>

			<!-- Existing Resources List -->
			{#if allResources.length === 0}
				<p class="text-caption text-secondary bg-ash/40 border border-dashed border-stone rounded-lg p-3 m-0">
					{t('dispatch.resources.empty')}
				</p>
			{:else}
				<div class="space-y-2 max-h-56 overflow-y-auto pr-1">
					{#each allResources as res (res.id)}
						<div class="flex items-center justify-between gap-2 rounded-lg border border-stone px-3 py-2 text-caption {res.isActive ? 'bg-white' : 'bg-ash/50 opacity-70'}">
							<div class="flex items-center gap-2 min-w-0">
								<span class="w-3 h-3 rounded-full shrink-0" style={`background-color: ${res.color};`}></span>
								<div class="min-w-0">
									<span class="font-semibold text-primary">{res.name}</span>
									{#if res.code}
										<span class="ml-1.5 font-mono text-[11px] text-secondary">({res.code})</span>
									{/if}
									<span class="block text-[11px] text-secondary">{t(`dispatch.resources.type.${res.resourceType}`)}</span>
								</div>
							</div>
							<div class="flex items-center gap-1.5 shrink-0">
								<button
									type="button"
									onclick={() => openResourceModal(res)}
									class="px-2 py-1 rounded border border-stone bg-white hover:bg-ash text-[11px] font-semibold text-primary cursor-pointer"
								>
									{t('common.edit')}
								</button>
								<form method="POST" action="?/deleteResource" class="inline" use:enhance>
									<input type="hidden" name="id" value={res.id} />
									<button
										type="submit"
										class="px-2 py-1 rounded border border-rose-200 bg-white hover:bg-rose-50 text-[11px] font-semibold text-rose-700 cursor-pointer"
									>
										{t('common.delete')}
									</button>
								</form>
							</div>
						</div>
					{/each}
				</div>
			{/if}

			<!-- Add / Edit Resource Form -->
			<form
				method="POST"
				action="?/saveResource"
				class="space-y-3 border-t border-stone pt-4"
				use:enhance={() => {
					savingResource = true;
					return async ({ result, update }) => {
						try {
							if (result.type === 'error') {
								toast.error(formatEnhanceUnexpectedError(result.error, t));
								return;
							}
							await update({ reset: false });
						} finally {
							savingResource = false;
						}
					};
				}}
			>
				<input type="hidden" name="id" value={editingResourceId} />
				<input type="hidden" name="isActive" value={resIsActive ? 'true' : 'false'} />

				<div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
					<label class="space-y-1 block">
						<span class="text-[11px] font-bold uppercase tracking-wider text-secondary block">{t('dispatch.resources.name')} *</span>
						<input
							type="text"
							name="name"
							bind:value={resName}
							placeholder={t('dispatch.resources.namePlaceholder')}
							required
							class="w-full h-9 rounded-input border border-stone bg-white px-2.5 text-body-sm text-primary"
						/>
					</label>
					<label class="space-y-1 block">
						<span class="text-[11px] font-bold uppercase tracking-wider text-secondary block">{t('dispatch.resources.code')}</span>
						<input
							type="text"
							name="code"
							bind:value={resCode}
							placeholder={t('dispatch.resources.codePlaceholder')}
							class="w-full h-9 rounded-input border border-stone bg-white px-2.5 text-body-sm font-mono text-primary"
						/>
					</label>
				</div>

				<div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
					<label class="space-y-1 block">
						<span class="text-[11px] font-bold uppercase tracking-wider text-secondary block">{t('dispatch.resources.type')}</span>
						<select
							name="resourceType"
							bind:value={resType}
							class="w-full h-9 rounded-input border border-stone bg-white px-2.5 text-body-sm text-primary"
						>
							{#each ['Bay', 'Lift', 'Bench', 'Vehicle', 'Room', 'Other'] as rt}
								<option value={rt}>{t(`dispatch.resources.type.${rt}`)}</option>
							{/each}
						</select>
					</label>
					<label class="space-y-1 block">
						<span class="text-[11px] font-bold uppercase tracking-wider text-secondary block">{t('dispatch.resources.color')}</span>
						<input
							type="color"
							name="color"
							bind:value={resColor}
							class="w-full h-9 rounded-input border border-stone bg-white p-1 cursor-pointer"
						/>
					</label>
				</div>

				<label class="space-y-1 block">
					<span class="text-[11px] font-bold uppercase tracking-wider text-secondary block">{t('dispatch.resources.notes')}</span>
					<input
						type="text"
						name="notes"
						bind:value={resNotes}
						class="w-full h-9 rounded-input border border-stone bg-white px-2.5 text-body-sm text-primary"
					/>
				</label>

				<div class="flex justify-end gap-2 pt-1">
					{#if editingResourceId > 0}
						<button
							type="button"
							onclick={() => openResourceModal()}
							class="h-9 px-3 rounded-button border border-stone bg-white text-caption font-medium text-secondary hover:text-primary cursor-pointer"
						>
							{t('settings.cancel')}
						</button>
					{/if}
					<button
						type="submit"
						disabled={savingResource}
						class="h-9 px-4 rounded-button bg-ink text-white hover:bg-ink/90 text-caption font-semibold cursor-pointer disabled:opacity-60"
					>
						{editingResourceId > 0 ? t('dispatch.resources.save') : t('dispatch.resources.add')}
					</button>
				</div>
			</form>
		</div>
	</div>
{/if}
