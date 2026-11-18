<script lang="ts">
	import { enhance } from '$app/forms';
	import PageHeader from '$lib/components/PageHeader.svelte';
	import DeleteConfirmButton from '$lib/components/DeleteConfirmButton.svelte';
	import Dropdown from '$lib/components/ui/dropdown/Dropdown.svelte';
	import { createTranslator } from '$lib/i18n';
	import type { NotificationTemplate } from '$lib/types/notification-templates';
	import type { ActionData, PageData } from './$types';

	let { data, form }: { data: PageData; form: ActionData } = $props();
	const t = $derived(createTranslator(data.locale));
	const templates = $derived(data.templates.items ?? []);
	const pageSizeOptions = [10, 20, 50, 100];

	const channels = ['WhatsApp', 'Email', 'Sms', 'InApp'];
	const locales = ['', 'en', 'id'];
	const placeholders = [
		'customer_name',
		'ticket_number',
		'device',
		'status',
		'estimated_cost',
		'final_cost',
		'amount_paid',
		'remaining_balance',
		'payment_status',
		'shop_name',
		'shop_phone',
		'ticket_url',
		'approval_status'
	];

	let copiedPlaceholder = $state<string | null>(null);

	function copyPlaceholder(token: string) {
		const snippet = `{{${token}}}`;
		if (navigator.clipboard) {
			navigator.clipboard.writeText(snippet);
			copiedPlaceholder = token;
			setTimeout(() => {
				if (copiedPlaceholder === token) copiedPlaceholder = null;
			}, 1800);
		}
	}

	let showModal = $state(false);
	let modalId = $state(0);
	let modalKey = $state('');
	let modalChannel = $state('WhatsApp');
	let modalLocale = $state('en');
	let modalName = $state('');
	let modalBody = $state('');
	let modalIsActive = $state(true);

	function openModal(template: NotificationTemplate | null = null) {
		if (template) {
			modalId = template.id;
			modalKey = template.key;
			modalChannel = template.channel;
			modalLocale = template.locale;
			modalName = template.name;
			modalBody = template.body;
			modalIsActive = template.isActive;
		} else {
			modalId = 0;
			modalKey = '';
			modalChannel = data.filters.channel ?? 'WhatsApp';
			modalLocale = data.locale === 'id' ? 'id' : 'en';
			modalName = '';
			modalBody = '';
			modalIsActive = true;
		}
		showModal = true;
	}

	function pageHref(page: number) {
		const params = new URLSearchParams();
		params.set('channel', data.filters.channel);
		params.set('sort', data.filters.sort);
		params.set('page', String(page));
		params.set('pageSize', String(data.templates.pageSize));
		if (data.filters.locale) params.set('locale', data.filters.locale);
		return `?${params.toString()}`;
	}

	const hasActiveFilters = $derived(
		Boolean(data.filters.channel !== 'WhatsApp' || data.filters.locale || data.filters.sort !== 'key-asc')
	);
</script>

<div class="space-y-lg pb-xl">
	<PageHeader
		items={[
			{ label: t('common.console'), href: '/app' },
			{ label: t('nav.settings'), href: '/app/settings' },
			{ label: t('settings.notificationTemplates') }
		]}
		title={t('settings.notificationTemplates')}
		subtitle={t('settings.notificationTemplatesSubtitle')}
	>
		{#snippet actions()}
			<button
				type="button"
				onclick={() => openModal()}
				class="h-10 px-4 rounded-button font-medium bg-primary text-white hover:opacity-90 transition-opacity cursor-pointer inline-flex items-center gap-1.5 text-body-sm shadow-2xs whitespace-nowrap"
			>
				<span class="material-symbols-outlined text-[18px]">add</span>
				<span>{t('settings.addTemplate')}</span>
			</button>
		{/snippet}
	</PageHeader>

	<!-- Standardized Bento Filters Card -->
	<form method="GET" data-bento class="bg-white border border-stone rounded-xl p-md shadow-2xs relative z-30 !overflow-visible">
		<input type="hidden" name="page" value="1" />
		<div class="flex flex-col sm:flex-row sm:items-end gap-md !overflow-visible">
			<div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-md flex-1 !overflow-visible">
				<div class="space-y-xs block">
					<span class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block">{t('settings.channel')}</span>
					<Dropdown name="channel" value={data.filters.channel ?? channels[0]} options={channels.map(c => ({ value: c, label: c }))} />
				</div>
				<div class="space-y-xs block">
					<span class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block">{t('settings.templateLocale')}</span>
					<Dropdown name="locale" value={data.filters.locale ?? ''} options={locales.map(l => ({ value: l, label: l ? l.toUpperCase() : t('settings.allLocales') }))} />
				</div>
				<div class="space-y-xs block">
					<span class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block">{t('settings.sort')}</span>
					<Dropdown
						name="sort"
						value={data.filters.sort}
						options={[
							{ value: 'key-asc', label: t('settings.sort.keyAsc') },
							{ value: 'key-desc', label: t('settings.sort.keyDesc') },
							{ value: 'locale-asc', label: t('settings.sort.localeAsc') },
							{ value: 'channel-asc', label: t('settings.sort.channelAsc') }
						]}
					/>
				</div>
				<div class="space-y-xs block">
					<span class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block">{t('settings.usersPageSize')}</span>
					<Dropdown
						name="pageSize"
						value={String(data.templates.pageSize)}
						options={pageSizeOptions.map(size => ({ value: String(size), label: String(size) }))}
						align="right"
					/>
				</div>
			</div>
			<div class="flex items-center gap-sm shrink-0">
				{#if hasActiveFilters}
					<a
						href="/app/settings/notification-templates"
						class="h-10 px-4 rounded-button font-medium border border-stone bg-white text-secondary hover:text-primary hover:bg-ash transition-colors inline-flex items-center justify-center text-body-sm shadow-2xs"
					>
						{t('common.reset')}
					</a>
				{/if}
				<button
					class="h-10 px-5 rounded-button font-medium bg-primary text-white hover:opacity-90 transition-opacity cursor-pointer inline-flex items-center justify-center text-body-sm shadow-2xs whitespace-nowrap"
					type="submit"
				>
					{t('kanban.applyFilters')}
				</button>
			</div>
		</div>
	</form>

	<!-- Placeholders Copyable Chips Bar -->
	<section data-bento class="bg-white border border-stone rounded-xl p-md shadow-2xs space-y-sm">
		<div class="flex items-center justify-between">
			<span class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block">{t('settings.placeholders')}</span>
			<span class="text-caption text-secondary">Click token to copy snippet</span>
		</div>
		<div class="flex flex-wrap gap-1.5">
			{#each placeholders as placeholder}
				<button
					type="button"
					onclick={() => copyPlaceholder(placeholder)}
					class="px-2.5 py-1 rounded-md text-caption font-mono transition-all border cursor-pointer inline-flex items-center gap-1 {copiedPlaceholder === placeholder ? 'bg-emerald-50 border-emerald-300 text-emerald-700 shadow-2xs' : 'bg-ash/50 border-stone/60 text-primary hover:bg-ash hover:border-stone'}"
					title={t('settings.copyPlaceholder')}
				>
					{#if copiedPlaceholder === placeholder}
						<span class="material-symbols-outlined text-[13px] text-emerald-600">check</span>
						<span>{t('settings.copied')}</span>
					{:else}
						<span>{'{'}{'{'}{placeholder}{'}'}{'}'}</span>
					{/if}
				</button>
			{/each}
		</div>
	</section>

	<!-- Template Cards Grid -->
	{#if templates.length === 0}
		<div class="rounded-xl border border-stone bg-white p-xl text-center shadow-2xs">
			<div class="w-12 h-12 mx-auto rounded-full bg-ash flex items-center justify-center text-secondary mb-3">
				<span class="material-symbols-outlined text-[24px]">chat_bubble</span>
			</div>
			<h4 class="font-subheading text-body font-semibold text-primary">{t('settings.noTemplates')}</h4>
			<p class="text-body-sm text-secondary mt-1 max-w-md mx-auto">Create automated customer notifications for intake confirmation, diagnostic estimates, ready for pickup, or ticket status updates.</p>
		</div>
	{:else}
		<div class="grid grid-cols-1 md:grid-cols-2 gap-md">
			{#each templates as template}
				<div data-bento class="rounded-xl border border-stone bg-white p-md sm:p-lg shadow-2xs flex flex-col justify-between hover:border-ink/20 transition-all space-y-md">
					<div class="space-y-sm">
						<!-- Card Top Line -->
						<div class="flex flex-wrap items-center justify-between gap-sm">
							<span class="font-mono text-caption font-semibold px-2 py-0.5 rounded bg-ash border border-stone/60 text-primary">
								{template.key}
							</span>
							<div class="flex items-center gap-1.5">
								<span class="px-2 py-0.5 rounded-full text-[11px] font-medium border {template.channel === 'WhatsApp' ? 'border-emerald-200 bg-emerald-50 text-emerald-700' : 'border-blue-200 bg-blue-50 text-blue-700'}">
									{template.channel}
								</span>
								<span class="px-2 py-0.5 rounded-full text-[11px] font-medium border border-stone bg-ash text-secondary font-mono">
									{template.locale.toUpperCase()}
								</span>
								{#if template.isActive}
									<span class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[11px] font-medium border border-emerald-200 bg-emerald-50 text-emerald-700">
										<span class="w-1.5 h-1.5 rounded-full bg-emerald-500"></span>
										{t('settings.active')}
									</span>
								{:else}
									<span class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[11px] font-medium border border-stone bg-ash text-secondary">
										<span class="w-1.5 h-1.5 rounded-full bg-secondary"></span>
										Inactive
									</span>
								{/if}
							</div>
						</div>

						<h3 class="font-subheading text-body font-semibold text-primary">
							{template.name}
						</h3>

						<!-- Message Body Preview -->
						<div class="rounded-lg border border-stone/70 bg-ash/30 p-sm text-body-sm font-mono text-secondary whitespace-pre-line max-h-40 overflow-y-auto pr-1">
							{template.body}
						</div>
					</div>

					<div class="flex items-center justify-end gap-sm border-t border-stone pt-sm">
						<button
							type="button"
							onclick={() => openModal(template)}
							class="px-2.5 py-1 rounded-button border border-stone bg-white text-primary hover:bg-ash text-caption font-medium cursor-pointer transition-colors shadow-2xs"
						>
							{t('customers.detail.edit')}
						</button>
						<form method="POST" action="?/delete" use:enhance>
							<input type="hidden" name="id" value={template.id} />
							<DeleteConfirmButton
								label={t('settings.deleteTemplate')}
								confirmTitle={t('common.deleteConfirmTitle')}
								confirmMessage={t('common.deleteConfirm')}
								cancelLabel={t('common.cancel')}
								class="px-2.5 py-1 rounded-button border border-rose-200 bg-rose-50 hover:bg-rose-100 text-rose-700 text-caption font-medium cursor-pointer transition-colors shadow-2xs"
							/>
						</form>
					</div>
				</div>
			{/each}
		</div>

		<!-- Pagination Footer -->
		<div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-sm rounded-xl border border-stone bg-white p-md shadow-2xs text-body-sm text-secondary">
			<span>{t('settings.templatesShowing')} <strong class="text-primary font-semibold">{templates.length}</strong> / {data.templates.totalItems}</span>
			<div class="flex items-center gap-xs">
				<a
					class="h-8 px-3 rounded-button border border-stone bg-white text-secondary hover:text-primary hover:bg-ash transition-colors inline-flex items-center justify-center text-caption font-medium shadow-2xs {data.templates.page <= 1 ? 'pointer-events-none opacity-40' : ''}"
					href={pageHref(Math.max(1, data.templates.page - 1))}
				>
					{t('common.previous')}
				</a>
				<span class="text-caption font-medium px-2">
					{data.templates.page} / {Math.max(1, data.templates.totalPages)}
				</span>
				<a
					class="h-8 px-3 rounded-button border border-stone bg-white text-secondary hover:text-primary hover:bg-ash transition-colors inline-flex items-center justify-center text-caption font-medium shadow-2xs {data.templates.page >= data.templates.totalPages ? 'pointer-events-none opacity-40' : ''}"
					href={pageHref(data.templates.page + 1)}
				>
					{t('common.next')}
				</a>
			</div>
		</div>
	{/if}
</div>

<!-- Notification Template Modal -->
{#if showModal}
	<div class="fixed inset-0 z-[60] flex items-center justify-center bg-black/40 backdrop-blur-xs px-md py-6" role="presentation">
		<div class="w-[min(640px,calc(100vw-2rem))] max-h-[calc(100vh-3rem)] overflow-y-auto rounded-xl border border-stone bg-white p-md sm:p-xl shadow-xl space-y-lg" role="dialog" aria-modal="true">
			<div class="flex items-start justify-between gap-md border-b border-stone pb-md">
				<div>
					<h3 class="font-subheading text-subheading font-semibold text-primary">
						{modalId > 0 ? t('settings.editTemplate') : t('settings.addTemplate')}
					</h3>
					<p class="text-caption text-secondary mt-0.5">
						Configure automated WhatsApp or email message copy with dynamic bracket tokens.
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
				action="?/save"
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
				<input type="hidden" name="provider" value="manual" />

				<div class="grid grid-cols-1 sm:grid-cols-3 gap-md">
					<div class="space-y-1 block sm:col-span-1">
						<label class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block" for="tpl-key">
							{t('settings.templateKey')}
						</label>
						<input
							id="tpl-key"
							name="key"
							bind:value={modalKey}
							required
							placeholder={t('settings.templateKeyPlaceholder')}
							class="w-full h-10 rounded-input border border-stone bg-white px-3 py-2 text-body-sm text-primary font-mono focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary shadow-2xs"
						/>
					</div>

					<div class="space-y-1 block sm:col-span-1">
						<span class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block">{t('settings.channel')}</span>
						<Dropdown name="channel" value={modalChannel} onchange={(v) => (modalChannel = v)} options={channels.map(c => ({ value: c, label: c }))} />
					</div>

					<div class="space-y-1 block sm:col-span-1">
						<span class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block">{t('settings.templateLocale')}</span>
						<Dropdown name="locale" value={modalLocale} onchange={(v) => (modalLocale = v)} options={[{ value: 'en', label: 'English (en)' }, { value: 'id', label: 'Indonesian (id)' }]} />
					</div>
				</div>

				<div class="space-y-1 block">
					<label class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block" for="tpl-name">
						{t('settings.templateName')}
					</label>
					<input
						id="tpl-name"
						name="name"
						bind:value={modalName}
						required
						placeholder="e.g., Ticket Created WhatsApp Notice"
						class="w-full h-10 rounded-input border border-stone bg-white px-3 py-2 text-body-sm text-primary focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary shadow-2xs"
					/>
				</div>

				<div class="space-y-1 block">
					<div class="flex items-center justify-between">
						<label class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block" for="tpl-body">
							{t('settings.templateBody')}
						</label>
						<span class="text-[11px] text-secondary">Supports dynamic tokens like {'{{customer_name}}'}</span>
					</div>
					<textarea
						id="tpl-body"
						name="body"
						bind:value={modalBody}
						rows="6"
						required
						placeholder={t('settings.templateBodyPlaceholder')}
						class="w-full rounded-input border border-stone bg-white px-3 py-2 text-body-sm text-primary font-mono focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary shadow-2xs"
					></textarea>
				</div>

				<label class="flex items-center gap-sm text-body-sm text-primary cursor-pointer select-none pt-1">
					<input
						type="checkbox"
						name="isActive"
						bind:checked={modalIsActive}
						class="rounded border-stone text-primary focus:ring-primary"
					/>
					<span class="font-medium">{t('settings.active')}</span>
				</label>

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
