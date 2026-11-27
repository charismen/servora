<script lang="ts">
	import { enhance } from '$app/forms';
	import { goto } from '$app/navigation';
	import { fade } from 'svelte/transition';
	import PageHeader from '$lib/components/PageHeader.svelte';
	import CurrencyInput from '$lib/components/CurrencyInput.svelte';
	import DeleteConfirmButton from '$lib/components/DeleteConfirmButton.svelte';
	import Dropdown from '$lib/components/ui/dropdown/Dropdown.svelte';
	import { can, permissions } from '$lib/auth/permissions';
	import { createTranslator, formatCurrency, resolveTerminology } from '$lib/i18n';
	import { toast } from '$lib/stores/toast.svelte';
	import type {
		BasePayMode,
		BonusTaskDefinition,
		CompensationTemplate,
		FallbackPayStrategy,
		FreelancePayPolicy,
		TaskItemizedRuleConfig,
		TaskPayoutTrigger,
		WageTierBracket
	} from '$lib/types/shop-users';
	import type { ActionData, PageData } from './$types';

	let { data, form }: { data: PageData; form: ActionData } = $props();
	const t = $derived(createTranslator(data.locale));
	const terms = $derived(resolveTerminology(data.user.workspace?.terminology, data.locale));
	const isItBlueprint = $derived((data.user.workspace?.industryBlueprintKey ?? 'it_computer') === 'it_computer');
	const isInternalWorkspace = $derived(data.user.workspace?.operatingMode === 'InternalMaintenance');
	const canWrite = $derived(can(data.user.permissions, permissions.settingsWrite));
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

	const tier2Label = $derived(
		isItBlueprint ? t('compensationCatalog.gamingRate') : t('compensationCatalog.premiumRate')
	);

	const BASE_PAY_MODES: BasePayMode[] = [
		'TieredDaily',
		'TieredDailyAverage',
		'TieredMonthly',
		'Hourly',
		'FixedDaily',
		'FixedMonthly',
		'CommissionOnly',
		'RevenueShare'
	];

	let activeTab = $state<'tasks' | 'templates'>('tasks');
	$effect(() => {
		activeTab = data.tab === 'templates' ? 'templates' : 'tasks';
	});

	let lastFormMessage = $state('');
	$effect(() => {
		if (!form) return;
		const key = (form as { messageKey?: string }).messageKey;
		const msg = key ? t(key) : (form as { message?: string }).message;
		if (!msg || msg === lastFormMessage) return;
		lastFormMessage = msg;
		if ((form as { success?: boolean }).success) {
			toast.success(msg);
		} else {
			toast.error(msg);
		}
	});

	function switchTab(next: 'tasks' | 'templates') {
		activeTab = next;
		void goto(`?tab=${next}`, { keepFocus: true, noScroll: true, replaceState: true });
	}

	// ---- Bonus Task Modal State ----
	let taskModalOpen = $state(false);
	let taskSubmitting = $state(false);
	let deletingTaskId = $state<number | null>(null);
	let taskDraft = $state({
		id: 0,
		taskKey: '',
		label: '',
		description: '',
		category: '',
		defaultStandardRate: 25000 as number | string,
		defaultGamingRate: 50000 as number | string,
		defaultCustomerPrice: 100000 as number | string,
		defaultWorkItemType: 'Service',
		defaultIsInternalOnly: false,
		legacyKeywords: '',
		isActive: true,
		sortOrder: 0
	});

	function openTaskModal(task: BonusTaskDefinition | null = null) {
		if (task) {
			taskDraft = {
				id: task.id,
				taskKey: task.taskKey,
				label: task.label,
				description: task.description ?? '',
				category: task.category ?? '',
				defaultStandardRate: task.defaultStandardRate,
				defaultGamingRate: task.defaultGamingRate,
				defaultCustomerPrice: task.defaultCustomerPrice,
				defaultWorkItemType: task.defaultWorkItemType || 'Service',
				defaultIsInternalOnly: task.defaultIsInternalOnly,
				legacyKeywords: task.legacyKeywords ?? '',
				isActive: task.isActive,
				sortOrder: task.sortOrder
			};
		} else {
			taskDraft = {
				id: 0,
				taskKey: '',
				label: '',
				description: '',
				category: '',
				defaultStandardRate: 25000,
				defaultGamingRate: 50000,
				defaultCustomerPrice: isInternalWorkspace ? 0 : 100000,
				defaultWorkItemType: 'Service',
				defaultIsInternalOnly: isInternalWorkspace,
				legacyKeywords: '',
				isActive: true,
				sortOrder: (data.bonusTasks.length + 1) * 10
			};
		}
		taskModalOpen = true;
	}

	// ---- Compensation Template Modal State ----
	let templateModalOpen = $state(false);
	let templateSubmitting = $state(false);
	let deletingTemplateId = $state<number | null>(null);
	let tplId = $state(0);
	let tplName = $state('');
	let tplDescription = $state('');
	let tplBasePayMode = $state<BasePayMode>('TieredDaily');
	let tplBaseHourlyRate = $state<number>(7500);
	let tplBrackets = $state<WageTierBracket[]>([]);
	let tplFixedDailyRate = $state<number>(100000);
	let tplFixedMonthlySalary = $state<number>(3000000);
	let tplStandardWorkDays = $state<number>(26);
	let tplProrateMonthly = $state<boolean>(false);
	let tplRevenueSharePercent = $state<number>(15);
	let tplFallbackStrategy = $state<FallbackPayStrategy>('ProratedHourly');
	let tplOvertimeThreshold = $state<string>('');
	let tplOvertimeMultiplier = $state<number>(1.5);
	let tplWeekendMultiplier = $state<number>(1.0);
	let tplTaskBonusActive = $state<boolean>(true);
	let tplTaskPayoutTrigger = $state<TaskPayoutTrigger>('TicketFinalized');
	let tplTaskRules = $state<TaskItemizedRuleConfig[]>([]);
	let tplIsActive = $state<boolean>(true);
	let tplSortOrder = $state<number>(10);

	const tplPayPolicyJson = $derived.by(() => {
		const policy: FreelancePayPolicy = {
			fixedDailyRate: Number(tplFixedDailyRate) || 0,
			fixedMonthlySalary: Number(tplFixedMonthlySalary) || 0,
			standardWorkDaysPerMonth: Math.max(1, Number(tplStandardWorkDays) || 26),
			prorateMonthlyByAttendance: tplProrateMonthly,
			revenueSharePercent: Number(tplRevenueSharePercent) || 0,
			fallbackStrategy: tplFallbackStrategy,
			overtimeThresholdHoursPerDay: tplOvertimeThreshold.trim() ? Number(tplOvertimeThreshold) : null,
			overtimeMultiplier: Number(tplOvertimeMultiplier) || 1.0,
			weekendMultiplier: Number(tplWeekendMultiplier) || 1.0,
			taskBonusTrigger: tplTaskPayoutTrigger
		};
		return JSON.stringify(policy);
	});

	const tplBonusConfigsJson = $derived.by(() =>
		JSON.stringify([
			{
				bonusTypeCode: 'TASK_ITEMIZED',
				isActive: tplTaskBonusActive,
				parameters: {
					payoutTrigger: tplTaskPayoutTrigger,
					rules: tplTaskRules
				}
			}
		])
	);

	function openTemplateModal(tpl: CompensationTemplate | null = null) {
		if (tpl) {
			tplId = tpl.id;
			tplName = tpl.name;
			tplDescription = tpl.description ?? '';
			tplBasePayMode = (tpl.basePayMode as BasePayMode) || 'TieredDaily';
			tplBaseHourlyRate = tpl.baseHourlyRate ?? 0;
			tplBrackets = tpl.tieredRates?.length ? tpl.tieredRates.map((b) => ({ ...b })) : [];
			const p = tpl.payPolicy ?? {};
			tplFixedDailyRate = p.fixedDailyRate ?? 0;
			tplFixedMonthlySalary = p.fixedMonthlySalary ?? 0;
			tplStandardWorkDays = p.standardWorkDaysPerMonth ?? 26;
			tplProrateMonthly = Boolean(p.prorateMonthlyByAttendance);
			tplRevenueSharePercent = p.revenueSharePercent ?? 0;
			tplFallbackStrategy = (p.fallbackStrategy as FallbackPayStrategy) || 'ProratedHourly';
			tplOvertimeThreshold = p.overtimeThresholdHoursPerDay != null ? String(p.overtimeThresholdHoursPerDay) : '';
			tplOvertimeMultiplier = p.overtimeMultiplier ?? 1.5;
			tplWeekendMultiplier = p.weekendMultiplier ?? 1.0;

			const taskCfg = tpl.bonusConfigs?.find((c) => c.bonusTypeCode === 'TASK_ITEMIZED');
			tplTaskBonusActive = taskCfg ? taskCfg.isActive : false;
			tplTaskPayoutTrigger =
				(taskCfg?.parameters?.payoutTrigger as TaskPayoutTrigger) ||
				(p.taskBonusTrigger as TaskPayoutTrigger) ||
				'TicketFinalized';
			const rawRules = taskCfg?.parameters?.rules as TaskItemizedRuleConfig[] | undefined;
			tplTaskRules = Array.isArray(rawRules) ? rawRules.map((r) => ({ ...r })) : [];
			tplIsActive = tpl.isActive;
			tplSortOrder = tpl.sortOrder;
		} else {
			tplId = 0;
			tplName = '';
			tplDescription = '';
			tplBasePayMode = 'TieredDaily';
			tplBaseHourlyRate = 7500;
			tplBrackets = [
				{ minHours: 5, maxHours: 6, flatRate: 35000, hourlyRate: null },
				{ minHours: 8, maxHours: 24, flatRate: 60000, hourlyRate: null }
			];
			tplFixedDailyRate = 100000;
			tplFixedMonthlySalary = 3000000;
			tplStandardWorkDays = 26;
			tplProrateMonthly = false;
			tplRevenueSharePercent = 15;
			tplFallbackStrategy = 'ProratedHourly';
			tplOvertimeThreshold = '';
			tplOvertimeMultiplier = 1.5;
			tplWeekendMultiplier = 1.0;
			tplTaskBonusActive = true;
			tplTaskPayoutTrigger = 'TicketFinalized';
			tplTaskRules = [];
			importTasksIntoTemplate();
			tplIsActive = true;
			tplSortOrder = (data.templates.length + 1) * 10;
		}
		templateModalOpen = true;
	}

	function importTasksIntoTemplate() {
		const activeTasks = data.bonusTasks.filter((task) => task.isActive);
		const rules: TaskItemizedRuleConfig[] = [];
		for (const task of activeTasks) {
			if (task.defaultGamingRate > 0 && task.defaultGamingRate !== task.defaultStandardRate) {
				rules.push({
					taskKey: task.taskKey,
					taskLabel: task.label,
					matcherKeyword: task.legacyKeywords ?? task.label,
					deviceCategory: 'Standard',
					rewardPerUnit: task.defaultStandardRate
				});
				rules.push({
					taskKey: task.taskKey,
					taskLabel: task.label,
					matcherKeyword: task.legacyKeywords ?? task.label,
					deviceCategory: 'Gaming',
					rewardPerUnit: task.defaultGamingRate
				});
			} else {
				rules.push({
					taskKey: task.taskKey,
					taskLabel: task.label,
					matcherKeyword: task.legacyKeywords ?? task.label,
					deviceCategory: 'Any',
					rewardPerUnit: task.defaultStandardRate
				});
			}
		}
		tplTaskRules = rules;
	}

	function addTplBracket() {
		tplBrackets = [...tplBrackets, { minHours: 1, maxHours: 4, flatRate: 25000, hourlyRate: null }];
	}

	function removeTplBracket(index: number) {
		tplBrackets = tplBrackets.filter((_, idx) => idx !== index);
	}

	function removeTplRule(index: number) {
		tplTaskRules = tplTaskRules.filter((_, idx) => idx !== index);
	}

	function handleWindowKeydown(e: KeyboardEvent) {
		if (e.key !== 'Escape') return;
		if (taskModalOpen && !taskSubmitting) taskModalOpen = false;
		else if (templateModalOpen && !templateSubmitting) templateModalOpen = false;
	}

	const showTieredSection = $derived(
		tplBasePayMode === 'TieredDaily' || tplBasePayMode === 'TieredDailyAverage' || tplBasePayMode === 'TieredMonthly'
	);
</script>

<svelte:window onkeydown={handleWindowKeydown} />

<div class="space-y-lg pb-xl min-w-0 max-w-full">
	<PageHeader
		items={[
			{ label: t('common.console'), href: '/app' },
			{ label: t('nav.settings'), href: '/app/settings' },
			{ label: t('compensationCatalog.title') }
		]}
		title={t('compensationCatalog.title')}
		subtitle={t('compensationCatalog.subtitle')}
	>
		{#snippet actions()}
			<div class="flex flex-wrap items-center gap-2">
				<a
					href="/app/users-roles"
					class="inline-flex h-10 items-center gap-1.5 rounded-button border border-stone bg-white px-3.5 text-body-sm font-medium text-ink hover:bg-ash transition-colors shadow-2xs"
				>
					<span class="material-symbols-outlined text-[18px]" aria-hidden="true">manage_accounts</span>
					<span>{t('nav.usersRoles')}</span>
				</a>
				{#if canWrite}
					{#if activeTab === 'tasks'}
						<button
							type="button"
							onclick={() => openTaskModal(null)}
							class="inline-flex h-10 items-center gap-1.5 rounded-button bg-ink px-4 text-body-sm font-medium text-white shadow-xs hover:bg-ink/90 active:scale-95 transition-all cursor-pointer"
						>
							<span class="material-symbols-outlined text-[18px]" aria-hidden="true">add</span>
							<span>{t('compensationCatalog.addTask')}</span>
						</button>
					{:else}
						<button
							type="button"
							onclick={() => openTemplateModal(null)}
							class="inline-flex h-10 items-center gap-1.5 rounded-button bg-ink px-4 text-body-sm font-medium text-white shadow-xs hover:bg-ink/90 active:scale-95 transition-all cursor-pointer"
						>
							<span class="material-symbols-outlined text-[18px]" aria-hidden="true">add</span>
							<span>{t('compensationCatalog.addTemplate')}</span>
						</button>
					{/if}
				{/if}
			</div>
		{/snippet}
	</PageHeader>

	<!-- Tabs -->
	<div class="flex items-center gap-2 border-b border-stone pb-2">
		<button
			type="button"
			onclick={() => switchTab('tasks')}
			class="inline-flex items-center gap-1.5 px-3.5 py-2 rounded-lg text-body-sm font-semibold transition-all cursor-pointer {activeTab === 'tasks' ? 'bg-ink text-white shadow-2xs' : 'text-secondary hover:text-ink hover:bg-ash'}"
		>
			<span class="material-symbols-outlined text-[18px]" aria-hidden="true">task_alt</span>
			<span>{t('compensationCatalog.tabTasks')}</span>
			<span class="px-1.5 py-0.5 rounded-full text-[11px] font-mono tabular-nums {activeTab === 'tasks' ? 'bg-white/20 text-white' : 'bg-ash text-secondary'}">
				{data.bonusTasks.length}
			</span>
		</button>
		<button
			type="button"
			onclick={() => switchTab('templates')}
			class="inline-flex items-center gap-1.5 px-3.5 py-2 rounded-lg text-body-sm font-semibold transition-all cursor-pointer {activeTab === 'templates' ? 'bg-ink text-white shadow-2xs' : 'text-secondary hover:text-ink hover:bg-ash'}"
		>
			<span class="material-symbols-outlined text-[18px]" aria-hidden="true">layers</span>
			<span>{t('compensationCatalog.tabTemplates')}</span>
			<span class="px-1.5 py-0.5 rounded-full text-[11px] font-mono tabular-nums {activeTab === 'templates' ? 'bg-white/20 text-white' : 'bg-ash text-secondary'}">
				{data.templates.length}
			</span>
		</button>
	</div>

	{#if activeTab === 'tasks'}
		<section class="rounded-xl border border-stone bg-white shadow-2xs overflow-hidden">
			<div class="px-5 py-4 border-b border-stone bg-surface-container-low flex flex-wrap items-center justify-between gap-3">
				<div>
					<h2 class="font-subheading text-[15px] font-bold text-ink">{t('compensationCatalog.tasksHeading')}</h2>
					<p class="text-xs text-secondary mt-0.5">{t('compensationCatalog.tasksHint')}</p>
				</div>
			</div>

			{#if data.bonusTasks.length === 0}
				<div class="p-12 text-center space-y-3">
					<div class="w-12 h-12 rounded-xl bg-ash border border-stone flex items-center justify-center text-secondary mx-auto">
						<span class="material-symbols-outlined text-[24px]" aria-hidden="true">task_alt</span>
					</div>
					<div>
						<h3 class="text-body-base font-bold text-ink">{t('compensationCatalog.noTasks')}</h3>
						<p class="text-body-sm text-secondary mt-1">{t('compensationCatalog.noTasksHint')}</p>
					</div>
					{#if canWrite}
						<button
							type="button"
							onclick={() => openTaskModal(null)}
							class="inline-flex h-10 items-center gap-1.5 rounded-button bg-ink px-4 text-body-sm font-medium text-white hover:bg-ink/90 cursor-pointer"
						>
							<span class="material-symbols-outlined text-[18px]" aria-hidden="true">add</span>
							<span>{t('compensationCatalog.addTask')}</span>
						</button>
					{/if}
				</div>
			{:else}
				<div class="overflow-x-auto">
					<table class="w-full text-left border-collapse">
						<thead>
							<tr class="border-b border-stone bg-surface-container-low text-[11px] font-bold uppercase tracking-wider text-secondary">
								<th class="px-4 py-3 font-label-caps">{t('compensationCatalog.colTask')}</th>
								<th class="px-4 py-3 font-label-caps">{t('compensationCatalog.colCategory')}</th>
								<th class="px-4 py-3 font-label-caps text-right">{t('compensationCatalog.standardRate')}</th>
								<th class="px-4 py-3 font-label-caps text-right">{tier2Label}</th>
								<th class="px-4 py-3 font-label-caps text-right">{t('compensationCatalog.customerPrice')}</th>
								<th class="px-4 py-3 font-label-caps">{t('settings.colStatus')}</th>
								{#if canWrite}
									<th class="px-4 py-3 font-label-caps text-right">{t('settings.colActions')}</th>
								{/if}
							</tr>
						</thead>
						<tbody class="divide-y divide-stone text-body-sm">
							{#each data.bonusTasks as task (task.id)}
								<tr class="align-middle hover:bg-ash/30 transition-colors">
									<td class="px-4 py-3">
										<div class="font-semibold text-ink">{task.label}</div>
										<div class="flex flex-wrap items-center gap-1.5 mt-0.5">
											<span class="font-mono text-[11px] text-secondary bg-ash px-1.5 py-0.5 rounded border border-stone/80">
												{task.taskKey}
											</span>
											{#if task.defaultIsInternalOnly}
												<span class="text-[10px] font-semibold uppercase px-1.5 py-0.5 rounded bg-amber-50 text-amber-900 border border-amber-200">
													{t('compensationCatalog.internalOnlyBadge')}
												</span>
											{/if}
										</div>
									</td>
									<td class="px-4 py-3 text-secondary text-xs">{task.category ?? '—'}</td>
									<td class="px-4 py-3 text-right font-mono tabular-nums font-semibold text-ink">
										{formatCurrency(task.defaultStandardRate, data.locale, currencyCode)}
									</td>
									<td class="px-4 py-3 text-right font-mono tabular-nums font-semibold text-ink">
										{formatCurrency(task.defaultGamingRate, data.locale, currencyCode)}
									</td>
									<td class="px-4 py-3 text-right font-mono tabular-nums text-secondary">
										{task.defaultIsInternalOnly ? '—' : formatCurrency(task.defaultCustomerPrice, data.locale, currencyCode)}
									</td>
									<td class="px-4 py-3 whitespace-nowrap">
										{#if task.isActive}
											<span class="inline-flex items-center gap-1.5 rounded-full border border-emerald-300 bg-emerald-500/10 px-2.5 py-0.5 text-[11px] font-semibold text-emerald-800">
												{t('settings.statusActive')}
											</span>
										{:else}
											<span class="inline-flex items-center gap-1.5 rounded-full border border-stone bg-ash px-2.5 py-0.5 text-[11px] font-semibold text-secondary">
												{t('settings.statusInactive')}
											</span>
										{/if}
									</td>
									{#if canWrite}
										<td class="px-4 py-3 text-right whitespace-nowrap">
											<div class="inline-flex items-center gap-1.5 justify-end">
												<button
													type="button"
													onclick={() => openTaskModal(task)}
													class="inline-flex h-8 items-center gap-1 rounded-button border border-stone bg-white px-2.5 text-xs font-semibold text-ink hover:bg-ash transition-colors cursor-pointer"
												>
													<span class="material-symbols-outlined text-[14px]" aria-hidden="true">edit</span>
													<span>{t('common.edit')}</span>
												</button>
												<form
													method="POST"
													action="?/deleteBonusTask"
													use:enhance={() => {
														deletingTaskId = task.id;
														return async ({ update }) => {
															await update();
															deletingTaskId = null;
														};
													}}
												>
													<input type="hidden" name="id" value={task.id} />
													<DeleteConfirmButton
														label={t('common.delete')}
														confirmTitle={t('common.deleteConfirmTitle')}
														confirmMessage={t('common.deleteConfirm')}
														cancelLabel={t('common.cancel')}
														submitting={deletingTaskId === task.id}
														class="inline-flex h-8 items-center rounded-button border border-error/30 bg-error/10 px-2.5 text-xs font-semibold text-error hover:bg-error/20 transition-colors cursor-pointer"
													/>
												</form>
											</div>
										</td>
									{/if}
								</tr>
							{/each}
						</tbody>
					</table>
				</div>
			{/if}
		</section>
	{:else}
		<section class="space-y-4">
			{#if data.templates.length === 0}
				<div class="rounded-xl border border-stone bg-white p-12 text-center space-y-3 shadow-2xs">
					<div class="w-12 h-12 rounded-xl bg-ash border border-stone flex items-center justify-center text-secondary mx-auto">
						<span class="material-symbols-outlined text-[24px]" aria-hidden="true">layers</span>
					</div>
					<div>
						<h3 class="text-body-base font-bold text-ink">{t('compensationCatalog.noTemplates')}</h3>
						<p class="text-body-sm text-secondary mt-1">{t('compensationCatalog.noTemplatesHint')}</p>
					</div>
					{#if canWrite}
						<button
							type="button"
							onclick={() => openTemplateModal(null)}
							class="inline-flex h-10 items-center gap-1.5 rounded-button bg-ink px-4 text-body-sm font-medium text-white hover:bg-ink/90 cursor-pointer"
						>
							<span class="material-symbols-outlined text-[18px]" aria-hidden="true">add</span>
							<span>{t('compensationCatalog.addTemplate')}</span>
						</button>
					{/if}
				</div>
			{:else}
				<div class="grid grid-cols-1 md:grid-cols-2 gap-4">
					{#each data.templates as tpl (tpl.id)}
						<article class="rounded-xl border border-stone bg-white p-5 shadow-2xs flex flex-col justify-between gap-4">
							<div class="space-y-3">
								<div class="flex items-start justify-between gap-3">
									<div class="min-w-0">
										<h3 class="font-subheading text-[15px] font-bold text-ink truncate">{tpl.name}</h3>
										{#if tpl.description}
											<p class="text-xs text-secondary mt-0.5">{tpl.description}</p>
										{/if}
									</div>
									<span class="shrink-0 rounded-full border px-2.5 py-0.5 text-[11px] font-semibold {tpl.isActive ? 'border-emerald-300 bg-emerald-500/10 text-emerald-800' : 'border-stone bg-ash text-secondary'}">
										{tpl.isActive ? t('settings.statusActive') : t('settings.statusInactive')}
									</span>
								</div>

								<div class="grid grid-cols-2 gap-2.5 rounded-lg border border-stone/80 bg-ash/40 p-3 text-xs">
									<div>
										<span class="text-secondary block">{t('settings.basePayMode')}</span>
										<strong class="text-ink font-semibold">{t(`settings.basePayMode.${tpl.basePayMode}`)}</strong>
									</div>
									<div>
										<span class="text-secondary block">{t('compensationCatalog.configuredBonuses')}</span>
										<strong class="text-ink font-mono tabular-nums">
											{tpl.bonusConfigs.filter((b) => b.isActive).length}
										</strong>
									</div>
									{#if tpl.basePayMode === 'FixedDaily' && tpl.payPolicy?.fixedDailyRate}
										<div>
											<span class="text-secondary block">{t('settings.payPolicy.fixedDailyRate')}</span>
											<strong class="text-ink font-mono tabular-nums">{formatCurrency(tpl.payPolicy.fixedDailyRate, data.locale, currencyCode)}</strong>
										</div>
									{:else if tpl.basePayMode === 'FixedMonthly' && tpl.payPolicy?.fixedMonthlySalary}
										<div>
											<span class="text-secondary block">{t('settings.payPolicy.fixedMonthlySalary')}</span>
											<strong class="text-ink font-mono tabular-nums">{formatCurrency(tpl.payPolicy.fixedMonthlySalary, data.locale, currencyCode)}</strong>
										</div>
									{:else if tpl.basePayMode === 'RevenueShare'}
										<div>
											<span class="text-secondary block">{t('settings.payPolicy.revenueSharePercent')}</span>
											<strong class="text-ink font-mono tabular-nums">{tpl.payPolicy?.revenueSharePercent ?? 0}%</strong>
										</div>
									{:else if tpl.baseHourlyRate > 0}
										<div>
											<span class="text-secondary block">{t('settings.baseHourlyRate')}</span>
											<strong class="text-ink font-mono tabular-nums">{formatCurrency(tpl.baseHourlyRate, data.locale, currencyCode)}</strong>
										</div>
									{/if}
									{#if tpl.tieredRates.length > 0}
										<div>
											<span class="text-secondary block">{t('settings.tieredBrackets')}</span>
											<strong class="text-ink font-mono tabular-nums">{tpl.tieredRates.length}</strong>
										</div>
									{/if}
								</div>
							</div>

							{#if canWrite}
								<div class="pt-3 border-t border-stone flex items-center justify-between gap-2">
									<button
										type="button"
										onclick={() => openTemplateModal(tpl)}
										class="inline-flex h-8 items-center gap-1 rounded-button border border-stone bg-white px-3 text-xs font-semibold text-ink hover:bg-ash transition-colors cursor-pointer"
									>
										<span class="material-symbols-outlined text-[14px]" aria-hidden="true">edit</span>
										<span>{t('common.edit')}</span>
									</button>
									<form
										method="POST"
										action="?/deleteTemplate"
										use:enhance={() => {
											deletingTemplateId = tpl.id;
											return async ({ update }) => {
												await update();
												deletingTemplateId = null;
											};
										}}
									>
										<input type="hidden" name="id" value={tpl.id} />
										<DeleteConfirmButton
											label={t('common.delete')}
											confirmTitle={t('common.deleteConfirmTitle')}
											confirmMessage={t('common.deleteConfirm')}
											cancelLabel={t('common.cancel')}
											submitting={deletingTemplateId === tpl.id}
											class="inline-flex h-8 items-center rounded-button border border-error/30 bg-error/10 px-2.5 text-xs font-semibold text-error hover:bg-error/20 transition-colors cursor-pointer"
										/>
									</form>
								</div>
							{/if}
						</article>
					{/each}
				</div>
			{/if}
		</section>
	{/if}
</div>

<!-- Bonus Task Modal -->
{#if taskModalOpen}
	<div
		transition:fade={{ duration: 150 }}
		class="fixed inset-0 z-50 bg-black/60 backdrop-blur-xs flex items-center justify-center p-4 overflow-y-auto"
		onclick={(e) => {
			if (e.target === e.currentTarget && !taskSubmitting) taskModalOpen = false;
		}}
		role="presentation"
	>
		<div
			class="bg-white border border-stone rounded-2xl shadow-2xl max-w-2xl w-full max-h-[92vh] flex flex-col overflow-hidden my-auto"
			role="dialog"
			tabindex="-1"
			aria-modal="true"
			aria-labelledby="bonus-task-modal-title"
		>
			<div class="px-6 py-4 border-b border-stone flex items-center justify-between bg-surface-container-low shrink-0">
				<div>
					<h3 id="bonus-task-modal-title" class="font-subheading text-[16px] font-bold text-ink">
						{taskDraft.id ? t('compensationCatalog.editTask') : t('compensationCatalog.addTask')}
					</h3>
					<p class="text-xs text-secondary mt-0.5">{t('compensationCatalog.taskModalSubtitle')}</p>
				</div>
				<button
					type="button"
					disabled={taskSubmitting}
					onclick={() => (taskModalOpen = false)}
					aria-label={t('common.close')}
					class="w-8 h-8 rounded-lg hover:bg-ash text-secondary hover:text-ink flex items-center justify-center cursor-pointer"
				>
					<span class="material-symbols-outlined text-[20px]" aria-hidden="true">close</span>
				</button>
			</div>

			<form
				method="POST"
				action="?/saveBonusTask"
				class="flex-1 overflow-y-auto p-6 space-y-4 custom-scrollbar"
				use:enhance={() => {
					taskSubmitting = true;
					return async ({ result, update }) => {
						taskSubmitting = false;
						await update();
						if (result.type === 'success') taskModalOpen = false;
					};
				}}
			>
				<input type="hidden" name="id" value={taskDraft.id} />

				<div class="grid grid-cols-1 sm:grid-cols-2 gap-4 items-end">
					<label class="space-y-1.5 block">
						<span class="font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block leading-4">
							{t('compensationCatalog.taskLabel')} *
						</span>
						<input
							name="label"
							required
							maxlength="150"
							bind:value={taskDraft.label}
							placeholder={t('compensationCatalog.taskLabelPlaceholder')}
							class="w-full h-10 rounded-input border border-stone bg-white px-3 text-body-sm text-ink focus:border-ink focus:outline-none"
						/>
					</label>
					<label class="space-y-1.5 block">
						<span class="font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block leading-4">
							{t('compensationCatalog.taskKey')} *
						</span>
						<input
							name="taskKey"
							required
							maxlength="60"
							bind:value={taskDraft.taskKey}
							placeholder="CLEANING_REPASTE"
							class="w-full h-10 rounded-input border border-stone bg-white px-3 text-body-sm font-mono uppercase text-ink focus:border-ink focus:outline-none"
						/>
					</label>
					<label class="space-y-1.5 block">
						<span class="font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block leading-4">
							{t('compensationCatalog.colCategory')}
						</span>
						<input
							name="category"
							maxlength="60"
							bind:value={taskDraft.category}
							placeholder={t('compensationCatalog.categoryPlaceholder')}
							class="w-full h-10 rounded-input border border-stone bg-white px-3 text-body-sm text-ink focus:border-ink focus:outline-none"
						/>
					</label>
					<div class="space-y-1.5">
						<span class="font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block leading-4">
							{t('tickets.workItems.type')}
						</span>
						<Dropdown
							name="defaultWorkItemType"
							bind:value={taskDraft.defaultWorkItemType}
							options={[
								{ value: 'Service', label: t('tickets.workItems.itemType.Service') },
								{ value: 'NonStockPart', label: t('tickets.workItems.itemType.NonStockPart') },
								{ value: 'Fee', label: t('tickets.workItems.itemType.Fee') }
							]}
						/>
					</div>
				</div>

				<div class="grid grid-cols-1 sm:grid-cols-3 gap-4 items-end">
					<div class="space-y-1.5">
						<span class="font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block leading-4">
							{t('compensationCatalog.standardRate')}
						</span>
						<CurrencyInput
							name="defaultStandardRate"
							prefix={currencySymbol}
							locale={data.locale}
							currency={currencyCode}
							bind:value={taskDraft.defaultStandardRate}
						/>
					</div>
					<div class="space-y-1.5">
						<span class="font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block leading-4">
							{tier2Label}
						</span>
						<CurrencyInput
							name="defaultGamingRate"
							prefix={currencySymbol}
							locale={data.locale}
							currency={currencyCode}
							bind:value={taskDraft.defaultGamingRate}
						/>
					</div>
					<div class="space-y-1.5">
						<span class="font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block leading-4">
							{t('compensationCatalog.customerPrice')}
						</span>
						<CurrencyInput
							name="defaultCustomerPrice"
							prefix={currencySymbol}
							locale={data.locale}
							currency={currencyCode}
							disabled={taskDraft.defaultIsInternalOnly}
							bind:value={taskDraft.defaultCustomerPrice}
						/>
					</div>
				</div>

				<label class="space-y-1.5 block">
					<span class="font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block leading-4">
						{t('compensationCatalog.legacyKeywords')}
					</span>
					<input
						name="legacyKeywords"
						maxlength="500"
						bind:value={taskDraft.legacyKeywords}
						placeholder={t('compensationCatalog.legacyKeywordsPlaceholder')}
						class="w-full h-10 rounded-input border border-stone bg-white px-3 text-body-sm text-ink focus:border-ink focus:outline-none"
					/>
				</label>

				<label class="space-y-1.5 block">
					<span class="font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block leading-4">
						{t('inventory.description')}
					</span>
					<input
						name="description"
						maxlength="500"
						bind:value={taskDraft.description}
						class="w-full h-10 rounded-input border border-stone bg-white px-3 text-body-sm text-ink focus:border-ink focus:outline-none"
					/>
				</label>

				<div class="grid grid-cols-1 sm:grid-cols-2 gap-3 pt-1">
					<label class="flex items-start gap-2.5 rounded-xl border border-stone bg-ash/30 p-3 cursor-pointer">
						<input
							type="checkbox"
							name="defaultIsInternalOnly"
							bind:checked={taskDraft.defaultIsInternalOnly}
							class="mt-0.5 rounded border-stone text-ink focus:ring-0"
						/>
						<span class="text-xs">
							<strong class="block text-ink font-semibold">{t('compensationCatalog.internalOnlyLabel')}</strong>
							<span class="text-secondary">{t('compensationCatalog.internalOnlyHint')}</span>
						</span>
					</label>
					<label class="flex items-start gap-2.5 rounded-xl border border-stone bg-ash/30 p-3 cursor-pointer">
						<input
							type="checkbox"
							name="isActive"
							bind:checked={taskDraft.isActive}
							class="mt-0.5 rounded border-stone text-ink focus:ring-0"
						/>
						<span class="text-xs">
							<strong class="block text-ink font-semibold">{t('settings.active')}</strong>
							<span class="text-secondary">{t('compensationCatalog.taskActiveHint', { ticket: terms.ticketSingular })}</span>
						</span>
					</label>
				</div>

				<input type="hidden" name="sortOrder" value={taskDraft.sortOrder} />

				<div class="pt-4 border-t border-stone flex items-center justify-end gap-2">
					<button
						type="button"
						disabled={taskSubmitting}
						onclick={() => (taskModalOpen = false)}
						class="h-10 px-4 rounded-button border border-stone bg-white text-secondary hover:text-ink hover:bg-ash text-body-sm font-medium cursor-pointer"
					>
						{t('common.cancel')}
					</button>
					<button
						type="submit"
						disabled={taskSubmitting}
						class="h-10 px-5 rounded-button bg-ink text-white hover:bg-ink/90 text-body-sm font-medium cursor-pointer inline-flex items-center gap-1.5 disabled:opacity-60"
					>
						{#if taskSubmitting}
							<span class="material-symbols-outlined text-[16px] animate-spin" aria-hidden="true">progress_activity</span>
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

<!-- Compensation Template Modal -->
{#if templateModalOpen}
	<div
		transition:fade={{ duration: 150 }}
		class="fixed inset-0 z-50 bg-black/60 backdrop-blur-xs flex items-center justify-center p-4 overflow-y-auto"
		onclick={(e) => {
			if (e.target === e.currentTarget && !templateSubmitting) templateModalOpen = false;
		}}
		role="presentation"
	>
		<div
			class="bg-white border border-stone rounded-2xl shadow-2xl max-w-3xl w-full max-h-[92vh] flex flex-col overflow-hidden my-auto"
			role="dialog"
			tabindex="-1"
			aria-modal="true"
			aria-labelledby="comp-template-modal-title"
		>
			<div class="px-6 py-4 border-b border-stone flex items-center justify-between bg-surface-container-low shrink-0">
				<div>
					<h3 id="comp-template-modal-title" class="font-subheading text-[16px] font-bold text-ink">
						{tplId ? t('compensationCatalog.editTemplate') : t('compensationCatalog.addTemplate')}
					</h3>
					<p class="text-xs text-secondary mt-0.5">{t('compensationCatalog.templateModalSubtitle')}</p>
				</div>
				<button
					type="button"
					disabled={templateSubmitting}
					onclick={() => (templateModalOpen = false)}
					aria-label={t('common.close')}
					class="w-8 h-8 rounded-lg hover:bg-ash text-secondary hover:text-ink flex items-center justify-center cursor-pointer"
				>
					<span class="material-symbols-outlined text-[20px]" aria-hidden="true">close</span>
				</button>
			</div>

			<form
				method="POST"
				action="?/saveTemplate"
				class="flex-1 overflow-y-auto p-6 space-y-5 custom-scrollbar"
				use:enhance={() => {
					templateSubmitting = true;
					return async ({ result, update }) => {
						templateSubmitting = false;
						await update();
						if (result.type === 'success') templateModalOpen = false;
					};
				}}
			>
				<input type="hidden" name="id" value={tplId} />
				<input type="hidden" name="tieredRatesJson" value={JSON.stringify(tplBrackets)} />
				<input type="hidden" name="payPolicyJson" value={tplPayPolicyJson} />
				<input type="hidden" name="bonusConfigsJson" value={tplBonusConfigsJson} />
				<input type="hidden" name="sortOrder" value={tplSortOrder} />

				<div class="grid grid-cols-1 sm:grid-cols-2 gap-4 items-end">
					<label class="space-y-1.5 block">
						<span class="font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block leading-4">
							{t('compensationCatalog.templateName')} *
						</span>
						<input
							name="name"
							required
							maxlength="120"
							bind:value={tplName}
							placeholder={t('compensationCatalog.templateNamePlaceholder')}
							class="w-full h-10 rounded-input border border-stone bg-white px-3 text-body-sm text-ink focus:border-ink focus:outline-none"
						/>
					</label>
					<label class="space-y-1.5 block">
						<span class="font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block leading-4">
							{t('inventory.description')}
						</span>
						<input
							name="description"
							maxlength="500"
							bind:value={tplDescription}
							class="w-full h-10 rounded-input border border-stone bg-white px-3 text-body-sm text-ink focus:border-ink focus:outline-none"
						/>
					</label>
				</div>

				<!-- Base Pay Configuration -->
				<div class="rounded-xl border border-stone bg-ash/30 p-4 space-y-4">
					<div class="grid grid-cols-1 sm:grid-cols-2 gap-4 items-end">
						<div class="space-y-1.5">
							<span class="font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block leading-4">
								{t('settings.basePayMode')}
							</span>
							<Dropdown
								name="basePayMode"
								bind:value={tplBasePayMode}
								options={BASE_PAY_MODES.map((m) => ({ value: m, label: t(`settings.basePayMode.${m}`) }))}
							/>
						</div>

						{#if tplBasePayMode === 'FixedDaily'}
							<div class="space-y-1.5">
								<span class="font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block leading-4">
									{t('settings.payPolicy.fixedDailyRate')}
								</span>
								<CurrencyInput prefix={currencySymbol} locale={data.locale} currency={currencyCode} bind:value={tplFixedDailyRate} />
							</div>
						{:else if tplBasePayMode === 'FixedMonthly'}
							<div class="space-y-1.5">
								<span class="font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block leading-4">
									{t('settings.payPolicy.fixedMonthlySalary')}
								</span>
								<CurrencyInput prefix={currencySymbol} locale={data.locale} currency={currencyCode} bind:value={tplFixedMonthlySalary} />
							</div>
						{:else if tplBasePayMode === 'RevenueShare'}
							<label class="space-y-1.5 block">
								<span class="font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block leading-4">
									{t('settings.payPolicy.revenueSharePercent')}
								</span>
								<input
									type="number"
									min="0"
									max="100"
									step="0.5"
									bind:value={tplRevenueSharePercent}
									class="w-full h-10 rounded-input border border-stone bg-white px-3 text-body-sm font-mono tabular-nums text-ink focus:border-ink focus:outline-none"
								/>
							</label>
						{:else if tplBasePayMode !== 'CommissionOnly'}
							<div class="space-y-1.5">
								<span class="font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block leading-4">
									{t('settings.baseHourlyRate')}
								</span>
								<CurrencyInput name="baseHourlyRate" prefix={currencySymbol} locale={data.locale} currency={currencyCode} bind:value={tplBaseHourlyRate} />
							</div>
						{/if}
					</div>

					{#if tplBasePayMode === 'FixedMonthly'}
						<div class="grid grid-cols-1 sm:grid-cols-2 gap-4 items-center">
							<label class="space-y-1.5 block">
								<span class="font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block leading-4">
									{t('settings.payPolicy.standardWorkDays')}
								</span>
								<input
									type="number"
									min="1"
									max="31"
									bind:value={tplStandardWorkDays}
									class="w-full h-10 rounded-input border border-stone bg-white px-3 text-body-sm font-mono tabular-nums text-ink focus:border-ink focus:outline-none"
								/>
							</label>
							<label class="flex items-center gap-2 text-xs font-medium text-ink pt-4 cursor-pointer">
								<input type="checkbox" bind:checked={tplProrateMonthly} class="rounded border-stone text-ink focus:ring-0" />
								<span>{t('settings.payPolicy.prorateMonthly')}</span>
							</label>
						</div>
					{/if}

					{#if showTieredSection}
						<div class="space-y-2.5 pt-2 border-t border-stone/60">
							<div class="flex items-center justify-between gap-2">
								<span class="text-xs font-bold text-ink uppercase tracking-wider font-label-caps">{t('settings.tieredBrackets')}</span>
								<button
									type="button"
									onclick={addTplBracket}
									class="px-2.5 py-1 rounded-button bg-ink text-white text-xs font-medium hover:bg-ink/90 cursor-pointer"
								>
									+ {t('settings.addTierBracket')}
								</button>
							</div>
							{#each tplBrackets as bracket, index}
								<div class="grid grid-cols-2 sm:grid-cols-[1fr_1fr_1.4fr_auto] gap-2 items-end rounded-lg border border-stone bg-white p-2.5">
									<label class="space-y-1">
										<span class="text-[10px] font-bold uppercase text-secondary block leading-4">{t('settings.tierMinHours')}</span>
										<input type="number" min="0" step="0.25" bind:value={bracket.minHours} class="w-full h-9 rounded-input border border-stone px-2 text-body-sm font-mono tabular-nums" />
									</label>
									<label class="space-y-1">
										<span class="text-[10px] font-bold uppercase text-secondary block leading-4">{t('settings.tierMaxHours')}</span>
										<input type="number" min="0" step="0.25" bind:value={bracket.maxHours} class="w-full h-9 rounded-input border border-stone px-2 text-body-sm font-mono tabular-nums" />
									</label>
									<div class="space-y-1">
										<span class="text-[10px] font-bold uppercase text-secondary block leading-4">{t('settings.tierFlatRate')}</span>
										<CurrencyInput prefix={currencySymbol} locale={data.locale} currency={currencyCode} bind:value={bracket.flatRate} inputClass="h-[34px] px-2 text-body-sm font-mono tabular-nums" />
									</div>
									<button
										type="button"
										onclick={() => removeTplBracket(index)}
										class="h-9 px-2.5 rounded-button border border-error/30 bg-error/10 text-error hover:bg-error/20 text-xs font-semibold cursor-pointer"
									>
										{t('settings.removeTier')}
									</button>
								</div>
							{/each}
						</div>
					{/if}
				</div>

				<!-- Task Itemized Bonus Rules -->
				<div class="rounded-xl border border-stone bg-white p-4 space-y-3">
					<div class="flex flex-wrap items-center justify-between gap-2">
						<label class="flex items-center gap-2 cursor-pointer">
							<input type="checkbox" bind:checked={tplTaskBonusActive} class="rounded border-stone text-ink focus:ring-0" />
							<span class="font-bold text-ink text-body-sm">{t('settings.bonus.taskItemizedTitle')}</span>
						</label>
						{#if tplTaskBonusActive}
							<button
								type="button"
								onclick={importTasksIntoTemplate}
								class="px-2.5 py-1 rounded-button border border-stone bg-white text-xs font-semibold text-ink hover:bg-ash cursor-pointer"
							>
								{t('compensationCatalog.importFromCatalog')}
							</button>
						{/if}
					</div>

					{#if tplTaskBonusActive}
						<div class="flex flex-wrap items-center gap-4 text-xs pt-1 border-t border-stone/60">
							<span class="font-semibold text-secondary">{t('settings.bonus.payoutTrigger')}:</span>
							<label class="inline-flex items-center gap-1.5 cursor-pointer text-ink">
								<input type="radio" value="TicketFinalized" bind:group={tplTaskPayoutTrigger} class="text-ink focus:ring-0" />
								<span>{t('settings.bonus.payoutTrigger.TicketFinalized')}</span>
							</label>
							<label class="inline-flex items-center gap-1.5 cursor-pointer text-ink">
								<input type="radio" value="TaskCompleted" bind:group={tplTaskPayoutTrigger} class="text-ink focus:ring-0" />
								<span>{t('settings.bonus.payoutTrigger.TaskCompleted')}</span>
							</label>
						</div>

						{#if tplTaskRules.length > 0}
							<div class="space-y-2 max-h-56 overflow-y-auto pr-1 custom-scrollbar">
								{#each tplTaskRules as rule, index}
									<div class="grid grid-cols-1 sm:grid-cols-[1.5fr_1fr_1.2fr_auto] gap-2 items-end rounded-lg border border-stone bg-ash/30 p-2.5">
										<label class="space-y-1">
											<span class="text-[10px] font-bold uppercase text-secondary block leading-4">{t('settings.bonus.taskLabel')}</span>
											<input type="text" bind:value={rule.taskLabel} class="w-full h-9 rounded-input border border-stone bg-white px-2 text-body-sm" />
										</label>
										<div class="space-y-1">
											<span class="text-[10px] font-bold uppercase text-secondary block leading-4">{t('compensationCatalog.assetTier', { asset: terms.assetSingular })}</span>
											<Dropdown
												size="sm"
												buttonClass="h-9"
												bind:value={rule.deviceCategory}
												options={[
													{ value: 'Any', label: t('settings.bonus.deviceCategory.Any') },
													{ value: 'Standard', label: t('settings.bonus.deviceCategory.Standard') },
													{ value: 'Gaming', label: isItBlueprint ? t('settings.bonus.deviceCategory.Gaming') : t('settings.bonus.deviceCategory.Premium') }
												]}
											/>
										</div>
										<div class="space-y-1">
											<span class="text-[10px] font-bold uppercase text-secondary block leading-4">{t('settings.bonus.taskReward')}</span>
											<CurrencyInput prefix={currencySymbol} locale={data.locale} currency={currencyCode} bind:value={rule.rewardPerUnit} inputClass="h-[34px] px-2 text-body-sm font-mono tabular-nums" />
										</div>
										<button
											type="button"
											onclick={() => removeTplRule(index)}
											class="h-9 px-2.5 rounded-button border border-error/30 bg-error/10 text-error hover:bg-error/20 text-xs font-semibold cursor-pointer"
										>
											{t('settings.removeTier')}
										</button>
									</div>
								{/each}
							</div>
						{/if}
					{/if}
				</div>

				<label class="flex items-center gap-2 text-body-sm font-medium text-ink cursor-pointer">
					<input type="checkbox" name="isActive" bind:checked={tplIsActive} class="rounded border-stone text-ink focus:ring-0" />
					<span>{t('settings.active')}</span>
				</label>

				<div class="pt-4 border-t border-stone flex items-center justify-end gap-2">
					<button
						type="button"
						disabled={templateSubmitting}
						onclick={() => (templateModalOpen = false)}
						class="h-10 px-4 rounded-button border border-stone bg-white text-secondary hover:text-ink hover:bg-ash text-body-sm font-medium cursor-pointer"
					>
						{t('common.cancel')}
					</button>
					<button
						type="submit"
						disabled={templateSubmitting}
						class="h-10 px-5 rounded-button bg-ink text-white hover:bg-ink/90 text-body-sm font-medium cursor-pointer inline-flex items-center gap-1.5 disabled:opacity-60"
					>
						{#if templateSubmitting}
							<span class="material-symbols-outlined text-[16px] animate-spin" aria-hidden="true">progress_activity</span>
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
