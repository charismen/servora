<script lang="ts">
	import { fade } from 'svelte/transition';
	import PageHeader from '$lib/components/PageHeader.svelte';
	import { createTranslator, formatDate, resolveFeatureConfig, resolveTerminology } from '$lib/i18n';
	import { activityEntityHref } from '$lib/types/activity';
	import type { PageData } from './$types';
	import GlowingBadge from '$lib/components/ui/glowing-badge/GlowingBadge.svelte';

	let { data }: { data: PageData } = $props();

	let isLoading = $state(false);

	const t = $derived(createTranslator(data.locale));
	const terms = $derived(resolveTerminology(data.user?.workspace?.terminology, data.locale));
	const features = $derived(
		resolveFeatureConfig(
			data.user?.workspace?.featureConfig ?? data.blueprintConfig?.featureConfig,
			data.user?.workspace?.operatingMode
		)
	);

	function formatActionType(actionType: string) {
		const key = 'activity.action.' + actionType;
		const translated = t(key);
		if (translated !== key) return translated;

		let formatted = actionType.includes('_')
			? actionType.replace(/_/g, ' ')
			: actionType.replace(/([A-Z])/g, ' $1');
		
		formatted = formatted.trim();
		return formatted.charAt(0).toUpperCase() + formatted.slice(1);
	}
	
	const SETTING_KEY_I18N_MAP: Record<string, string> = {
		'ShopProfile.ShopName': 'settings.storeName',
		'ShopProfile.PhoneNumber': 'settings.phoneNumber',
		'ShopProfile.WhatsAppNumber': 'settings.whatsappPhone',
		'ShopProfile.Email': 'settings.email',
		'ShopProfile.Address': 'settings.storeAddress',
		'ShopProfile.TicketPrefix': 'settings.globalPrefix',
		'TicketNumbering.DateFormat': 'settings.ticketDateFormat',
		'TicketNumbering.StartNumber': 'settings.startNumber',
		'TicketNumbering.MinDigits': 'settings.minDigits',
		'ShopProfile.CurrencyCode': 'settings.currency',
		'ShopProfile.ReceiptFooter': 'settings.receiptFooter',
		'Inventory.DefaultTaxPercent': 'settings.inventoryDefaultTaxPercent',
		'Inventory.RoundingIncrement': 'settings.inventoryRoundingIncrement',
		'Inventory.DefaultUnit': 'settings.inventoryDefaultUnit',
		'Inventory.AllowDecimalQuantity': 'settings.inventoryAllowDecimalQuantity',
		'Inventory.AutoCreateCategoryOnImport': 'settings.inventoryAutoCreateCategoryOnImport',
		'PhotoEvidence.RequireOnIntake': 'settings.requirePhotoEvidenceOnIntake',
		'PublicTracking.ShowPhotoEvidence': 'settings.showPhotoEvidenceOnPublicTracking',
		'Approval.RequireEstimate': 'settings.requireApprovalEstimate',
		'Payment.AllowUnpaidPickupOverride': 'settings.allowUnpaidPickupOverride',
		'PrintTemplate.ShowPaymentSummary': 'settings.showPaymentSummary'
	};

	// ponytail: sanitize raw template keys or snake_case descriptors in description
	function formatDescription(desc: string | null | undefined) {
		if (!desc) return '';
		
		const parts = desc.split(',').map(p => p.trim());
		const formattedParts = parts.map(part => {
			const i18nKey = SETTING_KEY_I18N_MAP[part];
			if (i18nKey) {
				const translated = t(i18nKey);
				return translated.replace(/\s*\(.*\)/, '').trim();
			}

			if (part === 'Ticket cancelled.') return t('activity.description.ticketCancelled');
			
			const key = 'activity.action.' + part;
			const translated = t(key);
			if (translated !== key) return translated;
			
			if (part.includes('_') && /^[a-z0-9_]+$/.test(part)) {
				const formatted = part.replace(/_/g, ' ');
				return formatted.charAt(0).toUpperCase() + formatted.slice(1);
			}
			return part;
		});
		
		return formattedParts.join(', ');
	}

	function formatTitle(title: string) {
		if (!title) return '';
		
		if (title === 'Ticket note added.') return t('activity.action.TicketNoteAdded');
		if (title === 'Shop user roles updated.') return t('activity.action.ShopUserRolesUpdated');
		if (title === 'Shop user reactivated.') return t('activity.action.ShopUserReactivated');
		if (title === 'Shop user deactivated.') return t('activity.action.ShopUserDeactivated');
		if (title === 'Pickup payment block overridden.') return t('activity.action.TicketPickupOverride');

		const statusPrefixes = [
			{ en: 'Approval Approved: ', action: 'TicketApprovalResolved', statusKey: 'tickets.approvalStatus.Approved' },
			{ en: 'Approval Rejected: ', action: 'TicketApprovalResolved', statusKey: 'tickets.approvalStatus.Rejected' },
			{ en: 'Approval Cancelled: ', action: 'TicketApprovalResolved', statusKey: 'tickets.approvalStatus.Cancelled' }
		];
		for (const sp of statusPrefixes) {
			if (title.startsWith(sp.en)) {
				const localizedAction = t('activity.action.' + sp.action);
				const localizedStatus = t(sp.statusKey);
				const remainder = title.slice(sp.en.length);
				return `${localizedAction} (${localizedStatus}): ${remainder}`;
			}
		}

		const prefixes = [
			{ en: 'Ticket created: ', action: 'TicketCreated' },
			{ en: 'Ticket updated: ', action: 'TicketUpdated' },
			{ en: 'Ticket ', action: 'TicketStatusChanged', contains: ' moved to ' },
			{ en: 'Ticket assignment updated: ', action: 'TicketAssigned' },
			{ en: 'Approval requested: ', action: 'TicketApprovalRequested' },
			{ en: 'Approval approved: ', action: 'TicketApprovalResolved' },
			{ en: 'Approval rejected: ', action: 'TicketApprovalResolved' },
			{ en: 'Approval cancelled: ', action: 'TicketApprovalResolved' },
			{ en: 'Work item added: ', action: 'TicketWorkItemCreated' },
			{ en: 'Work item updated: ', action: 'TicketWorkItemUpdated' },
			{ en: 'Work item removed: ', action: 'TicketWorkItemDeleted' },
			{ en: 'Payment recorded: ', action: 'TicketPaymentRecorded' },
			{ en: 'Refund recorded: ', action: 'TicketRefundRecorded' },
			{ en: 'Payment transaction voided: ', action: 'TicketPaymentVoided' },
			{ en: 'Billing updated: ', action: 'TicketBillingUpdated' },
			{ en: 'Payment updated: ', action: 'TicketPaymentUpdated' },
			{ en: 'Ticket deleted: ', action: 'TicketDeleted' },
			{ en: 'Template applied: ', action: 'TicketTemplateApplied' },
			{ en: 'Checklist updated: ', action: 'TicketChecklistUpdated' },
			{ en: 'Customer created: ', action: 'CustomerCreated' },
			{ en: 'Device created: ', action: 'DeviceCreated' },
			{ en: 'Shop user saved: ', action: 'ShopUserCreated' },
			{ en: 'Role created: ', action: 'RoleCreated' },
			{ en: 'Role updated: ', action: 'RoleUpdated' },
			{ en: 'Role deleted: ', action: 'RoleDeleted' },
			{ en: 'Stock added: ', action: 'InventoryStockIn' },
			{ en: 'Inventory adjusted: ', action: 'InventoryAdjusted' },
			{ en: 'WhatsApp message generated for ', action: 'WhatsAppMessageGenerated' }
		];

		for (const p of prefixes) {
			if (title.startsWith(p.en) && (!p.contains || title.includes(p.contains))) {
				const localizedAction = t('activity.action.' + p.action);
				const remainder = title.slice(p.en.length);
				return `${localizedAction}: ${remainder}`;
			}
		}

		if (title.startsWith('Reserved ')) {
			return title.replace('Reserved ', t('activity.action.InventoryReserved') + ' ');
		}
		if (title.startsWith('Released ')) {
			return title.replace('Released ', t('activity.action.InventoryReleased') + ' ');
		}
		if (title.startsWith('Consumed ')) {
			return title.replace('Consumed ', t('activity.action.InventoryConsumed') + ' ');
		}
		if (title.startsWith('Returned ')) {
			return title.replace('Returned ', t('activity.action.InventoryReturned') + ' ');
		}

		return title;
	}

	const todayDate = $derived(
		new Date().toLocaleDateString(data.locale === 'id' ? 'id-ID' : 'en-US', {
			weekday: 'long',
			month: 'short',
			day: 'numeric',
			year: 'numeric'
		})
	);
	const activities = $derived(data.activities ?? []);
	const summary = $derived(data.summary);
	const tickets = $derived(data.tickets ?? []);
	const closedStatusNames = $derived(
		new Set((data.statuses ?? []).filter((status) => status.isFinal || status.isCancelled).map((status) => status.name))
	);
	const activeTickets = $derived(tickets.filter((ticket) => !closedStatusNames.has(ticket.status)));
	const attentionTickets = $derived(
		activeTickets
			.filter(
				(ticket) =>
					['High', 'Urgent'].includes(ticket.priority) ||
					(features.showBillingAndPayments && !ticket.isInternalWorkOrder && ['Unpaid', 'Partial'].includes(ticket.paymentStatus)) ||
					ageDays(ticket.createdAt) > 3
			)
			.slice(0, 6)
	);

	const statusColorMap = $derived(
		new Map((data.statuses ?? []).map((s) => [s.name, s.color || '#555f6d']))
	);

	const statusRows = $derived(
		Object.entries(
			activeTickets.reduce<Record<string, number>>((rows, ticket) => {
				rows[ticket.status] = (rows[ticket.status] ?? 0) + 1;
				return rows;
			}, {})
		)
			.sort((a, b) => b[1] - a[1])
			.slice(0, 6)
	);

	const technicianRows = $derived(
		Object.entries(
			activeTickets.reduce<Record<string, number>>((rows, ticket) => {
				const name = ticket.assignedTechnician || t('tickets.detail.unassigned');
				rows[name] = (rows[name] ?? 0) + 1;
				return rows;
			}, {})
		)
			.sort((a, b) => b[1] - a[1])
			.slice(0, 6)
	);

	function ageDays(value: string) {
		return Math.max(0, Math.floor((Date.now() - new Date(value).getTime()) / 86_400_000));
	}

	function initials(name: string) {
		return name
			.split(' ')
			.map((part) => part[0])
			.join('')
			.slice(0, 2)
			.toUpperCase();
	}

	function activityIcon(actionType: string) {
		if (actionType.includes('Refund')) return 'currency_exchange';
		if (actionType.includes('Voided')) return 'cancel';
		if (actionType.includes('Payment') || actionType.includes('Billing')) return 'payments';
		if (actionType.includes('WorkItem')) return 'build';
		if (actionType.includes('Created')) return 'add';
		if (actionType.includes('Updated')) return 'edit';
		if (actionType.includes('Deleted')) return 'delete';
		return 'history';
	}

	// ponytail: animate progress bar from 0 → target on mount; CSS transition does the work
	function bar(node: HTMLElement, pct: number) {
		node.style.cssText += 'width:0%;transition:width 900ms cubic-bezier(0.4,0,0.2,1)';
		requestAnimationFrame(() => requestAnimationFrame(() => { node.style.width = pct + '%'; }));
	}

	const intakeCount = $derived(
		data.tickets.filter((t) => t.status.toLowerCase().includes('received') || t.status.toLowerCase().includes('intake')).length || summary.openTickets
	);
	const activeWorkbenchCount = $derived(
		data.tickets.filter((t) => t.status.toLowerCase().includes('progress') || t.status.toLowerCase().includes('diag') || t.status.toLowerCase().includes('repair')).length
	);
	const approvalCount = $derived(summary.waitingApproval);
	const readyPickupCount = $derived(
		data.tickets.filter((t) => t.status.toLowerCase().includes('ready') || t.status.toLowerCase().includes('pickup')).length
	);

	const operationsStages = $derived([
		{
			id: 'intake',
			label: t('dashboard.stageIntake'),
			subtitle: t('dashboard.stageIntakeSubtitle'),
			count: intakeCount,
			icon: 'desk',
			href: '/app/tickets',
			accent: 'bg-stone/60 text-ink'
		},
		{
			id: 'diagnosis',
			label: t('dashboard.stageDiagnosis'),
			subtitle: t('dashboard.stageDiagnosisSubtitle'),
			count: activeWorkbenchCount,
			icon: 'build',
			href: '/app/kanban',
			accent: 'bg-blue-50 text-blue-700 border-blue-200'
		},
		{
			id: 'approval',
			label: t('dashboard.stageApproval'),
			subtitle: t('dashboard.stageApprovalSubtitle'),
			count: approvalCount,
			icon: 'pending_actions',
			href: '/app/kanban',
			accent: 'bg-amber-50 text-amber-800 border-amber-200'
		},
		{
			id: 'pickup',
			label: t('dashboard.stagePickup'),
			subtitle: t('dashboard.stagePickupSubtitle'),
			count: readyPickupCount,
			icon: 'check_circle',
			href: '/app/tickets',
			accent: 'bg-emerald-50 text-emerald-800 border-emerald-200'
		}
	]);

	const metricCards = $derived([
		{
			id: 'open',
			label: t('dashboard.openTickets'),
			value: summary.openTickets,
			icon: 'confirmation_number',
			tone: 'text-primary',
			href: '/app/tickets'
		},
		{
			id: 'waitingApproval',
			label: t('dashboard.waitingApproval'),
			value: summary.waitingApproval,
			icon: 'pending_actions',
			tone: summary.waitingApproval > 0 ? 'text-amber-800' : 'text-primary',
			href: '/app/kanban'
		},
		...(features.showBillingAndPayments
			? [
					{
						id: 'unpaid',
						label: t('dashboard.unpaidTickets'),
						value: summary.unpaidTickets,
						icon: 'payments',
						tone: summary.unpaidTickets > 0 ? 'text-warning' : 'text-primary',
						href: '/app/tickets'
					}
				]
			: []),
		{
			id: 'blocked',
			label: t('dashboard.blocked'),
			value: summary.blockedTickets,
			icon: 'block',
			tone: summary.blockedTickets > 0 ? 'text-error' : 'text-primary',
			href: '/app/tickets'
		},
		{
			id: 'overdue',
			label: t('dashboard.overdue'),
			value: summary.overdueTickets,
			icon: 'schedule',
			tone: summary.overdueTickets > 0 ? 'text-error' : 'text-primary',
			href: '/app/tickets'
		},
		{
			id: 'completedToday',
			label: t('dashboard.todayCompleted'),
			value: summary.completedToday,
			icon: 'check_circle',
			tone: summary.completedToday > 0 ? 'text-success' : 'text-primary',
			href: '/app/reports'
		}
	]);
</script>

{#if isLoading}
	<div transition:fade={{ duration: 150 }} class="space-y-lg pb-xl">
		<!-- Header Skeleton -->
		<div class="flex flex-col gap-xs sm:gap-sm">
			<div class="w-24 h-3 bg-stone/30 animate-shimmer rounded mb-xs"></div>
			<div class="flex items-start justify-between gap-md">
				<div>
					<div class="w-48 h-7 bg-stone/40 animate-shimmer rounded mb-1"></div>
					<div class="w-64 h-4 bg-stone/20 animate-shimmer rounded"></div>
				</div>
				<div class="flex gap-sm">
					<div class="w-24 h-10 bg-stone/20 animate-shimmer rounded-lg"></div>
					<div class="w-32 h-10 bg-stone/30 animate-shimmer rounded-lg"></div>
				</div>
			</div>
		</div>

		<!-- Quick Actions Skeleton -->
		<div class="grid grid-cols-2 sm:grid-cols-4 gap-sm sm:gap-md">
			{#each Array(4) as _}
				<div class="rounded-xl border border-stone bg-white p-3.5 h-16 flex items-center gap-3">
					<div class="w-10 h-10 rounded-lg bg-stone/30 animate-shimmer shrink-0"></div>
					<div class="flex-1 space-y-1">
						<div class="w-20 h-4 bg-stone/30 animate-shimmer rounded"></div>
						<div class="w-14 h-3 bg-stone/20 animate-shimmer rounded"></div>
					</div>
				</div>
			{/each}
		</div>

		<!-- Stats Grid Skeleton (6 cards) -->
		<div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-sm sm:gap-md">
			{#each Array(6) as _}
				<div class="rounded-xl border border-stone bg-white p-4 h-24 flex flex-col justify-between">
					<div class="w-20 h-3 bg-stone/20 animate-shimmer rounded"></div>
					<div class="w-12 h-6 bg-stone/30 animate-shimmer rounded"></div>
				</div>
			{/each}
		</div>

		<!-- Main Split Skeleton -->
		<div class="grid grid-cols-12 gap-lg">
			<div class="col-span-12 lg:col-span-8 rounded-xl border border-stone bg-white p-lg h-96 flex flex-col justify-between">
				<div class="flex items-center justify-between mb-xl">
					<div class="w-36 h-5 bg-stone/30 animate-shimmer rounded"></div>
					<div class="w-16 h-4 bg-stone/20 animate-shimmer rounded"></div>
				</div>
				<div class="flex-1 space-y-md">
					{#each Array(5) as _}
						<div class="flex items-center justify-between py-2 border-b border-stone/50">
							<div class="w-20 h-4 bg-stone/30 animate-shimmer rounded"></div>
							<div class="w-28 h-4 bg-stone/20 animate-shimmer rounded"></div>
							<div class="w-20 h-4 bg-stone/20 animate-shimmer rounded"></div>
							<div class="w-12 h-4 bg-stone/30 animate-shimmer rounded"></div>
						</div>
					{/each}
				</div>
			</div>

			<div class="col-span-12 lg:col-span-4 rounded-xl border border-stone bg-white p-lg h-96 flex flex-col">
				<div class="mb-xl flex items-center justify-between">
					<div class="w-28 h-5 bg-stone/30 animate-shimmer rounded"></div>
					<div class="w-12 h-4 bg-stone/20 animate-shimmer rounded"></div>
				</div>
				<div class="space-y-lg flex-1">
					{#each Array(4) as _}
						<div class="flex gap-md">
							<div class="w-6 h-6 rounded-full bg-stone/30 animate-shimmer shrink-0"></div>
							<div class="flex-1 space-y-1.5">
								<div class="w-3/4 h-3.5 bg-stone/30 animate-shimmer rounded"></div>
								<div class="w-1/2 h-3 bg-stone/20 animate-shimmer rounded"></div>
							</div>
						</div>
					{/each}
				</div>
			</div>
		</div>
	</div>
{:else}
	<div transition:fade={{ duration: 250 }} class="space-y-lg pb-xl transition-all duration-300">
		<!-- Header -->
		<PageHeader
			items={[{ label: t('common.console') }, { label: t('nav.dashboard') }]}
			title={t('nav.dashboard')}
			subtitle={`${t('dashboard.overviewToday')} ${todayDate}.`}
		>
			{#snippet actions()}
				<a
					class="px-button-x py-button-y rounded-button border border-stone bg-white font-medium text-body-sm text-primary hover:bg-ash hover:border-slate transition-all flex items-center gap-xs shadow-2xs"
					href="/app/reports"
				>
					<span class="material-symbols-outlined text-[18px]">monitoring</span>
					<span>{t('nav.reports')}</span>
				</a>
				<a
					class="px-button-x py-button-y rounded-button bg-ink text-white font-semibold text-body-sm hover:opacity-90 active:scale-95 transition-all flex items-center gap-xs shadow-xs"
					href="/app/tickets/new"
				>
					<span class="material-symbols-outlined text-[18px]">add</span>
					<span>{t('nav.newTicket')}</span>
				</a>
			{/snippet}
		</PageHeader>

		<!-- Workbench Live Operations Pipeline -->
		<section aria-label={t('dashboard.operationsStrip')} class="grid grid-cols-2 lg:grid-cols-4 gap-sm sm:gap-md">
			{#each operationsStages as stage}
				<a
					href={stage.href}
					class="group flex flex-col justify-between p-4 rounded-xl border border-stone bg-white hover:border-ink hover:shadow-xs transition-all relative overflow-hidden"
				>
					<div class="flex items-start justify-between gap-2">
						<div class="space-y-0.5 min-w-0">
							<span class="font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block truncate">
								{stage.label}
							</span>
							<span class="text-[11px] text-secondary/80 block truncate">
								{stage.subtitle}
							</span>
						</div>
						<div class="w-8 h-8 rounded-lg {stage.accent} flex items-center justify-center shrink-0 border border-stone/60 group-hover:scale-105 transition-transform">
							<span class="material-symbols-outlined text-[18px]">{stage.icon}</span>
						</div>
					</div>
					<div class="mt-4 flex items-baseline justify-between pt-2 border-t border-stone/40">
						<span class="font-mono text-[24px] font-bold tracking-tight text-ink tabular-nums leading-none">
							{stage.count}
						</span>
						<span class="text-[11px] font-semibold text-secondary group-hover:text-ink flex items-center gap-0.5 transition-colors">
							<span>{t('dashboard.viewAll')}</span>
							<span class="material-symbols-outlined text-[14px] group-hover:translate-x-0.5 transition-transform">arrow_forward</span>
						</span>
					</div>
				</a>
			{/each}
		</section>

		<!-- Operational KPI Bento Grid -->
		<section class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-sm sm:gap-md" aria-label="Key Performance Indicators">
			{#each metricCards as card}
				<a
					data-bento
					href={card.href}
					class="group rounded-xl border border-stone bg-white p-3.5 shadow-2xs hover:border-ink hover:shadow-xs transition-all flex flex-col justify-between"
				>
					<div class="flex items-center justify-between gap-1.5">
						<span class="font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider truncate">{card.label}</span>
						<span class="material-symbols-outlined text-[16px] text-secondary group-hover:text-ink transition-colors">{card.icon}</span>
					</div>
					<div class="mt-3 flex items-baseline justify-between">
						<span class="font-mono text-[22px] font-bold tracking-tight tabular-nums {card.tone}">{card.value}</span>
						<span class="material-symbols-outlined text-[14px] text-secondary opacity-0 group-hover:opacity-100 group-hover:translate-x-0.5 transition-all">arrow_forward</span>
					</div>
				</a>
			{/each}
		</section>

		<!-- Main Split Layout -->
		<div class="grid grid-cols-12 gap-lg">
			<!-- Workbench Attention Queue (8 cols) -->
			<section data-bento class="col-span-12 lg:col-span-8 rounded-xl border border-stone bg-white shadow-sm flex flex-col relative">
				<div class="flex flex-wrap items-center justify-between gap-sm border-b border-stone px-card-padding py-3.5 bg-white">
					<div class="flex items-center gap-2">
						<span class="material-symbols-outlined text-secondary text-[20px]">notification_important</span>
						<h3 class="m-0 font-subheading text-body font-semibold text-primary leading-tight">{t('dashboard.attentionRequired')}</h3>
						<span class="rounded-full bg-error-container px-2 py-0.5 text-[10px] font-bold uppercase text-on-error-container tracking-wider">
							{attentionTickets.length} {t('dashboard.liveTickets')}
						</span>
					</div>
					<a class="text-body-sm font-semibold text-action-blue hover:underline flex items-center gap-1" href="/app/tickets">
						<span>{t('dashboard.viewAll')}</span>
						<span class="material-symbols-outlined text-[16px]">arrow_forward</span>
					</a>
				</div>
				<div class="overflow-x-auto">
					<table class="w-full min-w-[640px] border-collapse text-left">
						<thead class="bg-ash">
							<tr>
								<th class="px-card-padding py-3 font-label-caps text-label-caps text-secondary uppercase tracking-wider">{t('dashboard.ticketId')} / {terms.assetSingular}</th>
								<th class="px-card-padding py-3 font-label-caps text-label-caps text-secondary uppercase tracking-wider">{terms.requesterSingular}</th>
								<th class="px-card-padding py-3 font-label-caps text-label-caps text-secondary uppercase tracking-wider">{t('dashboard.status')}</th>
								<th class="px-card-padding py-3 font-label-caps text-label-caps text-secondary uppercase tracking-wider hidden sm:table-cell">{t('kanban.technician')}</th>
								<th class="px-card-padding py-3 text-right font-label-caps text-label-caps text-secondary uppercase tracking-wider">{t('dashboard.age')}</th>
								<th class="px-card-padding py-3 text-right font-label-caps text-label-caps text-secondary uppercase tracking-wider"><span class="sr-only">{t('tickets.list.actions')}</span></th>
							</tr>
						</thead>
						<tbody class="divide-y divide-stone text-body-sm">
							{#each attentionTickets as ticket}
								<tr class="hover:bg-ash/40 transition-colors group">
									<td class="px-card-padding py-3.5">
										<a class="font-mono text-body-sm font-bold text-primary group-hover:text-action-blue hover:underline inline-block" href="/app/tickets/{ticket.id}">
											{ticket.ticketNumber}
										</a>
										<div class="text-[12px] text-secondary font-medium flex items-center gap-1 mt-0.5">
											<span class="material-symbols-outlined text-[14px]">devices</span>
											<span class="truncate max-w-[200px]">{ticket.device || t('dashboard.device')}</span>
										</div>
									</td>
									<td class="px-card-padding py-3.5">
										<span class="font-medium text-primary block truncate max-w-[160px]">{ticket.customerName}</span>
										<span class="text-[12px] text-secondary block truncate mt-0.5">{ticket.customerPhone || '—'}</span>
									</td>
									<td class="px-card-padding py-3.5">
										<div class="flex flex-wrap items-center gap-1.5">
											<GlowingBadge status={ticket.status}>{ticket.status}</GlowingBadge>
											<span class="px-1.5 py-0.5 rounded text-[10px] font-bold uppercase tracking-wider {ticket.priority === 'Urgent' ? 'bg-red-500/10 text-red-700 border border-red-200' : ticket.priority === 'High' ? 'bg-amber-500/10 text-amber-800 border border-amber-200' : 'bg-ash text-secondary border border-stone'}">{t(`tickets.priority.${ticket.priority}`)}</span>
											{#if features.showBillingAndPayments && !ticket.isInternalWorkOrder && (ticket.paymentStatus === 'Unpaid' || ticket.paymentStatus === 'Partial')}
												<GlowingBadge status={ticket.paymentStatus}>{ticket.paymentStatus}</GlowingBadge>
											{/if}
										</div>
									</td>
									<td class="px-card-padding py-3.5 text-secondary hidden sm:table-cell">
										<span class="truncate max-w-[120px] block">{ticket.assignedTechnician || t('tickets.detail.unassigned')}</span>
									</td>
									<td class="px-card-padding py-3.5 text-right whitespace-nowrap">
										<span class="font-semibold text-primary">{ageDays(ticket.createdAt)} {t('dashboard.days')}</span>
										{#if ageDays(ticket.createdAt) > 3}
											<span class="text-[10px] font-bold text-error uppercase tracking-wider block mt-0.5">{t('dashboard.overdue')}</span>
										{/if}
									</td>
									<td class="px-card-padding py-3.5 text-right whitespace-nowrap">
										<a
											href="/app/tickets/{ticket.id}"
											class="inline-flex h-8 items-center gap-1.5 rounded-button border border-stone bg-white px-3 text-xs font-semibold text-ink shadow-2xs transition-all hover:border-slate hover:bg-ash group-hover:border-slate"
										>
											<span>{t('common.open')}</span>
											<span class="material-symbols-outlined text-[14px] text-secondary transition-transform group-hover:translate-x-0.5 group-hover:text-ink">arrow_forward</span>
										</a>
									</td>
								</tr>
							{:else}
								<tr>
									<td class="px-card-padding py-8 text-center font-body-sm text-secondary" colspan="6">
										<div class="flex flex-col items-center justify-center gap-1.5">
											<span class="material-symbols-outlined text-[28px] text-secondary">check_circle</span>
											<span>{t('dashboard.noAttention')}</span>
										</div>
									</td>
								</tr>
							{/each}
						</tbody>
					</table>
				</div>
			</section>

			<!-- Workshop Live Activity Stream (4 cols) -->
			<section data-bento class="col-span-12 lg:col-span-4 rounded-xl border border-stone bg-white p-card-padding shadow-sm flex flex-col">
				<div class="flex items-center justify-between border-b border-stone pb-3 mb-4">
					<div class="flex items-center gap-2">
						<span class="material-symbols-outlined text-secondary text-[20px]">history</span>
						<h3 class="m-0 font-subheading text-body font-semibold text-primary leading-tight">{t('dashboard.recentActivity')}</h3>
					</div>
					<a href="/app/activity" class="text-body-sm font-semibold text-action-blue hover:underline">
						{t('app.viewAllActivity')}
					</a>
				</div>
				<div class="relative flex-1 space-y-4 before:absolute before:bottom-2 before:left-[11px] before:top-2 before:w-[1px] before:bg-stone">
					{#if activities.length === 0}
						<p class="font-body-sm text-secondary py-4 text-center">{t('dashboard.noActivity')}</p>
					{:else}
						{#each activities as activity}
							{@const href = activityEntityHref(activity.entityType, activity.entityId)}
							<div class="relative pl-8">
								<div class="absolute left-0 top-0.5 flex h-6 w-6 items-center justify-center rounded-full bg-white border border-stone text-ink shadow-2xs z-10">
									<span class="material-symbols-outlined text-[13px]">{activityIcon(activity.actionType)}</span>
								</div>
								<div class="flex flex-col min-w-0">
									<span class="text-[10px] font-bold uppercase tracking-wider text-secondary leading-tight">{formatActionType(activity.actionType)}</span>
									{#if href}
										<a class="font-body-sm text-primary font-semibold hover:text-action-blue hover:underline truncate mt-0.5" {href}>{formatTitle(activity.title)}</a>
									{:else}
										<span class="font-body-sm text-primary font-semibold truncate mt-0.5">{formatTitle(activity.title)}</span>
									{/if}
									{#if activity.description}
										<p class="m-0 text-[12px] text-secondary line-clamp-1 mt-0.5">{formatDescription(activity.description)}</p>
									{/if}
									<span class="text-[11px] text-metal mt-1">{formatDate(activity.createdAt, data.locale, { dateStyle: 'medium', timeStyle: 'short' })}</span>
								</div>
							</div>
						{/each}
					{/if}
				</div>
			</section>
		</div>

		<!-- Lower Operational Status & Workload Grid -->
		<div class="grid grid-cols-12 gap-lg">
			<!-- Repair Pipeline (Status Distribution) (6 cols) -->
			<section data-bento class="col-span-12 lg:col-span-6 rounded-xl border border-stone bg-white p-card-padding shadow-sm flex flex-col">
				<div class="flex items-center justify-between border-b border-stone pb-3 mb-4">
					<div class="flex items-center gap-2">
						<span class="material-symbols-outlined text-secondary text-[20px]">view_column</span>
						<h3 class="m-0 font-subheading text-body font-semibold text-primary leading-tight">{t('dashboard.statusDistribution')}</h3>
					</div>
					<a class="text-body-sm font-semibold text-action-blue hover:underline flex items-center gap-1" href="/app/kanban">
						<span>{t('nav.kanban')}</span>
						<span class="material-symbols-outlined text-[16px]">arrow_forward</span>
					</a>
				</div>
				<div class="space-y-3.5 flex-1 justify-center flex flex-col">
					{#each statusRows as [status, count]}
						{@const pct = Math.max(4, Math.round((count / Math.max(1, activeTickets.length)) * 100))}
						<div class="space-y-1">
							<div class="flex items-center justify-between text-body-sm">
								<div class="flex items-center gap-2 min-w-0">
									<span class="w-2.5 h-2.5 rounded-full shrink-0" style="background-color: {statusColorMap.get(status) || '#555f6d'};"></span>
									<span class="font-medium text-primary truncate">{status}</span>
								</div>
								<div class="flex items-center gap-2 text-secondary shrink-0 font-mono text-[13px]">
									<span class="font-bold text-primary">{count}</span>
									<span class="text-metal">({pct}%)</span>
								</div>
							</div>
							<div class="h-2 w-full overflow-hidden rounded-full bg-ash">
								<div class="h-full rounded-full transition-all duration-700" style="background-color: {statusColorMap.get(status) || '#1c1815'}; width: {pct}%;"></div>
							</div>
						</div>
					{:else}
						<p class="font-body-sm text-secondary py-4 text-center">{t('dashboard.noActiveTickets')}</p>
					{/each}
				</div>
			</section>

			<!-- Technician Workbench Load (6 cols) -->
			<section data-bento class="col-span-12 lg:col-span-6 rounded-xl border border-stone bg-white p-card-padding shadow-sm flex flex-col">
				<div class="flex items-center justify-between border-b border-stone pb-3 mb-4">
					<div class="flex items-center gap-2">
						<span class="material-symbols-outlined text-secondary text-[20px]">engineering</span>
						<h3 class="m-0 font-subheading text-body font-semibold text-primary leading-tight">{t('dashboard.technicianWorkload')}</h3>
					</div>
					<span class="text-[12px] font-semibold text-secondary">{technicianRows.length} {t('common.active')}</span>
				</div>
				<div class="space-y-3 flex-1">
					{#each technicianRows as [name, count]}
						<div class="flex items-center gap-3 rounded-lg border border-stone bg-surface-container-low/40 p-2.5 hover:bg-ash/50 transition-colors">
							<div class="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-white border border-stone font-bold text-xs text-primary shadow-2xs">
								{initials(name)}
							</div>
							<div class="flex-1 min-w-0">
								<div class="flex items-baseline justify-between mb-1">
									<p class="m-0 font-body-sm font-bold text-primary truncate leading-none">{name}</p>
									<p class="m-0 text-[12px] text-secondary font-bold shrink-0">{count} {t('dashboard.jobs')}</p>
								</div>
								<div class="h-1.5 w-full overflow-hidden rounded-full bg-stone/60">
									<div class="h-full bg-primary rounded-full" use:bar={Math.min(100, count * 20)}></div>
								</div>
							</div>
						</div>
					{:else}
						<p class="font-body-sm text-secondary py-4 text-center">{t('dashboard.noActiveWorkload')}</p>
					{/each}
				</div>
			</section>
		</div>
	</div>
{/if}
