<script lang="ts">
	import { enhance } from '$app/forms';
	import { untrack } from 'svelte';
	import PageHeader from '$lib/components/PageHeader.svelte';
	import { can, permissions } from '$lib/auth/permissions';
	import { createTranslator } from '$lib/i18n';
	import type { OperatingMode, TerminologyConfig } from '$lib/types/auth';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();
	const t = $derived(createTranslator(data.locale));
	const canWrite = $derived(can(data.user.permissions, permissions.settingsWrite));

	let selectedBlueprint = $state(untrack(() => data.config?.industryBlueprintKey ?? 'it_computer'));
	let operatingMode = $state<OperatingMode>(untrack(() => data.config?.operatingMode ?? 'Commercial'));
	let terminology = $state<TerminologyConfig>(
		untrack(() => structuredClone(data.config?.terminology ?? data.user.workspace.terminology))
	);
	let newFieldType = $state('Text');

	const allFields = $derived([
		...(data.config?.assetFields ?? []),
		...(data.config?.ticketFields ?? [])
	]);
</script>

<div class="space-y-6">
	<PageHeader
		items={[
			{ label: t('nav.dashboard'), href: '/app' },
			{ label: t('nav.settings'), href: '/app/settings' },
			{ label: t('nav.blueprints') }
		]}
		title={t('blueprints.title')}
		subtitle={t('blueprints.subtitle')}
	>
		{#snippet actions()}
			<a href="/app/onboarding" class="rounded-xl border border-stone bg-white px-3.5 py-2 text-xs font-semibold text-ink hover:bg-ash">
				{t('nav.onboarding')}
			</a>
		{/snippet}
	</PageHeader>

	<!-- Blueprint Selector -->
	<form method="POST" action="?/applyBlueprint" use:enhance class="space-y-4 rounded-2xl border border-stone bg-white p-5 shadow-xs">
		<div class="flex flex-wrap items-center justify-between gap-2">
			<h2 class="text-sm font-semibold text-ink">{t('blueprints.activeBlueprint')}: <span class="text-claytile">{data.config?.industryBlueprintKey ?? '—'}</span></h2>
		</div>
		<div class="grid gap-3 md:grid-cols-3">
			{#each data.blueprints as bp (bp.key)}
				<label class="flex cursor-pointer flex-col rounded-xl border p-3.5 transition {selectedBlueprint === bp.key ? 'border-claytile bg-claytile/5' : 'border-stone bg-surface hover:border-claytile/40'}">
					<div class="flex items-center gap-2">
						<input type="radio" name="blueprintKey" value={bp.key} bind:group={selectedBlueprint} class="text-claytile" />
						<span class="text-xs font-bold text-ink">{data.locale === 'id' ? bp.nameId : bp.nameEn}</span>
					</div>
					<p class="mt-1 text-[11px] text-slate-600">{data.locale === 'id' ? bp.descriptionId : bp.descriptionEn}</p>
				</label>
			{/each}
		</div>
		{#if canWrite}
			<div class="flex flex-wrap items-center justify-between gap-3 border-t border-stone/60 pt-3">
				<div class="flex flex-wrap gap-4 text-xs text-slate-700">
					<label class="flex items-center gap-1.5"><input type="checkbox" name="replaceStatuses" class="rounded border-stone text-claytile" /> {t('onboarding.replaceStatuses')}</label>
					<label class="flex items-center gap-1.5"><input type="checkbox" name="replaceFieldsAndChecklists" checked class="rounded border-stone text-claytile" /> {t('onboarding.replaceFields')}</label>
				</div>
				<button type="submit" class="rounded-lg bg-ink px-4 py-2 text-xs font-semibold text-surface hover:bg-claytile">
					{t('blueprints.applyBlueprint')}
				</button>
			</div>
		{/if}
	</form>

	<!-- Operating Mode & Terminology -->
	<form method="POST" action="?/saveTerminology" use:enhance class="space-y-4 rounded-2xl border border-stone bg-white p-5 shadow-xs">
		<input type="hidden" name="terminologyJson" value={JSON.stringify(terminology)} />
		<h2 class="text-sm font-semibold text-ink">{t('onboarding.step2')} & {t('onboarding.terminologyCustomize')}</h2>
		<div class="grid gap-3 sm:grid-cols-3">
			{#each ['Commercial', 'InternalMaintenance', 'Hybrid'] as m}
				<label class="flex cursor-pointer items-center gap-2 rounded-xl border p-3 text-xs font-semibold {operatingMode === m ? 'border-claytile bg-claytile/5 text-ink' : 'border-stone text-slate-600'}">
					<input type="radio" name="operatingMode" value={m} bind:group={operatingMode} class="text-claytile" />
					<span>{t(`workspaces.mode.${m}`)}</span>
				</label>
			{/each}
		</div>
		<div class="grid gap-4 md:grid-cols-2">
			<div class="space-y-2 rounded-xl border border-stone/70 bg-surface/50 p-3.5">
				<div class="text-xs font-semibold text-claytile">{t('onboarding.terminologyEn')}</div>
				<div class="grid grid-cols-2 gap-2">
					<input bind:value={terminology.en.assetSingular} placeholder={t('onboarding.termAssetSingular')} class="rounded-lg border border-stone bg-white px-2.5 py-1.5 text-xs" />
					<input bind:value={terminology.en.assetPlural} placeholder={t('onboarding.termAssetPlural')} class="rounded-lg border border-stone bg-white px-2.5 py-1.5 text-xs" />
					<input bind:value={terminology.en.ticketSingular} placeholder={t('onboarding.termTicketSingular')} class="rounded-lg border border-stone bg-white px-2.5 py-1.5 text-xs" />
					<input bind:value={terminology.en.ticketPlural} placeholder={t('onboarding.termTicketPlural')} class="rounded-lg border border-stone bg-white px-2.5 py-1.5 text-xs" />
					<input bind:value={terminology.en.requesterSingular} placeholder={t('onboarding.termRequesterSingular')} class="rounded-lg border border-stone bg-white px-2.5 py-1.5 text-xs" />
					<input bind:value={terminology.en.requesterPlural} placeholder={t('onboarding.termRequesterPlural')} class="rounded-lg border border-stone bg-white px-2.5 py-1.5 text-xs" />
					<input bind:value={terminology.en.primaryIdentifierLabel} placeholder={t('onboarding.termPrimaryId')} class="rounded-lg border border-stone bg-white px-2.5 py-1.5 text-xs" />
					<input bind:value={terminology.en.meterLabel} placeholder={t('onboarding.termMeterLabel')} class="rounded-lg border border-stone bg-white px-2.5 py-1.5 text-xs" />
				</div>
			</div>
			<div class="space-y-2 rounded-xl border border-stone/70 bg-surface/50 p-3.5">
				<div class="text-xs font-semibold text-claytile">{t('onboarding.terminologyId')}</div>
				<div class="grid grid-cols-2 gap-2">
					<input bind:value={terminology.id.assetSingular} placeholder={t('onboarding.termAssetSingular')} class="rounded-lg border border-stone bg-white px-2.5 py-1.5 text-xs" />
					<input bind:value={terminology.id.assetPlural} placeholder={t('onboarding.termAssetPlural')} class="rounded-lg border border-stone bg-white px-2.5 py-1.5 text-xs" />
					<input bind:value={terminology.id.ticketSingular} placeholder={t('onboarding.termTicketSingular')} class="rounded-lg border border-stone bg-white px-2.5 py-1.5 text-xs" />
					<input bind:value={terminology.id.ticketPlural} placeholder={t('onboarding.termTicketPlural')} class="rounded-lg border border-stone bg-white px-2.5 py-1.5 text-xs" />
					<input bind:value={terminology.id.requesterSingular} placeholder={t('onboarding.termRequesterSingular')} class="rounded-lg border border-stone bg-white px-2.5 py-1.5 text-xs" />
					<input bind:value={terminology.id.requesterPlural} placeholder={t('onboarding.termRequesterPlural')} class="rounded-lg border border-stone bg-white px-2.5 py-1.5 text-xs" />
					<input bind:value={terminology.id.primaryIdentifierLabel} placeholder={t('onboarding.termPrimaryId')} class="rounded-lg border border-stone bg-white px-2.5 py-1.5 text-xs" />
					<input bind:value={terminology.id.meterLabel} placeholder={t('onboarding.termMeterLabel')} class="rounded-lg border border-stone bg-white px-2.5 py-1.5 text-xs" />
				</div>
			</div>
		</div>
		{#if canWrite}
			<div class="flex justify-end">
				<button type="submit" class="rounded-lg bg-claytile px-4 py-2 text-xs font-semibold text-white">{t('common.save')}</button>
			</div>
		{/if}
	</form>

	<!-- Custom Fields Manager -->
	<div class="space-y-4 rounded-2xl border border-stone bg-white p-5 shadow-xs">
		<h2 class="text-sm font-semibold text-ink">{t('blueprints.customFieldsTitle')}</h2>
		{#if allFields.length === 0}
			<p class="text-xs text-slate-500">{t('blueprints.noCustomFields')}</p>
		{:else}
			<div class="divide-y divide-stone/60">
				{#each allFields as field (field.id)}
					<div class="flex items-center justify-between gap-3 py-2.5 text-xs">
						<div>
							<span class="font-semibold text-ink">{field.labelEn}</span>
							<span class="text-slate-400"> / {field.labelId}</span>
							<span class="ml-2 rounded bg-ash px-1.5 py-0.5 text-[10px] font-semibold text-slate-600">{field.scope}</span>
							<span class="ml-1 rounded bg-surface px-1.5 py-0.5 text-[10px] text-slate-500">{field.fieldType}</span>
							{#if field.isRequired}<span class="ml-1 text-[10px] font-semibold text-red-600">*</span>{/if}
						</div>
						{#if canWrite}
							<form method="POST" action="?/deleteField" use:enhance>
								<input type="hidden" name="id" value={field.id} />
								<button type="submit" class="text-xs font-semibold text-red-600 hover:underline">{t('common.delete')}</button>
							</form>
						{/if}
					</div>
				{/each}
			</div>
		{/if}

		{#if canWrite}
			<form method="POST" action="?/createField" use:enhance class="space-y-3 rounded-xl border border-stone/70 bg-surface p-4">
				<div class="text-xs font-semibold text-ink">{t('blueprints.addCustomField')}</div>
				<div class="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
					<select name="scope" class="rounded-lg border border-stone bg-white px-2.5 py-1.5 text-xs">
						<option value="Asset">{t('blueprints.scopeAsset')}</option>
						<option value="Ticket">{t('blueprints.scopeTicket')}</option>
					</select>
					<input name="labelEn" required placeholder={t('blueprints.fieldLabelEn')} class="rounded-lg border border-stone bg-white px-2.5 py-1.5 text-xs" />
					<input name="labelId" required placeholder={t('blueprints.fieldLabelId')} class="rounded-lg border border-stone bg-white px-2.5 py-1.5 text-xs" />
					<select name="fieldType" bind:value={newFieldType} class="rounded-lg border border-stone bg-white px-2.5 py-1.5 text-xs">
						<option value="Text">Text</option>
						<option value="Number">Number</option>
						<option value="Select">Select</option>
						<option value="Boolean">Boolean</option>
						<option value="Date">Date</option>
						<option value="Multiline">Multiline</option>
					</select>
				</div>
				{#if newFieldType === 'Select'}
					<input name="options" placeholder={t('blueprints.fieldOptions')} class="w-full rounded-lg border border-stone bg-white px-2.5 py-1.5 text-xs" />
				{/if}
				<div class="flex flex-wrap items-center gap-4 text-xs text-slate-700">
					<label class="flex items-center gap-1.5"><input type="checkbox" name="isRequired" /> {t('blueprints.fieldRequired')}</label>
					<label class="flex items-center gap-1.5"><input type="checkbox" name="isSearchable" /> {t('blueprints.fieldSearchable')}</label>
					<label class="flex items-center gap-1.5"><input type="checkbox" name="showOnIntake" checked /> {t('blueprints.fieldShowIntake')}</label>
					<label class="flex items-center gap-1.5"><input type="checkbox" name="showOnDetail" checked /> {t('blueprints.fieldShowDetail')}</label>
					<label class="flex items-center gap-1.5"><input type="checkbox" name="showOnPrint" /> {t('blueprints.fieldShowPrint')}</label>
					<label class="flex items-center gap-1.5"><input type="checkbox" name="showOnPublicTracking" /> {t('blueprints.fieldShowPublic')}</label>
				</div>
				<div class="flex justify-end">
					<button type="submit" class="rounded-lg bg-ink px-3.5 py-1.5 text-xs font-semibold text-surface hover:bg-claytile">+ {t('blueprints.addCustomField')}</button>
				</div>
			</form>
		{/if}
	</div>

	<!-- Inspection Checklists Manager -->
	<div class="space-y-4 rounded-2xl border border-stone bg-white p-5 shadow-xs">
		<h2 class="text-sm font-semibold text-ink">{t('blueprints.checklistsTitle')}</h2>
		{#if (data.config?.inspectionChecklists ?? []).length === 0}
			<p class="text-xs text-slate-500">{t('blueprints.noChecklists')}</p>
		{:else}
			<div class="divide-y divide-stone/60">
				{#each data.config?.inspectionChecklists ?? [] as item (item.id)}
					<div class="flex items-center justify-between gap-3 py-2.5 text-xs">
						<div>
							<span class="font-semibold text-ink">{item.labelEn}</span>
							<span class="text-slate-400"> / {item.labelId}</span>
							{#if item.category}<span class="ml-2 rounded bg-ash px-1.5 py-0.5 text-[10px] text-slate-600">{item.category}</span>{/if}
						</div>
						{#if canWrite}
							<form method="POST" action="?/deleteChecklist" use:enhance>
								<input type="hidden" name="id" value={item.id} />
								<button type="submit" class="text-xs font-semibold text-red-600 hover:underline">{t('common.delete')}</button>
							</form>
						{/if}
					</div>
				{/each}
			</div>
		{/if}

		{#if canWrite}
			<form method="POST" action="?/createChecklist" use:enhance class="grid gap-3 rounded-xl border border-stone/70 bg-surface p-4 sm:grid-cols-4">
				<input name="labelEn" required placeholder={t('blueprints.fieldLabelEn')} class="rounded-lg border border-stone bg-white px-2.5 py-1.5 text-xs" />
				<input name="labelId" required placeholder={t('blueprints.fieldLabelId')} class="rounded-lg border border-stone bg-white px-2.5 py-1.5 text-xs" />
				<input name="category" placeholder={t('blueprints.checklistCategory')} class="rounded-lg border border-stone bg-white px-2.5 py-1.5 text-xs" />
				<button type="submit" class="rounded-lg bg-ink px-3.5 py-1.5 text-xs font-semibold text-surface hover:bg-claytile">+ {t('blueprints.addChecklist')}</button>
			</form>
		{/if}
	</div>
</div>
