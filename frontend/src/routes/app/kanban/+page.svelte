<script lang="ts">
	import { deserialize } from '$app/forms';
	import PageHeader from '$lib/components/PageHeader.svelte';
	import { tilt } from '$lib/components/tilt';
	import { createTranslator, formatDate, resolveFeatureConfig, resolveTerminology } from '$lib/i18n';
	import { toast } from '$lib/stores/toast.svelte';
	import type { ActionData, PageData } from './$types';
	import type { KanbanColumn, KanbanTicket } from '$lib/types/service-tickets';
	import GlowingBadge from '$lib/components/ui/glowing-badge/GlowingBadge.svelte';
	import Dropdown from '$lib/components/ui/dropdown/Dropdown.svelte';
	import DatePicker from '$lib/components/ui/date-picker/DatePicker.svelte';

	let { data, form }: { data: PageData; form: ActionData } = $props();
	const t = $derived(createTranslator(data.locale));
	const terms = $derived(resolveTerminology(data.user?.workspace?.terminology, data.locale));
	const features = $derived(
		resolveFeatureConfig(
			data.user?.workspace?.featureConfig ?? data.blueprintConfig?.featureConfig,
			data.user?.workspace?.operatingMode
		)
	);

	// Optimistic local override (null means use data.columns from server)
	let optimisticColumns = $state<KanbanColumn[] | null>(null);
	const columns = $derived(optimisticColumns ?? data.columns ?? []);

	$effect(() => {
		if (data.columns) {
			optimisticColumns = null;
		}
	});

	// Terminal status age filter & active workbench toggle
	let customTerminalAge = $state<string | null>(null);
	let customActiveOnly = $state<boolean | null>(null);
	const terminalAge = $derived(customTerminalAge ?? data.filters.terminalAge ?? '30');
	const activeOnly = $derived(customActiveOnly ?? data.filters.activeOnly ?? false);
	let collapsedColumns = $state<Set<number>>(new Set());
	let unhiddenColumns = $state<Set<number>>(new Set());

	$effect(() => {
		try {
			const saved = localStorage.getItem('servora_kanban_collapsed');
			if (saved) {
				collapsedColumns = new Set(JSON.parse(saved));
			}
		} catch {}
	});

	function toggleColumnCollapse(statusId: number) {
		const next = new Set(collapsedColumns);
		if (next.has(statusId)) {
			next.delete(statusId);
		} else {
			next.add(statusId);
		}
		collapsedColumns = next;
		try {
			localStorage.setItem('servora_kanban_collapsed', JSON.stringify([...next]));
		} catch {}
	}

	function toggleColumnUnhide(statusId: number) {
		const next = new Set(unhiddenColumns);
		if (next.has(statusId)) {
			next.delete(statusId);
		} else {
			next.add(statusId);
		}
		unhiddenColumns = next;
	}

	function isTerminalStatus(col: KanbanColumn): boolean {
		if (col.isFinal || col.isCancelled) return true;
		const name = col.name?.toLowerCase() ?? '';
		return name.includes('complete') || name.includes('selesai') || name.includes('cancel') || name.includes('batal');
	}

	function getColumnTickets(col: KanbanColumn): { visibleTickets: KanbanTicket[]; hiddenCount: number } {
		if (!isTerminalStatus(col) || terminalAge === 'all' || unhiddenColumns.has(col.statusId)) {
			return { visibleTickets: col.tickets, hiddenCount: 0 };
		}
		const maxDays = Number(terminalAge) || 30;
		const cutoff = Date.now() - maxDays * 24 * 60 * 60 * 1000;
		const visible: KanbanTicket[] = [];
		let hiddenCount = 0;

		for (const ticket of col.tickets) {
			const ticketTime = new Date(ticket.createdAt).getTime();
			if (ticketTime >= cutoff) {
				visible.push(ticket);
			} else {
				hiddenCount++;
			}
		}
		return { visibleTickets: visible, hiddenCount };
	}

	const displayedColumns = $derived(activeOnly ? columns.filter(c => !isTerminalStatus(c)) : columns);
	const totalTickets = $derived(displayedColumns.reduce((sum, column) => sum + column.tickets.length, 0));

	const priorities = ['', 'Low', 'Normal', 'High', 'Urgent'];
	const paymentStatuses = ['', 'Unpaid', 'Partial', 'Paid', 'Waived'];
	const approvalStatuses = ['', 'pending', 'approved', 'none'];

	const hasActiveFilters = $derived(
		Boolean(
			data.filters.search ||
			data.filters.priority ||
			data.filters.paymentStatus ||
			data.filters.approvalStatus ||
			data.filters.technicianUserId ||
			data.filters.from ||
			data.filters.to ||
			terminalAge !== '30' ||
			activeOnly
		)
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

	// Track ticket that failed to move so it can display a brief warning shake
	let failedTicketId = $state<number | null>(null);

	// Drag state
	interface DragState {
		status: 'idle' | 'down' | 'dragging' | 'settling';
		ticket: KanbanTicket | null;
		sourceStatusId: number | null;
		hoveredStatusId: number | null;
		startX: number;
		startY: number;
		currentX: number;
		currentY: number;
		lastX: number;
		lastTime: number;
		tilt: number;
		cardRect: {
			left: number;
			top: number;
			width: number;
			height: number;
		};
		grabOffsetX: number;
		grabOffsetY: number;
		moved: boolean;
	}

	let dragState = $state<DragState>({
		status: 'idle',
		ticket: null,
		sourceStatusId: null,
		hoveredStatusId: null,
		startX: 0,
		startY: 0,
		currentX: 0,
		currentY: 0,
		lastX: 0,
		lastTime: 0,
		tilt: 0,
		cardRect: { left: 0, top: 0, width: 0, height: 0 },
		grabOffsetX: 0,
		grabOffsetY: 0,
		moved: false
	});

	// Mobile Quick-Move Modal Sheet
	let mobileModal = $state<{
		open: boolean;
		ticket: KanbanTicket | null;
		sourceStatusId: number | null;
	}>({
		open: false,
		ticket: null,
		sourceStatusId: null
	});

	function openMobileMove(ticket: KanbanTicket, statusId: number) {
		mobileModal = {
			open: true,
			ticket,
			sourceStatusId: statusId
		};
	}

	function closeMobileMove() {
		mobileModal = {
			open: false,
			ticket: null,
			sourceStatusId: null
		};
	}

	function handleMobileStatusSelect(targetStatusId: number) {
		if (!mobileModal.ticket || !mobileModal.sourceStatusId) return;
		const ticket = mobileModal.ticket;
		const sourceId = mobileModal.sourceStatusId;
		closeMobileMove();
		if (sourceId !== targetStatusId) {
			executeMove(ticket, sourceId, targetStatusId);
		}
	}

	function getColumnAtPoint(clientX: number, clientY: number): number | null {
		const elements = document.elementsFromPoint(clientX, clientY);
		for (const el of elements) {
			const col = el.closest<HTMLElement>('[data-kanban-column-id]');
			if (col) {
				const id = Number(col.dataset.kanbanColumnId);
				if (!isNaN(id)) return id;
			}
		}
		// Fallback: horizontal span across all displayed column elements
		for (const col of displayedColumns) {
			const el = document.getElementById(`kanban-col-${col.statusId}`);
			if (el) {
				const rect = el.getBoundingClientRect();
				if (clientX >= rect.left && clientX <= rect.right) {
					return col.statusId;
				}
			}
		}
		return null;
	}

	function handlePointerDown(event: PointerEvent, ticket: KanbanTicket, statusId: number) {
		if (event.button !== 0) return;
		const target = event.target as HTMLElement;
		if (target.closest('button, input, select, textarea, [data-no-drag]')) return;

		const currentTarget = event.currentTarget as HTMLElement;
		const rect = currentTarget.getBoundingClientRect();

		dragState = {
			status: 'down',
			ticket,
			sourceStatusId: statusId,
			hoveredStatusId: statusId,
			startX: event.clientX,
			startY: event.clientY,
			currentX: event.clientX,
			currentY: event.clientY,
			lastX: event.clientX,
			lastTime: performance.now(),
			tilt: 0,
			cardRect: {
				left: rect.left,
				top: rect.top,
				width: rect.width,
				height: rect.height
			},
			grabOffsetX: event.clientX - rect.left,
			grabOffsetY: event.clientY - rect.top,
			moved: false
		};

		window.addEventListener('pointermove', handlePointerMove, { passive: false });
		window.addEventListener('pointerup', handlePointerUp);
		window.addEventListener('pointercancel', handlePointerCancel);
		window.addEventListener('keydown', handleKeyDown);
	}

	function handlePointerMove(event: PointerEvent) {
		if (dragState.status === 'idle') return;

		const dx = event.clientX - dragState.startX;
		const dy = event.clientY - dragState.startY;
		const dist = Math.hypot(dx, dy);

		if (!dragState.moved && dist > 4) {
			dragState.moved = true;
			dragState.status = 'dragging';
			document.body.style.cursor = 'grabbing';
			document.body.style.userSelect = 'none';
		}

		if (dragState.status === 'dragging') {
			event.preventDefault();

			const now = performance.now();
			const dt = Math.max(1, now - dragState.lastTime);
			const moveX = event.clientX - dragState.lastX;
			const velocity = (moveX / dt) * 16.67;

			// Fluid tilt between -3.5 and +3.5 degrees (Trello / Jira Cloud motion)
			const targetTilt = Math.max(-3.5, Math.min(3.5, velocity * 0.35));
			const newTilt = dragState.tilt * 0.65 + targetTilt * 0.35;

			dragState.currentX = event.clientX;
			dragState.currentY = event.clientY;
			dragState.lastX = event.clientX;
			dragState.lastTime = now;
			dragState.tilt = newTilt;

			const colId = getColumnAtPoint(event.clientX, event.clientY);
			if (colId !== null) {
				dragState.hoveredStatusId = colId;
			}
		}
	}

	function snapBackToOrigin() {
		dragState.status = 'settling';
		setTimeout(() => {
			dragState = {
				status: 'idle',
				ticket: null,
				sourceStatusId: null,
				hoveredStatusId: null,
				startX: 0,
				startY: 0,
				currentX: 0,
				currentY: 0,
				lastX: 0,
				lastTime: 0,
				tilt: 0,
				cardRect: { left: 0, top: 0, width: 0, height: 0 },
				grabOffsetX: 0,
				grabOffsetY: 0,
				moved: false
			};
		}, 200);
	}

	function handlePointerUp(event: PointerEvent) {
		window.removeEventListener('pointermove', handlePointerMove);
		window.removeEventListener('pointerup', handlePointerUp);
		window.removeEventListener('pointercancel', handlePointerCancel);
		window.removeEventListener('keydown', handleKeyDown);

		document.body.style.cursor = '';
		document.body.style.userSelect = '';

		if (dragState.status === 'dragging') {
			const ticket = dragState.ticket;
			const sourceId = dragState.sourceStatusId;
			const targetId = dragState.hoveredStatusId;

			if (ticket && sourceId !== null && targetId !== null && targetId !== sourceId) {
				dragState = {
					status: 'idle',
					ticket: null,
					sourceStatusId: null,
					hoveredStatusId: null,
					startX: 0,
					startY: 0,
					currentX: 0,
					currentY: 0,
					lastX: 0,
					lastTime: 0,
					tilt: 0,
					cardRect: { left: 0, top: 0, width: 0, height: 0 },
					grabOffsetX: 0,
					grabOffsetY: 0,
					moved: false
				};
				executeMove(ticket, sourceId, targetId);
			} else {
				snapBackToOrigin();
			}
		} else {
			dragState = {
				status: 'idle',
				ticket: null,
				sourceStatusId: null,
				hoveredStatusId: null,
				startX: 0,
				startY: 0,
				currentX: 0,
				currentY: 0,
				lastX: 0,
				lastTime: 0,
				tilt: 0,
				cardRect: { left: 0, top: 0, width: 0, height: 0 },
				grabOffsetX: 0,
				grabOffsetY: 0,
				moved: false
			};
		}
	}

	function handlePointerCancel() {
		window.removeEventListener('pointermove', handlePointerMove);
		window.removeEventListener('pointerup', handlePointerUp);
		window.removeEventListener('pointercancel', handlePointerCancel);
		window.removeEventListener('keydown', handleKeyDown);

		document.body.style.cursor = '';
		document.body.style.userSelect = '';

		if (dragState.status === 'dragging') {
			snapBackToOrigin();
		} else {
			dragState.status = 'idle';
		}
	}

	function handleKeyDown(event: KeyboardEvent) {
		if (event.key === 'Escape') {
			if (mobileModal.open) {
				closeMobileMove();
			} else if (dragState.status === 'dragging') {
				snapBackToOrigin();
			}
		}
	}

	function handleCardClick(event: MouseEvent) {
		if (dragState.moved) {
			event.preventDefault();
			event.stopPropagation();
		}
	}

	async function executeMove(ticket: KanbanTicket, fromStatusId: number, toStatusId: number) {
		if (fromStatusId === toStatusId) return;

		const targetCol = columns.find(c => c.statusId === toStatusId);
		const targetName = targetCol ? targetCol.name : '';

		// 1. Optimistic Update (0ms perceived latency)
		optimisticColumns = columns.map(col => {
			if (col.statusId === fromStatusId) {
				return {
					...col,
					tickets: col.tickets.filter(t => t.id !== ticket.id)
				};
			}
			if (col.statusId === toStatusId) {
				return {
					...col,
					tickets: [...col.tickets, ticket]
				};
			}
			return col;
		});

		// 2. Dispatch Server Action
		const formData = new FormData();
		formData.set('ticketId', String(ticket.id));
		formData.set('statusId', String(toStatusId));

		try {
			const response = await fetch('?/moveTicket', {
				method: 'POST',
				body: formData
			});

			const text = await response.text();
			const result = deserialize(text);

			if (result.type === 'success') {
				toast.success(t('kanban.ticketMoved', { status: targetName }));
			} else {
				// Revert on server rejection
				optimisticColumns = null;
				const message = (result.type === 'failure' && result.data && typeof result.data === 'object' && 'message' in result.data)
					? String(result.data.message)
					: t('kanban.ticketMoveFailed');
				toast.error(message);
				triggerFailedShake(ticket.id);
			}
		} catch (error) {
			optimisticColumns = null;
			toast.error(t('kanban.ticketMoveFailed'));
			triggerFailedShake(ticket.id);
		}
	}

	function triggerFailedShake(ticketId: number) {
		failedTicketId = ticketId;
		setTimeout(() => {
			if (failedTicketId === ticketId) {
				failedTicketId = null;
			}
		}, 800);
	}

	$effect(() => {
		return () => {
			window.removeEventListener('pointermove', handlePointerMove);
			window.removeEventListener('pointerup', handlePointerUp);
			window.removeEventListener('pointercancel', handlePointerCancel);
			window.removeEventListener('keydown', handleKeyDown);
			document.body.style.cursor = '';
			document.body.style.userSelect = '';
		};
	});
</script>

<div class="space-y-lg pb-xl">
	<PageHeader items={[{ label: t('common.console'), href: '/app' }, { label: t('nav.kanban') }]} title={`${t('kanban.title')} — ${terms.ticketPlural}`} subtitle={t('kanban.subtitle')}>
		{#snippet actions()}
		<a class="px-button-x py-button-y rounded-button font-medium font-body-sm text-body-sm inline-flex items-center gap-2 bg-ink text-white hover:bg-ink/90 active:scale-95 transition-all shadow-xs shrink-0" href="/app/tickets/new">
			<span class="material-symbols-outlined text-[18px]">add</span>
			<span>+ {terms.ticketSingular}</span>
		</a>
		{/snippet}
	</PageHeader>

	<!-- View & Queue Bar -->
	<div class="flex flex-col lg:flex-row lg:items-center justify-between gap-3">
		<div class="flex flex-wrap items-center gap-2.5">
			<!-- Queue Switcher Tabs -->
			<div class="inline-flex rounded-input border border-stone/80 bg-ash/40 p-1 text-caption font-medium shadow-2xs">
				<a
					href="?myTickets=false{activeOnly ? '&activeOnly=true' : ''}{terminalAge !== '30' ? `&terminalAge=${terminalAge}` : ''}"
					class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-button text-body-sm font-medium transition-all shrink-0 {!data.filters.myTickets ? 'bg-white text-ink font-semibold shadow-xs' : 'text-secondary hover:text-ink'}"
				>
					<span class="material-symbols-outlined text-[18px]">view_kanban</span>
					<span>{t('kanban.allBoards')}</span>
				</a>
				<a
					href="?myTickets=true{activeOnly ? '&activeOnly=true' : ''}{terminalAge !== '30' ? `&terminalAge=${terminalAge}` : ''}"
					class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-button text-body-sm font-medium transition-all shrink-0 {data.filters.myTickets ? 'bg-white text-ink font-semibold shadow-xs' : 'text-secondary hover:text-ink'}"
				>
					<span class="material-symbols-outlined text-[18px]">engineering</span>
					<span>{t('kanban.myQueue')}</span>
				</a>
			</div>

			<!-- Active Workbench vs All Stages Toggle -->
			<div class="inline-flex rounded-input border border-stone/80 bg-ash/40 p-1 text-caption font-medium shadow-2xs">
				<button
					type="button"
					class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-button text-body-sm font-medium transition-all shrink-0 cursor-pointer {!activeOnly ? 'bg-white text-ink font-semibold shadow-xs' : 'text-secondary hover:text-ink'}"
					onclick={() => { customActiveOnly = false; }}
				>
					<span class="material-symbols-outlined text-[18px]">view_column</span>
					<span>{t('kanban.allColumns')}</span>
				</button>
				<button
					type="button"
					class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-button text-body-sm font-medium transition-all shrink-0 cursor-pointer {activeOnly ? 'bg-white text-ink font-semibold shadow-xs' : 'text-secondary hover:text-ink'}"
					onclick={() => { customActiveOnly = true; }}
				>
					<span class="material-symbols-outlined text-[18px]">handyman</span>
					<span>{t('kanban.activeWorkbenchOnly')}</span>
				</button>
			</div>
		</div>

		<!-- View Switcher (List vs Kanban) -->
		<div class="inline-flex rounded-input border border-stone/80 bg-ash/40 p-1 text-caption font-medium shadow-2xs self-start lg:self-auto">
			<a
				href="/app/tickets{data.filters.myTickets ? '?myTickets=true' : ''}"
				class="inline-flex items-center gap-1.5 px-3.5 py-1.5 rounded-button text-body-sm font-medium transition-all shrink-0 text-secondary hover:text-ink"
			>
				<span class="material-symbols-outlined text-[18px]">table_rows</span>
				<span>{t('tickets.list.viewList')}</span>
			</a>
			<span
				class="inline-flex items-center gap-1.5 px-3.5 py-1.5 rounded-button text-body-sm font-semibold transition-all shrink-0 bg-white text-ink shadow-xs"
			>
				<span class="material-symbols-outlined text-[18px]">view_kanban</span>
				<span>{t('tickets.list.viewKanban')}</span>
			</span>
		</div>
	</div>

	<!-- Filters Bar -->
	<form data-bento class="bg-surface-container-lowest border border-stone rounded-xl p-md shadow-2xs !overflow-visible relative z-30" method="GET">
		{#if data.filters.myTickets}
			<input type="hidden" name="myTickets" value="true" />
		{/if}
		{#if activeOnly}
			<input type="hidden" name="activeOnly" value="true" />
		{/if}
		<div class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 xl:grid-cols-[minmax(200px,1fr)_140px_140px_140px_140px_150px_130px_130px_auto] gap-md items-end">
			<label class="space-y-1 sm:col-span-2 md:col-span-1">
				<span class="block font-label-caps text-label-caps text-secondary uppercase text-[11px] font-bold tracking-wider whitespace-nowrap truncate">{t('tickets.list.search')}</span>
				<div class="relative">
					<span class="material-symbols-outlined absolute left-3 top-1/2 -translate-y-1/2 text-secondary text-[18px]">search</span>
					<input class="w-full pl-9 pr-3 py-2 border border-stone rounded-input font-body-sm text-body-sm focus:border-ink focus:ring-1 focus:ring-ink/10 focus:outline-none bg-white text-ink transition-colors" name="search" placeholder={t('kanban.searchPlaceholder')} type="search" value={data.filters.search} />
				</div>
			</label>
			<div class="space-y-1">
				<span class="block font-label-caps text-label-caps text-secondary uppercase text-[11px] font-bold tracking-wider whitespace-nowrap truncate">{t('kanban.priority')}</span>
				<Dropdown
					name="priority"
					value={data.filters.priority}
					options={priorities.map(p => ({ value: p, label: p ? (t(`tickets.priority.${p}`) || p) : t('kanban.allPriorities') }))}
				/>
			</div>
			<div class="space-y-1">
				<span class="block font-label-caps text-label-caps text-secondary uppercase text-[11px] font-bold tracking-wider whitespace-nowrap truncate">{t('kanban.paymentStatus')}</span>
				<Dropdown
					name="paymentStatus"
					value={data.filters.paymentStatus}
					options={paymentStatuses.map(s => ({ value: s, label: s || t('kanban.allPaymentStatuses') }))}
				/>
			</div>
			<div class="space-y-1">
				<span class="block font-label-caps text-label-caps text-secondary uppercase text-[11px] font-bold tracking-wider whitespace-nowrap truncate">{t('kanban.approvalStatus')}</span>
				<Dropdown
					name="approvalStatus"
					value={data.filters.approvalStatus}
					options={approvalStatuses.map(s => ({ value: s, label: s ? t(`kanban.approval.${s}`) : t('kanban.allApprovalStatuses') }))}
				/>
			</div>
			<div class="space-y-1">
				<span class="block font-label-caps text-label-caps text-secondary uppercase text-[11px] font-bold tracking-wider whitespace-nowrap truncate">{t('kanban.technician')}</span>
				<Dropdown
					name="technicianUserId"
					value={data.filters.technicianUserId}
					options={[{ value: '', label: t('kanban.allTechnicians') }, ...data.staff.map(user => ({ value: user.id.toString(), label: user.fullName }))]}
				/>
			</div>
			<div class="space-y-1">
				<span class="block font-label-caps text-label-caps text-secondary uppercase text-[11px] font-bold tracking-wider whitespace-nowrap truncate">{t('kanban.terminalAge')}</span>
				<Dropdown
					name="terminalAge"
					value={terminalAge}
					onchange={(val) => { customTerminalAge = val; }}
					options={[
						{ value: '30', label: t('kanban.terminalAge.30') },
						{ value: '14', label: t('kanban.terminalAge.14') },
						{ value: '7', label: t('kanban.terminalAge.7') },
						{ value: 'all', label: t('kanban.terminalAge.all') }
					]}
				/>
			</div>
			<div class="space-y-1">
				<span class="block font-label-caps text-label-caps text-secondary uppercase text-[11px] font-bold tracking-wider whitespace-nowrap truncate">{t('reports.from')}</span>
				<DatePicker name="from" value={data.filters.from} clearable />
			</div>
			<div class="space-y-1">
				<span class="block font-label-caps text-label-caps text-secondary uppercase text-[11px] font-bold tracking-wider whitespace-nowrap truncate">{t('reports.to')}</span>
				<DatePicker name="to" value={data.filters.to} clearable />
			</div>
			<div class="flex items-center gap-2">
				<button class="w-full sm:w-auto h-10 px-4 rounded-button bg-ink text-white font-medium text-body-sm hover:bg-ink/90 active:scale-95 transition-all shadow-xs cursor-pointer flex items-center justify-center gap-1.5 shrink-0" type="submit">
					<span class="material-symbols-outlined text-[18px]">filter_alt</span>
					<span>{t('kanban.applyFilters')}</span>
				</button>
				{#if hasActiveFilters}
					<a
						href="?{data.filters.myTickets ? 'myTickets=true' : ''}"
						class="h-10 px-3 rounded-button border border-stone bg-white text-secondary hover:text-ink hover:bg-ash font-medium text-body-sm transition-all flex items-center justify-center gap-1 shrink-0 shadow-2xs"
						title={t('kanban.clearFilters')}
					>
						<span class="material-symbols-outlined text-[18px]">restart_alt</span>
						<span class="hidden sm:inline">{t('kanban.clearFilters')}</span>
					</a>
				{/if}
			</div>
		</div>
	</form>

	<div class="flex items-center justify-between">
		<p class="font-body-sm text-body-sm text-secondary">
			{t('tickets.list.showing')} <span class="font-bold text-primary font-mono">{totalTickets}</span>
			{#if activeOnly}
				<span class="ml-1 text-xs text-secondary/80 font-medium">({t('kanban.activeWorkbenchOnly')})</span>
			{/if}
		</p>
		<p class="hidden lg:flex items-center gap-1 font-body-sm text-[12px] text-secondary">
			<span class="material-symbols-outlined text-[15px]">touch_app</span>
			<span>{t('kanban.dragHint')}</span>
		</p>
	</div>

	<!-- Kanban Desktop Grid -->
	<div class="hidden lg:block overflow-x-auto bg-ash/50 p-md rounded-xl border border-stone">
		<div class="flex gap-md min-w-max pb-2 items-start">
			{#each displayedColumns as column (column.statusId)}
				{@const isTargetHovered = dragState.status === 'dragging' && dragState.hoveredStatusId === column.statusId}
				{@const isSourceCol = dragState.sourceStatusId === column.statusId}
				{@const isCollapsed = collapsedColumns.has(column.statusId)}
				{@const { visibleTickets, hiddenCount } = getColumnTickets(column)}

				{#if isCollapsed}
					<!-- Collapsed Column (Jira / Azure DevOps style 56px vertical strip) -->
					<div
						id={`kanban-col-${column.statusId}`}
						data-kanban-column-id={column.statusId}
						role="region"
						aria-label={column.name}
						class="w-14 min-w-[56px] min-h-[64vh] flex flex-col items-center py-3.5 bg-surface-container-lowest border rounded-xl shadow-2xs relative group transition-all duration-200 select-none {isTargetHovered && !isSourceCol ? 'border-ink ring-2 ring-ink/20 bg-ink/5' : 'border-stone hover:border-ink/50'}"
					>
						<button
							type="button"
							class="w-7 h-7 rounded-full border border-stone bg-white hover:bg-ash flex items-center justify-center text-secondary hover:text-ink transition-colors shadow-2xs cursor-pointer"
							title={t('kanban.expandColumn')}
							onclick={() => toggleColumnCollapse(column.statusId)}
						>
							<span class="material-symbols-outlined text-[16px]">right_panel_open</span>
						</button>

						<button
							type="button"
							class="flex-1 w-full flex flex-col items-center py-3 cursor-pointer bg-transparent border-0"
							onclick={() => toggleColumnCollapse(column.statusId)}
							title={t('kanban.expandColumn')}
						>
							<span class="w-2.5 h-2.5 rounded-full shrink-0" style={`background-color: ${column.color};`}></span>

							<span class="[writing-mode:vertical-rl] rotate-180 text-xs font-bold tracking-tight text-ink mt-3 py-2 truncate max-h-[300px]">
								{column.name}
							</span>

							{#if isTargetHovered && !isSourceCol}
								<div class="my-auto flex flex-col items-center gap-1 text-ink animate-pulse">
									<span class="material-symbols-outlined text-[20px]">south</span>
								</div>
							{/if}

							<span class="mt-auto px-2 py-0.5 rounded-full text-[11px] font-bold font-mono border border-stone/60 bg-ash/80 text-secondary shrink-0 {isTargetHovered && !isSourceCol ? 'border-ink/60 bg-ink text-white font-bold' : ''}">
								{column.tickets.length}
							</span>
						</button>
					</div>
				{:else}
					<!-- Expanded Column -->
					<section
						id={`kanban-col-${column.statusId}`}
						data-kanban-column-id={column.statusId}
						data-bento
						aria-label={column.name}
						class="w-80 min-h-[64vh] flex flex-col bg-surface-container-lowest border rounded-xl shadow-2xs relative group transition-all duration-200 {isTargetHovered && !isSourceCol ? 'border-ink ring-2 ring-ink/15 bg-ash/30' : 'border-stone'}"
						role="list"
					>
						<div class="flex items-center justify-between px-md py-3 sticky top-0 bg-white/95 backdrop-blur-md border-b border-stone rounded-t-xl z-10">
							<div class="flex items-center gap-2 min-w-0 mr-2">
								<span class="w-2.5 h-2.5 rounded-full shrink-0 relative flex">
									<span class="relative inline-flex rounded-full h-2.5 w-2.5" style={`background-color: ${column.color};`}></span>
								</span>
								<h3 class="font-body-base text-[13px] text-ink font-bold tracking-tight truncate">{column.name}</h3>
							</div>
							<div class="flex items-center gap-1.5 shrink-0">
								{#if hiddenCount > 0}
									<span class="text-[10px] font-mono font-bold text-secondary bg-ash px-2 py-0.5 rounded-full border border-stone/60" title={t('kanban.showingRecent', { shown: visibleTickets.length, total: column.tickets.length })}>
										{visibleTickets.length}/{column.tickets.length}
									</span>
								{:else}
									<span class="px-2 py-0.5 rounded-full text-[11px] font-bold font-mono border border-stone/60 bg-ash/80 text-secondary shrink-0 {isTargetHovered && !isSourceCol ? 'border-ink/60 bg-ink text-white font-bold' : ''}">
										{column.tickets.length}
									</span>
								{/if}
								<button
									type="button"
									class="w-6 h-6 rounded-md hover:bg-ash flex items-center justify-center text-secondary/70 hover:text-ink transition-colors cursor-pointer"
									title={t('kanban.collapseColumn')}
									onclick={() => toggleColumnCollapse(column.statusId)}
								>
									<span class="material-symbols-outlined text-[16px]">left_panel_close</span>
								</button>
							</div>
						</div>

						<div class="flex-1 space-y-2.5 p-2.5">
							{#each visibleTickets as ticket (ticket.id)}
								{@const isCurrentDragging = (dragState.status === 'dragging' || dragState.status === 'settling') && dragState.ticket?.id === ticket.id}
								{@const isPicked = dragState.status === 'down' && dragState.ticket?.id === ticket.id}
								{@const isFailed = failedTicketId === ticket.id}

								<div role="listitem" class="relative">
									{#if isCurrentDragging}
										<!-- Dashed placeholder slot in origin column -->
										<div
											class="w-full border-2 border-dashed border-stone/80 bg-stone/20 rounded-lg"
											style={`height: ${dragState.cardRect.height || 116}px;`}
											aria-hidden="true"
										></div>
									{:else}
										<a
											use:tilt
											draggable="false"
											ondragstart={(e) => e.preventDefault()}
											class="block bg-white border rounded-lg p-3.5 shadow-2xs hover:border-ink/40 transition-all cursor-grab active:cursor-grabbing select-none group/card {isFailed ? 'border-red-500 ring-2 ring-red-500/20 animate-card-shake' : 'border-stone'} {isPicked ? 'scale-[1.03] border-ink/40 z-30' : ''}"
											href="/app/tickets/{ticket.id}"
											style="touch-action: none;"
											onpointerdown={(event) => handlePointerDown(event, ticket, column.statusId)}
											onclick={handleCardClick}
										>
											{@render ticketCardContent(ticket)}
										</a>
									{/if}
								</div>
							{/each}

							{#if isTargetHovered && !isSourceCol}
								<!-- Animated drop target slot inside destination column -->
								<div
									class="w-full border-2 border-dashed border-ink/40 bg-ink/5 rounded-lg py-3.5 px-3 flex flex-col items-center justify-center gap-1 text-center transition-all animate-pulse"
									style={`min-height: ${dragState.cardRect.height || 96}px;`}
									aria-hidden="true"
								>
									<span class="material-symbols-outlined text-ink/70 text-[20px]">south</span>
									<span class="text-xs font-semibold text-ink/90">{t('kanban.dropHere')}</span>
								</div>
							{/if}

							<!-- Show Older Tickets Expander -->
							{#if hiddenCount > 0}
								<button
									type="button"
									class="w-full py-2 px-3 border border-dashed border-stone rounded-lg text-xs font-semibold text-secondary hover:text-ink hover:border-ink/40 bg-white/60 hover:bg-white flex items-center justify-center gap-1.5 transition-all shadow-2xs cursor-pointer"
									onclick={() => toggleColumnUnhide(column.statusId)}
								>
									<span class="material-symbols-outlined text-[16px]">history</span>
									<span>{t('kanban.showOlderTickets', { count: hiddenCount })}</span>
								</button>
							{:else if isTerminalStatus(column) && unhiddenColumns.has(column.statusId) && column.tickets.length > visibleTickets.length}
								<button
									type="button"
									class="w-full py-1.5 px-3 border border-stone/50 rounded-lg text-[11px] font-medium text-secondary hover:text-ink bg-ash/40 hover:bg-ash flex items-center justify-center gap-1 transition-all cursor-pointer"
									onclick={() => toggleColumnUnhide(column.statusId)}
								>
									<span class="material-symbols-outlined text-[14px]">unfold_less</span>
									<span>{t('kanban.hideOlderTickets')}</span>
								</button>
							{/if}

							{#if visibleTickets.length === 0 && !(isTargetHovered && !isSourceCol)}
								<div class="border border-dashed border-stone/80 rounded-lg p-5 text-center text-secondary text-xs flex flex-col items-center justify-center gap-1 bg-white/40">
									<span class="material-symbols-outlined text-[18px] text-secondary/60">drag_indicator</span>
									<span>{t('kanban.empty')}</span>
								</div>
							{/if}
						</div>
					</section>
				{/if}
			{/each}
		</div>
	</div>

	<!-- Mobile / Small Screens Accordion View -->
	<div class="lg:hidden space-y-md">
		{#each displayedColumns as column (column.statusId)}
			{@const { visibleTickets, hiddenCount } = getColumnTickets(column)}
			<section class="bg-surface-container-lowest border border-stone rounded-xl overflow-hidden shadow-2xs">
				<div class="flex items-center justify-between px-md py-sm bg-surface-container-low border-b border-stone">
					<div class="flex items-center gap-2">
						<span class="w-2.5 h-2.5 rounded-full" style={`background-color: ${column.color};`}></span>
						<h3 class="font-subheading text-body-sm font-bold text-ink">{column.name}</h3>
					</div>
					<div class="flex items-center gap-1.5">
						{#if hiddenCount > 0}
							<span class="px-2 py-0.5 rounded-full text-caption font-mono font-bold bg-white border border-stone text-secondary">
								{visibleTickets.length}/{column.tickets.length}
							</span>
						{:else}
							<span class="px-2 py-0.5 rounded-full text-caption font-mono font-bold bg-white border border-stone text-secondary">
								{column.tickets.length}
							</span>
						{/if}
					</div>
				</div>
				<div class="divide-y divide-stone">
					{#if visibleTickets.length === 0}
						<p class="p-md text-body-sm text-secondary text-center">{t('kanban.empty')}</p>
					{:else}
						{#each visibleTickets as ticket (ticket.id)}
							{@const isFailed = failedTicketId === ticket.id}
							<div class="p-md hover:bg-ash/60 transition-colors {isFailed ? 'bg-red-50/50 animate-card-shake' : ''}">
								<div class="flex items-start justify-between gap-md mb-1">
									<div class="flex items-center gap-2">
										<a href="/app/tickets/{ticket.id}" class="font-mono font-bold text-ink text-body-sm hover:underline">
											{ticket.ticketNumber}
										</a>
										<span class="px-1.5 py-0.5 rounded text-[10px] font-bold uppercase tracking-wider border {priorityClass(ticket.priority)}">
											{t(`tickets.priority.${ticket.priority}`) || ticket.priority}
										</span>
									</div>
									<span class="text-[11px] text-secondary font-mono">
										{formatDate(ticket.createdAt, data.locale, { dateStyle: 'medium' })}
									</span>
								</div>
								<a href="/app/tickets/{ticket.id}" class="block group">
									<h4 class="font-body-base text-body-sm font-semibold text-ink group-hover:text-primary transition-colors">{ticket.device}</h4>
									<p class="text-caption text-secondary line-clamp-1 mt-0.5">
										<span class="font-medium text-ink/80">{ticket.customerName}</span> · {ticket.complaint}
									</p>
								</a>
								{#if ticket.waitingApproval || (features.showBillingAndPayments && ticket.unpaid) || ticket.blocked || ticket.overdue}
									<div class="flex flex-wrap gap-1 mt-2">
										{#if ticket.waitingApproval}<GlowingBadge status="Waiting Approval">{t('kanban.waitingApproval')}</GlowingBadge>{/if}
										{#if features.showBillingAndPayments && ticket.unpaid}<GlowingBadge status="Unpaid">{t('kanban.unpaid')}</GlowingBadge>{/if}
										{#if ticket.blocked}<GlowingBadge status="Cancelled">{t('kanban.blocked')}</GlowingBadge>{/if}
										{#if ticket.overdue}<GlowingBadge status="Unpaid">{t('kanban.overdue')}</GlowingBadge>{/if}
									</div>
								{/if}
								<!-- Mobile Quick Move Action -->
								<div class="flex items-center justify-between mt-3 pt-2.5 border-t border-stone/60">
									<div class="text-[11px] text-secondary flex items-center gap-1 font-mono">
										<span class="material-symbols-outlined text-[14px]">person</span>
										<span>{ticket.assignedTechnician ?? t('tickets.detail.unassigned')}</span>
									</div>
									<button
										type="button"
										class="px-2.5 py-1 text-xs font-semibold rounded-button border border-stone bg-white hover:bg-ash active:scale-95 transition-all text-ink flex items-center gap-1 shadow-2xs cursor-pointer"
										onclick={() => openMobileMove(ticket, column.statusId)}
									>
										<span class="material-symbols-outlined text-[15px]">swap_horiz</span>
										<span>{t('kanban.moveStatus')}</span>
									</button>
								</div>
							</div>
						{/each}
						{#if hiddenCount > 0}
							<div class="p-md bg-ash/30">
								<button
									type="button"
									class="w-full py-2 px-3 border border-dashed border-stone rounded-lg text-xs font-semibold text-secondary hover:text-ink hover:border-ink/40 bg-white flex items-center justify-center gap-1.5 transition-all shadow-2xs cursor-pointer"
									onclick={() => toggleColumnUnhide(column.statusId)}
								>
									<span class="material-symbols-outlined text-[16px]">history</span>
									<span>{t('kanban.showOlderTickets', { count: hiddenCount })}</span>
								</button>
							</div>
						{/if}
					{/if}
				</div>
			</section>
		{/each}
	</div>
</div>

<!-- Floating Card Clone with Spring Physics & Dynamic Tilt -->
{#if (dragState.status === 'dragging' || dragState.status === 'settling') && dragState.ticket}
	{@const isSettling = dragState.status === 'settling'}
	{@const posX = isSettling ? dragState.cardRect.left : dragState.currentX - dragState.grabOffsetX}
	{@const posY = isSettling ? dragState.cardRect.top : dragState.currentY - dragState.grabOffsetY}
	{@const tiltDeg = isSettling ? 0 : dragState.tilt}
	{@const cardScale = isSettling ? 1.0 : 1.035}
	<div
		class="fixed pointer-events-none z-[9999]"
		style="
			left: 0;
			top: 0;
			width: {dragState.cardRect.width}px;
			transform: translate3d({posX}px, {posY}px, 0) scale({cardScale}) rotate({tiltDeg}deg);
			transform-origin: {dragState.grabOffsetX}px {dragState.grabOffsetY}px;
			transition: {isSettling ? 'transform 200ms cubic-bezier(0.16, 1, 0.3, 1), box-shadow 200ms ease' : 'none'};
			box-shadow: 0 32px 64px rgba(0, 0, 0, 0.24), 0 16px 32px rgba(0, 0, 0, 0.12);
			border-radius: 0.5rem;
		"
	>
		<div class="bg-white border border-ink/40 rounded-lg p-3.5 shadow-lg">
			{@render ticketCardContent(dragState.ticket)}
		</div>
	</div>
{/if}

<!-- Mobile Quick-Move Modal Sheet -->
{#if mobileModal.open && mobileModal.ticket}
	<div
		class="fixed inset-0 bg-ink/40 backdrop-blur-xs z-50 flex items-end sm:items-center justify-center p-0 sm:p-4"
		role="dialog"
		aria-modal="true"
		aria-labelledby="mobile-move-title"
	>
		<!-- Backdrop click to dismiss -->
		<button
			type="button"
			class="fixed inset-0 cursor-default bg-transparent border-0 w-full h-full"
			onclick={closeMobileMove}
			aria-label={t('common.close')}
			tabindex="-1"
		></button>

		<div
			class="relative bg-white w-full sm:max-w-md rounded-t-2xl sm:rounded-2xl border border-stone shadow-xl overflow-hidden p-5 space-y-4 max-h-[85vh] flex flex-col z-10"
		>
			<div class="flex items-center justify-between pb-3 border-b border-stone">
				<div class="min-w-0 pr-2">
					<h3 id="mobile-move-title" class="font-subheading text-body-base font-bold text-ink">
						{t('kanban.moveStatus')}
					</h3>
					<p class="text-xs text-secondary truncate mt-0.5">
						<span class="font-mono font-bold text-ink">{mobileModal.ticket.ticketNumber}</span>
						<span> · {mobileModal.ticket.device}</span>
					</p>
				</div>
				<button
					type="button"
					class="w-8 h-8 rounded-full border border-stone hover:bg-ash flex items-center justify-center text-secondary hover:text-ink transition-colors cursor-pointer shrink-0"
					onclick={closeMobileMove}
					aria-label={t('common.close')}
				>
					<span class="material-symbols-outlined text-[18px]">close</span>
				</button>
			</div>

			<div class="space-y-2 overflow-y-auto flex-1 pr-1">
				<p class="text-xs font-bold uppercase tracking-wider text-secondary px-1">
					{t('kanban.selectStatus')}
				</p>
				<div class="space-y-1.5">
					{#each columns as col}
						{@const isCurrent = col.statusId === mobileModal.sourceStatusId}
						<button
							type="button"
							disabled={isCurrent}
							class="w-full flex items-center justify-between p-3 rounded-xl border text-left transition-all {isCurrent ? 'border-stone/60 bg-ash/40 opacity-60 cursor-default' : 'border-stone hover:border-ink hover:bg-ash/50 active:scale-98 cursor-pointer shadow-2xs'}"
							onclick={() => handleMobileStatusSelect(col.statusId)}
						>
							<div class="flex items-center gap-2.5 min-w-0">
								<span class="w-3 h-3 rounded-full shrink-0" style={`background-color: ${col.color};`}></span>
								<span class="font-body-base text-body-sm font-semibold text-ink truncate">{col.name}</span>
								{#if isCurrent}
									<span class="text-[10px] font-bold uppercase tracking-wider px-1.5 py-0.5 rounded bg-ash text-secondary border border-stone">
										{t('kanban.currentStatus')}
									</span>
								{/if}
							</div>
							<span class="text-caption font-mono font-bold text-secondary px-2 py-0.5 rounded-full bg-white border border-stone shrink-0">
								{col.tickets.length}
							</span>
						</button>
					{/each}
				</div>
			</div>

			<div class="pt-3 border-t border-stone flex justify-end">
				<button
					type="button"
					class="px-4 py-2 rounded-button border border-stone text-body-sm font-medium hover:bg-ash transition-colors cursor-pointer"
					onclick={closeMobileMove}
				>
					{t('common.cancel')}
				</button>
			</div>
		</div>
	</div>
{/if}

{#snippet ticketCardContent(ticket: KanbanTicket)}
	<div class="flex items-start justify-between gap-2 mb-1.5">
		<span class="px-1.5 py-0.5 bg-ash/80 border border-stone/60 text-[11px] font-bold font-mono text-ink rounded">
			{ticket.ticketNumber}
		</span>
		<span class="px-1.5 py-0.5 rounded text-[10px] font-bold uppercase tracking-wider border {priorityClass(ticket.priority)}">
			{t(`tickets.priority.${ticket.priority}`) || ticket.priority}
		</span>
	</div>
	<h4 class="font-body-base text-body-sm font-semibold text-ink leading-snug group-hover/card:text-primary transition-colors">
		{ticket.device}
	</h4>
	<p class="text-xs text-secondary line-clamp-2 mt-0.5">
		<span class="font-medium text-ink/80">{ticket.customerName}</span> · {ticket.complaint}
	</p>
	{#if ticket.waitingApproval || (features.showBillingAndPayments && ticket.unpaid) || ticket.blocked || ticket.overdue}
		<div class="flex flex-wrap gap-1 my-2">
			{#if ticket.waitingApproval}<GlowingBadge status="Waiting Approval">{t('kanban.waitingApproval')}</GlowingBadge>{/if}
			{#if features.showBillingAndPayments && ticket.unpaid}<GlowingBadge status="Unpaid">{t('kanban.unpaid')}</GlowingBadge>{/if}
			{#if ticket.blocked}<GlowingBadge status="Cancelled">{t('kanban.blocked')}</GlowingBadge>{/if}
			{#if ticket.overdue}<GlowingBadge status="Unpaid">{t('kanban.overdue')}</GlowingBadge>{/if}
		</div>
	{/if}
	<div class="flex items-center justify-between pt-2 mt-2 border-t border-stone/60">
		<span class="text-[11px] text-secondary flex items-center gap-1 font-mono">
			<span class="material-symbols-outlined text-[14px]">schedule</span>
			<span>{formatDate(ticket.createdAt, data.locale, { dateStyle: 'medium' })}</span>
		</span>
		<div class="h-5 min-w-5 px-1 rounded-full border border-stone bg-ash flex items-center justify-center text-[9px] font-bold text-ink" title={ticket.assignedTechnician ?? t('tickets.detail.unassigned')}>
			{ticket.assignedTechnician?.slice(0, 2).toUpperCase() ?? '--'}
		</div>
	</div>
{/snippet}

<style>
	@keyframes card-shake {
		0%, 100% {
			transform: translateX(0);
		}
		20%, 60% {
			transform: translateX(-5px);
		}
		40%, 80% {
			transform: translateX(5px);
		}
	}
	.animate-card-shake {
		animation: card-shake 0.4s ease-in-out;
	}
</style>
