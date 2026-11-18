<script lang="ts">
	import { enhance } from '$app/forms';
	import PageHeader from '$lib/components/PageHeader.svelte';
	import DeleteConfirmButton from '$lib/components/DeleteConfirmButton.svelte';
	import Dropdown from '$lib/components/ui/dropdown/Dropdown.svelte';
	import { createTranslator } from '$lib/i18n';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();
	const t = $derived(createTranslator(data.locale));
	const templates = $derived(data.templates.items ?? []);
	const pageSizeOptions = [10, 20, 50, 100];

	const hasActiveFilters = $derived(Boolean(data.filters.search));

	let showModal = $state(false);
	let modalId = $state(0);
	let modalName = $state('');
	let modalDescription = $state('');
	let modalChecklistLines = $state('');
	let modalIsActive = $state(true);
	let modalError = $state<string | null>(null);
	let savingTemplate = $state(false);
	let deactivatingId = $state<number | null>(null);

	const deleteConfirmRefs: Record<number, { close: () => void } | undefined> = {};

	type TemplateItem = PageData['templates']['items'][number];

	function checklistText(template: TemplateItem) {
		return template.checklistItems
			.toSorted((a, b) => a.sortOrder - b.sortOrder)
			.map((item) => item.title)
			.join('\n');
	}

	function openModal(template: TemplateItem | null = null) {
		modalError = null;
		if (template) {
			modalId = template.id;
			modalName = template.name;
			modalDescription = template.description ?? '';
			modalChecklistLines = checklistText(template);
			modalIsActive = template.isActive;
		} else {
			modalId = 0;
			modalName = '';
			modalDescription = '';
			modalChecklistLines = '';
			modalIsActive = true;
		}
		showModal = true;
	}

	function closeModal() {
		if (savingTemplate) return;
		showModal = false;
		modalError = null;
	}

	function pageHref(page: number) {
		const params = new URLSearchParams();
		params.set('page', String(page));
		params.set('pageSize', String(data.templates.pageSize));
		if (data.filters.search) params.set('search', data.filters.search);
		return `?${params.toString()}`;
	}
</script>

<svelte:window
	onkeydown={(event) => {
		if (event.key === 'Escape' && showModal && !savingTemplate) {
			closeModal();
		}
	}}
/>

<div class="space-y-lg pb-xl">
	<PageHeader
		items={[{ label: t('common.console'), href: '/app' }, { label: t('nav.settings'), href: '/app/settings' }, { label: t('settings.serviceTemplates') }]}
		title={t('settings.serviceTemplates')}
		subtitle={t('settings.serviceTemplatesSubtitle')}
	>
		{#snippet actions()}
			<button
				type="button"
				onclick={() => openModal()}
				class="h-10 px-4 rounded-button font-medium bg-primary text-white hover:opacity-90 transition-opacity cursor-pointer inline-flex items-center gap-1.5 text-body-sm shadow-2xs whitespace-nowrap"
			>
				<span class="material-symbols-outlined text-[18px]" aria-hidden="true">add</span>
				<span>{t('settings.serviceTemplates.new')}</span>
			</button>
		{/snippet}
	</PageHeader>

	<!-- Standardized Bento Filters Card -->
	<form method="GET" data-bento class="bg-white border border-stone rounded-xl p-md shadow-2xs relative z-30 !overflow-visible">
		<input type="hidden" name="page" value="1" />
		<div class="flex flex-col sm:flex-row sm:items-end gap-md !overflow-visible">
			<div class="flex-1 space-y-xs block">
				<label for="tpl-search" class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block">{t('settings.serviceTemplates.search')}</label>
				<div class="relative flex items-center">
					<span class="material-symbols-outlined absolute left-3 text-secondary text-[18px] pointer-events-none" aria-hidden="true">search</span>
					<input
						id="tpl-search"
						type="text"
						name="search"
						value={data.filters.search}
						class="w-full h-10 rounded-input border border-stone bg-white pl-9 pr-3 text-body-sm text-primary placeholder:text-secondary/60 focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary shadow-2xs"
						placeholder={t('settings.serviceTemplates.searchPlaceholder')}
					/>
				</div>
			</div>
			<div class="w-full sm:w-36 space-y-xs block">
				<span class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block">{t('settings.serviceTemplates.pageSize')}</span>
				<Dropdown
					name="pageSize"
					align="right"
					value={String(data.templates.pageSize)}
					options={pageSizeOptions.map(size => ({ value: String(size), label: String(size) }))}
				/>
			</div>
			<div class="flex items-center gap-sm shrink-0 w-full sm:w-auto">
				{#if hasActiveFilters}
					<a
						href="/app/settings/service-templates"
						class="h-10 px-4 rounded-button font-medium border border-stone bg-white text-secondary hover:text-primary hover:bg-ash transition-colors inline-flex items-center justify-center text-body-sm shadow-2xs flex-1 sm:flex-none"
					>
						{t('common.reset')}
					</a>
				{/if}
				<button
					class="h-10 px-5 rounded-button font-medium bg-primary text-white hover:opacity-90 transition-opacity cursor-pointer inline-flex items-center justify-center text-body-sm shadow-2xs whitespace-nowrap flex-1 sm:flex-none"
					type="submit"
				>
					{t('reports.apply')}
				</button>
			</div>
		</div>
	</form>

	<!-- Template Cards Grid -->
	{#if templates.length === 0}
		<div class="rounded-xl border border-stone bg-white p-xl text-center shadow-2xs">
			<div class="w-12 h-12 mx-auto rounded-full bg-ash flex items-center justify-center text-secondary mb-3">
				<span class="material-symbols-outlined text-[24px]" aria-hidden="true">checklist</span>
			</div>
			<h4 class="font-subheading text-body font-semibold text-primary">{t('settings.serviceTemplates.empty')}</h4>
			<p class="text-body-sm text-secondary mt-1 max-w-md mx-auto">{t('settings.serviceTemplates.emptyDescription')}</p>
		</div>
	{:else}
		<div class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-md">
			{#each templates as template (template.id)}
				<div data-bento class="rounded-xl border border-stone bg-white p-md sm:p-lg shadow-2xs flex flex-col justify-between hover:border-ink/20 transition-all space-y-md">
					<div class="space-y-sm">
						<div class="flex items-start justify-between gap-sm">
							<h3 class="font-subheading text-body font-semibold text-primary break-words">
								{template.name}
							</h3>
							{#if template.isActive}
								<span class="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-caption font-medium border border-emerald-200 bg-emerald-50 text-emerald-700 shrink-0">
									<span class="w-1.5 h-1.5 rounded-full bg-emerald-500"></span>
									{t('settings.serviceTemplates.active')}
								</span>
							{:else}
								<span class="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-caption font-medium border border-stone bg-ash text-secondary shrink-0">
									<span class="w-1.5 h-1.5 rounded-full bg-secondary"></span>
									{t('settings.serviceTemplates.inactive')}
								</span>
							{/if}
						</div>

						{#if template.description}
							<p class="text-body-sm text-secondary line-clamp-2">{template.description}</p>
						{/if}

						<!-- Checklist Preview -->
						<div class="rounded-lg border border-stone/70 bg-ash/30 p-sm space-y-xs">
							<div class="flex items-center justify-between text-caption text-secondary font-medium pb-1 border-b border-stone/50">
								<span>{t('settings.serviceTemplates.checklist')}</span>
								<span class="font-mono text-primary font-semibold">{template.checklistItems.length} {t('settings.serviceTemplates.items')}</span>
							</div>
							<ul class="space-y-1 text-body-sm text-primary max-h-36 overflow-y-auto pr-1">
								{#each template.checklistItems.toSorted((a, b) => a.sortOrder - b.sortOrder) as item, idx (item.id)}
									<li class="flex items-start gap-1.5 text-caption">
										<span class="text-secondary font-mono w-4 shrink-0 text-right">{idx + 1}.</span>
										<span class="truncate">{item.title}</span>
									</li>
								{/each}
							</ul>
						</div>
					</div>

					<div class="flex items-center justify-end gap-sm border-t border-stone pt-sm">
						<button
							type="button"
							onclick={() => openModal(template)}
							class="h-8 px-3 rounded-button border border-stone bg-white text-primary hover:bg-ash text-caption font-medium cursor-pointer transition-colors shadow-2xs inline-flex items-center justify-center"
						>
							{t('customers.detail.edit')}
						</button>
						{#if template.isActive}
							<form
								method="POST"
								action="?/delete"
								use:enhance={({ cancel }) => {
									if (deactivatingId !== null) {
										cancel();
										return;
									}
									const targetId = template.id;
									deactivatingId = targetId;
									return async ({ update, result }) => {
										if (result.type === 'success') {
											deleteConfirmRefs[targetId]?.close();
										}
										await update();
										if (result.type !== 'success') {
											deleteConfirmRefs[targetId]?.close();
										}
										deactivatingId = null;
									};
								}}
							>
								<input type="hidden" name="id" value={template.id} />
								<DeleteConfirmButton
									bind:this={deleteConfirmRefs[template.id]}
									label={t('settings.serviceTemplates.deactivate')}
									confirmTitle={t('settings.serviceTemplates.deactivateTitle')}
									confirmMessage={t('settings.serviceTemplates.deactivateMessage')}
									cancelLabel={t('common.cancel')}
									confirmIcon="block"
									variant="warning"
									submitting={deactivatingId === template.id}
									class="h-8 px-3 rounded-button border border-amber-200 bg-amber-50 hover:bg-amber-100 text-amber-800 text-caption font-medium cursor-pointer transition-colors shadow-2xs inline-flex items-center justify-center disabled:opacity-60 disabled:cursor-not-allowed"
								/>
							</form>
						{/if}
					</div>
				</div>
			{/each}
		</div>

		<!-- Pagination Footer -->
		<div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-sm rounded-xl border border-stone bg-white p-md shadow-2xs text-body-sm text-secondary">
			<span>{t('settings.serviceTemplates.showing')} <strong class="text-primary font-semibold">{templates.length}</strong> / {data.templates.totalCount}</span>
			<div class="flex items-center gap-xs">
				<a
					class="h-8 px-3 rounded-button border border-stone bg-white text-secondary hover:text-primary hover:bg-ash transition-colors inline-flex items-center justify-center text-caption font-medium shadow-2xs {data.templates.page <= 1 ? 'pointer-events-none opacity-40' : ''}"
					href={pageHref(Math.max(1, data.templates.page - 1))}
					aria-disabled={data.templates.page <= 1 ? 'true' : undefined}
					tabindex={data.templates.page <= 1 ? -1 : undefined}
				>
					{t('activity.previous')}
				</a>
				<span class="text-caption font-medium px-2">
					{data.templates.page} / {Math.max(1, Math.ceil(data.templates.totalCount / data.templates.pageSize))}
				</span>
				<a
					class="h-8 px-3 rounded-button border border-stone bg-white text-secondary hover:text-primary hover:bg-ash transition-colors inline-flex items-center justify-center text-caption font-medium shadow-2xs {data.templates.page * data.templates.pageSize >= data.templates.totalCount ? 'pointer-events-none opacity-40' : ''}"
					href={pageHref(data.templates.page + 1)}
					aria-disabled={data.templates.page * data.templates.pageSize >= data.templates.totalCount ? 'true' : undefined}
					tabindex={data.templates.page * data.templates.pageSize >= data.templates.totalCount ? -1 : undefined}
				>
					{t('activity.next')}
				</a>
			</div>
		</div>
	{/if}
</div>

<!-- Template Add / Edit Modal -->
{#if showModal}
	<!-- svelte-ignore a11y_click_events_have_key_events -->
	<!-- svelte-ignore a11y_no_noninteractive_element_interactions -->
	<div
		class="fixed inset-0 z-[60] flex items-center justify-center bg-black/40 backdrop-blur-xs px-md py-6"
		role="presentation"
		onclick={(event) => {
			if (event.target === event.currentTarget) {
				closeModal();
			}
		}}
	>
		<div
			class="w-[min(620px,calc(100vw-2rem))] max-h-[calc(100vh-3rem)] overflow-y-auto rounded-xl border border-stone bg-white p-md sm:p-xl shadow-xl space-y-lg"
			role="dialog"
			aria-modal="true"
			aria-labelledby="service-template-modal-title"
		>
			<div class="flex items-start justify-between gap-md border-b border-stone pb-md">
				<div>
					<h3 id="service-template-modal-title" class="font-subheading text-subheading font-semibold text-primary">
						{modalId > 0 ? t('settings.serviceTemplates.edit') : t('settings.serviceTemplates.new')}
					</h3>
					<p class="text-caption text-secondary mt-0.5">
						{t('settings.serviceTemplates.modalSubtitle')}
					</p>
				</div>
				<button
					type="button"
					onclick={closeModal}
					disabled={savingTemplate}
					aria-label={t('common.close')}
					class="p-1 rounded-md text-secondary hover:text-primary hover:bg-ash transition-colors cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed"
				>
					<span class="material-symbols-outlined text-[20px]" aria-hidden="true">close</span>
				</button>
			</div>

			<form
				method="POST"
				action="?/save"
				class="space-y-md"
				use:enhance={({ cancel }) => {
					if (savingTemplate) {
						cancel();
						return;
					}
					savingTemplate = true;
					modalError = null;
					return async ({ update, result }) => {
						if (result.type === 'failure') {
							const dataPayload = result.data as { message?: string } | undefined;
							modalError = dataPayload?.message || t('common.error');
						} else if (result.type === 'error') {
							modalError = t('common.error');
						}
						await update();
						savingTemplate = false;
						if (result.type === 'success') {
							showModal = false;
							modalError = null;
						}
					};
				}}
			>
				<input type="hidden" name="id" value={modalId} />

				{#if modalError}
					<div role="alert" class="rounded-lg border border-error/25 bg-error-container px-3.5 py-2.5 text-body-sm text-error flex items-start gap-2">
						<span class="material-symbols-outlined text-[18px] shrink-0 mt-0.5" aria-hidden="true">error</span>
						<span>{modalError}</span>
					</div>
				{/if}

				<div class="space-y-1 block">
					<label class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block" for="tpl-name">
						{t('settings.serviceTemplates.name')}
					</label>
					<input
						id="tpl-name"
						type="text"
						name="name"
						bind:value={modalName}
						required
						disabled={savingTemplate}
						placeholder={t('settings.serviceTemplates.namePlaceholder')}
						class="w-full h-10 rounded-input border border-stone bg-white px-3 py-2 text-body-sm text-primary focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary shadow-2xs"
					/>
				</div>

				<div class="space-y-1 block">
					<label class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block" for="tpl-desc">
						{t('settings.serviceTemplates.description')}
					</label>
					<input
						id="tpl-desc"
						type="text"
						name="description"
						bind:value={modalDescription}
						disabled={savingTemplate}
						placeholder={t('settings.serviceTemplates.descriptionPlaceholder')}
						class="w-full h-10 rounded-input border border-stone bg-white px-3 py-2 text-body-sm text-primary focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary shadow-2xs"
					/>
				</div>

				<div class="space-y-1 block">
					<div class="flex items-center justify-between gap-2">
						<label class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block" for="tpl-checklist">
							{t('settings.serviceTemplates.checklist')}
						</label>
						<span class="text-[11px] text-secondary">{t('settings.serviceTemplates.checklistHint')}</span>
					</div>
					<textarea
						id="tpl-checklist"
						name="checklistLines"
						bind:value={modalChecklistLines}
						rows="6"
						required
						disabled={savingTemplate}
						placeholder={t('settings.serviceTemplates.checklistPlaceholder')}
						class="w-full rounded-input border border-stone bg-white px-3 py-2 text-body-sm text-primary font-mono focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary shadow-2xs"
					></textarea>
				</div>

				<label class="flex items-center gap-sm text-body-sm text-primary cursor-pointer select-none pt-1">
					<input
						type="checkbox"
						name="isActive"
						bind:checked={modalIsActive}
						disabled={savingTemplate}
					/>
					<span class="font-medium">{t('settings.serviceTemplates.active')}</span>
				</label>

				<div class="flex justify-end gap-sm border-t border-stone pt-md">
					<button
						type="button"
						onclick={closeModal}
						disabled={savingTemplate}
						class="h-10 px-4 rounded-button border border-stone bg-white text-secondary hover:text-primary hover:bg-ash text-body-sm font-medium cursor-pointer shadow-2xs transition-colors inline-flex items-center justify-center disabled:opacity-60 disabled:cursor-not-allowed"
					>
						{t('common.cancel')}
					</button>
					<button
						type="submit"
						disabled={savingTemplate}
						class="h-10 px-5 rounded-button font-medium bg-primary text-white hover:opacity-90 cursor-pointer shadow-2xs text-body-sm transition-opacity inline-flex items-center justify-center gap-1.5 disabled:opacity-60 disabled:cursor-not-allowed"
					>
						{#if savingTemplate}
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
