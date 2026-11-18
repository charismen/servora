<script lang="ts">
	import { enhance } from '$app/forms';
	import { goto } from '$app/navigation';
	import PageHeader from '$lib/components/PageHeader.svelte';
	import DeleteConfirmButton from '$lib/components/DeleteConfirmButton.svelte';
	import CurrencyInput from '$lib/components/CurrencyInput.svelte';
	import Dropdown from '$lib/components/ui/dropdown/Dropdown.svelte';
	import Pagination from '$lib/components/ui/pagination/Pagination.svelte';
	import { can, permissions } from '$lib/auth/permissions';
	import { createTranslator, formatCurrency } from '$lib/i18n';
	import { fade } from 'svelte/transition';
	import type {
		BasePayMode,
		CompletionMilestone,
		ShopPermission,
		ShopRole,
		ShopUser,
		TaskItemizedRuleConfig,
		WageTierBracket
	} from '$lib/types/shop-users';
	import type { ActionData, PageData } from './$types';

	let { data, form }: { data: PageData; form: ActionData } = $props();
	const t = $derived(createTranslator(data.locale));
	const canWrite = $derived(can(data.user.permissions, permissions.settingsWrite));
	const currencyCode = $derived(data.shopProfile?.currencyCode ?? 'IDR');

	let activeTab = $state<'users' | 'roles'>('users');
	let roleSearch = $state('');
	let filterStatus = $state('');
	let filterContract = $state('');
	let filterRoleId = $state('0');
	let filterSort = $state('name-asc');
	let filterPageSize = $state('10');

	$effect(() => {
		activeTab = data.tab === 'roles' ? 'roles' : 'users';
	});

	$effect(() => {
		filterStatus = data.filters.status ?? '';
		filterContract = data.filters.contract ?? '';
		filterRoleId = String(data.filters.roleId ?? 0);
		filterSort = data.filters.sort ?? 'name-asc';
		filterPageSize = String(data.users.pageSize ?? 10);
	});

	const users = $derived(data.users.items ?? []);
	const pageSizeOptions = [5, 10, 20, 50, 100];

	const hasActiveUserFilters = $derived(
		Boolean(
			data.filters.search ||
				data.filters.status ||
				data.filters.contract ||
				data.filters.roleId ||
				(data.filters.sort && data.filters.sort !== 'name-asc')
		)
	);

	const filteredRoles = $derived(
		data.roles.filter((role) => {
			const term = roleSearch.trim().toLowerCase();
			return !term || `${role.name} ${role.description ?? ''} ${(role.permissions ?? []).join(' ')}`.toLowerCase().includes(term);
		})
	);

	// Modal & submission states
	let addUserModalOpen = $state(false);
	let addUserSubmitting = $state(false);
	let newShowPassword = $state(false);
	let newEmploymentType = $state<string>('FullTime');

	let editingStaffUser = $state<ShopUser | null>(null);
	let editUserPassword = $state('');
	let showEditPassword = $state(false);
	let editUserRoleIds = $state<number[]>([]);
	let editUserSubmitting = $state(false);
	let editRolesSubmitting = $state(false);
	let editUserFeedback = $state<{ success: boolean; message: string } | null>(null);
	let editRolesFeedback = $state<{ success: boolean; message: string } | null>(null);

	let statusSubmittingUserId = $state<number | null>(null);

	let editingRole = $state<ShopRole | null>(null);
	let roleSubmitting = $state(false);
	let deletingRoleId = $state<number | null>(null);
	let selectedPermissions = $state<string[]>([]);

	let editingUser = $state<ShopUser | null>(null);
	let freelanceSubmitting = $state(false);
	let modalBasePayMode = $state<BasePayMode>('TieredDaily');
	let modalBaseHourlyRate = $state<number>(7500);
	let modalBrackets = $state<WageTierBracket[]>([]);
	let modalMaintenanceActive = $state(true);
	let modalMaintenanceMode = $state<'Simple' | 'Milestones'>('Simple');
	let modalMaintenanceThreshold = $state(4);
	let modalMaintenanceRewardPerUnit = $state(15000);
	let modalMaintenanceFlatReward = $state(0);
	let modalMaintenanceCountAll = $state(false);
	let modalMaintenanceIsRetroactive = $state(true);
	let modalMilestones = $state<CompletionMilestone[]>([
		{ minUnits: 1, maxUnits: 3, rewardPerUnit: 150000 },
		{ minUnits: 4, maxUnits: 9999, rewardPerUnit: 250000 }
	]);
	let modalCancellationActive = $state(true);
	let modalCancellationFee = $state(25000);
	let modalCancellationRequireEvidence = $state(true);
	let modalTaskItemizedActive = $state(false);
	let modalTaskRules = $state<TaskItemizedRuleConfig[]>([]);
	let nextTaskRuleSeq = 1;

	const permissionCategoriesMeta: { key: string; titleKey: string; icon: string }[] = [
		{ key: 'customers', titleKey: 'settings.permissionGroup.customers', icon: 'person' },
		{ key: 'devices', titleKey: 'settings.permissionGroup.devices', icon: 'devices' },
		{ key: 'serviceTickets', titleKey: 'settings.permissionGroup.serviceTickets', icon: 'confirmation_number' },
		{ key: 'inventory', titleKey: 'settings.permissionGroup.inventory', icon: 'inventory_2' },
		{ key: 'assets', titleKey: 'settings.permissionGroup.assets', icon: 'precision_manufacturing' },
		{ key: 'attendance', titleKey: 'settings.permissionGroup.attendance', icon: 'schedule' },
		{ key: 'payroll', titleKey: 'settings.permissionGroup.payroll', icon: 'payments' },
		{ key: 'reports', titleKey: 'settings.permissionGroup.reports', icon: 'bar_chart' },
		{ key: 'settings', titleKey: 'settings.permissionGroup.settings', icon: 'tune' },
		{ key: 'other', titleKey: 'settings.permissionGroup.other', icon: 'lock' }
	];

	function getPermissionCategoryKey(code: string): string {
		const lower = code.toLowerCase();
		if (lower.includes('customer')) return 'customers';
		if (lower.includes('device')) return 'devices';
		if (lower.includes('serviceticket')) return 'serviceTickets';
		if (lower.includes('inventory')) return 'inventory';
		if (lower.includes('asset')) return 'assets';
		if (lower.includes('attendance')) return 'attendance';
		if (lower.includes('payroll')) return 'payroll';
		if (lower.includes('report')) return 'reports';
		if (lower.includes('setting')) return 'settings';
		return 'other';
	}

	const groupedPermissions = $derived.by(() => {
		const map = new Map<string, ShopPermission[]>();
		for (const cat of permissionCategoriesMeta) {
			map.set(cat.key, []);
		}
		for (const perm of data.permissions) {
			const key = getPermissionCategoryKey(perm.code);
			const list = map.get(key) ?? [];
			list.push(perm);
			map.set(key, list);
		}
		return permissionCategoriesMeta
			.map((cat) => ({
				...cat,
				permissions: map.get(cat.key) ?? []
			}))
			.filter((cat) => cat.permissions.length > 0);
	});

	$effect(() => {
		const handleEscape = (e: KeyboardEvent) => {
			if (e.key !== 'Escape') return;
			if (editingUser && !freelanceSubmitting) {
				editingUser = null;
			} else if (editingRole && !roleSubmitting) {
				editingRole = null;
			} else if (editingStaffUser && !editUserSubmitting && !editRolesSubmitting) {
				editingStaffUser = null;
			} else if (addUserModalOpen && !addUserSubmitting) {
				addUserModalOpen = false;
			}
		};
		window.addEventListener('keydown', handleEscape);
		return () => window.removeEventListener('keydown', handleEscape);
	});

	function buildQuery(
		overrides: Partial<{
			tab: 'users' | 'roles';
			page: number;
			pageSize: number;
			search: string;
			status: string;
			contract: string;
			roleId: number;
			sort: string;
		}> = {}
	) {
		const params = new URLSearchParams();
		const targetTab = overrides.tab ?? activeTab;
		if (targetTab === 'roles') params.set('tab', 'roles');

		const page = overrides.page ?? data.users.page;
		const pageSize = overrides.pageSize ?? data.users.pageSize;
		const search = overrides.search ?? data.filters.search;
		const status = overrides.status ?? data.filters.status;
		const contract = overrides.contract ?? data.filters.contract;
		const roleId = overrides.roleId ?? data.filters.roleId;
		const sort = overrides.sort ?? data.filters.sort;

		if (targetTab === 'users') {
			params.set('page', String(page));
			params.set('pageSize', String(pageSize));
			params.set('sort', sort);
			if (search) params.set('search', search);
			if (status) params.set('status', status);
			if (contract) params.set('contract', contract);
			if (roleId) params.set('roleId', String(roleId));
		}

		const qs = params.toString();
		return qs ? `?${qs}` : '?tab=users';
	}

	function switchTab(nextTab: 'users' | 'roles') {
		activeTab = nextTab;
		void goto(buildQuery({ tab: nextTab }), { keepFocus: true, noScroll: true, replaceState: true });
	}

	function applyQuickFilter(
		overrides: Partial<{ status: string; contract: string; roleId: number; search: string }>
	) {
		activeTab = 'users';
		void goto(buildQuery({ tab: 'users', page: 1, ...overrides }), { keepFocus: true, noScroll: true });
	}

	function pageHref(page: number) {
		return buildQuery({ tab: 'users', page });
	}

	function getRoleStaffCount(roleId: number): number {
		return data.summary.roleCounts?.[roleId] ?? users.filter((u) => u.roles.some((r) => r.id === roleId)).length;
	}

	function openEditUserModal(user: ShopUser) {
		editingStaffUser = user;
		editUserPassword = '';
		showEditPassword = false;
		editUserRoleIds = user.roles.map((r) => r.id);
		editUserFeedback = null;
		editRolesFeedback = null;
	}

	function openRoleModal(role: ShopRole | null = null) {
		if (role) {
			editingRole = { ...role };
			selectedPermissions = [...(role.permissions ?? [])];
		} else {
			editingRole = { id: 0, name: '', description: '', permissions: [] };
			selectedPermissions = [];
		}
	}

	function togglePermission(code: string) {
		if (selectedPermissions.includes(code)) {
			selectedPermissions = selectedPermissions.filter((c) => c !== code);
		} else {
			selectedPermissions = [...selectedPermissions, code];
		}
	}

	function toggleCategoryPermissions(category: { permissions: ShopPermission[] }) {
		const categoryCodes = category.permissions.map((p) => p.code);
		const allSelected = categoryCodes.every((c) => selectedPermissions.includes(c));
		if (allSelected) {
			selectedPermissions = selectedPermissions.filter((c) => !categoryCodes.includes(c));
		} else {
			const newSet = new Set([...selectedPermissions, ...categoryCodes]);
			selectedPermissions = Array.from(newSet);
		}
	}

	function openFreelanceModal(user: ShopUser) {
		editingUser = user;
		const profile = user.freelanceProfile;
		modalBasePayMode = (profile?.basePayMode as BasePayMode) ?? 'TieredDaily';
		modalBaseHourlyRate = profile?.baseHourlyRate ?? 7500;
		modalBrackets = profile?.tieredRates?.length
			? profile.tieredRates.map((b) => ({ ...b }))
			: [
					{ minHours: 5, maxHours: 6, flatRate: 35000, hourlyRate: null },
					{ minHours: 8, maxHours: 24, flatRate: 60000, hourlyRate: null }
				];

		const maintCfg = profile?.bonusConfigs?.find((c) => c.bonusTypeCode === 'MAINTENANCE_COMPLETION');
		modalMaintenanceActive = maintCfg ? maintCfg.isActive : true;
		modalMaintenanceThreshold = Number(maintCfg?.parameters?.threshold ?? 4);
		modalMaintenanceRewardPerUnit = Number(maintCfg?.parameters?.rewardPerUnit ?? 15000);
		modalMaintenanceFlatReward = Number(maintCfg?.parameters?.flatReward ?? 0);
		modalMaintenanceCountAll = Boolean(maintCfg?.parameters?.countAllUnitsOnceThresholdMet ?? false);
		const rawMilestones = maintCfg?.parameters?.milestones as CompletionMilestone[] | undefined;
		modalMilestones =
			Array.isArray(rawMilestones) && rawMilestones.length > 0
				? rawMilestones.map((m) => ({
						minUnits: Number(m.minUnits),
						maxUnits: Number(m.maxUnits),
						rewardPerUnit: Number(m.rewardPerUnit),
						flatReward: Number(m.flatReward ?? 0)
					}))
				: [
						{ minUnits: 1, maxUnits: 3, rewardPerUnit: 150000 },
						{ minUnits: 4, maxUnits: 9999, rewardPerUnit: 250000 }
					];
		modalMaintenanceMode =
			(maintCfg?.parameters?.mode as 'Simple' | 'Milestones') ||
			(Array.isArray(rawMilestones) && rawMilestones.length > 0 ? 'Milestones' : 'Simple');
		modalMaintenanceIsRetroactive = maintCfg?.parameters?.isRetroactive !== false;

		const cancelCfg = profile?.bonusConfigs?.find((c) => c.bonusTypeCode === 'CUSTOMER_CANCELLATION');
		modalCancellationActive = cancelCfg ? cancelCfg.isActive : true;
		modalCancellationFee = Number(cancelCfg?.parameters?.feePerIncident ?? 25000);
		modalCancellationRequireEvidence =
			cancelCfg?.parameters?.requireDiagnosisOrInspectionHistory !== undefined
				? Boolean(cancelCfg.parameters.requireDiagnosisOrInspectionHistory)
				: true;

		const taskCfg = profile?.bonusConfigs?.find((c) => c.bonusTypeCode === 'TASK_ITEMIZED');
		modalTaskItemizedActive = taskCfg ? taskCfg.isActive : false;
		const rawRules = taskCfg?.parameters?.rules as TaskItemizedRuleConfig[] | undefined;
		modalTaskRules =
			Array.isArray(rawRules) && rawRules.length > 0
				? rawRules.map((r) => ({
						taskKey: String(r.taskKey || `TASK_${Date.now()}_${nextTaskRuleSeq++}`),
						taskLabel: String(r.taskLabel || ''),
						matcherKeyword: String(r.matcherKeyword || ''),
						deviceCategory: (r.deviceCategory || 'Any') as 'Any' | 'Standard' | 'Gaming',
						rewardPerUnit: Number(r.rewardPerUnit || 25000)
					}))
				: [];
	}

	function loadDefaultMilestones() {
		modalMilestones = [
			{ minUnits: 1, maxUnits: 3, rewardPerUnit: 150000 },
			{ minUnits: 4, maxUnits: 9999, rewardPerUnit: 250000 }
		];
	}

	function addMilestone() {
		const last = modalMilestones[modalMilestones.length - 1];
		const nextMin = last ? last.maxUnits + 1 : 1;
		modalMilestones = [
			...modalMilestones,
			{ minUnits: nextMin, maxUnits: nextMin + 3, rewardPerUnit: (last?.rewardPerUnit ?? 150000) + 50000 }
		];
	}

	function removeMilestone(index: number) {
		modalMilestones = modalMilestones.filter((_, idx) => idx !== index);
	}

	function loadDefaultTaskRules() {
		modalTaskRules = [
			{
				taskKey: 'OS_INSTALL',
				taskLabel: 'OS / Software Reinstallation',
				matcherKeyword: 'OS,Install OS,Clean Install,OS Reinstall,Reinstall OS,Windows',
				deviceCategory: 'Any',
				rewardPerUnit: 25000
			},
			{
				taskKey: 'KEYBOARD_BATTERY',
				taskLabel: 'Keyboard / Battery Replacement',
				matcherKeyword: 'Keyboard,Battery Replacement,Replace Battery,New Battery,Swap Battery,Ganti Baterai,Tukar Baterai,Pasang Baterai',
				deviceCategory: 'Any',
				rewardPerUnit: 25000
			},
			{
				taskKey: 'CLEANING_REPASTE_STD',
				taskLabel: 'Cleaning & Thermal Repasting (Standard)',
				matcherKeyword: 'Repaste,Repasting,Cleaning,Clean Dust,Thermal,Pasta',
				deviceCategory: 'Standard',
				rewardPerUnit: 25000
			},
			{
				taskKey: 'CLEANING_REPASTE_GAMING',
				taskLabel: 'Cleaning & Thermal Repasting (Gaming)',
				matcherKeyword: 'Repaste,Repasting,Cleaning,Clean Dust,Thermal,Pasta',
				deviceCategory: 'Gaming',
				rewardPerUnit: 50000
			}
		];
	}

	function addTaskRule() {
		modalTaskRules = [
			...modalTaskRules,
			{
				taskKey: `TASK_${Date.now()}_${nextTaskRuleSeq++}`,
				taskLabel: '',
				matcherKeyword: '',
				deviceCategory: 'Any',
				rewardPerUnit: 25000
			}
		];
	}

	function removeTaskRule(index: number) {
		modalTaskRules = modalTaskRules.filter((_, idx) => idx !== index);
	}

	function loadDefaultTiers() {
		modalBrackets = [
			{ minHours: 5, maxHours: 6, flatRate: 35000, hourlyRate: null },
			{ minHours: 8, maxHours: 24, flatRate: 60000, hourlyRate: null }
		];
	}

	function addBracket() {
		modalBrackets = [...modalBrackets, { minHours: 1, maxHours: 4, flatRate: 25000, hourlyRate: null }];
	}

	function removeBracket(index: number) {
		modalBrackets = modalBrackets.filter((_, idx) => idx !== index);
	}

	const employmentLabel = (type: string) =>
		type === 'Freelance' ? t('settings.employmentType.Freelance') : t('settings.employmentType.FullTime');

	function getFreelanceSummary(user: ShopUser): string {
		const profile = user.freelanceProfile;
		if (!profile) return t('usersRoles.compensationDefault');
		const activeBonuses = profile.bonusConfigs?.filter((c) => c.isActive).length ?? 0;
		const modeText = t(`settings.basePayMode.${profile.basePayMode}`);
		if (profile.basePayMode === 'Hourly' && profile.baseHourlyRate > 0) {
			return `${formatCurrency(profile.baseHourlyRate, data.locale, currencyCode)} · ${t('usersRoles.compensationConfigured', { count: activeBonuses })}`;
		}
		return activeBonuses > 0
			? `${modeText} · ${t('usersRoles.compensationConfigured', { count: activeBonuses })}`
			: modeText;
	}
</script>

<div class="space-y-lg pb-xl min-w-0 max-w-full">
	<PageHeader
		items={[
			{ label: t('common.console'), href: '/app' },
			{ label: t('nav.usersRoles') }
		]}
		title={t('nav.usersRoles')}
		subtitle={t('settings.usersRolesSubtitle')}
	>
		{#snippet actions()}
			{#if canWrite}
				{#if activeTab === 'users'}
					<button
						type="button"
						onclick={() => (addUserModalOpen = true)}
						class="inline-flex h-9 items-center gap-1.5 rounded-button bg-ink px-3.5 text-body-sm font-medium text-white shadow-xs hover:bg-ink/90 active:scale-95 transition-all cursor-pointer"
					>
						<span class="material-symbols-outlined text-[18px]">person_add</span>
						<span>{t('settings.addUser')}</span>
					</button>
				{:else}
					<button
						type="button"
						onclick={() => openRoleModal()}
						class="inline-flex h-9 items-center gap-1.5 rounded-button bg-ink px-3.5 text-body-sm font-medium text-white shadow-xs hover:bg-ink/90 active:scale-95 transition-all cursor-pointer"
					>
						<span class="material-symbols-outlined text-[18px]">add_moderator</span>
						<span>{t('settings.newRole')}</span>
					</button>
				{/if}
			{/if}
		{/snippet}
	</PageHeader>

	{#if !canWrite}
		<div class="rounded-xl border border-stone bg-surface-container-low px-md py-sm text-body-sm text-secondary flex items-center gap-2">
			<span class="material-symbols-outlined text-[18px] text-secondary">visibility</span>
			<span>{t('usersRoles.readOnlyNotice')}</span>
		</div>
	{/if}

	{#if form?.message || form?.messageKey}
		<div
			class="rounded-xl border px-md py-sm text-body-sm font-medium shadow-2xs flex items-center gap-2 {form?.success === false
				? 'border-error/25 bg-error-container text-on-error-container'
				: 'border-emerald-300 bg-emerald-500/10 text-emerald-800'}"
		>
			<span class="material-symbols-outlined text-[18px]">
				{form?.success === false ? 'error' : 'check_circle'}
			</span>
			<span>{form.messageKey ? t(form.messageKey) : form.message}</span>
		</div>
	{/if}

	<!-- Unified Directory Strip -->
	<div class="rounded-xl border border-stone bg-stone shadow-2xs overflow-hidden">
		<div class="grid grid-cols-2 lg:grid-cols-4 gap-px">
			<button
				type="button"
				onclick={() => applyQuickFilter({ status: '', contract: '', roleId: 0 })}
				class="px-md py-3.5 text-left transition-colors cursor-pointer flex items-center justify-between gap-sm min-w-0 {activeTab === 'users' && !data.filters.status && !data.filters.contract && !data.filters.roleId ? 'bg-ash/60' : 'bg-surface-container-lowest hover:bg-ash/50'}"
			>
				<div class="min-w-0">
					<span class="block text-[11px] font-bold uppercase tracking-wider text-secondary font-label-caps truncate">
						{t('usersRoles.summary.totalUsers')}
					</span>
					<span class="block font-headline-sm text-[22px] font-bold text-ink tabular-nums leading-tight mt-0.5">
						{data.summary.totalUsers}
					</span>
				</div>
				<span class="material-symbols-outlined text-[20px] text-secondary shrink-0">groups</span>
			</button>

			<button
				type="button"
				onclick={() => applyQuickFilter({ status: 'Active', contract: '', roleId: 0 })}
				class="px-md py-3.5 text-left transition-colors cursor-pointer flex items-center justify-between gap-sm min-w-0 {activeTab === 'users' && data.filters.status === 'Active' ? 'bg-ash/60' : 'bg-surface-container-lowest hover:bg-ash/50'}"
			>
				<div class="min-w-0">
					<span class="block text-[11px] font-bold uppercase tracking-wider text-secondary font-label-caps truncate">
						{t('usersRoles.summary.activeUsers')}
					</span>
					<span class="flex items-center gap-2 font-headline-sm text-[22px] font-bold text-ink tabular-nums leading-tight mt-0.5">
						<span>{data.summary.activeUsers}</span>
						<span class="w-2 h-2 rounded-full bg-emerald-600 shrink-0"></span>
					</span>
				</div>
				<span class="material-symbols-outlined text-[20px] text-secondary shrink-0">verified_user</span>
			</button>

			<button
				type="button"
				onclick={() => applyQuickFilter({ contract: 'Freelance', status: '', roleId: 0 })}
				class="px-md py-3.5 text-left transition-colors cursor-pointer flex items-center justify-between gap-sm min-w-0 {activeTab === 'users' && data.filters.contract === 'Freelance' ? 'bg-ash/60' : 'bg-surface-container-lowest hover:bg-ash/50'}"
			>
				<div class="min-w-0">
					<span class="block text-[11px] font-bold uppercase tracking-wider text-secondary font-label-caps truncate">
						{t('usersRoles.summary.freelanceUsers')}
					</span>
					<span class="block font-headline-sm text-[22px] font-bold text-ink tabular-nums leading-tight mt-0.5">
						{data.summary.freelanceUsers}
					</span>
				</div>
				<span class="material-symbols-outlined text-[20px] text-secondary shrink-0">payments</span>
			</button>

			<button
				type="button"
				onclick={() => switchTab('roles')}
				class="px-md py-3.5 text-left transition-colors cursor-pointer flex items-center justify-between gap-sm min-w-0 {activeTab === 'roles' ? 'bg-ash/60' : 'bg-surface-container-lowest hover:bg-ash/50'}"
			>
				<div class="min-w-0">
					<span class="block text-[11px] font-bold uppercase tracking-wider text-secondary font-label-caps truncate">
						{t('usersRoles.summary.rolesDefined')}
					</span>
					<span class="block font-headline-sm text-[22px] font-bold text-ink tabular-nums leading-tight mt-0.5">
						{data.summary.rolesCount}
					</span>
					<span class="block text-[11px] text-secondary leading-tight mt-0.5 truncate">
						{t('usersRoles.summary.permissionsAcross', { count: data.summary.permissionsCount })}
					</span>
				</div>
				<span class="material-symbols-outlined text-[20px] text-secondary shrink-0">admin_panel_settings</span>
			</button>
		</div>
	</div>

	<!-- Segmented Tab Switcher -->
	<div class="flex flex-wrap items-center justify-between gap-sm">
		<div class="inline-flex max-w-full overflow-x-auto p-1 rounded-xl bg-surface-container-low border border-stone select-none shadow-2xs" role="tablist">
			<button
				type="button"
				role="tab"
				aria-selected={activeTab === 'users'}
				onclick={() => switchTab('users')}
				class="inline-flex items-center gap-2 px-3 sm:px-4 py-1.5 rounded-lg text-body-sm font-medium whitespace-nowrap transition-all cursor-pointer {activeTab === 'users'
					? 'bg-white text-ink font-semibold shadow-2xs border border-stone/80'
					: 'text-secondary hover:text-ink'}"
			>
				<span class="material-symbols-outlined text-[18px]">group</span>
				<span>{t('settings.users')}</span>
				<span class="rounded-full bg-ash px-2 py-0.5 text-[11px] font-bold text-secondary tabular-nums">
					{data.users.totalItems ?? users.length}
				</span>
			</button>
			<button
				type="button"
				role="tab"
				aria-selected={activeTab === 'roles'}
				onclick={() => switchTab('roles')}
				class="inline-flex items-center gap-2 px-3 sm:px-4 py-1.5 rounded-lg text-body-sm font-medium whitespace-nowrap transition-all cursor-pointer {activeTab === 'roles'
					? 'bg-white text-ink font-semibold shadow-2xs border border-stone/80'
					: 'text-secondary hover:text-ink'}"
			>
				<span class="material-symbols-outlined text-[18px]">security</span>
				<span>{t('settings.rolesPermissions')}</span>
				<span class="rounded-full bg-ash px-2 py-0.5 text-[11px] font-bold text-secondary tabular-nums">
					{data.roles.length}
				</span>
			</button>
		</div>
	</div>

	{#if activeTab === 'users'}
		<!-- Users Roster Section -->
		<div class="rounded-xl border border-stone bg-surface-container-lowest shadow-2xs min-w-0 w-full">
			<!-- Search & Filter Toolbar -->
			<div class="p-md border-b border-stone bg-surface-container-low/50 rounded-t-xl">
				<form method="GET" class="grid grid-cols-2 sm:grid-cols-12 2xl:grid-cols-[minmax(220px,1.6fr)_minmax(125px,1fr)_minmax(125px,1fr)_minmax(145px,1.1fr)_minmax(125px,1fr)_110px_auto] gap-sm items-end">
					<input type="hidden" name="page" value="1" />
					<label class="space-y-1 col-span-2 sm:col-span-6 2xl:col-span-1 min-w-0">
						<span class="block font-label-caps text-secondary uppercase text-[11px] font-bold tracking-wider whitespace-nowrap truncate">
							{t('settings.searchUsers')}
						</span>
						<div class="relative">
							<span class="material-symbols-outlined absolute left-3 top-1/2 -translate-y-1/2 text-secondary text-[18px] pointer-events-none">
								search
							</span>
							<input
								name="search"
								type="search"
								value={data.filters.search}
								class="w-full h-10 pl-9 pr-3 rounded-input border border-stone bg-white text-body-sm text-ink placeholder:text-secondary/60 focus:border-ink focus:outline-none"
								placeholder={t('settings.searchUsersPlaceholder')}
							/>
						</div>
					</label>

					<div class="space-y-1 col-span-1 sm:col-span-3 2xl:col-span-1 min-w-0">
						<span class="block font-label-caps text-secondary uppercase text-[11px] font-bold tracking-wider whitespace-nowrap truncate">
							{t('usersRoles.filter.statusLabel')}
						</span>
						<Dropdown
							name="status"
							bind:value={filterStatus}
							options={[
								{ value: '', label: t('usersRoles.filter.statusAll') },
								{ value: 'Active', label: t('settings.statusActive') },
								{ value: 'Inactive', label: t('settings.statusInactive') }
							]}
						/>
					</div>

					<div class="space-y-1 col-span-1 sm:col-span-3 2xl:col-span-1 min-w-0">
						<span class="block font-label-caps text-secondary uppercase text-[11px] font-bold tracking-wider whitespace-nowrap truncate">
							{t('usersRoles.filter.contractLabel')}
						</span>
						<Dropdown
							name="contract"
							bind:value={filterContract}
							options={[
								{ value: '', label: t('usersRoles.filter.contractAll') },
								{ value: 'FullTime', label: t('settings.employmentType.FullTime') },
								{ value: 'Freelance', label: t('settings.employmentType.Freelance') }
							]}
						/>
					</div>

					<div class="space-y-1 col-span-1 sm:col-span-3 2xl:col-span-1 min-w-0">
						<span class="block font-label-caps text-secondary uppercase text-[11px] font-bold tracking-wider whitespace-nowrap truncate">
							{t('usersRoles.filter.roleLabel')}
						</span>
						<Dropdown
							name="roleId"
							bind:value={filterRoleId}
							options={[
								{ value: '0', label: t('usersRoles.filter.roleAll') },
								...data.roles.map((r) => ({ value: String(r.id), label: r.name }))
							]}
						/>
					</div>

					<div class="space-y-1 col-span-1 sm:col-span-3 2xl:col-span-1 min-w-0">
						<span class="block font-label-caps text-secondary uppercase text-[11px] font-bold tracking-wider whitespace-nowrap truncate">
							{t('settings.sort')}
						</span>
						<Dropdown
							name="sort"
							bind:value={filterSort}
							options={[
								{ value: 'name-asc', label: t('settings.sort.nameAsc') },
								{ value: 'name-desc', label: t('settings.sort.nameDesc') },
								{ value: 'email-asc', label: t('settings.sort.emailAsc') },
								{ value: 'status-asc', label: t('settings.sort.statusAsc') }
							]}
						/>
					</div>

					<div class="space-y-1 col-span-1 sm:col-span-3 2xl:col-span-1 min-w-0">
						<span class="block font-label-caps text-secondary uppercase text-[11px] font-bold tracking-wider whitespace-nowrap truncate">
							{t('settings.usersPageSize')}
						</span>
						<Dropdown
							name="pageSize"
							align="right"
							bind:value={filterPageSize}
							options={pageSizeOptions.map((size) => ({
								value: String(size),
								label: `${size} ${t('usersRoles.perPageSuffix')}`
							}))}
						/>
					</div>

					<div class="col-span-1 sm:col-span-3 2xl:col-span-1 flex items-center gap-xs justify-end min-w-0">
						<button
							class="h-10 flex-1 sm:flex-none px-4 rounded-button border border-stone bg-white text-ink hover:bg-ash text-body-sm font-medium transition-all shadow-2xs cursor-pointer whitespace-nowrap"
							type="submit"
						>
							{t('reports.apply')}
						</button>
						{#if hasActiveUserFilters}
							<a
								href="?page=1&pageSize={data.users.pageSize}&sort=name-asc"
								class="h-10 w-10 rounded-button border border-stone bg-white text-secondary hover:text-ink hover:bg-ash transition-all shadow-2xs flex items-center justify-center shrink-0"
								title={t('settings.clearFilters')}
								aria-label={t('settings.clearFilters')}
							>
								<span class="material-symbols-outlined text-[18px]">restart_alt</span>
							</a>
						{/if}
					</div>
				</form>
			</div>

			<!-- Users Table -->
			<div class="overflow-x-auto">
				<table class="w-full text-left border-collapse">
					<thead>
						<tr class="border-b border-stone bg-surface-container-low text-[11px] font-bold uppercase tracking-wider text-secondary">
							<th class="px-md py-3.5 font-label-caps">{t('settings.colWorker')}</th>
							<th class="px-md py-3.5 font-label-caps">{t('settings.colContract')}</th>
							<th class="px-md py-3.5 font-label-caps">{t('settings.colRoles')}</th>
							<th class="px-md py-3.5 font-label-caps">{t('settings.colStatus')}</th>
							{#if canWrite}
								<th class="px-md py-3.5 font-label-caps text-right">{t('settings.colActions')}</th>
							{/if}
						</tr>
					</thead>
					<tbody class="divide-y divide-stone font-body-sm text-body-sm">
						{#each users as user (user.userId)}
							<tr class="align-middle hover:bg-ash/40 transition-colors">
								<!-- Team Member -->
								<td class="px-md py-3.5">
									<div class="flex items-center gap-3">
										<div class="w-9 h-9 rounded-full border border-stone bg-ash flex items-center justify-center font-bold text-xs text-ink shrink-0">
											{user.fullName.slice(0, 2).toUpperCase()}
										</div>
										<div class="min-w-0">
											<div class="font-semibold text-ink leading-snug truncate">{user.fullName}</div>
											<div class="text-xs text-secondary font-mono tracking-tight truncate">{user.email}</div>
										</div>
									</div>
								</td>

								<!-- Contract Type -->
								<td class="px-md py-3.5 whitespace-nowrap">
									<div class="space-y-1">
										<span
											class="inline-flex items-center rounded-full px-2.5 py-0.5 text-[11px] font-semibold border {user.employmentType === 'Freelance'
												? 'border-amber-300 bg-amber-500/10 text-amber-800'
												: 'border-stone bg-ash text-secondary'}"
										>
											{employmentLabel(user.employmentType)}
										</span>
										{#if user.employmentType === 'Freelance'}
											<div class="text-[11px] text-secondary tabular-nums">
												{getFreelanceSummary(user)}
											</div>
										{/if}
									</div>
								</td>

								<!-- Assigned Roles -->
								<td class="px-md py-3.5">
									<div class="flex flex-wrap items-center gap-1.5 max-w-xs">
										{#if user.roles.length > 0}
											{#each user.roles as role (role.id)}
												<button
													type="button"
													onclick={() => applyQuickFilter({ roleId: role.id })}
													class="rounded border border-stone/80 bg-white px-2 py-0.5 text-[11px] font-medium text-secondary hover:text-ink hover:border-ink/40 transition-colors shadow-2xs cursor-pointer"
												>
													{role.name}
												</button>
											{/each}
										{:else}
											<span class="text-xs text-secondary/60 italic">—</span>
										{/if}
									</div>
								</td>

								<!-- Status -->
								<td class="px-md py-3.5 whitespace-nowrap">
									{#if user.status === 'Active'}
										<span class="inline-flex items-center gap-1.5 rounded-full border border-emerald-300 bg-emerald-500/10 px-2.5 py-0.5 text-[11px] font-semibold text-emerald-800">
											<span class="w-1.5 h-1.5 rounded-full bg-emerald-600"></span>
											<span>{t('settings.statusActive')}</span>
										</span>
									{:else}
										<span class="inline-flex items-center gap-1.5 rounded-full border border-stone bg-ash px-2.5 py-0.5 text-[11px] font-semibold text-secondary">
											<span class="w-1.5 h-1.5 rounded-full bg-secondary"></span>
											<span>{t('settings.statusInactive')}</span>
										</span>
									{/if}
								</td>

								<!-- Actions -->
								{#if canWrite}
									<td class="px-md py-3.5 text-right whitespace-nowrap">
										<div class="inline-flex items-center gap-1.5 justify-end">
											<button
												type="button"
												onclick={() => openEditUserModal(user)}
												class="inline-flex h-8 items-center gap-1 rounded-button border border-stone bg-white px-2.5 text-xs font-semibold text-ink hover:bg-ash transition-colors shadow-2xs cursor-pointer"
											>
												<span class="material-symbols-outlined text-[15px]">edit</span>
												<span>{t('settings.editUser')}</span>
											</button>
											{#if user.employmentType === 'Freelance'}
												<button
													type="button"
													onclick={() => openFreelanceModal(user)}
													class="inline-flex h-8 items-center gap-1 rounded-button border border-stone bg-white px-2.5 text-xs font-semibold text-ink hover:bg-ash transition-colors shadow-2xs cursor-pointer"
													title={t('settings.configureCompensation')}
												>
													<span class="material-symbols-outlined text-[15px]">payments</span>
													<span>{t('settings.configureCompensation')}</span>
												</button>
											{/if}
											<form
												method="POST"
												action="?/setStatus"
												class="inline-block"
												use:enhance={() => {
													statusSubmittingUserId = user.userId;
													return async ({ update }) => {
														await update();
														statusSubmittingUserId = null;
													};
												}}
											>
												<input type="hidden" name="userId" value={user.userId} />
												<input type="hidden" name="status" value={user.status} />
												<button
													type="submit"
													disabled={statusSubmittingUserId === user.userId}
													class="inline-flex h-8 items-center gap-1.5 rounded-button border border-stone bg-white px-2.5 text-xs font-semibold text-secondary hover:text-ink hover:bg-ash transition-colors shadow-2xs cursor-pointer disabled:opacity-60 disabled:cursor-not-allowed"
												>
													{#if statusSubmittingUserId === user.userId}
														<span class="inline-block w-3 h-3 border-2 border-secondary border-t-transparent rounded-full animate-spin"></span>
													{/if}
													<span>{user.status === 'Active' ? t('settings.deactivate') : t('settings.reactivate')}</span>
												</button>
											</form>
										</div>
									</td>
								{/if}
							</tr>
						{:else}
							<tr>
								<td colspan={canWrite ? 5 : 4} class="px-md py-14 text-center">
									<div class="max-w-md mx-auto flex flex-col items-center justify-center text-center">
										<div class="w-12 h-12 rounded-xl bg-ash border border-stone flex items-center justify-center text-secondary mb-3 shadow-2xs">
											<span class="material-symbols-outlined text-[24px]">group_off</span>
										</div>
										<h4 class="font-bold text-ink text-body-base mb-1">{t('settings.noUsersFound')}</h4>
										<p class="text-body-sm text-secondary mb-4">{t('settings.noUsersFoundSubtitle')}</p>
										{#if hasActiveUserFilters}
											<a
												href="?page=1&pageSize={data.users.pageSize}&sort=name-asc"
												class="px-button-x py-button-y rounded-button font-medium font-body-sm text-body-sm inline-flex items-center gap-1.5 border border-stone bg-white text-ink hover:bg-ash transition-all shadow-2xs"
											>
												<span class="material-symbols-outlined text-[18px]">restart_alt</span>
												<span>{t('settings.clearFilters')}</span>
											</a>
										{:else if canWrite}
											<button
												type="button"
												onclick={() => (addUserModalOpen = true)}
												class="px-button-x py-button-y rounded-button font-medium font-body-sm text-body-sm inline-flex items-center gap-1.5 bg-ink text-white hover:bg-ink/90 transition-all shadow-xs cursor-pointer"
											>
												<span class="material-symbols-outlined text-[18px]">person_add</span>
												<span>{t('settings.addUser')}</span>
											</button>
										{/if}
									</div>
								</td>
							</tr>
						{/each}
					</tbody>
				</table>
			</div>

			<!-- Pagination -->
			{#if data.users.totalItems > 0}
				<Pagination
					page={data.users.page}
					pageSize={data.users.pageSize}
					totalCount={data.users.totalItems}
					hrefBuilder={pageHref}
					showingLabel={t('settings.usersShowing')}
					toLabel="-"
					ofLabel="/"
					itemsLabel=""
					previousLabel={t('common.previous')}
					nextLabel={t('common.next')}
				/>
			{/if}
		</div>
	{:else}
		<!-- Roles & Permissions Tab -->
		<div class="space-y-md">
			<!-- Role Search -->
			<div class="flex flex-col sm:flex-row gap-sm sm:items-center sm:justify-between">
				<div class="relative w-full sm:max-w-sm">
					<span class="material-symbols-outlined absolute left-3 top-1/2 -translate-y-1/2 text-secondary text-[18px] pointer-events-none">
						search
					</span>
					<input
						bind:value={roleSearch}
						type="search"
						class="w-full h-10 pl-9 pr-3 rounded-input border border-stone bg-white text-body-sm text-ink placeholder:text-secondary/60 focus:border-ink focus:outline-none"
						placeholder={t('usersRoles.filter.roleSearchPlaceholder')}
					/>
				</div>
				{#if roleSearch}
					<button
						type="button"
						onclick={() => (roleSearch = '')}
						class="inline-flex h-9 items-center gap-1.5 rounded-button border border-stone bg-white px-3 text-body-sm font-medium text-secondary hover:text-ink hover:bg-ash transition-colors shadow-2xs cursor-pointer self-start sm:self-auto"
					>
						<span class="material-symbols-outlined text-[16px]">restart_alt</span>
						<span>{t('settings.clearFilters')}</span>
					</button>
				{/if}
			</div>

			<!-- Roles Grid -->
			<div class="grid grid-cols-1 md:grid-cols-2 gap-md">
				{#each filteredRoles as role (role.id)}
					{@const staffCount = getRoleStaffCount(role.id)}
					{@const grantedCount = role.permissions?.length ?? 0}
					{@const totalPerms = Math.max(1, data.permissions.length)}
					{@const coveragePercent = Math.min(100, Math.round((grantedCount / totalPerms) * 100))}
					<div class="rounded-xl border border-stone bg-surface-container-lowest p-5 shadow-2xs flex flex-col justify-between space-y-4 hover:border-outline-variant transition-colors">
						<div class="space-y-3">
							<div class="flex items-start justify-between gap-2">
								<div class="flex items-center gap-2.5 min-w-0">
									<div class="w-9 h-9 rounded-lg bg-surface-container-low border border-stone flex items-center justify-center text-ink shrink-0">
										<span class="material-symbols-outlined text-[18px]">shield</span>
									</div>
									<div class="min-w-0">
										<h3 class="font-bold text-ink text-body-base leading-tight truncate">{role.name}</h3>
										<p class="text-xs text-secondary mt-0.5 line-clamp-2">
											{role.description || '—'}
										</p>
									</div>
								</div>
								<button
									type="button"
									onclick={() => applyQuickFilter({ roleId: role.id, status: '', contract: '' })}
									class="rounded-full border border-stone/80 bg-ash px-2.5 py-0.5 text-[11px] font-semibold text-secondary hover:text-ink hover:border-ink/40 transition-colors shrink-0 tabular-nums cursor-pointer"
									title={t('usersRoles.viewAssignedUsers')}
								>
									{t('settings.assignedStaffCount', { count: staffCount })}
								</button>
							</div>

							<!-- Granted Permissions Summary & Coverage -->
							<div class="pt-3 border-t border-stone/60 space-y-2.5">
								<div class="space-y-1.5">
									<div class="flex items-center justify-between text-xs text-secondary">
										<span class="font-semibold text-ink">
											{t('settings.permissionsCount', { count: grantedCount })}
										</span>
										<span class="text-[11px] font-mono tabular-nums">
											{grantedCount} / {data.permissions.length}
										</span>
									</div>
									<div
										class="h-1.5 w-full rounded-full bg-ash overflow-hidden"
										aria-label={t('usersRoles.permissionCoverage')}
									>
										<div class="h-full rounded-full bg-ink transition-all duration-200" style="width: {coveragePercent}%"></div>
									</div>
								</div>

								<div class="flex flex-wrap gap-1 pt-0.5">
									{#if grantedCount > 0}
										{#each (role.permissions ?? []).slice(0, 6) as perm (perm)}
											<span class="rounded border border-stone/70 bg-ash/50 px-1.5 py-0.5 text-[10.5px] font-mono text-secondary">
												{perm.replace(/^Permissions\./, '')}
											</span>
										{/each}
										{#if grantedCount > 6}
											<span class="rounded border border-stone/70 bg-white px-1.5 py-0.5 text-[10.5px] font-semibold text-secondary tabular-nums">
												{t('usersRoles.morePermissions', { count: grantedCount - 6 })}
											</span>
										{/if}
									{:else}
										<span class="text-xs text-secondary/60 italic">—</span>
									{/if}
								</div>
							</div>
						</div>

						<!-- Action Footer -->
						{#if canWrite}
							<div class="pt-3 border-t border-stone flex items-center justify-between gap-2">
								<button
									type="button"
									onclick={() => openRoleModal(role)}
									class="inline-flex h-8 items-center gap-1.5 rounded-button border border-stone bg-white px-3 text-xs font-semibold text-ink hover:bg-ash transition-colors shadow-2xs cursor-pointer"
								>
									<span class="material-symbols-outlined text-[15px]">edit</span>
									<span>{t('settings.editRole')}</span>
								</button>

								<form
									method="POST"
									action="?/deleteRole"
									use:enhance={() => {
										deletingRoleId = role.id;
										return async ({ update }) => {
											await update();
											deletingRoleId = null;
										};
									}}
								>
									<input type="hidden" name="roleId" value={role.id} />
									<DeleteConfirmButton
										label={t('common.delete')}
										confirmTitle={t('common.deleteConfirmTitle')}
										confirmMessage={t('common.deleteConfirm')}
										cancelLabel={t('common.cancel')}
										submitting={deletingRoleId === role.id}
										class="inline-flex h-8 items-center rounded-button border border-error/30 bg-error/10 px-2.5 text-xs font-semibold text-error hover:bg-error/20 transition-colors shadow-2xs cursor-pointer"
									/>
								</form>
							</div>
						{/if}
					</div>
				{:else}
					<div class="col-span-full rounded-xl border border-stone bg-surface-container-lowest p-14 text-center">
						<div class="max-w-md mx-auto flex flex-col items-center justify-center text-center">
							<div class="w-12 h-12 rounded-xl bg-ash border border-stone flex items-center justify-center text-secondary mb-3 shadow-2xs">
								<span class="material-symbols-outlined text-[24px]">security</span>
							</div>
							<h4 class="font-bold text-ink text-body-base mb-1">{t('settings.noRolesFound')}</h4>
							<p class="text-body-sm text-secondary mb-4">{t('settings.noRolesFoundSubtitle')}</p>
							{#if roleSearch}
								<button
									type="button"
									onclick={() => (roleSearch = '')}
									class="px-button-x py-button-y rounded-button font-medium font-body-sm text-body-sm inline-flex items-center gap-1.5 border border-stone bg-white text-ink hover:bg-ash transition-all shadow-2xs cursor-pointer"
								>
									<span class="material-symbols-outlined text-[18px]">restart_alt</span>
									<span>{t('settings.clearFilters')}</span>
								</button>
							{:else if canWrite}
								<button
									type="button"
									onclick={() => openRoleModal()}
									class="px-button-x py-button-y rounded-button font-medium font-body-sm text-body-sm inline-flex items-center gap-1.5 bg-ink text-white hover:bg-ink/90 transition-all shadow-xs cursor-pointer"
								>
									<span class="material-symbols-outlined text-[18px]">add_moderator</span>
									<span>{t('settings.newRole')}</span>
								</button>
							{/if}
						</div>
					</div>
				{/each}
			</div>
		</div>
	{/if}
</div>

<!-- Add User Modal -->
{#if addUserModalOpen}
	<div transition:fade={{ duration: 150 }} class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-xs p-4" role="presentation">
		<div
			class="w-[min(640px,calc(100vw-2rem))] max-h-[calc(100vh-3rem)] overflow-y-auto rounded-2xl border border-stone bg-white p-6 shadow-2xl space-y-5"
			role="dialog"
			aria-modal="true"
			aria-labelledby="add-user-modal-title"
		>
			<div class="flex items-start justify-between gap-md border-b border-stone pb-3.5">
				<div>
					<h3 id="add-user-modal-title" class="font-subheading text-[16px] font-bold text-ink">{t('settings.addUser')}</h3>
					<p class="text-xs text-secondary mt-0.5">{t('settings.newUserSubtitle')}</p>
				</div>
				<button
					type="button"
					onclick={() => (addUserModalOpen = false)}
					aria-label={t('common.close')}
					class="text-secondary hover:text-ink cursor-pointer p-1"
				>
					<span class="material-symbols-outlined text-[20px]">close</span>
				</button>
			</div>

			<form
				method="POST"
				action="?/createUser"
				class="space-y-4"
				use:enhance={() => {
					addUserSubmitting = true;
					return async ({ result, update }) => {
						addUserSubmitting = false;
						await update();
						if (result.type === 'success') {
							addUserModalOpen = false;
						}
					};
				}}
			>
				<div class="grid grid-cols-1 sm:grid-cols-2 gap-sm">
					<label class="space-y-1 block">
						<span class="text-[11px] font-bold uppercase tracking-wider text-secondary font-label-caps block">{t('settings.firstName')}</span>
						<input
							name="firstName"
							class="w-full rounded-input border border-stone bg-white p-2 text-body-sm text-ink focus:border-ink focus:outline-none"
							placeholder={t('settings.firstName')}
						/>
					</label>
					<label class="space-y-1 block">
						<span class="text-[11px] font-bold uppercase tracking-wider text-secondary font-label-caps block">{t('settings.lastName')}</span>
						<input
							name="lastName"
							class="w-full rounded-input border border-stone bg-white p-2 text-body-sm text-ink focus:border-ink focus:outline-none"
							placeholder={t('settings.lastName')}
						/>
					</label>
					<label class="space-y-1 block">
						<span class="text-[11px] font-bold uppercase tracking-wider text-secondary font-label-caps block">{t('auth.email')} *</span>
						<input
							name="email"
							type="email"
							required
							class="w-full rounded-input border border-stone bg-white p-2 text-body-sm text-ink focus:border-ink focus:outline-none"
							placeholder="name@shop.com"
						/>
					</label>
					<label class="space-y-1 block">
						<span class="text-[11px] font-bold uppercase tracking-wider text-secondary font-label-caps block">{t('auth.password')} *</span>
						<div class="relative flex items-center">
							<input
								name="password"
								type={newShowPassword ? 'text' : 'password'}
								required
								minlength="6"
								class="w-full rounded-input border border-stone bg-white p-2 pr-9 text-body-sm text-ink focus:border-ink focus:outline-none"
								placeholder="••••••••"
							/>
							<button
								type="button"
								class="absolute right-2.5 text-secondary hover:text-ink cursor-pointer p-0.5"
								aria-label={newShowPassword ? t('usersRoles.hidePassword') : t('usersRoles.showPassword')}
								onclick={() => (newShowPassword = !newShowPassword)}
							>
								<span class="material-symbols-outlined text-[18px]">
									{newShowPassword ? 'visibility_off' : 'visibility'}
								</span>
							</button>
						</div>
					</label>
					<div class="space-y-1 block sm:col-span-2">
						<span class="text-[11px] font-bold uppercase tracking-wider text-secondary font-label-caps block">{t('settings.contractType')}</span>
						<Dropdown
							name="employmentType"
							bind:value={newEmploymentType}
							options={[
								{ value: 'FullTime', label: t('settings.employmentType.FullTime') },
								{ value: 'Freelance', label: t('settings.employmentType.Freelance') }
							]}
						/>
					</div>
				</div>

				<div class="space-y-2">
					<span class="text-[11px] font-bold uppercase tracking-wider text-secondary font-label-caps block">{t('settings.rolesPermissions')}</span>
					<div class="grid grid-cols-1 sm:grid-cols-2 gap-2">
						{#each data.roles as role (role.id)}
							<label class="rounded-lg border border-stone p-2.5 text-xs text-secondary flex items-start gap-2.5 hover:bg-ash/50 cursor-pointer">
								<input type="checkbox" name="roleIds" value={role.id} class="mt-0.5 rounded border-stone text-ink focus:ring-0" />
								<div>
									<span class="font-semibold text-ink block leading-tight">{role.name}</span>
									{#if role.description}
										<span class="text-[11px] text-secondary block mt-0.5">{role.description}</span>
									{/if}
								</div>
							</label>
						{/each}
					</div>
				</div>

				<div class="flex justify-end gap-2 border-t border-stone pt-3.5">
					<button
						type="button"
						disabled={addUserSubmitting}
						onclick={() => (addUserModalOpen = false)}
						class="px-4 py-2 rounded-button border border-stone bg-white text-ink hover:bg-ash text-body-sm font-medium transition-all shadow-2xs cursor-pointer disabled:opacity-60"
					>
						{t('common.cancel')}
					</button>
					<button
						type="submit"
						disabled={addUserSubmitting}
						class="px-4 py-2 rounded-button bg-ink text-white font-medium text-body-sm hover:bg-ink/90 active:scale-95 transition-all shadow-xs cursor-pointer inline-flex items-center gap-1.5 disabled:opacity-60 disabled:cursor-not-allowed"
					>
						{#if addUserSubmitting}
							<span class="inline-block w-3.5 h-3.5 border-2 border-white border-t-transparent rounded-full animate-spin"></span>
						{/if}
						<span>{t('settings.addUser')}</span>
					</button>
				</div>
			</form>
		</div>
	</div>
{/if}

<!-- Edit User Modal -->
{#if editingStaffUser}
	<div transition:fade={{ duration: 150 }} class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-xs p-4" role="presentation">
		<div
			class="w-[min(640px,calc(100vw-2rem))] max-h-[calc(100vh-3rem)] overflow-y-auto rounded-2xl border border-stone bg-white p-6 shadow-2xl space-y-5"
			role="dialog"
			aria-modal="true"
			aria-labelledby="edit-user-modal-title"
		>
			<div class="flex items-start justify-between gap-md border-b border-stone pb-3.5">
				<div>
					<h3 id="edit-user-modal-title" class="font-subheading text-[16px] font-bold text-ink">
						{t('settings.editUser')} — {editingStaffUser.fullName}
					</h3>
					<p class="text-xs text-secondary mt-0.5">
						{t('settings.editUserSubtitle')}
					</p>
				</div>
				<button
					type="button"
					onclick={() => (editingStaffUser = null)}
					aria-label={t('common.close')}
					class="text-secondary hover:text-ink cursor-pointer p-1"
				>
					<span class="material-symbols-outlined text-[20px]">close</span>
				</button>
			</div>

			<!-- Section 1: User Profile & Credentials -->
			<form
				method="POST"
				action="?/updateUser"
				class="rounded-xl border border-stone bg-surface-container-low/40 p-4 space-y-3.5"
				use:enhance={() => {
					editUserSubmitting = true;
					editUserFeedback = null;
					return async ({ result, update }) => {
						editUserSubmitting = false;
						await update({ reset: false });
						if (result.type === 'success') {
							const emailNotice = editingStaffUser?.email ? ` (${editingStaffUser.email})` : '';
							editUserFeedback = { success: true, message: `${t('settings.userUpdated')}${emailNotice}` };
							editUserPassword = '';
							setTimeout(() => (editUserFeedback = null), 6000);
						} else if (result.type === 'failure') {
							const failureData = result.data as { message?: string } | undefined;
							editUserFeedback = { success: false, message: failureData?.message || t('settings.saveFailed') };
						}
					};
				}}
			>
				<input type="hidden" name="userId" value={editingStaffUser.userId} />
				<div class="font-bold text-ink text-xs uppercase tracking-wider font-label-caps">
					{t('settings.general')} & {t('auth.password')}
				</div>

				<div class="grid grid-cols-1 sm:grid-cols-2 gap-sm">
					<label class="space-y-1 block">
						<span class="text-[11px] font-bold uppercase tracking-wider text-secondary font-label-caps block">{t('settings.firstName')}</span>
						<input
							name="firstName"
							required
							class="w-full rounded-input border border-stone bg-white p-2 text-body-sm text-ink focus:border-ink focus:outline-none"
							value={editingStaffUser.fullName.split(' ')[0] ?? ''}
						/>
					</label>
					<label class="space-y-1 block">
						<span class="text-[11px] font-bold uppercase tracking-wider text-secondary font-label-caps block">{t('settings.lastName')}</span>
						<input
							name="lastName"
							class="w-full rounded-input border border-stone bg-white p-2 text-body-sm text-ink focus:border-ink focus:outline-none"
							value={editingStaffUser.fullName.split(' ').slice(1).join(' ')}
						/>
					</label>
					<label class="space-y-1 block">
						<span class="text-[11px] font-bold uppercase tracking-wider text-secondary font-label-caps block">{t('settings.email')}</span>
						<input
							name="email"
							type="email"
							required
							class="w-full rounded-input border border-stone bg-white p-2 text-body-sm text-ink focus:border-ink focus:outline-none"
							value={editingStaffUser.email}
						/>
					</label>
					<div class="space-y-1 block">
						<span class="text-[11px] font-bold uppercase tracking-wider text-secondary font-label-caps block">{t('settings.contractType')}</span>
						<Dropdown
							name="employmentType"
							value={editingStaffUser.employmentType === 'Freelance' ? 'Freelance' : 'FullTime'}
							options={[
								{ value: 'FullTime', label: t('settings.employmentType.FullTime') },
								{ value: 'Freelance', label: t('settings.employmentType.Freelance') }
							]}
						/>
					</div>
					<label class="space-y-1 block sm:col-span-2">
						<div class="flex items-center justify-between gap-2">
							<span class="text-[11px] font-bold uppercase tracking-wider text-secondary font-label-caps block">{t('settings.temporaryPassword')}</span>
							<span class="text-[10.5px] text-secondary font-medium">{t('settings.temporaryPasswordHelp')}</span>
						</div>
						<div class="relative flex items-center">
							<input
								name="password"
								type={showEditPassword ? 'text' : 'password'}
								autocomplete="new-password"
								minlength="6"
								class="w-full rounded-input border border-stone bg-white p-2 pr-9 text-body-sm text-ink focus:border-ink focus:outline-none"
								placeholder={t('settings.temporaryPasswordPlaceholder')}
								bind:value={editUserPassword}
							/>
							<button
								type="button"
								class="absolute right-2.5 text-secondary hover:text-ink cursor-pointer p-0.5"
								onclick={() => (showEditPassword = !showEditPassword)}
								aria-label={showEditPassword ? t('usersRoles.hidePassword') : t('usersRoles.showPassword')}
							>
								<span class="material-symbols-outlined text-[18px]">
									{showEditPassword ? 'visibility_off' : 'visibility'}
								</span>
							</button>
						</div>
					</label>
				</div>

				<div class="flex items-center justify-between pt-2">
					{#if editUserFeedback}
						<div class="text-xs font-semibold flex items-center gap-1 {editUserFeedback.success ? 'text-emerald-700' : 'text-error'}">
							<span class="material-symbols-outlined text-[16px]">{editUserFeedback.success ? 'check_circle' : 'error'}</span>
							<span>{editUserFeedback.message}</span>
						</div>
					{:else}
						<div></div>
					{/if}
					<button
						class="px-4 py-2 rounded-button bg-ink text-white font-medium text-body-sm hover:bg-ink/90 active:scale-95 transition-all shadow-xs cursor-pointer inline-flex items-center gap-1.5 disabled:opacity-60 disabled:cursor-not-allowed"
						type="submit"
						disabled={editUserSubmitting}
					>
						{#if editUserSubmitting}
							<span class="inline-block w-3.5 h-3.5 border-2 border-white border-t-transparent rounded-full animate-spin"></span>
						{/if}
						<span>{t('settings.save')}</span>
					</button>
				</div>
			</form>

			<!-- Section 2: Role Assignments -->
			<form
				method="POST"
				action="?/updateRoles"
				class="rounded-xl border border-stone bg-surface-container-low/40 p-4 space-y-3.5"
				use:enhance={() => {
					editRolesSubmitting = true;
					editRolesFeedback = null;
					return async ({ result, update }) => {
						editRolesSubmitting = false;
						await update({ reset: false });
						if (result.type === 'success') {
							editRolesFeedback = { success: true, message: t('settings.userRolesUpdated') };
							setTimeout(() => (editRolesFeedback = null), 4000);
						} else if (result.type === 'failure') {
							const failureData = result.data as { message?: string } | undefined;
							editRolesFeedback = { success: false, message: failureData?.message || t('settings.saveFailed') };
						}
					};
				}}
			>
				<input type="hidden" name="userId" value={editingStaffUser.userId} />
				<div class="font-bold text-ink text-xs uppercase tracking-wider font-label-caps">
					{t('settings.rolesPermissions')}
				</div>

				<div class="grid grid-cols-1 sm:grid-cols-2 gap-2">
					{#each data.roles as role (role.id)}
						<label
							class="rounded-lg border p-2.5 text-xs flex items-start gap-2.5 transition-colors cursor-pointer select-none {editUserRoleIds.includes(role.id)
								? 'border-ink bg-white shadow-2xs'
								: 'border-stone/60 bg-white/70 hover:border-stone'}"
						>
							<input
								type="checkbox"
								name="roleIds"
								value={role.id}
								checked={editUserRoleIds.includes(role.id)}
								onchange={(e) => {
									const checked = (e.currentTarget as HTMLInputElement).checked;
									if (checked) {
										editUserRoleIds = [...editUserRoleIds, role.id];
									} else {
										editUserRoleIds = editUserRoleIds.filter((id) => id !== role.id);
									}
								}}
								class="mt-0.5 rounded border-stone text-ink focus:ring-0"
							/>
							<div class="min-w-0">
								<span class="font-semibold text-ink block leading-tight">{role.name}</span>
								{#if role.description}
									<span class="text-[11px] text-secondary block mt-0.5">{role.description}</span>
								{/if}
							</div>
						</label>
					{/each}
				</div>

				<div class="flex items-center justify-between pt-2">
					{#if editRolesFeedback}
						<div class="text-xs font-semibold flex items-center gap-1 {editRolesFeedback.success ? 'text-emerald-700' : 'text-error'}">
							<span class="material-symbols-outlined text-[16px]">{editRolesFeedback.success ? 'check_circle' : 'error'}</span>
							<span>{editRolesFeedback.message}</span>
						</div>
					{:else}
						<div></div>
					{/if}
					<button
						class="px-4 py-2 rounded-button bg-ink text-white font-medium text-body-sm hover:bg-ink/90 active:scale-95 transition-all shadow-xs cursor-pointer inline-flex items-center gap-1.5 disabled:opacity-60 disabled:cursor-not-allowed"
						type="submit"
						disabled={editRolesSubmitting}
					>
						{#if editRolesSubmitting}
							<span class="inline-block w-3.5 h-3.5 border-2 border-white border-t-transparent rounded-full animate-spin"></span>
						{/if}
						<span>{t('settings.saveRoles')}</span>
					</button>
				</div>
			</form>

			<div class="flex justify-end border-t border-stone pt-3">
				<button
					type="button"
					onclick={() => (editingStaffUser = null)}
					class="px-4 py-2 rounded-button border border-stone bg-white text-ink hover:bg-ash text-body-sm font-medium transition-all shadow-2xs cursor-pointer"
				>
					{t('common.close')}
				</button>
			</div>
		</div>
	</div>
{/if}

<!-- Add / Edit Role Modal -->
{#if editingRole}
	<div transition:fade={{ duration: 150 }} class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-xs p-4" role="presentation">
		<div
			class="w-[min(760px,calc(100vw-2rem))] max-h-[calc(100vh-3rem)] overflow-y-auto rounded-2xl border border-stone bg-white p-6 shadow-2xl space-y-5"
			role="dialog"
			aria-modal="true"
			aria-labelledby="role-modal-title"
		>
			<div class="flex items-start justify-between gap-md border-b border-stone pb-3.5">
				<div>
					<h3 id="role-modal-title" class="font-subheading text-[16px] font-bold text-ink">
						{editingRole.id ? t('settings.editRole') : t('settings.newRole')}
					</h3>
					<p class="text-xs text-secondary mt-0.5">
						{editingRole.id ? t('settings.editRoleSubtitle') : t('settings.newRoleSubtitle')}
					</p>
				</div>
				<button
					type="button"
					onclick={() => (editingRole = null)}
					aria-label={t('common.close')}
					class="text-secondary hover:text-ink cursor-pointer p-1"
				>
					<span class="material-symbols-outlined text-[20px]">close</span>
				</button>
			</div>

			<form
				method="POST"
				action="?/saveRole"
				class="space-y-5"
				use:enhance={() => {
					roleSubmitting = true;
					return async ({ result, update }) => {
						roleSubmitting = false;
						await update();
						if (result.type === 'success') {
							editingRole = null;
						}
					};
				}}
			>
				<input type="hidden" name="roleId" value={editingRole.id} />

				<!-- Name & Description -->
				<div class="grid grid-cols-1 sm:grid-cols-2 gap-sm">
					<label class="space-y-1 block">
						<span class="text-[11px] font-bold uppercase tracking-wider text-secondary font-label-caps block">
							{t('settings.roleName')} *
						</span>
						<input
							name="name"
							value={editingRole.name}
							required
							class="w-full rounded-input border border-stone bg-white p-2 text-body-sm text-ink focus:border-ink focus:outline-none"
							placeholder={t('usersRoles.roleNamePlaceholder')}
						/>
					</label>
					<label class="space-y-1 block">
						<span class="text-[11px] font-bold uppercase tracking-wider text-secondary font-label-caps block">
							{t('settings.roleDescription')}
						</span>
						<input
							name="description"
							value={editingRole.description ?? ''}
							class="w-full rounded-input border border-stone bg-white p-2 text-body-sm text-ink focus:border-ink focus:outline-none"
							placeholder={t('usersRoles.roleDescPlaceholder')}
						/>
					</label>
				</div>

				<!-- Grouped Permissions -->
				<div class="space-y-3">
					<div class="flex items-center justify-between gap-2">
						<span class="text-[11px] font-bold uppercase tracking-wider text-secondary font-label-caps block tabular-nums">
							{t('settings.rolesPermissions')} ({t('usersRoles.selectedPermissionsCount', { count: selectedPermissions.length })})
						</span>
						<div class="flex items-center gap-2">
							<button
								type="button"
								onclick={() => (selectedPermissions = data.permissions.map((p) => p.code))}
								class="text-xs font-semibold text-action-blue hover:underline cursor-pointer"
							>
								{t('settings.selectAll')}
							</button>
							<span class="text-stone">·</span>
							<button
								type="button"
								onclick={() => (selectedPermissions = [])}
								class="text-xs font-semibold text-secondary hover:text-ink cursor-pointer"
							>
								{t('settings.deselectAll')}
							</button>
						</div>
					</div>

					<div class="space-y-3 max-h-[380px] overflow-y-auto pr-1">
						{#each groupedPermissions as group (group.key)}
							<div class="rounded-xl border border-stone/80 bg-surface-container-low/40 p-3 space-y-2.5">
								<div class="flex items-center justify-between">
									<div class="flex items-center gap-2">
										<span class="material-symbols-outlined text-[18px] text-secondary">{group.icon}</span>
										<span class="font-bold text-ink text-xs">{t(group.titleKey)}</span>
									</div>
									<button
										type="button"
										onclick={() => toggleCategoryPermissions(group)}
										class="text-[11px] font-medium text-secondary hover:text-ink cursor-pointer"
									>
										{group.permissions.every((p) => selectedPermissions.includes(p.code))
											? t('settings.deselectAll')
											: t('settings.selectAll')}
									</button>
								</div>

								<div class="grid grid-cols-1 sm:grid-cols-2 gap-2">
									{#each group.permissions as perm (perm.code)}
										<label
											class="flex items-start gap-2.5 rounded-lg border p-2 text-xs transition-colors cursor-pointer select-none {selectedPermissions.includes(perm.code)
												? 'border-ink bg-white shadow-2xs text-ink'
												: 'border-stone/60 bg-white/70 text-secondary hover:border-stone'}"
										>
											<input
												type="checkbox"
												name="permissions"
												value={perm.code}
												checked={selectedPermissions.includes(perm.code)}
												onchange={() => togglePermission(perm.code)}
												class="mt-0.5 rounded border-stone text-ink focus:ring-0"
											/>
											<div class="min-w-0">
												<span class="font-semibold block leading-tight">
													{perm.code.replace(/^Permissions\.[^.]+\./, '')}
												</span>
												<span class="font-mono text-[10px] text-secondary block truncate mt-0.5">
													{perm.code}
												</span>
											</div>
										</label>
									{/each}
								</div>
							</div>
						{/each}
					</div>
				</div>

				<div class="flex justify-end gap-2 border-t border-stone pt-3.5">
					<button
						type="button"
						disabled={roleSubmitting}
						onclick={() => (editingRole = null)}
						class="px-4 py-2 rounded-button border border-stone bg-white text-ink hover:bg-ash text-body-sm font-medium transition-all shadow-2xs cursor-pointer disabled:opacity-60"
					>
						{t('common.cancel')}
					</button>
					<button
						type="submit"
						disabled={roleSubmitting}
						class="px-4 py-2 rounded-button bg-ink text-white font-medium text-body-sm hover:bg-ink/90 active:scale-95 transition-all shadow-xs cursor-pointer inline-flex items-center gap-1.5 disabled:opacity-60 disabled:cursor-not-allowed"
					>
						{#if roleSubmitting}
							<span class="inline-block w-3.5 h-3.5 border-2 border-white border-t-transparent rounded-full animate-spin"></span>
						{/if}
						<span>{t('settings.save')}</span>
					</button>
				</div>
			</form>
		</div>
	</div>
{/if}

<!-- Freelance Compensation Modal -->
{#if editingUser}
	<div transition:fade={{ duration: 150 }} class="fixed inset-0 z-[60] flex items-center justify-center bg-black/60 backdrop-blur-xs px-md py-6" role="presentation">
		<div
			class="w-[min(860px,calc(100vw-2rem))] max-h-[calc(100vh-3rem)] overflow-y-auto rounded-2xl border border-stone bg-white p-6 sm:p-7 shadow-2xl space-y-6"
			role="dialog"
			aria-modal="true"
			aria-labelledby="freelance-modal-title"
		>
			<div class="flex items-start justify-between gap-md border-b border-stone pb-3.5">
				<div>
					<h3 id="freelance-modal-title" class="font-subheading text-[16px] font-bold text-ink">
						{t('settings.freelanceModalTitle')} — {editingUser.fullName}
					</h3>
					<p class="text-xs text-secondary mt-0.5">{t('settings.freelanceModalSubtitle')}</p>
				</div>
				<button
					type="button"
					onclick={() => (editingUser = null)}
					aria-label={t('common.close')}
					class="text-secondary hover:text-ink cursor-pointer p-1"
				>
					<span class="material-symbols-outlined text-[20px]">close</span>
				</button>
			</div>

			<form
				method="POST"
				action="?/saveFreelanceProfile"
				class="space-y-6"
				use:enhance={() => {
					freelanceSubmitting = true;
					return async ({ update, result }) => {
						freelanceSubmitting = false;
						await update();
						if (result.type === 'success') {
							editingUser = null;
						}
					};
				}}
			>
				<input type="hidden" name="userId" value={editingUser.userId} />
				<input type="hidden" name="tieredRatesJson" value={JSON.stringify(modalBrackets)} />
				<input type="hidden" name="maintenanceMilestonesJson" value={JSON.stringify(modalMilestones)} />
				<input type="hidden" name="maintenanceMode" value={modalMaintenanceMode} />
				<input type="hidden" name="maintenanceIsRetroactive" value={modalMaintenanceIsRetroactive ? 'on' : 'off'} />
				<input type="hidden" name="taskItemizedRulesJson" value={JSON.stringify(modalTaskRules)} />

				<!-- Base Pay Mode & Hourly Rate -->
				<div class="grid grid-cols-1 md:grid-cols-2 gap-md p-4 rounded-xl border border-stone/80 bg-surface-container-low/40">
					<div class="space-y-1 block">
						<span class="text-[11px] font-bold uppercase tracking-wider text-secondary font-label-caps block">{t('settings.basePayMode')}</span>
						<Dropdown
							name="basePayMode"
							bind:value={modalBasePayMode}
							options={[
								{ value: 'TieredDailyAverage', label: t('settings.basePayMode.TieredDailyAverage') },
								{ value: 'TieredDaily', label: t('settings.basePayMode.TieredDaily') },
								{ value: 'Hourly', label: t('settings.basePayMode.Hourly') },
								{ value: 'TieredMonthly', label: t('settings.basePayMode.TieredMonthly') }
							]}
						/>
					</div>

					<label class="space-y-1 block">
						<span class="text-[11px] font-bold uppercase tracking-wider text-secondary font-label-caps block">{t('settings.baseHourlyRate')}</span>
						<CurrencyInput
							name="baseHourlyRate"
							prefix={currencyCode === 'IDR' ? 'Rp' : '$'}
							locale={data.locale}
							currency={currencyCode}
							bind:value={modalBaseHourlyRate}
						/>
						<span class="block text-[11px] text-secondary">{t('settings.baseHourlyRateHint')}</span>
					</label>
				</div>

				{#if modalBasePayMode !== 'Hourly'}
					<div class="rounded-xl border border-stone bg-white p-4 space-y-3.5 shadow-2xs">
						<div class="flex flex-wrap items-center justify-between gap-sm border-b border-stone/60 pb-2.5">
							<div>
								<h4 class="font-bold text-ink text-xs uppercase tracking-wider font-label-caps">{t('settings.tieredBrackets')}</h4>
								<p class="text-[11px] text-secondary mt-0.5">{t('usersRoles.tierBracketsHelp')}</p>
							</div>
							<div class="flex flex-wrap gap-sm">
								<button
									type="button"
									onclick={loadDefaultTiers}
									class="px-2.5 py-1 rounded-button border border-stone bg-white text-xs font-medium text-ink hover:bg-ash transition-all shadow-2xs cursor-pointer"
								>
									{t('settings.loadDefaultTiers')}
								</button>
								<button
									type="button"
									onclick={addBracket}
									class="px-2.5 py-1 rounded-button bg-ink text-white text-xs font-medium hover:bg-ink/90 active:scale-95 transition-all shadow-2xs cursor-pointer inline-flex items-center gap-1"
								>
									<span class="material-symbols-outlined text-[14px]">add</span>
									<span>{t('settings.addTierBracket')}</span>
								</button>
							</div>
						</div>

						<div class="space-y-2">
							{#each modalBrackets as bracket, index}
								<div class="grid grid-cols-1 sm:grid-cols-[1fr_1fr_1.3fr_1.2fr_auto] gap-2 items-end rounded-lg border border-stone bg-ash/30 p-2.5">
									<label class="space-y-1">
										<span class="text-[10.5px] font-bold uppercase tracking-wider text-secondary font-label-caps block">{t('settings.tierMinHours')}</span>
										<input
											type="number"
											min="0"
											max="744"
											step="0.25"
											bind:value={bracket.minHours}
											class="w-full rounded-input border border-stone bg-white p-1.5 text-body-sm text-ink focus:border-ink focus:outline-none"
										/>
									</label>
									<label class="space-y-1">
										<span class="text-[10.5px] font-bold uppercase tracking-wider text-secondary font-label-caps block">{t('settings.tierMaxHours')}</span>
										<input
											type="number"
											min="0"
											max="744"
											step="0.25"
											bind:value={bracket.maxHours}
											class="w-full rounded-input border border-stone bg-white p-1.5 text-body-sm text-ink focus:border-ink focus:outline-none"
										/>
									</label>
									<label class="space-y-1">
										<span class="text-[10.5px] font-bold uppercase tracking-wider text-secondary font-label-caps block">{t('settings.tierFlatRate')}</span>
										<CurrencyInput
											bind:value={bracket.flatRate}
											prefix={currencyCode === 'IDR' ? 'Rp' : '$'}
											locale={data.locale}
											currency={currencyCode}
											inputClass="p-1.5 text-body-sm"
										/>
									</label>
									<label class="space-y-1">
										<span class="text-[10.5px] font-bold uppercase tracking-wider text-secondary font-label-caps block">{t('settings.tierOvertimeHourly')}</span>
										<CurrencyInput
											bind:value={bracket.hourlyRate}
											prefix={currencyCode === 'IDR' ? 'Rp' : '$'}
											locale={data.locale}
											currency={currencyCode}
											inputClass="p-1.5 text-body-sm"
										/>
									</label>
									<button
										type="button"
										onclick={() => removeBracket(index)}
										class="h-9 px-2.5 rounded-button border border-error/30 bg-error/10 text-error hover:bg-error/20 text-xs font-semibold cursor-pointer transition-colors"
									>
										{t('settings.removeTier')}
									</button>
								</div>
							{/each}
						</div>
					</div>
				{/if}

				<!-- Bonus Schemes -->
				<div class="space-y-3.5">
					<div>
						<h4 class="font-bold text-ink text-xs uppercase tracking-wider font-label-caps">{t('settings.bonusSchemesTitle')}</h4>
						<p class="text-xs text-secondary mt-0.5">{t('settings.bonusSchemesSubtitle')}</p>
					</div>

					<!-- Bonus 1: Maintenance Completion Threshold & Milestones -->
					<div class="rounded-xl border border-stone bg-white p-4 space-y-3.5 shadow-2xs">
						<label class="flex items-start gap-2.5 cursor-pointer">
							<input type="checkbox" name="maintenanceActive" bind:checked={modalMaintenanceActive} class="mt-0.5 rounded border-stone text-ink focus:ring-0" />
							<div>
								<span class="font-bold text-ink text-body-sm block">{t('settings.bonus.maintenanceTitle')}</span>
								<span class="text-xs text-secondary block mt-0.5">{t('settings.bonus.maintenanceDesc')}</span>
							</div>
						</label>

						{#if modalMaintenanceActive}
							<div class="flex flex-wrap items-center gap-md pt-1 border-t border-stone/60">
								<span class="text-xs font-bold text-ink">{t('settings.bonus.maintenanceMode')}:</span>
								<label class="flex items-center gap-1.5 text-xs cursor-pointer text-ink font-medium">
									<input type="radio" name="maintenanceModeRadio" value="Milestones" bind:group={modalMaintenanceMode} class="text-ink focus:ring-0" />
									<span>{t('settings.bonus.maintenanceMode.Milestones')}</span>
								</label>
								<label class="flex items-center gap-1.5 text-xs cursor-pointer text-ink font-medium">
									<input type="radio" name="maintenanceModeRadio" value="Simple" bind:group={modalMaintenanceMode} class="text-ink focus:ring-0" />
									<span>{t('settings.bonus.maintenanceMode.Simple')}</span>
								</label>
							</div>

							{#if modalMaintenanceMode === 'Milestones'}
								<div class="space-y-2.5 pt-1">
									<div class="flex flex-wrap items-center justify-between gap-sm">
										<label class="flex items-center gap-2 text-xs font-medium text-ink cursor-pointer">
											<input type="checkbox" bind:checked={modalMaintenanceIsRetroactive} class="rounded border-stone text-ink focus:ring-0" />
											<span>{t('settings.bonus.isRetroactive')}</span>
										</label>
										<div class="flex flex-wrap gap-xs">
											<button
												type="button"
												onclick={loadDefaultMilestones}
												class="px-2 py-1 rounded-button border border-stone bg-white text-xs font-medium text-ink hover:bg-ash transition-all shadow-2xs cursor-pointer"
											>
												{t('settings.bonus.loadDefaultMilestones')}
											</button>
											<button
												type="button"
												onclick={addMilestone}
												class="px-2 py-1 rounded-button bg-ink text-white text-xs font-medium hover:bg-ink/90 active:scale-95 transition-all shadow-2xs cursor-pointer"
											>
												+ {t('settings.bonus.addMilestone')}
											</button>
										</div>
									</div>

									<div class="space-y-2">
										{#each modalMilestones as milestone, index}
											<div class="grid grid-cols-1 sm:grid-cols-[1fr_1fr_1.5fr_auto] gap-2 items-end rounded-lg border border-stone bg-ash/30 p-2.5">
												<label class="space-y-1">
													<span class="text-[10.5px] font-bold uppercase tracking-wider text-secondary font-label-caps block">{t('settings.bonus.milestoneMinUnits')}</span>
													<input
														type="number"
														min="0"
														step="1"
														bind:value={milestone.minUnits}
														class="w-full rounded-input border border-stone bg-white p-1.5 text-body-sm text-ink focus:border-ink focus:outline-none"
													/>
												</label>
												<label class="space-y-1">
													<span class="text-[10.5px] font-bold uppercase tracking-wider text-secondary font-label-caps block">{t('settings.bonus.milestoneMaxUnits')}</span>
													<input
														type="number"
														min="0"
														step="1"
														bind:value={milestone.maxUnits}
														class="w-full rounded-input border border-stone bg-white p-1.5 text-body-sm text-ink focus:border-ink focus:outline-none"
													/>
												</label>
												<label class="space-y-1">
													<span class="text-[10.5px] font-bold uppercase tracking-wider text-secondary font-label-caps block">{t('settings.bonus.milestoneRewardPerUnit')}</span>
													<CurrencyInput
														bind:value={milestone.rewardPerUnit}
														prefix={currencyCode === 'IDR' ? 'Rp' : '$'}
														locale={data.locale}
														currency={currencyCode}
														inputClass="p-1.5 text-body-sm"
													/>
												</label>
												<button
													type="button"
													onclick={() => removeMilestone(index)}
													class="h-9 px-2.5 rounded-button border border-error/30 bg-error/10 text-error hover:bg-error/20 text-xs font-semibold cursor-pointer transition-colors"
												>
													{t('settings.removeTier')}
												</button>
											</div>
										{/each}
									</div>
								</div>
							{:else}
								<div class="grid grid-cols-1 sm:grid-cols-3 gap-md pt-1">
									<label class="space-y-1 block">
										<span class="text-[11px] font-bold uppercase tracking-wider text-secondary font-label-caps block">{t('settings.bonus.thresholdUnits')}</span>
										<input
											type="number"
											name="maintenanceThreshold"
											min="0"
											step="1"
											bind:value={modalMaintenanceThreshold}
											class="w-full rounded-input border border-stone bg-white p-2 text-body-sm text-ink focus:border-ink focus:outline-none"
										/>
									</label>
									<label class="space-y-1 block">
										<span class="text-[11px] font-bold uppercase tracking-wider text-secondary font-label-caps block">{t('settings.bonus.rewardPerUnit')}</span>
										<CurrencyInput
											name="maintenanceRewardPerUnit"
											prefix={currencyCode === 'IDR' ? 'Rp' : '$'}
											locale={data.locale}
											currency={currencyCode}
											bind:value={modalMaintenanceRewardPerUnit}
										/>
									</label>
									<label class="space-y-1 block">
										<span class="text-[11px] font-bold uppercase tracking-wider text-secondary font-label-caps block">{t('settings.bonus.flatMilestoneReward')}</span>
										<CurrencyInput
											name="maintenanceFlatReward"
											prefix={currencyCode === 'IDR' ? 'Rp' : '$'}
											locale={data.locale}
											currency={currencyCode}
											bind:value={modalMaintenanceFlatReward}
										/>
									</label>
								</div>
								<label class="flex items-center gap-2 text-xs text-secondary font-medium cursor-pointer">
									<input type="checkbox" name="maintenanceCountAll" bind:checked={modalMaintenanceCountAll} class="rounded border-stone text-ink focus:ring-0" />
									<span>{t('settings.bonus.countAllUnits')}</span>
								</label>
							{/if}
						{/if}
					</div>

					<!-- Bonus 2: Customer Check Cancellation Fee -->
					<div class="rounded-xl border border-stone bg-white p-4 space-y-3.5 shadow-2xs">
						<label class="flex items-start gap-2.5 cursor-pointer">
							<input type="checkbox" name="cancellationActive" bind:checked={modalCancellationActive} class="mt-0.5 rounded border-stone text-ink focus:ring-0" />
							<div>
								<span class="font-bold text-ink text-body-sm block">{t('settings.bonus.cancellationTitle')}</span>
								<span class="text-xs text-secondary block mt-0.5">{t('settings.bonus.cancellationDesc')}</span>
							</div>
						</label>

						{#if modalCancellationActive}
							<div class="grid grid-cols-1 sm:grid-cols-2 gap-md pt-1 items-center">
								<label class="space-y-1 block">
									<span class="text-[11px] font-bold uppercase tracking-wider text-secondary font-label-caps block">{t('settings.bonus.feePerIncident')}</span>
									<CurrencyInput
										name="cancellationFeePerIncident"
										prefix={currencyCode === 'IDR' ? 'Rp' : '$'}
										locale={data.locale}
										currency={currencyCode}
										bind:value={modalCancellationFee}
									/>
								</label>
								<label class="flex items-center gap-2 text-xs text-secondary font-medium pt-4 cursor-pointer">
									<input type="checkbox" name="cancellationRequireEvidence" bind:checked={modalCancellationRequireEvidence} class="rounded border-stone text-ink focus:ring-0" />
									<span>{t('settings.bonus.requireInspectionEvidence')}</span>
								</label>
							</div>
						{/if}
					</div>

					<!-- Bonus 3: Role-Specific Task Itemized Bonus -->
					<div class="rounded-xl border border-stone bg-white p-4 space-y-3.5 shadow-2xs">
						<div class="flex flex-wrap items-center justify-between gap-sm">
							<label class="flex items-start gap-2.5 cursor-pointer">
								<input type="checkbox" name="taskItemizedActive" bind:checked={modalTaskItemizedActive} class="mt-0.5 rounded border-stone text-ink focus:ring-0" />
								<div>
									<span class="font-bold text-ink text-body-sm block">{t('settings.bonus.taskItemizedTitle')}</span>
									<span class="text-xs text-secondary block mt-0.5">{t('settings.bonus.taskItemizedDesc')}</span>
								</div>
							</label>
							{#if modalTaskItemizedActive}
								<div class="flex flex-wrap gap-xs">
									<button
										type="button"
										onclick={loadDefaultTaskRules}
										class="px-2 py-1 rounded-button border border-stone bg-white text-xs font-medium text-ink hover:bg-ash transition-all shadow-2xs cursor-pointer"
									>
										{t('settings.bonus.loadDefaultTasks')}
									</button>
									<button
										type="button"
										onclick={addTaskRule}
										class="px-2 py-1 rounded-button bg-ink text-white text-xs font-medium hover:bg-ink/90 active:scale-95 transition-all shadow-2xs cursor-pointer inline-flex items-center gap-1"
									>
										<span class="material-symbols-outlined text-[14px]">add</span>
										<span>{t('settings.bonus.addTaskRule')}</span>
									</button>
								</div>
							{/if}
						</div>

						{#if modalTaskItemizedActive}
							{#if modalTaskRules.length === 0}
								<p class="text-xs text-secondary italic py-2 mb-0">{t('settings.bonus.noTasksConfigured')}</p>
							{:else}
								<div class="space-y-2">
									{#each modalTaskRules as rule, index}
										<div class="grid grid-cols-1 sm:grid-cols-[1.5fr_1.5fr_1fr_1.2fr_auto] gap-2 items-end rounded-lg border border-stone bg-ash/30 p-2.5">
											<label class="space-y-1">
												<span class="text-[10.5px] font-bold uppercase tracking-wider text-secondary font-label-caps block">{t('settings.bonus.taskLabel')}</span>
												<input
													type="text"
													bind:value={rule.taskLabel}
													placeholder="OS Reinstall"
													class="w-full rounded-input border border-stone bg-white p-1.5 text-body-sm text-ink focus:border-ink focus:outline-none"
												/>
											</label>
											<label class="space-y-1">
												<span class="text-[10.5px] font-bold uppercase tracking-wider text-secondary font-label-caps block">{t('settings.bonus.matcherKeyword')}</span>
												<input
													type="text"
													bind:value={rule.matcherKeyword}
													placeholder="OS,Windows"
													class="w-full rounded-input border border-stone bg-white p-1.5 text-body-sm text-ink focus:border-ink focus:outline-none"
												/>
											</label>
											<div class="space-y-1">
												<span class="text-[10.5px] font-bold uppercase tracking-wider text-secondary font-label-caps block">{t('settings.bonus.deviceCategory')}</span>
												<Dropdown
													size="sm"
													bind:value={rule.deviceCategory}
													options={[
														{ value: 'Any', label: t('settings.bonus.deviceCategory.Any') },
														{ value: 'Standard', label: t('settings.bonus.deviceCategory.Standard') },
														{ value: 'Gaming', label: t('settings.bonus.deviceCategory.Gaming') }
													]}
												/>
											</div>
											<label class="space-y-1">
												<span class="text-[10.5px] font-bold uppercase tracking-wider text-secondary font-label-caps block">{t('settings.bonus.taskReward')}</span>
												<CurrencyInput
													bind:value={rule.rewardPerUnit}
													prefix={currencyCode === 'IDR' ? 'Rp' : '$'}
													locale={data.locale}
													currency={currencyCode}
													inputClass="p-1.5 text-body-sm"
												/>
											</label>
											<button
												type="button"
												onclick={() => removeTaskRule(index)}
												class="h-9 px-2.5 rounded-button border border-error/30 bg-error/10 text-error hover:bg-error/20 text-xs font-semibold cursor-pointer transition-colors"
											>
												{t('settings.removeTier')}
											</button>
										</div>
									{/each}
								</div>
							{/if}
						{/if}
					</div>
				</div>

				<div class="flex justify-end gap-2 border-t border-stone pt-3.5">
					<button
						type="button"
						disabled={freelanceSubmitting}
						onclick={() => (editingUser = null)}
						class="px-4 py-2 rounded-button border border-stone bg-white text-ink hover:bg-ash text-body-sm font-medium transition-all shadow-2xs cursor-pointer disabled:opacity-60"
					>
						{t('common.cancel')}
					</button>
					<button
						type="submit"
						disabled={freelanceSubmitting}
						class="px-4 py-2 rounded-button bg-ink text-white font-medium text-body-sm hover:bg-ink/90 active:scale-95 transition-all shadow-xs cursor-pointer inline-flex items-center gap-1.5 disabled:opacity-60 disabled:cursor-not-allowed"
					>
						{#if freelanceSubmitting}
							<span class="inline-block w-3.5 h-3.5 border-2 border-white border-t-transparent rounded-full animate-spin"></span>
						{/if}
						<span>{t('common.save')}</span>
					</button>
				</div>
			</form>
		</div>
	</div>
{/if}
