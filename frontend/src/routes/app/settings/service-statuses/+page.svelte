<script lang="ts">
	import { enhance } from '$app/forms';
	import PageHeader from '$lib/components/PageHeader.svelte';
	import DeleteConfirmButton from '$lib/components/DeleteConfirmButton.svelte';
	import Dropdown from '$lib/components/ui/dropdown/Dropdown.svelte';
	import { createTranslator } from '$lib/i18n';
	import type { ServiceStatus } from '$lib/types/service-statuses';
	import type { ActionData, PageData } from './$types';

	let { data, form }: { data: PageData; form: ActionData } = $props();
	const t = $derived(createTranslator(data.locale));
	const colors = ['#64748b', '#2563eb', '#7c3aed', '#d97706', '#0d9488', '#059669', '#dc2626'];

	let showModal = $state(false);
	let modalId = $state(0);
	let modalName = $state('');
	let modalColor = $state('#64748b');
	let modalSortOrder = $state(1);
	let modalIsInitial = $state(false);
	let modalIsFinal = $state(false);
	let modalIsCancelled = $state(false);
	let modalIsBlocked = $state(false);

	function openModal(status: ServiceStatus | null = null) {
		if (status) {
			modalId = status.id;
			modalName = status.name;
			modalColor = status.color || colors[0];
			modalSortOrder = status.sortOrder;
			modalIsInitial = status.isInitial;
			modalIsFinal = status.isFinal;
			modalIsCancelled = status.isCancelled;
			modalIsBlocked = status.isBlocked;
		} else {
			modalId = 0;
			modalName = '';
			modalColor = colors[0];
			modalSortOrder = data.statuses.length + 1;
			modalIsInitial = false;
			modalIsFinal = false;
			modalIsCancelled = false;
			modalIsBlocked = false;
		}
		showModal = true;
	}
</script>

<div class="space-y-lg pb-xl">
	<PageHeader
		items={[{ label: t('common.console'), href: '/app' }, { label: t('nav.settings'), href: '/app/settings' }, { label: t('settings.serviceStatuses') }]}
		title={t('settings.serviceStatuses')}
		subtitle={t('settings.serviceStatusesSubtitle')}
	>
		{#snippet actions()}
			<button
				type="button"
				onclick={() => openModal()}
				class="h-10 px-4 rounded-button font-medium bg-primary text-white hover:opacity-90 transition-opacity cursor-pointer inline-flex items-center gap-1.5 text-body-sm shadow-2xs whitespace-nowrap"
			>
				<span class="material-symbols-outlined text-[18px]">add</span>
				<span>{t('settings.serviceStatuses.addStatus')}</span>
			</button>
		{/snippet}
	</PageHeader>

	<!-- Status List Table -->
	<section data-bento class="bg-white border border-stone rounded-xl shadow-2xs overflow-x-auto">
		<table class="w-full min-w-[700px] border-collapse text-left">
			<thead class="bg-ash/50 border-b border-stone">
				<tr>
					<th class="px-card-padding py-3 font-label-caps text-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('settings.serviceStatuses.name')}</th>
					<th class="px-card-padding py-3 font-label-caps text-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('settings.serviceStatuses.color')}</th>
					<th class="px-card-padding py-3 font-label-caps text-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('settings.serviceStatuses.order')}</th>
					<th class="px-card-padding py-3 font-label-caps text-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('settings.serviceStatuses.behavior')}</th>
					<th class="px-card-padding py-3 font-label-caps text-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider text-right">{t('settings.serviceStatuses.actions')}</th>
				</tr>
			</thead>
			<tbody class="divide-y divide-stone text-body-sm">
				{#each data.statuses as status}
					<tr class="hover:bg-ash/40 transition-colors">
						<td class="px-card-padding py-3">
							<div class="flex items-center gap-2.5">
								<span class="w-3 h-3 rounded-full shrink-0 shadow-2xs border border-black/10" style="background-color: {status.color};"></span>
								<span class="font-semibold text-primary">{status.name}</span>
							</div>
						</td>
						<td class="px-card-padding py-3">
							<span class="font-mono text-caption text-secondary px-2 py-0.5 rounded-md bg-ash border border-stone/60">
								{status.color}
							</span>
						</td>
						<td class="px-card-padding py-3">
							<span class="font-mono text-caption font-semibold text-secondary px-2 py-0.5 rounded-md bg-ash/70 border border-stone/50">
								#{status.sortOrder}
							</span>
						</td>
						<td class="px-card-padding py-3">
							<div class="flex flex-wrap items-center gap-1.5">
								{#if status.isInitial}
									<span class="inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-medium border border-blue-200 bg-blue-50 text-blue-700">
										{t('settings.serviceStatuses.initial')}
									</span>
								{/if}
								{#if status.isFinal}
									<span class="inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-medium border border-emerald-200 bg-emerald-50 text-emerald-700">
										{t('settings.serviceStatuses.final')}
									</span>
								{/if}
								{#if status.isCancelled}
									<span class="inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-medium border border-rose-200 bg-rose-50 text-rose-700">
										{t('settings.serviceStatuses.cancelled')}
									</span>
								{/if}
								{#if status.isBlocked}
									<span class="inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-medium border border-amber-200 bg-amber-50 text-amber-700">
										{t('settings.serviceStatuses.blocked')}
									</span>
								{/if}
								{#if !status.isInitial && !status.isFinal && !status.isCancelled && !status.isBlocked}
									<span class="text-caption text-secondary/60">Standard</span>
								{/if}
							</div>
						</td>
						<td class="px-card-padding py-3 text-right whitespace-nowrap">
							<div class="inline-flex items-center gap-xs">
								<button
									type="button"
									onclick={() => openModal(status)}
									class="px-2.5 py-1 rounded-button border border-stone bg-white text-primary hover:bg-ash text-caption font-medium cursor-pointer transition-colors shadow-2xs"
								>
									{t('customers.detail.edit')}
								</button>
								<form method="POST" action="?/delete" use:enhance>
									<input type="hidden" name="id" value={status.id} />
									<DeleteConfirmButton
										label={t('common.delete')}
										confirmTitle={t('common.deleteConfirmTitle')}
										confirmMessage={t('common.deleteConfirm')}
										cancelLabel={t('common.cancel')}
										class="px-2.5 py-1 rounded-button border border-rose-200 bg-rose-50 hover:bg-rose-100 text-rose-700 text-caption font-medium cursor-pointer transition-colors shadow-2xs"
									/>
								</form>
							</div>
						</td>
					</tr>
				{:else}
					<tr class="border-t border-stone">
						<td colspan="5" class="px-card-padding py-8 text-center text-body-sm text-secondary">
							<div class="flex flex-col items-center justify-center gap-1">
								<span class="material-symbols-outlined text-[24px] text-secondary/60">account_tree</span>
								<span class="text-caption font-medium text-secondary">No service statuses configured.</span>
							</div>
						</td>
					</tr>
				{/each}
			</tbody>
		</table>
	</section>
</div>

<!-- Add / Edit Status Modal -->
{#if showModal}
	<div class="fixed inset-0 z-[60] flex items-center justify-center bg-black/40 backdrop-blur-xs px-md py-6" role="presentation">
		<div class="w-[min(560px,calc(100vw-2rem))] max-h-[calc(100vh-3rem)] overflow-y-auto rounded-xl border border-stone bg-white p-md sm:p-xl shadow-xl space-y-lg" role="dialog" aria-modal="true">
			<div class="flex items-start justify-between gap-md border-b border-stone pb-md">
				<div>
					<h3 class="font-subheading text-subheading font-semibold text-primary">
						{modalId > 0 ? t('settings.serviceStatuses.editStatus') : t('settings.serviceStatuses.addStatus')}
					</h3>
					<p class="text-caption text-secondary mt-0.5">
						Configure lifecycle behavior, badge color, and display sequence.
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

				<div class="space-y-1 block">
					<label class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block" for="status-name">
						{t('settings.serviceStatuses.name')}
					</label>
					<input
						id="status-name"
						name="name"
						bind:value={modalName}
						required
						placeholder="e.g., In Diagnostics, Waiting on Customer"
						class="w-full h-10 rounded-input border border-stone bg-white px-3 py-2 text-body-sm text-primary focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary shadow-2xs"
					/>
				</div>

				<div class="grid grid-cols-1 sm:grid-cols-2 gap-md">
					<div class="space-y-1 block">
						<span class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block">
							{t('settings.serviceStatuses.color')}
						</span>
						<div class="flex items-center gap-2">
							<span class="w-4 h-4 rounded-full shrink-0 border border-stone shadow-2xs" style="background-color: {modalColor};"></span>
							<div class="flex-1">
								<Dropdown
									name="color"
									value={modalColor}
									onchange={(val) => (modalColor = val)}
									options={colors.map((color) => ({ value: color, label: color }))}
								/>
							</div>
						</div>
					</div>

					<div class="space-y-1 block">
						<label class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block" for="status-order">
							{t('settings.serviceStatuses.order')}
						</label>
						<input
							id="status-order"
							name="sortOrder"
							type="number"
							min="0"
							bind:value={modalSortOrder}
							class="w-full h-10 rounded-input border border-stone bg-white px-3 py-2 text-body-sm text-primary font-mono focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary shadow-2xs"
							required
						/>
					</div>
				</div>

				<!-- Behavior Flags Cards -->
				<div class="space-y-2 pt-1">
					<span class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block">
						{t('settings.serviceStatuses.behavior')}
					</span>
					<div class="grid grid-cols-1 sm:grid-cols-2 gap-sm">
						<label class="flex items-start gap-sm rounded-xl border border-stone bg-ash/30 p-sm text-body-sm text-secondary hover:bg-ash/60 transition-colors cursor-pointer select-none">
							<input type="checkbox" name="isInitial" bind:checked={modalIsInitial} class="mt-0.5 rounded border-stone text-primary focus:ring-primary" />
							<div>
								<span class="font-medium text-primary text-caption block">{t('settings.serviceStatuses.initial')}</span>
								<span class="text-[11px] text-secondary block">Assigned automatically to newly created tickets</span>
							</div>
						</label>

						<label class="flex items-start gap-sm rounded-xl border border-stone bg-ash/30 p-sm text-body-sm text-secondary hover:bg-ash/60 transition-colors cursor-pointer select-none">
							<input type="checkbox" name="isFinal" bind:checked={modalIsFinal} class="mt-0.5 rounded border-stone text-primary focus:ring-primary" />
							<div>
								<span class="font-medium text-primary text-caption block">{t('settings.serviceStatuses.final')}</span>
								<span class="text-[11px] text-secondary block">Marks work as delivered or completed</span>
							</div>
						</label>

						<label class="flex items-start gap-sm rounded-xl border border-stone bg-ash/30 p-sm text-body-sm text-secondary hover:bg-ash/60 transition-colors cursor-pointer select-none">
							<input type="checkbox" name="isCancelled" bind:checked={modalIsCancelled} class="mt-0.5 rounded border-stone text-primary focus:ring-primary" />
							<div>
								<span class="font-medium text-primary text-caption block">{t('settings.serviceStatuses.cancelled')}</span>
								<span class="text-[11px] text-secondary block">Designates customer or shop cancellation</span>
							</div>
						</label>

						<label class="flex items-start gap-sm rounded-xl border border-stone bg-ash/30 p-sm text-body-sm text-secondary hover:bg-ash/60 transition-colors cursor-pointer select-none">
							<input type="checkbox" name="isBlocked" bind:checked={modalIsBlocked} class="mt-0.5 rounded border-stone text-primary focus:ring-primary" />
							<div>
								<span class="font-medium text-primary text-caption block">{t('settings.serviceStatuses.blocked')}</span>
								<span class="text-[11px] text-secondary block">Indicates parts shortage or customer hold</span>
							</div>
						</label>
					</div>
				</div>

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
