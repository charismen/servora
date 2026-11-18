<script lang="ts">
	import { enhance } from '$app/forms';
	import { goto } from '$app/navigation';
	import { can, permissions } from '$lib/auth/permissions';
	import PageHeader from '$lib/components/PageHeader.svelte';
	import CurrencyInput from '$lib/components/CurrencyInput.svelte';
	import DatePicker from '$lib/components/ui/date-picker/DatePicker.svelte';
	import Dropdown from '$lib/components/ui/dropdown/Dropdown.svelte';
	import { createTranslator, formatCurrency } from '$lib/i18n';
	import { fade } from 'svelte/transition';
	import type { WorkerPayrollRow, BonusEvaluationResult } from '$lib/types/payroll';
	import type { ActionData, PageData } from './$types';

	let { data, form }: { data: PageData; form: ActionData } = $props();
	const t = $derived(createTranslator(data.locale));
	const canManage = $derived(can(data.user.permissions, permissions.payrollManage));
	const currencyCode = $derived(data.shopProfile?.currencyCode ?? 'IDR');

	let filterCycle = $state<'weekly' | 'monthly'>('monthly');
	let filterFrom = $state('');
	let filterTo = $state('');
	let filterRoleId = $state('');
	let filterIncludeFullTime = $state(false);

	let adjustmentModalOpen = $state(false);
	let activeWorker = $state<WorkerPayrollRow | null>(null);
	let adjustmentAmount = $state<number | string>('');
	let adjustmentReason = $state('');
	let adjustmentSubmitting = $state(false);
	let adjustmentError = $state('');
	let adjustmentCurrencyRef = $state<{ focus: () => Promise<void> } | null>(null);

	const isDeduction = $derived(String(adjustmentAmount ?? '').trim().startsWith('-'));
	const parsedAdjustmentAmount = $derived.by(() => {
		const raw = String(adjustmentAmount ?? '').trim();
		if (!raw || raw === '-') return 0;
		const num = Number(raw);
		return Number.isFinite(num) ? num : 0;
	});
	const adjustmentTone = $derived<'default' | 'positive' | 'negative'>(
		isDeduction ? 'negative' : parsedAdjustmentAmount > 0 ? 'positive' : 'default'
	);
	const projectedFinalPayable = $derived(
		activeWorker ? activeWorker.finalPayableAmount + parsedAdjustmentAmount : 0
	);

	let ticketModalOpen = $state(false);
	let activeBonusTickets = $state<{
		workerName: string;
		bonusName: string;
		bonusAmount: number;
		formulaSummary: string;
		referenceNumbers: string[];
	} | null>(null);
	let ticketSearchQuery = $state('');
	let copiedTicket = $state<string | null>(null);

	const summaryTotals = $derived(
		data.summary?.totals ?? {
			totalFreelanceWorkers: 0,
			totalDaysWorked: 0,
			totalApprovedHours: 0,
			totalPendingShifts: 0,
			totalBaseWages: 0,
			totalBonuses: 0,
			totalAdjustments: 0,
			totalPayableAmount: 0
		}
	);
	const workers = $derived(data.summary?.workers ?? []);

	$effect(() => {
		filterCycle = data.filters.cycle;
		filterFrom = data.filters.from;
		filterTo = data.filters.to;
		filterRoleId = data.filters.roleId;
		filterIncludeFullTime = data.filters.includeFullTime;
	});

	const hasActiveFilters = $derived(
		Boolean(filterRoleId || filterIncludeFullTime)
	);

	const exportQueryString = $derived.by(() => {
		const query = new URLSearchParams({
			from: filterFrom,
			to: filterTo,
			cycle: filterCycle,
			includeFullTime: String(filterIncludeFullTime)
		});
		if (filterRoleId) query.set('roleId', filterRoleId);
		return query.toString();
	});

	function selectCyclePreset(cycle: 'weekly' | 'monthly') {
		filterCycle = cycle;
		const now = new Date();
		if (cycle === 'weekly') {
			const dayOfWeek = now.getDay();
			const diffToMonday = (dayOfWeek + 6) % 7;
			const monday = new Date(now);
			monday.setDate(now.getDate() - diffToMonday);
			const sunday = new Date(monday);
			sunday.setDate(monday.getDate() + 6);
			filterFrom = `${monday.getFullYear()}-${String(monday.getMonth() + 1).padStart(2, '0')}-${String(monday.getDate()).padStart(2, '0')}`;
			filterTo = `${sunday.getFullYear()}-${String(sunday.getMonth() + 1).padStart(2, '0')}-${String(sunday.getDate()).padStart(2, '0')}`;
		} else {
			const lastDay = new Date(now.getFullYear(), now.getMonth() + 1, 0).getDate();
			filterFrom = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-01`;
			filterTo = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(lastDay).padStart(2, '0')}`;
		}
		applyFilters();
	}

	function resetFilters() {
		filterRoleId = '';
		filterIncludeFullTime = false;
		applyFilters();
	}

	function toggleIncludeFullTime() {
		filterIncludeFullTime = true;
		applyFilters();
	}

	function applyFilters() {
		const query = new URLSearchParams({
			from: filterFrom,
			to: filterTo,
			cycle: filterCycle,
			includeFullTime: String(filterIncludeFullTime)
		});
		if (filterRoleId) query.set('roleId', filterRoleId);
		goto(`/app/payroll?${query.toString()}`);
	}

	function openAdjustmentModal(worker: WorkerPayrollRow) {
		activeWorker = worker;
		adjustmentAmount = '';
		adjustmentReason = '';
		adjustmentError = '';
		adjustmentSubmitting = false;
		adjustmentModalOpen = true;
		adjustmentCurrencyRef?.focus();
	}

	function setAdjustmentDirection(direction: 'bonus' | 'deduction') {
		adjustmentError = '';
		const raw = String(adjustmentAmount ?? '').trim();
		const unsigned = raw.replace(/^[+-]+/, '');
		if (direction === 'deduction') {
			adjustmentAmount = unsigned ? `-${unsigned}` : '-';
		} else {
			adjustmentAmount = unsigned;
		}
		adjustmentCurrencyRef?.focus();
	}

	function openTicketModal(worker: WorkerPayrollRow, bonus: BonusEvaluationResult) {
		activeBonusTickets = {
			workerName: worker.fullName,
			bonusName: bonus.bonusName,
			bonusAmount: bonus.totalBonusAmount,
			formulaSummary: bonus.formulaSummary,
			referenceNumbers: bonus.referenceNumbers
		};
		ticketSearchQuery = '';
		ticketModalOpen = true;
	}

	function copyTicketNumber(ticketNum: string) {
		if (typeof navigator !== 'undefined' && navigator.clipboard) {
			navigator.clipboard.writeText(ticketNum);
			copiedTicket = ticketNum;
			setTimeout(() => {
				if (copiedTicket === ticketNum) copiedTicket = null;
			}, 2000);
		}
	}

	const filteredTickets = $derived.by(() => {
		if (!activeBonusTickets) return [];
		const q = ticketSearchQuery.trim().toLowerCase();
		if (!q) return activeBonusTickets.referenceNumbers;
		return activeBonusTickets.referenceNumbers.filter((num) => num.toLowerCase().includes(q));
	});
</script>

<svelte:window
	onkeydown={(e) => {
		if (e.key === 'Escape') {
			if (!adjustmentSubmitting) adjustmentModalOpen = false;
			ticketModalOpen = false;
		}
	}}
/>

<svelte:head>
	<title>{t('payroll.title')} · {t('app.title')}</title>
</svelte:head>

<div class="space-y-lg pb-xl">
	<!-- Page Header with Standardized Action Snippet -->
	<PageHeader
		items={[
			{ label: t('common.console'), href: '/app' },
			{ label: t('nav.payroll') }
		]}
		title={t('payroll.title')}
		subtitle={t('payroll.subtitle')}
	>
		{#snippet actions()}
			<div class="flex flex-wrap items-center gap-2">
				<!-- Segmented Cycle Toggle -->
				<div class="inline-flex rounded-input border border-stone/80 bg-ash/40 p-1 text-caption font-medium shadow-2xs">
					<button
						type="button"
						class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-button text-body-sm font-medium transition-all shrink-0 cursor-pointer {filterCycle === 'weekly' ? 'bg-white text-ink font-semibold shadow-xs' : 'text-secondary hover:text-ink'}"
						onclick={() => selectCyclePreset('weekly')}
					>
						<span class="material-symbols-outlined text-[16px]">view_week</span>
						<span>{t('payroll.cycleWeekly')}</span>
					</button>
					<button
						type="button"
						class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-button text-body-sm font-medium transition-all shrink-0 cursor-pointer {filterCycle === 'monthly' ? 'bg-white text-ink font-semibold shadow-xs' : 'text-secondary hover:text-ink'}"
						onclick={() => selectCyclePreset('monthly')}
					>
						<span class="material-symbols-outlined text-[16px]">calendar_month</span>
						<span>{t('payroll.cycleMonthly')}</span>
					</button>
				</div>

				<a
					class="inline-flex h-9 items-center gap-1.5 rounded-button border border-stone bg-white px-3.5 text-body-sm font-medium text-ink hover:bg-ash transition-all shadow-2xs shrink-0 {workers.length === 0 ? 'pointer-events-none opacity-40' : ''}"
					href={`/app/payroll/export?${exportQueryString}`}
					aria-disabled={workers.length === 0}
				>
					<span class="material-symbols-outlined text-[18px]">download</span>
					<span>{t('payroll.exportCsv')}</span>
				</a>
			</div>
		{/snippet}
	</PageHeader>

	<!-- Summary Totals Overview Cards -->
	<div class="grid gap-md grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-5">
		<div data-bento class="rounded-xl border border-stone bg-surface-container-lowest p-4 shadow-2xs flex flex-col justify-between gap-2.5 min-w-0 {workers.length === 0 ? 'opacity-85' : ''}">
			<div class="flex items-center justify-between gap-2 min-w-0">
				<span class="font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider truncate" title={t('payroll.totalWorkers')}>{t('payroll.totalWorkers')}</span>
				<span class="material-symbols-outlined text-secondary text-[18px] shrink-0">group</span>
			</div>
			<div class="font-mono tabular-nums text-xl lg:text-[22px] font-bold tracking-tight text-ink leading-tight truncate">{summaryTotals.totalFreelanceWorkers}</div>
		</div>

		<div data-bento class="rounded-xl border border-stone bg-surface-container-lowest p-4 shadow-2xs flex flex-col justify-between gap-2.5 min-w-0 {summaryTotals.totalPendingShifts > 0 ? 'border-amber-300 bg-amber-50/15' : ''} {workers.length === 0 ? 'opacity-85' : ''}">
			<div class="flex items-center justify-between gap-2 min-w-0">
				<span class="font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider truncate" title={t('payroll.totalApprovedHours')}>{t('payroll.totalApprovedHours')}</span>
				<span class="material-symbols-outlined text-secondary text-[18px] shrink-0">schedule</span>
			</div>
			<div class="min-w-0">
				<div class="font-mono tabular-nums text-xl lg:text-[22px] font-bold tracking-tight text-ink leading-tight truncate">
					{summaryTotals.totalApprovedHours.toFixed(1)} <span class="text-xs text-secondary font-sans font-normal">{t('payroll.hours')}</span>
				</div>
				{#if summaryTotals.totalPendingShifts > 0}
					<span class="mt-1.5 inline-flex items-center gap-1 text-[11px] font-bold uppercase tracking-wider text-amber-800 bg-amber-100/70 border border-amber-300 px-1.5 py-0.5 rounded truncate max-w-full">
						<span class="material-symbols-outlined text-[13px] shrink-0">warning</span>
						<span class="truncate">{summaryTotals.totalPendingShifts} {t('payroll.pendingShiftsWarning')}</span>
					</span>
				{/if}
			</div>
		</div>

		<div data-bento class="rounded-xl border border-stone bg-surface-container-lowest p-4 shadow-2xs flex flex-col justify-between gap-2.5 min-w-0 {workers.length === 0 ? 'opacity-85' : ''}">
			<div class="flex items-center justify-between gap-2 min-w-0">
				<span class="font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider truncate" title={t('payroll.totalBaseWages')}>{t('payroll.totalBaseWages')}</span>
				<span class="material-symbols-outlined text-secondary text-[18px] shrink-0">account_balance_wallet</span>
			</div>
			<div class="font-mono tabular-nums text-lg xl:text-[20px] font-bold tracking-tight text-ink leading-tight truncate" title={formatCurrency(summaryTotals.totalBaseWages, data.locale, currencyCode)}>
				{formatCurrency(summaryTotals.totalBaseWages, data.locale, currencyCode)}
			</div>
		</div>

		<div data-bento class="rounded-xl border border-stone bg-surface-container-lowest p-4 shadow-2xs flex flex-col justify-between gap-2.5 min-w-0 {workers.length === 0 ? 'opacity-85' : ''}">
			<div class="flex items-center justify-between gap-2 min-w-0">
				<span class="font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider truncate" title={t('payroll.totalBonuses')}>{t('payroll.totalBonuses')}</span>
				<span class="material-symbols-outlined text-emerald-700 text-[18px] shrink-0">military_tech</span>
			</div>
			<div class="font-mono tabular-nums text-lg xl:text-[20px] font-bold tracking-tight text-emerald-700 leading-tight truncate" title={formatCurrency(summaryTotals.totalBonuses, data.locale, currencyCode)}>
				{formatCurrency(summaryTotals.totalBonuses, data.locale, currencyCode)}
			</div>
		</div>

		<div data-bento class="rounded-xl border border-stone bg-surface-container-lowest p-4 shadow-2xs flex flex-col justify-between gap-2.5 sm:col-span-2 lg:col-span-2 xl:col-span-1 min-w-0 {workers.length === 0 ? 'opacity-85' : ''}">
			<div class="flex items-center justify-between gap-2 min-w-0">
				<span class="font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider truncate" title={t('payroll.totalPayable')}>{t('payroll.totalPayable')}</span>
				<span class="material-symbols-outlined text-ink text-[18px] shrink-0">payments</span>
			</div>
			<div class="font-mono tabular-nums text-xl lg:text-[22px] font-bold tracking-tight text-ink leading-tight truncate" title={formatCurrency(summaryTotals.totalPayableAmount, data.locale, currencyCode)}>
				{formatCurrency(summaryTotals.totalPayableAmount, data.locale, currencyCode)}
			</div>
		</div>
	</div>

	<!-- Standardized Filter Bar -->
	<form
		class="rounded-xl border border-stone bg-surface-container-lowest p-md shadow-2xs !overflow-visible relative z-30"
		onsubmit={(event) => {
			event.preventDefault();
			applyFilters();
		}}
	>
		<div class="grid gap-md grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:flex xl:flex-wrap xl:items-end items-end !overflow-visible">
			<div class="space-y-1 block min-w-0 xl:w-[175px] xl:shrink-0">
				<span class="block font-label-caps text-secondary uppercase text-[11px] font-bold tracking-wider">{t('attendance.filterFrom')}</span>
				<DatePicker name="from" bind:value={filterFrom} clearable />
			</div>
			<div class="space-y-1 block min-w-0 xl:w-[175px] xl:shrink-0">
				<span class="block font-label-caps text-secondary uppercase text-[11px] font-bold tracking-wider">{t('attendance.filterTo')}</span>
				<DatePicker name="to" bind:value={filterTo} clearable />
			</div>
			<div class="space-y-1 min-w-0 xl:w-[200px] xl:shrink-0">
				<span class="block font-label-caps text-secondary uppercase text-[11px] font-bold tracking-wider">{t('attendance.filterRole')}</span>
				<Dropdown
					bind:value={filterRoleId}
					options={[
						{ value: '', label: t('attendance.allRoles') },
						...data.roles.map((role) => ({ value: String(role.id), label: role.name }))
					]}
				/>
			</div>
			<div class="flex h-10 items-center sm:col-span-1 lg:col-span-2 xl:pl-1 min-w-0">
				<label class="inline-flex items-center gap-2 text-body-sm font-medium text-ink cursor-pointer select-none">
					<input type="checkbox" bind:checked={filterIncludeFullTime} class="rounded border-stone text-ink focus:ring-ink" />
					<span class="whitespace-nowrap">{t('payroll.includeFullTime')}</span>
				</label>
			</div>
			<div class="flex items-center justify-end gap-2 sm:col-span-2 lg:col-span-1 xl:ml-auto shrink-0">
				{#if hasActiveFilters}
					<button
						type="button"
						class="h-10 px-3 flex-1 sm:flex-none rounded-button border border-stone bg-white text-secondary hover:text-ink hover:bg-ash font-medium text-body-sm transition-all flex items-center justify-center gap-1 shrink-0 shadow-2xs cursor-pointer"
						onclick={resetFilters}
						title={t('payroll.clearFilters')}
					>
						<span class="material-symbols-outlined text-[18px]">restart_alt</span>
						<span>{t('payroll.clearFilters')}</span>
					</button>
				{/if}
				<button
					class="h-10 px-5 flex-1 sm:flex-none rounded-button bg-ink text-white font-medium text-body-sm hover:bg-ink/90 active:scale-95 transition-all shadow-xs cursor-pointer flex items-center justify-center gap-1.5 shrink-0"
					type="submit"
				>
					<span class="material-symbols-outlined text-[18px]">filter_alt</span>
					<span>{t('reports.apply')}</span>
				</button>
			</div>
		</div>
	</form>

	<!-- High-Density Payroll Summary Table -->
	<div data-bento class="rounded-xl border border-stone bg-surface-container-lowest shadow-2xs overflow-hidden">
		<div class="overflow-x-auto custom-scrollbar">
			<table class="w-full min-w-[960px] text-left border-collapse">
				<thead>
					<tr class="border-b border-stone bg-surface-container-low text-[11px] font-bold uppercase tracking-wider text-secondary">
						<th class="px-md py-4 font-label-caps w-[220px] min-w-[200px]">{t('payroll.colWorker')}</th>
						<th class="px-md py-4 font-label-caps w-[110px] min-w-[100px] whitespace-nowrap">{t('payroll.colDaysHours')}</th>
						<th class="px-md py-4 font-label-caps w-[200px] min-w-[180px]">{t('payroll.colBaseWage')}</th>
						<th class="px-md py-4 font-label-caps w-[240px] min-w-[220px]">{t('payroll.colBonuses')}</th>
						<th class="px-md py-4 font-label-caps w-[220px] max-w-[240px]">{t('payroll.colAdjustments')}</th>
						<th class="px-md py-4 font-label-caps w-[130px] min-w-[120px] text-right whitespace-nowrap">{t('payroll.colFinalPayable')}</th>
						<th class="px-md py-4 font-label-caps w-[110px] min-w-[100px] text-right whitespace-nowrap">{t('payroll.colActions')}</th>
					</tr>
				</thead>
				<tbody class="divide-y divide-stone font-body-sm text-body-sm">
					{#each workers as worker (worker.shopUserId)}
						<tr class="align-top hover:bg-ash/40 transition-colors">
							<!-- Worker & Role -->
							<td class="px-md py-4 w-[220px] min-w-[200px]">
								<div class="flex items-start gap-2.5">
									<div class="w-8 h-8 rounded-full border border-stone bg-ash flex items-center justify-center font-bold text-xs text-ink shrink-0 mt-0.5">
										{worker.fullName.slice(0, 2).toUpperCase()}
									</div>
									<div class="min-w-0">
										<div class="font-semibold text-ink leading-tight">{worker.fullName}</div>
										<div class="text-xs text-secondary font-mono tracking-tight">{worker.email}</div>
										<div class="mt-1.5 flex flex-wrap items-center gap-1">
											<span
												class="rounded px-1.5 py-0.5 text-[10.5px] font-bold uppercase tracking-wider border {worker.employmentType === 'Freelance' ? 'border-amber-300 bg-amber-500/10 text-amber-800' : 'border-stone bg-ash text-secondary'}"
											>
												{t(`settings.employmentType.${worker.employmentType}`)}
											</span>
											{#each worker.roles as role (role)}
												<span class="rounded border border-stone/80 bg-white px-1.5 py-0.5 text-[10.5px] font-medium text-secondary">
													{role}
												</span>
											{/each}
										</div>
										<div class="mt-1 text-[11px] text-secondary">
											{t(`settings.basePayMode.${worker.basePayMode}`)}
										</div>
									</div>
								</div>
							</td>

							<!-- Days & Hours Worked -->
							<td class="px-md py-4 w-[110px] min-w-[100px] whitespace-nowrap">
								<div class="font-bold text-ink font-mono tabular-nums">
									{worker.totalDaysWorked} <span class="text-xs font-normal text-secondary font-sans">{t('payroll.days')}</span>
								</div>
								<div class="text-xs font-mono tabular-nums font-semibold text-secondary mt-0.5">
									{worker.totalHoursWorked.toFixed(2)} {t('payroll.hours')}
								</div>
								{#if worker.pendingShiftCount > 0}
									<a
										class="mt-1.5 inline-flex items-center gap-1 rounded border border-amber-300 bg-amber-500/10 px-2 py-0.5 text-[11px] font-bold text-amber-800 hover:underline"
										href={`/app/attendance?from=${filterFrom}&to=${filterTo}&userId=${worker.userId}&status=Pending`}
									>
										<span class="material-symbols-outlined text-[12px]">schedule</span>
										<span>{worker.pendingShiftCount} {t('payroll.pendingShiftsWarning')}</span>
									</a>
								{/if}
							</td>

							<!-- Base Calculated Wage -->
							<td class="px-md py-4 w-[200px] min-w-[180px]">
								<div class="font-bold font-mono tabular-nums text-ink text-body-sm">
									{formatCurrency(worker.baseCalculatedWage, data.locale, currencyCode)}
								</div>
								{#if worker.tierBreakdown.length > 0}
									<div class="mt-1.5 space-y-1">
										{#each worker.tierBreakdown as tier, index (`${worker.shopUserId}-tier-${index}`)}
											<div class="rounded-lg border border-stone/80 bg-ash/50 p-2 text-xs">
												<div class="font-semibold text-ink">{tier.tierLabel}</div>
												<div class="text-secondary font-mono tabular-nums mt-0.5">
													{tier.shiftCount}x · {tier.totalHours.toFixed(1)}{t('payroll.hours')} =
													<strong class="text-ink font-mono tabular-nums">
														{formatCurrency(tier.subtotalAmount, data.locale, currencyCode)}
													</strong>
												</div>
											</div>
										{/each}
									</div>
								{:else}
									<div class="mt-1 text-xs text-secondary/60 italic">{t('payroll.noTiers')}</div>
								{/if}
							</td>

							<!-- Itemized Bonus Breakdown -->
							<td class="px-md py-4 w-[240px] min-w-[220px]">
								<div class="font-bold font-mono tabular-nums text-emerald-700 text-body-sm">
									{formatCurrency(worker.totalBonusAmount, data.locale, currencyCode)}
								</div>
								{#if worker.bonusBreakdown.length > 0}
									<div class="mt-1.5 space-y-1.5">
										{#each worker.bonusBreakdown as bonus (bonus.bonusTypeCode)}
											<div class="rounded-lg border border-stone/80 bg-ash/50 p-2 text-xs space-y-1">
												<div class="flex items-start justify-between gap-2 min-w-0">
													<span class="font-semibold text-ink break-words min-w-0">{bonus.bonusName}</span>
													<span class="font-mono tabular-nums font-bold text-emerald-700 shrink-0">
														{formatCurrency(bonus.totalBonusAmount, data.locale, currencyCode)}
													</span>
												</div>
												<div class="text-secondary text-[11px] leading-relaxed">{bonus.formulaSummary}</div>
												{#if bonus.referenceNumbers.length > 0}
													<div class="pt-0.5">
														<button
															type="button"
															class="inline-flex items-center gap-1.5 rounded-md border border-stone/90 bg-white px-2 py-0.5 text-[11px] font-medium text-ink hover:border-slate hover:bg-ash/80 hover:text-claytile transition-all cursor-pointer shadow-2xs group"
															onclick={() => openTicketModal(worker, bonus)}
															title={t('payroll.viewTickets')}
														>
															<span class="material-symbols-outlined text-[13px] text-secondary group-hover:text-claytile transition-colors">confirmation_number</span>
															<span class="font-semibold font-mono tabular-nums">{bonus.referenceNumbers.length}</span>
															<span>{t('payroll.seeTickets')}</span>
															<span class="material-symbols-outlined text-[12px] text-secondary group-hover:translate-x-0.5 group-hover:text-claytile transition-all">arrow_forward</span>
														</button>
													</div>
												{/if}
											</div>
										{/each}
									</div>
								{:else}
									<div class="mt-1 text-xs text-secondary/60 italic">{t('payroll.noBonuses')}</div>
								{/if}
							</td>

							<!-- Manual Adjustments -->
							<td class="px-md py-4 w-[220px] max-w-[240px]">
								<div class="font-bold font-mono tabular-nums {worker.totalAdjustments < 0 ? 'text-error' : 'text-ink'} text-body-sm">
									{formatCurrency(worker.totalAdjustments, data.locale, currencyCode)}
								</div>
								{#if worker.adjustments.length > 0}
									<div class="mt-1.5 space-y-1.5">
										{#each worker.adjustments as adj (adj.id)}
											<div class="rounded-lg border border-stone/80 bg-ash/50 p-2 text-xs">
												<div class="flex items-center justify-between gap-1.5">
													<span class="font-mono font-bold tabular-nums text-[12px] {adj.amount < 0 ? 'text-error' : 'text-emerald-700'}">
														{adj.amount > 0 ? '+' : ''}{formatCurrency(adj.amount, data.locale, currencyCode)}
													</span>
													{#if canManage}
														<form method="POST" action="?/deleteAdjustment" use:enhance class="shrink-0">
															<input type="hidden" name="id" value={adj.id} />
															<button
																type="submit"
																class="w-4 h-4 rounded hover:bg-error/10 text-error/60 hover:text-error flex items-center justify-center text-xs font-bold cursor-pointer transition-colors"
																title={t('common.delete')}
															>
																×
															</button>
														</form>
													{/if}
												</div>
												<div
													class="text-secondary text-[11px] font-sans leading-tight mt-1 line-clamp-2 break-words"
													title={adj.reason}
												>
													{adj.reason}
												</div>
											</div>
										{/each}
									</div>
								{/if}
								{#if canManage}
									<button
										type="button"
										class="mt-2 inline-flex items-center gap-1 rounded-button border border-stone bg-white px-2.5 py-1 text-xs font-semibold text-ink hover:bg-ash transition-colors shadow-2xs cursor-pointer"
										onclick={() => openAdjustmentModal(worker)}
									>
										<span class="material-symbols-outlined text-[14px]">add</span>
										<span>{t('payroll.addAdjustment')}</span>
									</button>
								{/if}
							</td>

							<!-- Final Total Payable -->
							<td class="px-md py-4 w-[130px] min-w-[120px] text-right whitespace-nowrap">
								<div class="font-mono tabular-nums text-base font-bold text-ink">
									{formatCurrency(worker.finalPayableAmount, data.locale, currencyCode)}
								</div>
							</td>

							<!-- Actions -->
							<td class="px-md py-4 w-[110px] min-w-[100px] text-right whitespace-nowrap">
								<a
									class="inline-flex h-8 items-center gap-1.5 rounded-button border border-stone bg-white px-3 text-xs font-semibold text-ink hover:bg-ash transition-colors shadow-2xs"
									href={`/app/payroll/${worker.userId}/slip?from=${filterFrom}&to=${filterTo}&cycle=${filterCycle}`}
								>
									<span class="material-symbols-outlined text-[15px]">print</span>
									<span>{t('payroll.printSlip')}</span>
								</a>
							</td>
						</tr>
					{:else}
						<tr>
							<td colspan="7" class="px-md py-16 text-center">
								<div class="w-full max-w-[540px] mx-auto flex flex-col items-center justify-center text-center">
									<div class="w-14 h-14 rounded-2xl bg-ash border border-stone flex items-center justify-center text-secondary mb-3.5 shadow-2xs">
										<span class="material-symbols-outlined text-[28px]">payments</span>
									</div>
									<h4 class="font-bold text-ink text-body-base m-0 mb-1.5">
										{t('payroll.empty.title')}
									</h4>
									<div class="text-body-sm text-secondary max-w-[440px] mx-auto mb-5 leading-relaxed">
										{#if !filterIncludeFullTime}
											{t('payroll.empty.fullTimeExcludedNotice')}
										{:else}
											{t('payroll.empty.subtitle')}
										{/if}
									</div>
									<div class="flex flex-wrap items-center justify-center gap-2.5">
										{#if !filterIncludeFullTime}
											<button
												type="button"
												class="px-4 py-2 rounded-button bg-ink text-white font-medium text-body-sm hover:bg-ink/90 active:scale-95 transition-all shadow-xs inline-flex items-center gap-1.5 cursor-pointer"
												onclick={toggleIncludeFullTime}
											>
												<span class="material-symbols-outlined text-[18px]">badge</span>
												<span>{t('payroll.empty.includeFullTimeCta')}</span>
											</button>
										{/if}
										{#if hasActiveFilters}
											<button
												type="button"
												class="px-4 py-2 rounded-button border border-stone bg-white text-ink hover:bg-ash font-medium text-body-sm transition-all shadow-2xs inline-flex items-center gap-1.5 cursor-pointer"
												onclick={resetFilters}
											>
												<span class="material-symbols-outlined text-[18px]">restart_alt</span>
												<span>{t('payroll.clearFilters')}</span>
											</button>
										{/if}
										{#if can(data.user.permissions, permissions.attendanceRead)}
											<a
												href="/app/attendance"
												class="px-4 py-2 rounded-button border border-stone bg-white text-ink hover:bg-ash font-medium text-body-sm transition-all shadow-2xs inline-flex items-center gap-1.5"
											>
												<span class="material-symbols-outlined text-[18px]">schedule</span>
												<span>{t('payroll.empty.checkAttendance')}</span>
											</a>
										{/if}
										{#if can(data.user.permissions, permissions.settingsRead)}
											<a
												href="/app/users-roles"
												class="px-4 py-2 rounded-button border border-stone bg-white text-secondary hover:text-ink hover:bg-ash font-medium text-body-sm transition-all shadow-2xs inline-flex items-center gap-1.5"
											>
												<span class="material-symbols-outlined text-[18px]">tune</span>
												<span>{t('payroll.empty.configureStaff')}</span>
											</a>
										{/if}
									</div>
								</div>
							</td>
						</tr>
					{/each}
				</tbody>
			</table>
		</div>
	</div>
</div>

<!-- Manual Payroll Adjustment Modal -->
{#if adjustmentModalOpen && activeWorker}
	<div
		transition:fade={{ duration: 150 }}
		class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-xs p-4"
		role="presentation"
		onclick={(e) => {
			if (e.target === e.currentTarget && !adjustmentSubmitting) adjustmentModalOpen = false;
		}}
	>
		<div
			class="w-[min(480px,calc(100vw-2rem))] max-h-[calc(100vh-3rem)] flex flex-col rounded-2xl border border-stone bg-white shadow-2xl overflow-hidden"
			role="dialog"
			aria-modal="true"
			aria-labelledby="adjustment-modal-title"
		>
			<!-- Modal Header -->
			<div class="flex items-start justify-between gap-md border-b border-stone px-6 py-4 bg-surface-container-low">
				<div class="flex items-center gap-3 min-w-0">
					<div class="w-10 h-10 rounded-xl bg-ash border border-stone flex items-center justify-center text-claytile shrink-0 shadow-2xs">
						<span class="material-symbols-outlined text-[22px]">payments</span>
					</div>
					<div class="min-w-0">
						<h3 id="adjustment-modal-title" class="font-subheading text-[16px] font-bold text-ink truncate leading-tight">
							{t('payroll.adjustmentModalTitle')}
						</h3>
						<p class="text-xs text-secondary mt-0.5 font-mono truncate">
							{activeWorker.fullName} · {filterFrom} – {filterTo}
						</p>
					</div>
				</div>
				<button
					type="button"
					class="w-8 h-8 rounded-lg hover:bg-ash text-secondary hover:text-ink flex items-center justify-center cursor-pointer transition-colors shrink-0 disabled:opacity-40 disabled:cursor-not-allowed"
					onclick={() => {
						if (!adjustmentSubmitting) adjustmentModalOpen = false;
					}}
					disabled={adjustmentSubmitting}
					aria-label={t('common.close')}
				>
					<span class="material-symbols-outlined text-[20px]">close</span>
				</button>
			</div>

			<form
				method="POST"
				action="?/addAdjustment"
				class="flex flex-col min-h-0 flex-1"
				use:enhance={({ cancel }) => {
					adjustmentError = '';
					if (parsedAdjustmentAmount === 0) {
						cancel();
						adjustmentError = t('payroll.adjustmentAmountNonZero');
						adjustmentCurrencyRef?.focus();
						return;
					}
					adjustmentSubmitting = true;
					return async ({ result, update }) => {
						adjustmentSubmitting = false;
						await update();
						if (result.type === 'success') {
							adjustmentModalOpen = false;
						} else if (result.type === 'failure') {
							const errData = result.data as { message?: string; messageKey?: string } | undefined;
							if (errData?.messageKey) {
								const translated = t(errData.messageKey);
								adjustmentError = translated !== errData.messageKey ? translated : errData.messageKey;
							} else if (errData?.message) {
								adjustmentError = errData.message;
							}
						}
					};
				}}
			>
				<input type="hidden" name="userId" value={activeWorker.userId} />
				<input type="hidden" name="periodStart" value={filterFrom} />
				<input type="hidden" name="periodEnd" value={filterTo} />

				<div class="px-6 py-5 space-y-4 overflow-y-auto custom-scrollbar">
					{#if adjustmentError}
						<div role="alert" class="rounded-lg border border-error/25 bg-error-container px-3 py-2.5 text-xs font-medium text-error flex items-start gap-2">
							<span class="material-symbols-outlined text-[16px] shrink-0 mt-0.5">error</span>
							<span>{adjustmentError}</span>
						</div>
					{/if}

					<!-- Adjustment Direction Segmented Control -->
					<div class="space-y-1.5">
						<span class="font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block">
							{t('payroll.adjustmentType')}
						</span>
						<div class="grid grid-cols-2 gap-1 rounded-xl border border-stone bg-ash/70 p-1" role="group" aria-label={t('payroll.adjustmentType')}>
							<button
								type="button"
								class="inline-flex h-8.5 items-center justify-center gap-1.5 rounded-lg text-xs transition-all cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed {!isDeduction ? 'bg-white text-emerald-800 font-semibold shadow-2xs border border-stone/70' : 'text-secondary font-medium hover:text-ink'}"
								onclick={() => setAdjustmentDirection('bonus')}
								disabled={adjustmentSubmitting}
								aria-pressed={!isDeduction}
							>
								<span class="material-symbols-outlined text-[16px]">add_circle</span>
								<span>{t('payroll.adjustmentTypeBonus')}</span>
							</button>
							<button
								type="button"
								class="inline-flex h-8.5 items-center justify-center gap-1.5 rounded-lg text-xs transition-all cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed {isDeduction ? 'bg-white text-error font-semibold shadow-2xs border border-stone/70' : 'text-secondary font-medium hover:text-ink'}"
								onclick={() => setAdjustmentDirection('deduction')}
								disabled={adjustmentSubmitting}
								aria-pressed={isDeduction}
							>
								<span class="material-symbols-outlined text-[16px]">do_not_disturb_on</span>
								<span>{t('payroll.adjustmentTypeDeduction')}</span>
							</button>
						</div>
					</div>

					<div class="space-y-1">
						<label for="payroll-adjustment-amount" class="font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block">
							{t('payroll.adjustmentAmount')} ({currencyCode}) *
						</label>
						<CurrencyInput
							id="payroll-adjustment-amount"
							bind:this={adjustmentCurrencyRef}
							name="amount"
							locale={data.locale}
							currency={currencyCode}
							prefix={currencyCode === 'IDR' ? 'Rp' : '$'}
							allowNegative={true}
							tone={adjustmentTone}
							inputClass="h-10 font-mono tabular-nums font-semibold"
							placeholder="0"
							disabled={adjustmentSubmitting}
							bind:value={adjustmentAmount}
							oninput={() => {
								adjustmentError = '';
							}}
							required
						/>
						<span class="block text-xs text-secondary leading-relaxed mt-1">
							{t('payroll.adjustmentAmountHint')}
						</span>
					</div>

					<label class="space-y-1 block">
						<span class="font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block">
							{t('payroll.adjustmentReason')} *
						</span>
						<input
							class="w-full h-10 rounded-input border border-stone bg-white px-3 text-body-sm text-ink focus:border-ink focus:ring-1 focus:ring-ink/10 focus:outline-none transition-colors"
							type="text"
							name="reason"
							maxlength={500}
							disabled={adjustmentSubmitting}
							placeholder={t('payroll.adjustmentReasonPlaceholder')}
							bind:value={adjustmentReason}
							required
						/>
					</label>

					<!-- Live Payable Impact Preview -->
					<div class="rounded-xl border border-stone/80 bg-ash/45 px-3.5 py-2.5 flex flex-col sm:flex-row sm:items-center justify-between gap-2 text-xs">
						<div class="space-y-0.5 shrink-0">
							<div class="text-[11px] font-medium text-secondary uppercase tracking-wider">
								{t('payroll.adjustmentCurrentPayable')}
							</div>
							<div class="font-mono tabular-nums font-semibold text-ink">
								{formatCurrency(activeWorker.finalPayableAmount, data.locale, currencyCode)}
							</div>
						</div>
						<span class="material-symbols-outlined text-[18px] text-secondary/70 hidden sm:inline shrink-0">arrow_forward</span>
						<div class="space-y-0.5 sm:text-right min-w-0">
							<div class="text-[11px] font-medium text-secondary uppercase tracking-wider">
								{t('payroll.adjustmentProjectedPayable')}
							</div>
							{#if parsedAdjustmentAmount !== 0}
								<div class="flex flex-wrap items-center justify-start sm:justify-end gap-x-1.5 gap-y-0.5 font-mono tabular-nums font-bold {projectedFinalPayable < 0 ? 'text-error' : 'text-ink'}">
									<span class="text-[11px] font-semibold {parsedAdjustmentAmount < 0 ? 'text-error' : 'text-emerald-700'}">
										({parsedAdjustmentAmount > 0 ? '+' : ''}{formatCurrency(parsedAdjustmentAmount, data.locale, currencyCode)})
									</span>
									<span>{formatCurrency(projectedFinalPayable, data.locale, currencyCode)}</span>
								</div>
							{:else}
								<div class="text-secondary/70 italic font-normal">
									{t('payroll.adjustmentPreviewPlaceholder')}
								</div>
							{/if}
						</div>
					</div>
				</div>

				<div class="flex justify-end gap-2 border-t border-stone px-6 py-4 bg-surface-container-low/60">
					<button
						type="button"
						class="px-4 py-2 rounded-button border border-stone bg-white text-secondary hover:text-ink hover:bg-ash font-medium text-body-sm transition-all cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed"
						onclick={() => (adjustmentModalOpen = false)}
						disabled={adjustmentSubmitting}
					>
						{t('common.cancel')}
					</button>
					<button
						type="submit"
						class="px-5 py-2 rounded-button bg-ink text-white font-medium text-body-sm hover:bg-ink/90 active:scale-95 transition-all shadow-xs cursor-pointer inline-flex items-center gap-1.5 disabled:opacity-60 disabled:cursor-not-allowed"
						disabled={adjustmentSubmitting}
					>
						{#if adjustmentSubmitting}
							<span class="material-symbols-outlined text-[16px] animate-spin">progress_activity</span>
							<span>{t('common.saving')}</span>
						{:else}
							<span>{t('common.save')}</span>
						{/if}
					</button>
				</div>
			</form>
		</div>
	</div>
{/if}

<!-- Qualifying Bonus Tickets Modal -->
{#if ticketModalOpen && activeBonusTickets}
	<div
		transition:fade={{ duration: 150 }}
		class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-xs p-4"
		role="presentation"
		onclick={(e) => {
			if (e.target === e.currentTarget) ticketModalOpen = false;
		}}
	>
		<div
			class="w-[min(560px,calc(100vw-2rem))] max-h-[calc(100vh-3rem)] flex flex-col rounded-2xl border border-stone bg-white shadow-2xl overflow-hidden"
			role="dialog"
			aria-modal="true"
		>
			<!-- Modal Header -->
			<div class="flex items-start justify-between gap-md border-b border-stone px-6 py-4 bg-surface-container-low">
				<div class="flex items-center gap-3 min-w-0">
					<div class="w-10 h-10 rounded-xl bg-ash border border-stone flex items-center justify-center text-claytile shrink-0 shadow-2xs">
						<span class="material-symbols-outlined text-[22px]">confirmation_number</span>
					</div>
					<div class="min-w-0">
						<h3 class="font-subheading text-[16px] font-bold text-ink truncate leading-tight">
							{t('payroll.ticketsModalTitle')}
						</h3>
						<p class="text-xs text-secondary mt-0.5 truncate">
							<strong class="text-ink font-medium">{activeBonusTickets.workerName}</strong> · {activeBonusTickets.bonusName}
						</p>
					</div>
				</div>
				<button
					type="button"
					class="w-8 h-8 rounded-lg hover:bg-ash text-secondary hover:text-ink flex items-center justify-center cursor-pointer transition-colors shrink-0"
					onclick={() => (ticketModalOpen = false)}
					aria-label={t('common.close')}
				>
					<span class="material-symbols-outlined text-[20px]">close</span>
				</button>
			</div>

			<!-- Bonus Context Banner -->
			<div class="px-6 py-3.5 border-b border-stone/80 bg-white space-y-2">
				<div class="flex flex-wrap items-center justify-between gap-2">
					<div class="inline-flex items-center gap-1.5 rounded-full border border-emerald-200 bg-emerald-50 px-2.5 py-0.5 text-xs font-bold text-emerald-800 font-mono tabular-nums">
						<span class="material-symbols-outlined text-[14px]">payments</span>
						<span>+{formatCurrency(activeBonusTickets.bonusAmount, data.locale, currencyCode)}</span>
					</div>
					<div class="text-xs text-secondary font-mono tabular-nums">
						<strong class="text-ink font-semibold">{activeBonusTickets.referenceNumbers.length}</strong> {t('payroll.totalQualifyingUnits')}
					</div>
				</div>
				<div class="rounded-lg border border-stone/80 bg-ash/50 p-2.5 text-xs text-secondary leading-relaxed">
					<span class="font-semibold text-ink">{t('payroll.colBonuses')}:</span> {activeBonusTickets.formulaSummary}
				</div>

				{#if activeBonusTickets.referenceNumbers.length > 4}
					<div class="relative pt-1">
						<span class="material-symbols-outlined absolute left-2.5 top-[13px] text-[16px] text-secondary">search</span>
						<input
							type="text"
							bind:value={ticketSearchQuery}
							placeholder={t('payroll.searchTicketsPlaceholder')}
							class="w-full h-8 pl-8 pr-3 rounded-lg border border-stone bg-ash/30 text-xs font-mono text-ink placeholder:text-secondary focus:bg-white focus:border-slate focus:outline-none transition-all"
						/>
					</div>
				{/if}
			</div>

			<!-- Tickets List -->
			<div class="px-6 py-4 overflow-y-auto max-h-[340px] custom-scrollbar space-y-2">
				{#if filteredTickets.length === 0}
					<div class="py-8 text-center text-secondary text-xs italic">
						{activeBonusTickets.referenceNumbers.length === 0 ? t('payroll.noTicketsRecorded') : t('common.noOptions')}
					</div>
				{:else}
					<div class="grid grid-cols-1 sm:grid-cols-2 gap-2">
						{#each filteredTickets as ticketNum, i (ticketNum)}
							<div class="flex items-center justify-between gap-2 rounded-lg border border-stone/80 bg-ash/30 p-2.5 hover:border-slate hover:bg-white transition-all shadow-2xs group">
								<div class="flex items-center gap-2 min-w-0">
									<span class="text-[11px] font-mono text-secondary tabular-nums">#{i + 1}</span>
									<span class="font-mono text-xs font-bold text-ink truncate select-all">{ticketNum}</span>
								</div>
								<div class="flex items-center gap-1 shrink-0">
									<button
										type="button"
										class="w-7 h-7 rounded-md border border-stone/80 bg-white text-secondary hover:text-ink hover:border-slate flex items-center justify-center text-xs transition-colors cursor-pointer"
										onclick={() => copyTicketNumber(ticketNum)}
										title={copiedTicket === ticketNum ? t('payroll.ticketCopied') : t('payroll.copyTicketNumber')}
									>
										<span class="material-symbols-outlined text-[14px]">
											{copiedTicket === ticketNum ? 'check' : 'content_copy'}
										</span>
									</button>
									<a
										href={`/app/tickets?search=${encodeURIComponent(ticketNum)}`}
										target="_blank"
										rel="noopener noreferrer"
										class="w-7 h-7 rounded-md border border-stone/80 bg-white text-claytile hover:bg-claytile hover:text-white hover:border-claytile flex items-center justify-center text-xs transition-colors shadow-2xs"
										title={t('payroll.openTicket')}
									>
										<span class="material-symbols-outlined text-[14px]">open_in_new</span>
									</a>
								</div>
							</div>
						{/each}
					</div>
				{/if}
			</div>

			<!-- Modal Footer -->
			<div class="flex items-center justify-between border-t border-stone px-6 py-3.5 bg-surface-container-low text-xs">
				<div class="text-secondary font-mono tabular-nums">
					{t('common.pagination.showing')} <strong class="text-ink font-semibold">{filteredTickets.length}</strong> / {activeBonusTickets.referenceNumbers.length}
				</div>
				<button
					type="button"
					class="px-4 py-1.5 rounded-button bg-ink text-white font-medium text-body-sm hover:bg-ink/90 active:scale-95 transition-all shadow-xs cursor-pointer"
					onclick={() => (ticketModalOpen = false)}
				>
					{t('common.close')}
				</button>
			</div>
		</div>
	</div>
{/if}
