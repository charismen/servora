<script lang="ts">
	import type { LayoutData } from './$types';
	import { page } from '$app/stores';
	import { enhance } from '$app/forms';
	import { onMount } from 'svelte';
	import { fade } from 'svelte/transition';
	import { can, permissions } from '$lib/auth/permissions';
	import { createTranslator, formatDate, locales, resolveTerminology } from '$lib/i18n';
	import { activityEntityHref } from '$lib/types/activity';
	import { magnetic } from '$lib/components/magnetic';
	import { highlight } from '$lib/components/highlight';
	import Dropdown from '$lib/components/ui/dropdown/Dropdown.svelte';
	import ToastContainer from '$lib/components/ui/toast/ToastContainer.svelte';
	import WorkspaceSwitcher from '$lib/components/WorkspaceSwitcher.svelte';
	import { toast } from '$lib/stores/toast.svelte';
	import logo from '$lib/assets/favicon.svg';

	let sidebarFormEl: HTMLFormElement;
	let headerFormEl: HTMLFormElement;

	let { children, data }: { children: import('svelte').Snippet; data: LayoutData } = $props();
	const t = $derived(createTranslator(data.locale));
	const terms = $derived(resolveTerminology(data.user.workspace?.terminology, data.locale));
	const canViewOrg = $derived(
		can(data.user.permissions, permissions.organizationsRead) ||
			data.user.organization?.role === 'Owner' ||
			data.user.organization?.role === 'Admin' ||
			data.user.organization?.role === 'Executive'
	);
	let sidebarOpen = $state(true);
	let mobileOpen = $state(false);
	let isDesktop = $state(true);
	let isLoggingOut = $state(false);
	let profileOpen = $state(false);

	let searchInputEl: HTMLInputElement | undefined = $state();
	let isMac = $state(true);

	// ponytail: clickOutside action for user profile popup menu in layout sidebar
	function clickOutside(node: HTMLElement) {
		const handleClick = (event: MouseEvent) => {
			if (node && !node.contains(event.target as Node)) {
				profileOpen = false;
			}
		};
		document.addEventListener('click', handleClick, true);
		return {
			destroy() {
				document.removeEventListener('click', handleClick, true);
			}
		};
	}

	let notificationsOpen = $state(false);
	let hasUnread = $state(false);
	const recentActivities = $derived(data.recentActivities ?? []);

	onMount(() => {
		isMac = typeof navigator !== 'undefined' && /Mac|iPod|iPhone|iPad/.test(navigator.platform || navigator.userAgent);

		const mq = window.matchMedia('(min-width: 1024px)');
		isDesktop = mq.matches;
		const handleMediaChange = (e: MediaQueryListEvent) => {
			isDesktop = e.matches;
			if (!e.matches) {
				sidebarOpen = true;
			}
		};
		mq.addEventListener('change', handleMediaChange);

		try {
			const lastRead = localStorage.getItem('servora_notifications_read_at');
			if (recentActivities.length > 0) {
				const latestTime = new Date(recentActivities[0].createdAt).getTime();
				if (!lastRead || latestTime > Number(lastRead)) {
					hasUnread = true;
				}
			}
		} catch {
			// ignore storage access errors
		}

		return () => {
			mq.removeEventListener('change', handleMediaChange);
		};
	});

	const isSidebarExpanded = $derived(!isDesktop || sidebarOpen);

	function markNotificationsRead() {
		hasUnread = false;
		try {
			localStorage.setItem('servora_notifications_read_at', String(Date.now()));
		} catch {
			// ignore storage access errors
		}
	}

	// ponytail: clickOutside action for header notifications popup menu
	function clickOutsideNotification(node: HTMLElement) {
		const handleClick = (event: MouseEvent) => {
			if (node && !node.contains(event.target as Node)) {
				notificationsOpen = false;
			}
		};
		document.addEventListener('click', handleClick, true);
		return {
			destroy() {
				document.removeEventListener('click', handleClick, true);
			}
		};
	}

	const brandName = $derived(data.user.shop?.name || data.shopProfile?.shopName || t('brand.name'));

	let lastHandledForm: unknown = null;

	$effect(() => {
		const formResult = $page.form as Record<string, unknown> | null | undefined;
		if (!formResult) {
			lastHandledForm = null;
			return;
		}
		if (formResult === lastHandledForm) return;
		lastHandledForm = formResult;

		const isWarning = formResult.variant === 'warning';
		const isError =
			!isWarning &&
			(formResult.success === false ||
				formResult.ok === false ||
				formResult.variant === 'error' ||
				$page.status >= 400 ||
				Boolean(formResult.error || formResult.errors || formResult.errorKey));
		const isSuccess = !isError && !isWarning && (formResult.success === true || formResult.ok === true);

		let message = '';
		if (typeof formResult.messageKey === 'string') {
			if (formResult.messageKey === 'inventory.import.applied' && formResult.result) {
				const res = formResult.result as { created?: number; updated?: number; skipped?: number };
				message = `${t('inventory.import.applied')} ${t('inventory.import.created')}: ${res.created ?? 0}, ${t('inventory.import.updated')}: ${res.updated ?? 0}, ${t('inventory.import.skipped')}: ${res.skipped ?? 0}.`;
			} else {
				const translated = t(formResult.messageKey);
				message = translated !== formResult.messageKey ? translated : formResult.messageKey;
			}
		} else if (typeof formResult.message === 'string' && formResult.message.trim()) {
			message = formResult.message.trim();
		} else if (isSuccess) {
			message = t('common.saved');
		} else if (isError) {
			message = t('common.error');
		}

		if (!message) return;

		if (isWarning) {
			toast.warning(message);
		} else if (isError) {
			toast.error(message);
		} else if (isSuccess) {
			toast.success(message);
		} else {
			toast.info(message);
		}
	});

	interface NavItem {
		href: string;
		label: string;
		icon: string;
		visible: boolean;
	}

	interface NavSection {
		id: string;
		title: string;
		items: NavItem[];
	}

	const navSections = $derived<NavSection[]>([
		{
			id: 'workspace',
			title: t('nav.sections.workspace'),
			items: [
				{ href: '/app', label: t('nav.dashboard'), icon: 'dashboard', visible: true },
				{ href: '/app/tickets', label: terms.ticketPlural || t('nav.tickets'), icon: 'confirmation_number', visible: can(data.user.permissions, permissions.serviceTicketsRead) },
				{ href: '/app/kanban', label: t('nav.kanban'), icon: 'view_kanban', visible: can(data.user.permissions, permissions.serviceTicketsRead) },
				{ href: '/app/dispatch', label: t('nav.dispatch'), icon: 'calendar_month', visible: can(data.user.permissions, permissions.serviceTicketsRead) },
				{ href: '/app/maintenance', label: t('nav.maintenance'), icon: 'event_repeat', visible: can(data.user.permissions, permissions.maintenanceRead) }
			].filter((item) => item.visible)
		},
		{
			id: 'operations',
			title: t('nav.sections.operations'),
			items: [
				{ href: '/app/customers', label: terms.requesterPlural || t('nav.customers'), icon: 'group', visible: can(data.user.permissions, permissions.customersRead) },
				{ href: '/app/inventory', label: t('nav.inventory'), icon: 'inventory_2', visible: can(data.user.permissions, permissions.inventoryRead) },
				{ href: '/app/assets', label: t('nav.assets'), icon: 'precision_manufacturing', visible: can(data.user.permissions, permissions.assetsRead) }
			].filter((item) => item.visible)
		},
		{
			id: 'staff',
			title: t('nav.sections.staff'),
			items: [
				{ href: '/app/attendance', label: t('nav.attendance'), icon: 'schedule', visible: can(data.user.permissions, permissions.attendanceRead) },
				{ href: '/app/payroll', label: t('nav.payroll'), icon: 'payments', visible: can(data.user.permissions, permissions.payrollRead) }
			].filter((item) => item.visible)
		},
		{
			id: 'system',
			title: t('nav.sections.insights'),
			items: [
				{ href: '/app/organization', label: t('nav.organization'), icon: 'corporate_fare', visible: canViewOrg },
				{ href: '/app/reports', label: t('nav.reports'), icon: 'assessment', visible: can(data.user.permissions, permissions.reportsRead) },
				{ href: '/app/activity', label: t('nav.activity'), icon: 'feed', visible: can(data.user.permissions, permissions.reportsRead) },
				{ href: '/app/users-roles', label: t('nav.usersRoles'), icon: 'manage_accounts', visible: can(data.user.permissions, permissions.settingsRead) },
				{ href: '/app/settings/blueprints', label: t('nav.blueprints'), icon: 'tune', visible: can(data.user.permissions, permissions.settingsRead) },
				{ href: '/app/settings', label: t('nav.settings'), icon: 'settings', visible: can(data.user.permissions, permissions.settingsRead) }
			].filter((item) => item.visible)
		}
	].filter((section) => section.items.length > 0));

	// Longest segment-boundary match prevents child items like /app/settings/blueprints from also activating /app/settings.
	const activeNavHref = $derived.by(() => {
		const pathname = $page.url.pathname;
		const allHrefs = navSections.flatMap((section) => section.items.map((item) => item.href));
		const matches = allHrefs.filter(
			(href) => pathname === href || (href !== '/app' && pathname.startsWith(`${href}/`))
		);
		matches.sort((a, b) => b.length - a.length);
		return matches[0] ?? null;
	});

	$effect(() => {
		const handleKeyDown = (e: KeyboardEvent) => {
			const target = e.target as HTMLElement | null;
			const isInput =
				target &&
				(target.tagName === 'INPUT' ||
					target.tagName === 'TEXTAREA' ||
					target.isContentEditable ||
					target.getAttribute('role') === 'textbox');

			// Global shortcut ⌘K / Ctrl+K
			if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === 'k') {
				e.preventDefault();
				searchInputEl?.focus();
				searchInputEl?.select();
				return;
			}

			// Escape inside search input blurs it
			if (e.key === 'Escape' && target === searchInputEl) {
				searchInputEl?.blur();
				return;
			}

			if (isInput) return;

			if (e.key === '[' && !e.metaKey && !e.ctrlKey && !e.altKey) {
				e.preventDefault();
				sidebarOpen = !sidebarOpen;
			}
		};
		window.addEventListener('keydown', handleKeyDown);
		return () => {
			window.removeEventListener('keydown', handleKeyDown);
		};
	});

	const fabConfig = $derived.by(() => {
		const path = $page.url.pathname;
		if (path === '/app/tickets/new') return null;
		if (path === '/app' || path.startsWith('/app/tickets') || path.startsWith('/app/kanban')) {
			return {
				href: '/app/tickets/new',
				label: t('nav.newTicket'),
				icon: 'add',
				visible: can(data.user.permissions, permissions.serviceTicketsWrite)
			};
		}
		if (path.startsWith('/app/customers')) {
			return {
				href: '/app/customers/new',
				label: t('customers.list.newCustomer'),
				icon: 'person_add',
				visible: can(data.user.permissions, permissions.customersWrite)
			};
		}
		if (path.startsWith('/app/inventory')) {
			return {
				href: '#add-item-section',
				label: t('inventory.addItem'),
				icon: 'add_shopping_cart',
				visible: can(data.user.permissions, permissions.inventoryWrite)
			};
		}
		return null;
	});

	$effect(() => {
		// Close mobile drawer on route change (ponytail: keep simple and dynamic)
		if ($page.url.pathname) {
			mobileOpen = false;
		}
	});

	$effect(() => {
		// Global Bento spotlight tracker (ponytail: single global listener for zero-boilerplate spotlights)
		const handleGlobalMouseMove = (e: MouseEvent) => {
			const target = (e.target as HTMLElement).closest?.('[data-bento]') as HTMLElement | null;
			if (!target) return;
			const rect = target.getBoundingClientRect();
			const x = e.clientX - rect.left;
			const y = e.clientY - rect.top;
			target.style.setProperty('--x', `${x}px`);
			target.style.setProperty('--y', `${y}px`);
		};
		window.addEventListener('mousemove', handleGlobalMouseMove);
		return () => {
			window.removeEventListener('mousemove', handleGlobalMouseMove);
		};
	});
</script>

<!-- Backdrop Overlay for Mobile -->
{#if mobileOpen}
	<!-- svelte-ignore a11y_click_events_have_key_events -->
	<!-- svelte-ignore a11y_no_static_element_interactions -->
	<div 
		class="fixed inset-0 bg-black/50 z-40 lg:hidden" 
		onclick={() => mobileOpen = false}
	></div>
{/if}

<!-- Sidebar Navigation Shell -->
<aside class="fixed top-0 bottom-0 z-50 flex flex-col gap-2.5 bg-surface-container-lowest border-r border-stone transition-all duration-200 ease-in-out select-none
	w-[248px] lg:h-screen {mobileOpen ? 'left-0' : 'left-[-248px]'} lg:left-0 {sidebarOpen ? 'lg:w-[248px] p-3' : 'lg:w-[80px] p-3 lg:px-2.5 lg:py-3'}"
>
	<!-- Brand Identity Header -->
	<div class="{isSidebarExpanded ? 'px-2.5 flex items-center justify-between' : 'px-1 lg:flex lg:flex-col lg:items-center lg:gap-2.5 flex items-center justify-between'} gap-2 border-b border-stone/70 pt-1 pb-3">
		<div class="min-w-0 {isSidebarExpanded ? 'lg:flex' : 'lg:hidden'} flex items-center gap-2.5">
			<div class="w-8 h-8 rounded-[9px] bg-surface-container-low border border-stone/80 flex items-center justify-center p-1 shrink-0 shadow-2xs">
				<img src={logo} alt="" class="w-5 h-5 object-contain" />
			</div>
			<div class="min-w-0">
				<div class="flex items-center gap-1.5">
					<span class="font-subheading text-ink font-bold text-[14.5px] leading-tight truncate tracking-tight">{brandName}</span>
					<span class="inline-block w-1.5 h-1.5 rounded-full bg-emerald-500 animate-pulse shrink-0" title={t('nav.statusOnline')}></span>
				</div>
				<p class="font-label-caps text-secondary text-[10px] font-medium uppercase tracking-[0.06em] truncate mt-0.5">{t('nav.managementConsole')}</p>
			</div>
		</div>
		{#if !isSidebarExpanded}
			<div class="relative group hidden lg:block">
				<div class="w-9 h-9 rounded-[10px] bg-surface-container-low border border-stone/80 flex items-center justify-center p-1.5 shrink-0 shadow-2xs">
					<img src={logo} alt={brandName} class="w-5 h-5 object-contain" />
				</div>
				<!-- Floating Tooltip for Logo in Collapsed Mode -->
				<div class="hidden lg:flex absolute left-full ml-3 px-2.5 py-1.5 bg-ink text-white text-[12px] font-medium rounded-md shadow-xl pointer-events-none opacity-0 group-hover:opacity-100 -translate-x-1 group-hover:translate-x-0 transition-all duration-150 whitespace-nowrap z-50 items-center gap-1.5 border border-white/10">
					<span class="font-semibold">{brandName}</span>
					<span class="w-1.5 h-1.5 rounded-full bg-emerald-400"></span>
				</div>
			</div>
		{/if}
		<!-- Desktop Toggle Button -->
		<button
			class="hidden lg:flex w-7 h-7 rounded-md border border-stone bg-white text-secondary hover:text-ink hover:bg-ash transition-colors items-center justify-center shrink-0 cursor-pointer shadow-2xs active:scale-95 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-claytile/45"
			type="button"
			aria-label={sidebarOpen ? t('nav.collapseSidebar') : t('nav.expandSidebar')}
			title={(sidebarOpen ? t('nav.collapseSidebar') : t('nav.expandSidebar')) + ' ( [ )'}
			onclick={() => (sidebarOpen = !sidebarOpen)}
		>
			<span class="material-symbols-outlined text-[16px]">{sidebarOpen ? 'dock_to_left' : 'dock_to_right'}</span>
		</button>
		<!-- Mobile Close Button -->
		<button
			class="lg:hidden w-8 h-8 rounded-lg border border-stone bg-white text-secondary hover:text-ink hover:bg-ash transition-colors flex items-center justify-center shrink-0 cursor-pointer shadow-2xs focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-claytile/45"
			type="button"
			aria-label={t('nav.closeMenu')}
			onclick={() => mobileOpen = false}
		>
			<span class="material-symbols-outlined text-[18px]">close</span>
		</button>
	</div>

	<!-- Workspace Switcher -->
	{#if data.user.workspace}
		<div class="px-0">
			<WorkspaceSwitcher
				currentWorkspace={data.user.workspace}
				accessibleWorkspaces={data.user.accessibleWorkspaces ?? []}
				locale={data.locale}
				compact={!isSidebarExpanded}
				canManage={can(data.user.permissions, permissions.settingsWrite)}
			/>
		</div>
	{/if}

	<!-- Main Grouped Navigation Items -->
	<nav aria-label={t('nav.primary')} class="flex-1 min-h-0 flex flex-col gap-3.5 overflow-y-auto overflow-x-hidden custom-scrollbar pr-0.5 py-0.5">
		{#each navSections as section, sIdx (section.id)}
			<div>
				{#if isSidebarExpanded}
					<div class="px-2.5 pb-1 flex items-center">
						<span class="font-label-caps text-secondary uppercase text-[10.5px] font-semibold tracking-[0.07em] leading-none">
							{section.title}
						</span>
					</div>
				{:else if sIdx > 0}
					<div class="my-1.5 border-t border-stone/70 mx-2"></div>
				{/if}

				<div class="space-y-0.5">
					{#each section.items as item (item.href)}
						{@const isActive = activeNavHref === item.href}
						<a
							class="group relative flex items-center {isSidebarExpanded ? 'justify-start gap-2.5 px-2.5' : 'justify-center px-1.5'} min-h-[38px] py-1.5 rounded-[10px] duration-150 ease-out transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-claytile/45 focus-visible:ring-offset-1
								{isActive
									? 'bg-ink text-white font-semibold shadow-xs'
									: 'text-secondary font-medium hover:text-ink hover:bg-ash active:bg-stone/60'}"
							href={item.href}
							onclick={() => { mobileOpen = false; }}
							aria-label={item.label}
							aria-current={isActive ? 'page' : undefined}
							title={!isSidebarExpanded ? item.label : undefined}
						>
							<span
								class="material-symbols-outlined text-[19px] leading-none transition-transform duration-150 shrink-0 {isActive ? 'text-white' : 'text-secondary group-hover:text-ink group-hover:scale-105'}"
								style={isActive ? "font-variation-settings: 'FILL' 1;" : ""}
							>
								{item.icon}
							</span>
							{#if isSidebarExpanded}
								<span class="block truncate text-[13.5px] leading-snug tracking-[-0.01em]">{item.label}</span>
							{/if}
						</a>
					{/each}
				</div>
			</div>
		{/each}
	</nav>

	<!-- Mobile-only Locale Selector in Sidebar -->
	<div class="lg:hidden px-0 mb-1">
		<form bind:this={sidebarFormEl} method="POST" action="/locale" class="w-full">
			<input type="hidden" name="redirectTo" value={$page.url.pathname + $page.url.search} />
			<label class="sr-only" for="sidebar-locale">{t('locale.label')}</label>
			<Dropdown
				name="locale"
				value={data.locale}
				options={locales.map(l => ({ value: l.code, label: l.label }))}
				onchange={() => setTimeout(() => sidebarFormEl?.requestSubmit(), 0)}
			/>
		</form>
	</div>

	<!-- Sidebar Footer: Profile -->
	<div class="mt-auto border-t border-stone/70 pt-2.5 px-0 relative" use:clickOutside>
		<!-- Popover Menu -->
		{#if profileOpen}
			<div transition:fade={{ duration: 150 }} class="absolute bottom-16 left-0 right-0 max-w-[232px] sm:w-64 z-50 bg-white border border-stone rounded-xl shadow-xl p-md space-y-md select-none">
				<div class="border-b border-stone pb-sm">
					<p class="font-body-sm font-semibold text-primary truncate leading-none mb-1">{data.user.fullName || data.user.email}</p>
					<p class="text-[11px] text-secondary truncate">{data.user.email}</p>
					<div class="flex flex-wrap gap-xs mt-2">
						{#each data.user.roles as role}
							<span class="px-1.5 py-0.5 rounded bg-ash text-secondary text-[9px] font-bold uppercase tracking-wider">{role}</span>
						{/each}
					</div>
				</div>
				<div class="space-y-xs">
					<a
						href="/app/settings/security"
						class="flex items-center gap-sm px-md py-sm rounded-lg text-body-sm transition-colors cursor-pointer focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-claytile/45 {$page.url.pathname.startsWith('/app/settings/security') ? 'bg-ash font-semibold text-ink' : 'text-secondary hover:bg-ash/60 hover:text-primary'}"
						onclick={() => { profileOpen = false; mobileOpen = false; }}
					>
						<span class="material-symbols-outlined text-[18px]">shield_lock</span>
						<span>{t('settings.accountSecurity')}</span>
					</a>
					{#if can(data.user.permissions, permissions.settingsRead)}
						<a
							href="/app/users-roles"
							class="flex items-center gap-sm px-md py-sm rounded-lg text-body-sm transition-colors cursor-pointer focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-claytile/45 {activeNavHref === '/app/users-roles' ? 'bg-ash font-semibold text-ink' : 'text-secondary hover:bg-ash/60 hover:text-primary'}"
							onclick={() => { profileOpen = false; mobileOpen = false; }}
						>
							<span class="material-symbols-outlined text-[18px]">manage_accounts</span>
							<span>{t('nav.usersRoles')}</span>
						</a>
						<a
							href="/app/settings"
							class="flex items-center gap-sm px-md py-sm rounded-lg text-body-sm transition-colors cursor-pointer focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-claytile/45 {activeNavHref === '/app/settings' ? 'bg-ash font-semibold text-ink' : 'text-secondary hover:bg-ash/60 hover:text-primary'}"
							onclick={() => { profileOpen = false; mobileOpen = false; }}
						>
							<span class="material-symbols-outlined text-[18px]">settings</span>
							<span>{t('nav.settings')}</span>
						</a>
					{/if}
					<button type="button" class="w-full flex items-center gap-sm px-md py-sm rounded-lg text-body-sm text-secondary hover:bg-ash/60 hover:text-primary transition-colors cursor-pointer text-left focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-claytile/45" onclick={() => { profileOpen = false; alert(t('app.helpComingSoon')); }}>
						<span class="material-symbols-outlined text-[18px]">help</span>
						<span>{t('common.help')}</span>
					</button>
				</div>
				<div class="border-t border-stone pt-sm">
					<form method="POST" action="/logout" class="w-full">
						<button class="w-full flex items-center gap-sm px-md py-sm rounded-lg text-body-sm text-error hover:bg-error/5 transition-colors cursor-pointer text-left font-semibold focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-error/40" type="submit" onclick={() => isLoggingOut = true}>
							<span class="material-symbols-outlined text-[18px]">logout</span>
							<span>{t('nav.signOut')}</span>
						</button>
					</form>
				</div>
			</div>
		{/if}

		<!-- Profile Trigger Card -->
		<button
			type="button"
			onclick={() => profileOpen = !profileOpen}
			aria-expanded={profileOpen}
			aria-haspopup="menu"
			class="w-full flex items-center {isSidebarExpanded ? 'gap-2.5 px-2.5 py-2' : 'justify-center p-1.5'} rounded-[10px] border border-transparent hover:bg-ash hover:border-stone/60 active:scale-[0.98] transition-all text-left cursor-pointer overflow-hidden group relative focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-claytile/45"
			title={isSidebarExpanded ? undefined : (data.user.fullName || data.user.email)}
		>
			<div class="relative shrink-0">
				<div class="w-8 h-8 rounded-full border border-stone bg-surface-container-low flex items-center justify-center overflow-hidden font-bold text-xs text-ink">
					{(data.user.fullName || data.user.email || 'A')[0].toUpperCase()}
				</div>
				<span class="absolute bottom-0 right-0 w-2 h-2 rounded-full bg-emerald-500 ring-2 ring-white"></span>
			</div>
			{#if isSidebarExpanded}
				<div class="overflow-hidden flex flex-col flex-1 min-w-0">
					<span class="font-body-sm font-semibold text-ink text-[13px] truncate leading-tight">{data.user.fullName || data.user.email}</span>
					<span class="font-label-caps text-[10px] text-secondary uppercase tracking-[0.06em] truncate mt-0.5">{data.user.roles[0] || t('nav.staffFallback')}</span>
				</div>
				<span class="material-symbols-outlined text-secondary group-hover:text-ink text-[18px] shrink-0 transition-transform duration-200 {profileOpen ? 'rotate-180' : ''}">
					unfold_more
				</span>
			{:else}
				<!-- Floating Tooltip for Profile in Collapsed Mode -->
				<div class="hidden lg:flex absolute left-full ml-3 px-2.5 py-1.5 bg-ink text-white text-[12px] font-medium rounded-md shadow-xl pointer-events-none opacity-0 group-hover:opacity-100 -translate-x-1 group-hover:translate-x-0 transition-all duration-150 whitespace-nowrap z-50 items-center gap-1.5 border border-white/10">
					<span class="leading-none">{data.user.fullName || data.user.email}</span>
					<span class="text-[10px] text-lead uppercase font-mono">({data.user.roles[0] || t('nav.staffFallback')})</span>
				</div>
			{/if}
		</button>
	</div>
</aside>

<!-- Top Navigation Shell -->
<header class="fixed top-0 right-0 left-0 {sidebarOpen ? 'lg:left-[248px]' : 'lg:left-[80px]'} h-16 bg-surface-container-lowest border-b border-stone flex justify-between items-center gap-xs sm:gap-md px-sm sm:px-md lg:gap-xl lg:px-xl z-40 transition-[left] duration-200 ease-in-out">
	<!-- Hamburger Menu for Mobile -->
	<button
		use:magnetic={{ range: 40, strength: 0.25 }}
		class="lg:hidden w-10 h-10 flex items-center justify-center rounded-lg hover:bg-secondary/10 text-primary shrink-0 cursor-pointer"
		type="button"
		onclick={() => {
			mobileOpen = true;
			sidebarOpen = true;
		}}
		aria-label={t('nav.openMenu')}
	>
		<span class="material-symbols-outlined text-[24px]">menu</span>
	</button>

	<form action="/app/tickets" method="GET" class="flex-1 min-w-0 lg:min-w-[320px] max-w-[560px]">
		<div class="relative flex items-center rounded-input group">
			<span class="material-symbols-outlined absolute left-3.5 text-secondary text-[18px] pointer-events-none group-focus-within:text-ink transition-colors">search</span>
			<input 
				bind:this={searchInputEl}
				name="search"
				value={$page.url.pathname === '/app/tickets' ? ($page.url.searchParams.get('search') ?? '') : ''}
				class="w-full h-9.5 pl-10 pr-16 bg-ash/60 border border-stone focus:bg-white focus:border-ink focus:ring-2 focus:ring-ink/10 rounded-input font-body-sm text-ink placeholder:text-secondary focus:outline-none transition-all shadow-2xs" 
				placeholder={t('nav.globalSearch')} 
				type="search"
			/>
			<kbd class="hidden sm:inline-flex items-center absolute right-3 pointer-events-none text-[10px] font-mono text-metal bg-white border border-stone px-1.5 py-0.5 rounded shadow-3xs select-none">
				{isMac ? '⌘K' : 'Ctrl+K'}
			</kbd>
		</div>
	</form>
	<div class="flex items-center gap-xs sm:gap-sm lg:gap-md">
		<!-- Trailing Actions -->
		<div class="relative" use:clickOutsideNotification>
			<button 
				onclick={() => {
					notificationsOpen = !notificationsOpen;
					if (notificationsOpen && hasUnread) {
						markNotificationsRead();
					}
				}}
				use:magnetic={{ range: 40, strength: 0.25 }} 
				class="relative p-2 text-secondary hover:text-primary transition-colors cursor-pointer block rounded-lg hover:bg-ash/50"
				type="button"
				aria-label={t('app.notifications')}
			>
				<span class="material-symbols-outlined text-[20px]">notifications</span>
				{#if hasUnread}
					<span class="absolute top-2 right-2 w-2 h-2 bg-error rounded-full ring-2 ring-white"></span>
				{/if}
			</button>
			{#if notificationsOpen}
				<div transition:fade={{ duration: 150 }} class="absolute right-0 mt-2 z-50 w-80 max-w-[calc(100vw-2rem)] bg-white border border-stone rounded-xl shadow-xl p-md select-none">
					<div class="flex items-center justify-between border-b border-stone pb-sm mb-sm">
						<span class="font-body-sm font-semibold text-primary">{t('app.notifications')}</span>
						{#if hasUnread}
							<button type="button" class="text-[11px] text-secondary hover:text-primary font-semibold cursor-pointer" onclick={markNotificationsRead}>{t('app.markAllRead')}</button>
						{/if}
					</div>
					{#if recentActivities.length > 0}
						<div class="max-h-72 overflow-y-auto divide-y divide-stone">
							{#each recentActivities as activity}
								{@const href = activityEntityHref(activity.entityType, activity.entityId)}
								<a
									href={href ?? '/app/activity'}
									class="flex gap-sm items-start p-sm rounded-lg hover:bg-ash/50 transition-all text-left"
									onclick={() => {
										hasUnread = false;
										notificationsOpen = false;
									}}
								>
									{#if hasUnread}
										<div class="w-2 h-2 bg-error rounded-full mt-2 shrink-0"></div>
									{/if}
									<div class="flex-1 min-w-0 space-y-0.5">
										<p class="font-body-sm text-primary mb-0 font-medium truncate">{activity.title}</p>
										{#if activity.description}
											<p class="text-[11px] text-secondary mb-0 truncate">{activity.description}</p>
										{/if}
										<p class="text-[10px] text-overcast mb-0">{formatDate(activity.createdAt, data.locale, { dateStyle: 'medium', timeStyle: 'short' })}</p>
									</div>
								</a>
							{/each}
						</div>
						<div class="border-t border-stone pt-sm mt-xs text-center">
							<a href="/app/activity" class="text-[11px] font-semibold text-primary hover:underline" onclick={() => (notificationsOpen = false)}>
								{t('app.viewAllActivity')}
							</a>
						</div>
					{:else}
						<div class="space-y-sm py-xs">
							{#if hasUnread}
								<button type="button" class="w-full flex gap-sm items-start p-sm rounded-lg hover:bg-ash/50 transition-all cursor-pointer text-left border-0 outline-none" onclick={() => hasUnread = false}>
									<div class="w-2 h-2 bg-error rounded-full mt-2 shrink-0"></div>
									<div class="flex-1 space-y-0.5">
										<p class="font-body-sm text-primary mb-0 font-medium">{t('app.welcomeNotificationTitle')}</p>
										<p class="text-[11px] text-secondary mb-0">{t('app.welcomeNotificationBody')}</p>
									</div>
								</button>
							{:else}
								<p class="text-body-sm text-secondary text-center py-md mb-0">{t('app.noNewNotifications')}</p>
							{/if}
						</div>
					{/if}
				</div>
			{/if}
		</div>
		<form bind:this={headerFormEl} method="POST" action="/locale" class="hidden sm:block">
			<input type="hidden" name="redirectTo" value={$page.url.pathname + $page.url.search} />
			<label class="sr-only" for="app-locale">{t('locale.label')}</label>
			<Dropdown
				name="locale"
				value={data.locale}
				label={data.locale === 'id' ? 'ID' : 'EN'}
				options={locales.map(l => ({ value: l.code, label: l.label }))}
				onchange={() => setTimeout(() => headerFormEl?.requestSubmit(), 0)}
				size="sm"
				class="w-20 shrink-0"
				align="right"
			/>
		</form>
		<!-- CTA (Suppressed on screens with dedicated in-page ticket intake actions: Dashboard, Tickets, Kanban, New Ticket) -->
		{#if can(data.user.permissions, permissions.serviceTicketsWrite) && $page.url.pathname !== '/app' && $page.url.pathname !== '/app/tickets' && $page.url.pathname !== '/app/kanban' && !$page.url.pathname.startsWith('/app/tickets/new')}
			<a use:magnetic href="/app/tickets/new" class="hidden lg:flex px-button-x py-button-y rounded-button font-body-sm font-semibold bg-ink text-white hover:opacity-90 active:scale-95 transition-all items-center justify-center gap-xs shadow-xs shrink-0">
				<span class="material-symbols-outlined text-[18px]">add</span>
				<span>{t('nav.newTicket')}</span>
			</a>
		{/if}
	</div>
</header>

<!-- Main Content Canvas -->
<main class="ml-0 {sidebarOpen ? 'lg:ml-[248px]' : 'lg:ml-[80px]'} mt-16 min-w-0 flex flex-col transition-[margin-left] duration-200 ease-in-out" style="min-height: calc(100vh - 64px);">
	<!-- View Content -->
	<div class="flex-1 min-w-0 overflow-y-auto p-md lg:p-xl bg-background text-on-surface font-body-base" style="font-size: 16px; line-height: 1.5; letter-spacing: -0.24px;">
		{#if data.user.requiresTwoFactorSetup && !$page.url.pathname.startsWith('/app/settings/security')}
			<div class="mb-5 flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-amber-300 bg-amber-50 px-4 py-3 text-sm">
				<div class="flex items-start gap-2.5">
					<span class="material-symbols-outlined text-[20px] text-amber-700 mt-0.5 shrink-0">shield_lock</span>
					<div>
						<div class="font-semibold text-amber-950">{t('security.policyBannerTitle')}</div>
						<div class="text-xs text-amber-900">{t('security.policyBannerBody')}</div>
					</div>
				</div>
				<a
					href="/app/settings/security"
					class="rounded-lg bg-amber-800 px-3.5 py-1.5 text-xs font-semibold text-white transition hover:bg-amber-900"
				>
					{t('security.policyBannerCta')}
				</a>
			</div>
		{/if}
		{#if data.user.workspace && !data.user.workspace.setupCompleted && !$page.url.pathname.startsWith('/app/onboarding') && can(data.user.permissions, permissions.settingsWrite)}
			<div class="mb-5 flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-claytile/30 bg-claytile/5 px-4 py-3 text-sm">
				<div>
					<div class="font-semibold text-ink">{t('workspaces.setupBannerTitle')}</div>
					<div class="text-xs text-slate-600">{t('workspaces.setupBannerBody')}</div>
				</div>
				<a
					href="/app/onboarding"
					class="rounded-lg bg-claytile px-3.5 py-1.5 text-xs font-semibold text-white transition hover:opacity-90"
				>
					{t('workspaces.setupBannerCta')}
				</a>
			</div>
		{/if}
		{@render children()}
	</div>
</main>

<!-- Mobile Floating Action Button (FAB) (ponytail: page-dynamic FAB following industry standards) -->
{#if fabConfig && fabConfig.visible}
	<a 
		use:magnetic={{ range: 60, strength: 0.4 }}
		href={fabConfig.href} 
		class="lg:hidden fixed bottom-6 right-6 w-14 h-14 bg-ink text-white rounded-full flex items-center justify-center shadow-lg active:scale-95 transition-all z-40 cursor-pointer print:hidden"
		aria-label={fabConfig.label}
		title={fabConfig.label}
	>
		<span class="material-symbols-outlined text-[28px]">{fabConfig.icon}</span>
	</a>
{/if}

{#if isLoggingOut}
	<div transition:fade={{ duration: 200 }} class="fixed inset-0 bg-white/60 backdrop-blur-md flex flex-col items-center justify-center z-[100] cursor-wait select-none">
		<div class="flex flex-col items-center gap-md">
			<div class="relative w-12 h-12 flex items-center justify-center">
				<div class="absolute w-12 h-12 rounded-full border-4 border-primary/20 animate-ping"></div>
				<div class="w-8 h-8 rounded-full bg-primary shadow-lg animate-pulse"></div>
			</div>
			<span class="font-body-sm font-semibold text-primary tracking-wide animate-pulse">{t('app.signingOut')}</span>
		</div>
	</div>
{/if}

<ToastContainer />

<style>
	@media print {
		:global(aside),
		:global(header) {
			display: none !important;
		}

		:global(main) {
			margin-left: 0 !important;
			margin-top: 0 !important;
			min-height: auto !important;
		}

		:global(main > div) {
			padding: 0 !important;
			background: #ffffff !important;
		}
	}
</style>
