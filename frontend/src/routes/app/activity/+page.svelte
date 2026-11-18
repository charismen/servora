<script lang="ts">
	import PageHeader from '$lib/components/PageHeader.svelte';
	import { createTranslator, formatDate } from '$lib/i18n';
	import { activityEntityHref } from '$lib/types/activity';
	import type { PageData } from './$types';
	import DatePicker from '$lib/components/ui/date-picker/DatePicker.svelte';
	import Dropdown from '$lib/components/ui/dropdown/Dropdown.svelte';

	let { data }: { data: PageData } = $props();
	const t = $derived(createTranslator(data.locale));
	const previousPage = $derived(Math.max(1, data.activity.page - 1));
	const nextPage = $derived(Math.min(Math.max(1, data.activity.totalPages), data.activity.page + 1));
	const pageSizeOptions = [10, 25, 50, 100];

	const hasActiveFilters = $derived(
		Boolean(
			data.filters.search ||
			data.filters.entityType ||
			data.filters.actionType ||
			data.filters.from ||
			data.filters.to
		)
	);

	const entityTypeOptions = $derived([
		{ value: '', label: t('activity.allEntities') },
		{ value: 'ServiceTicket', label: t('activity.entity.ServiceTicket') },
		{ value: 'Customer', label: t('activity.entity.Customer') },
		{ value: 'Device', label: t('activity.entity.Device') },
		{ value: 'InventoryItem', label: t('activity.entity.InventoryItem') },
		{ value: 'Attendance', label: t('activity.entity.Attendance') },
		{ value: 'FreelanceProfile', label: t('activity.entity.FreelanceProfile') },
		{ value: 'PayrollAdjustment', label: t('activity.entity.PayrollAdjustment') },
		{ value: 'ShopUser', label: t('activity.entity.ShopUser') },
		{ value: 'Role', label: t('activity.entity.Role') },
		{ value: 'Settings', label: t('activity.entity.Settings') }
	]);

	const actionTypeOptions = $derived([
		{ value: '', label: t('activity.allActions') },
		{ value: 'TicketCreated', label: t('activity.action.TicketCreated') },
		{ value: 'TicketUpdated', label: t('activity.action.TicketUpdated') },
		{ value: 'TicketStatusChanged', label: t('activity.action.TicketStatusChanged') },
		{ value: 'TicketAssigned', label: t('activity.action.TicketAssigned') },
		{ value: 'TicketNoteAdded', label: t('activity.action.TicketNoteAdded') },
		{ value: 'TicketApprovalRequested', label: t('activity.action.TicketApprovalRequested') },
		{ value: 'TicketApprovalResolved', label: t('activity.action.TicketApprovalResolved') },
		{ value: 'TicketWorkItemCreated', label: t('activity.action.TicketWorkItemCreated') },
		{ value: 'TicketWorkItemUpdated', label: t('activity.action.TicketWorkItemUpdated') },
		{ value: 'TicketWorkItemDeleted', label: t('activity.action.TicketWorkItemDeleted') },
		{ value: 'TicketPaymentRecorded', label: t('activity.action.TicketPaymentRecorded') },
		{ value: 'TicketRefundRecorded', label: t('activity.action.TicketRefundRecorded') },
		{ value: 'TicketPaymentVoided', label: t('activity.action.TicketPaymentVoided') },
		{ value: 'TicketBillingUpdated', label: t('activity.action.TicketBillingUpdated') },
		{ value: 'TicketPaymentUpdated', label: t('activity.action.TicketPaymentUpdated') },
		{ value: 'CustomerCreated', label: t('activity.action.CustomerCreated') },
		{ value: 'DeviceCreated', label: t('activity.action.DeviceCreated') },
		{ value: 'InventoryStockIn', label: t('activity.action.InventoryStockIn') },
		{ value: 'InventoryAdjusted', label: t('activity.action.InventoryAdjusted') },
		{ value: 'InventoryReserved', label: t('activity.action.InventoryReserved') },
		{ value: 'InventoryConsumed', label: t('activity.action.InventoryConsumed') }
	]);

	function pageHref(page: number) {
		const params = new URLSearchParams();
		params.set('page', String(page));
		params.set('pageSize', String(data.activity.pageSize));
		for (const key of ['search', 'entityType', 'actionType', 'from', 'to'] as const) {
			const value = data.filters[key];
			if (value) params.set(key, value);
		}
		return `?${params.toString()}`;
	}

	function formatEntity(type: string) {
		const key = 'activity.entity.' + type;
		const translated = t(key);
		if (translated !== key) return translated;
		return type.replace(/([A-Z])/g, ' $1').trim();
	}

	function formatAction(action: string) {
		const key = 'activity.action.' + action;
		const translated = t(key);
		if (translated !== key) return translated;
		return action.replace(/([A-Z])/g, ' $1').trim();
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

	function getEntityIcon(entityType: string, actionType: string): { icon: string; bg: string } {
		if (entityType === 'ServiceTicket') {
			if (actionType.includes('Refund')) return { icon: 'currency_exchange', bg: 'bg-amber-50 text-amber-700 border-amber-200' };
			if (actionType.includes('Voided')) return { icon: 'cancel', bg: 'bg-rose-50 text-rose-700 border-rose-200' };
			if (actionType.includes('Payment') || actionType.includes('Billing')) return { icon: 'payments', bg: 'bg-emerald-50 text-emerald-700 border-emerald-200' };
			if (actionType.includes('WorkItem')) return { icon: 'build', bg: 'bg-sky-50 text-sky-700 border-sky-200' };
			if (actionType.includes('Approval')) return { icon: 'verified', bg: 'bg-indigo-50 text-indigo-700 border-indigo-200' };
			if (actionType.includes('Deleted')) return { icon: 'delete', bg: 'bg-rose-50 text-rose-700 border-rose-200' };
			return { icon: 'confirmation_number', bg: 'bg-blue-50 text-blue-700 border-blue-200' };
		}
		if (entityType === 'Customer') return { icon: 'person', bg: 'bg-violet-50 text-violet-700 border-violet-200' };
		if (entityType === 'Device') return { icon: 'devices', bg: 'bg-cyan-50 text-cyan-700 border-cyan-200' };
		if (entityType === 'InventoryItem') return { icon: 'inventory_2', bg: 'bg-amber-50 text-amber-700 border-amber-200' };
		if (entityType === 'Attendance') return { icon: 'schedule', bg: 'bg-teal-50 text-teal-700 border-teal-200' };
		if (entityType === 'FreelanceProfile' || entityType === 'PayrollAdjustment') return { icon: 'badge', bg: 'bg-purple-50 text-purple-700 border-purple-200' };
		if (entityType === 'ShopUser' || entityType === 'Role') return { icon: 'shield_person', bg: 'bg-stone-100 text-stone-700 border-stone-300' };
		if (entityType === 'Settings') return { icon: 'settings', bg: 'bg-stone-100 text-stone-700 border-stone-300' };
		return { icon: 'history', bg: 'bg-ash text-secondary border-stone' };
	}
</script>

<div class="space-y-lg pb-xl">
	<PageHeader
		items={[{ label: t('common.console'), href: '/app' }, { label: t('nav.activity') }]}
		title={t('activity.title')}
		subtitle={t('activity.subtitle')}
	/>

	<!-- Standardized Bento Filter Toolbar -->
	<form
		data-bento
		class="bg-white border border-stone rounded-xl p-md shadow-2xs grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-[1fr_170px_190px_140px_140px_100px_auto] gap-md items-end relative z-30 !overflow-visible"
		method="GET"
	>
		<input type="hidden" name="page" value="1" />
		<label class="space-y-xs block">
			<span class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block">{t('activity.search')}</span>
			<div class="relative flex items-center">
				<span class="material-symbols-outlined absolute left-3 text-secondary text-[18px] pointer-events-none">search</span>
				<input
					name="search"
					value={data.filters.search}
					class="w-full h-10 rounded-input border border-stone bg-white pl-9 pr-3 text-body-sm text-primary placeholder:text-secondary/60 focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary shadow-2xs"
					placeholder={t('activity.searchPlaceholder')}
				/>
			</div>
		</label>
		<div class="space-y-xs block">
			<span class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block">{t('activity.entityType')}</span>
			<Dropdown
				name="entityType"
				value={data.filters.entityType}
				options={entityTypeOptions}
			/>
		</div>
		<div class="space-y-xs block">
			<span class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block">{t('activity.actionType')}</span>
			<Dropdown
				name="actionType"
				value={data.filters.actionType}
				options={actionTypeOptions}
			/>
		</div>
		<div class="space-y-xs block">
			<span class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block">{t('reports.from')}</span>
			<DatePicker name="from" value={data.filters.from} clearable />
		</div>
		<div class="space-y-xs block">
			<span class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block">{t('reports.to')}</span>
			<DatePicker name="to" value={data.filters.to} clearable />
		</div>
		<div class="space-y-xs block">
			<span class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block">{t('activity.pageSize')}</span>
			<Dropdown
				name="pageSize"
				value={String(data.activity.pageSize)}
				options={pageSizeOptions.map(size => ({ value: String(size), label: String(size) }))}
				align="right"
			/>
		</div>
		<div class="flex items-center gap-sm shrink-0">
			{#if hasActiveFilters}
				<a
					href="/app/activity"
					class="h-10 px-4 rounded-button font-medium border border-stone bg-white text-secondary hover:text-primary hover:bg-ash transition-colors inline-flex items-center justify-center text-body-sm shadow-2xs"
				>
					{t('common.reset')}
				</a>
			{/if}
			<button
				class="h-10 px-5 rounded-button font-medium bg-primary text-white hover:opacity-90 transition-opacity cursor-pointer inline-flex items-center justify-center text-body-sm shadow-2xs whitespace-nowrap"
				type="submit"
			>
				{t('reports.apply')}
			</button>
		</div>
	</form>

	<!-- Audit Timeline Feed -->
	<section data-bento class="bg-white border border-stone rounded-xl shadow-2xs overflow-hidden">
		{#if data.activity.items.length === 0}
			<div class="p-xl text-center">
				<div class="w-12 h-12 mx-auto rounded-full bg-ash flex items-center justify-center text-secondary mb-3">
					<span class="material-symbols-outlined text-[24px]">manage_search</span>
				</div>
				<h4 class="font-subheading text-body font-semibold text-primary">{t('activity.empty')}</h4>
				<p class="text-body-sm text-secondary mt-1 max-w-md mx-auto">{t('inventory.noItemsCopy')}</p>
			</div>
		{:else}
			<div class="divide-y divide-stone">
				{#each data.activity.items as item}
					{@const href = activityEntityHref(item.entityType, item.entityId)}
					{@const iconMeta = getEntityIcon(item.entityType, item.actionType)}
					<div class="p-md flex items-start gap-md hover:bg-ash/40 transition-colors">
						<!-- Event Category Icon Badge -->
						<div class="w-9 h-9 rounded-lg border flex items-center justify-center shrink-0 mt-0.5 {iconMeta.bg}">
							<span class="material-symbols-outlined text-[20px]">{iconMeta.icon}</span>
						</div>

						<!-- Event Content -->
						<div class="flex-1 min-w-0 space-y-1">
							<div class="flex flex-col sm:flex-row sm:items-baseline sm:justify-between gap-1">
								{#if href}
									<a class="font-semibold text-primary hover:underline text-body-sm break-words" {href}>
										{formatTitle(item.title)}
									</a>
								{:else}
									<p class="font-semibold text-primary text-body-sm break-words">
										{formatTitle(item.title)}
									</p>
								{/if}
								<span class="text-caption text-secondary font-mono whitespace-nowrap shrink-0">
									{formatDate(item.createdAt, data.locale, { dateStyle: 'medium', timeStyle: 'short' })}
								</span>
							</div>

							<div class="flex flex-wrap items-center gap-1.5 text-caption text-secondary">
								<span class="px-2 py-0.5 rounded-full bg-ash border border-stone text-[11px] font-medium text-secondary">
									{formatEntity(item.entityType)}
								</span>
								<span class="text-stone">·</span>
								<span class="font-medium text-primary">
									{formatAction(item.actionType)}
								</span>
								{#if item.description}
									<span class="text-stone">·</span>
									<span class="text-secondary truncate max-w-xl">
										{formatDescription(item.description)}
									</span>
								{/if}
							</div>
						</div>
					</div>
				{/each}
			</div>
		{/if}
	</section>

	<!-- Standardized Bottom Pagination -->
	<div class="flex items-center justify-between text-body-sm bg-white border border-stone rounded-xl p-md shadow-2xs">
		<a
			class="h-9 px-4 rounded-button border border-stone bg-white text-secondary hover:text-primary hover:bg-ash transition-colors inline-flex items-center justify-center text-caption font-medium shadow-2xs {data.activity.page <= 1 ? 'pointer-events-none opacity-40' : ''}"
			href={pageHref(previousPage)}
		>
			{t('activity.previous')}
		</a>
		<span class="text-caption font-medium text-secondary">
			{data.activity.page} / {Math.max(1, data.activity.totalPages)}
		</span>
		<a
			class="h-9 px-4 rounded-button border border-stone bg-white text-secondary hover:text-primary hover:bg-ash transition-colors inline-flex items-center justify-center text-caption font-medium shadow-2xs {data.activity.page >= data.activity.totalPages ? 'pointer-events-none opacity-40' : ''}"
			href={pageHref(nextPage)}
		>
			{t('activity.next')}
		</a>
	</div>
</div>
