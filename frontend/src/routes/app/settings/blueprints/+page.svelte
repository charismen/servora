<script lang="ts">
	import { enhance } from '$app/forms';
	import { untrack } from 'svelte';
	import PageHeader from '$lib/components/PageHeader.svelte';
	import { can, permissions } from '$lib/auth/permissions';
	import { createTranslator, resolveFeatureConfig } from '$lib/i18n';
	import { toast } from '$lib/stores/toast.svelte';
	import type { OperatingMode, TerminologyConfig, WorkspaceFeatureConfig } from '$lib/types/auth';
	import type { ActionData, PageData } from './$types';

	let { data, form }: { data: PageData; form: ActionData } = $props();
	const t = $derived(createTranslator(data.locale));
	const canWrite = $derived(can(data.user.permissions, permissions.settingsWrite));

	const allAttachmentTypes = [
		'FrontView',
		'BackView',
		'DeviceFront',
		'DeviceBack',
		'Screen',
		'Keyboard',
		'ExteriorWalkaround',
		'Odometer',
		'EngineBay',
		'IndoorUnit',
		'OutdoorUnit',
		'GaugeReading',
		'SiteLocation',
		'EquipmentTag',
		'SafetyCheck',
		'SerialOrTag',
		'Accessories',
		'BeforeRepair',
		'AfterRepair',
		'Damage',
		'Receipt',
		'Other'
	];

	let selectedBlueprint = $state(untrack(() => data.config?.industryBlueprintKey ?? 'it_computer'));
	let operatingMode = $state<OperatingMode>(untrack(() => data.config?.operatingMode ?? 'Commercial'));
	let terminology = $state<TerminologyConfig>(
		untrack(() => structuredClone(data.config?.terminology ?? data.user.workspace.terminology))
	);
	let featureConfig = $state<WorkspaceFeatureConfig>(
		untrack(() =>
			structuredClone(
				resolveFeatureConfig(
					data.config?.featureConfig ?? data.user.workspace.featureConfig,
					data.config?.operatingMode ?? data.user.workspace.operatingMode
				)
			)
		)
	);
	let quickAccessoriesEnText = $state(untrack(() => featureConfig.quickAccessoriesEn.join(', ')));
	let quickAccessoriesIdText = $state(untrack(() => featureConfig.quickAccessoriesId.join(', ')));
	let newFieldType = $state('Text');

	let syncedKey = $state('');
	$effect(() => {
		const nextKey = `${data.config?.industryBlueprintKey}|${data.config?.operatingMode}|${JSON.stringify(data.config?.featureConfig)}`;
		if (data.config && nextKey !== syncedKey) {
			syncedKey = nextKey;
			untrack(() => {
				selectedBlueprint = data.config!.industryBlueprintKey;
				operatingMode = data.config!.operatingMode;
				terminology = structuredClone(data.config!.terminology);
				featureConfig = structuredClone(resolveFeatureConfig(data.config!.featureConfig, data.config!.operatingMode));
				quickAccessoriesEnText = featureConfig.quickAccessoriesEn.join(', ');
				quickAccessoriesIdText = featureConfig.quickAccessoriesId.join(', ');
			});
		}
	});

	let lastToastForm: unknown = null;
	$effect(() => {
		const result = form as { success?: boolean; message?: string; messageKey?: string } | null | undefined;
		if (!result || result === lastToastForm) return;
		lastToastForm = result;
		const text = result.messageKey ? t(result.messageKey) : result.message;
		if (text) {
			if (result.success) toast.success(text);
			else toast.error(text);
		}
	});

	const serializedFeatureConfig = $derived.by(() => ({
		...featureConfig,
		securityCredentialMode: featureConfig.showSecurityCredentials
			? featureConfig.securityCredentialMode === 'None'
				? 'PasscodeOnly'
				: featureConfig.securityCredentialMode
			: 'None',
		quickAccessoriesEn: quickAccessoriesEnText
			.split(',')
			.map((s) => s.trim())
			.filter(Boolean),
		quickAccessoriesId: quickAccessoriesIdText
			.split(',')
			.map((s) => s.trim())
			.filter(Boolean)
	}));

	function toggleAttachmentType(typeKey: string) {
		const exists = featureConfig.attachmentTypes.includes(typeKey);
		const next = exists
			? featureConfig.attachmentTypes.filter((item) => item !== typeKey)
			: [...featureConfig.attachmentTypes, typeKey];
		featureConfig.attachmentTypes = next.length > 0 ? next : ['Other'];
	}

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

	<!-- Operating Mode, Terminology & Workflow Features -->
	<form method="POST" action="?/saveTerminology" use:enhance class="space-y-5 rounded-2xl border border-stone bg-white p-5 shadow-xs">
		<input type="hidden" name="terminologyJson" value={JSON.stringify(terminology)} />
		<input type="hidden" name="featureConfigJson" value={JSON.stringify(serializedFeatureConfig)} />
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
					<input bind:value={terminology.en.secondaryIdentifierLabel} placeholder={t('onboarding.termSecondaryId')} class="rounded-lg border border-stone bg-white px-2.5 py-1.5 text-xs" />
					<input bind:value={terminology.en.meterLabel} placeholder={t('onboarding.termMeterLabel')} class="col-span-2 rounded-lg border border-stone bg-white px-2.5 py-1.5 text-xs" />
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
					<input bind:value={terminology.id.secondaryIdentifierLabel} placeholder={t('onboarding.termSecondaryId')} class="rounded-lg border border-stone bg-white px-2.5 py-1.5 text-xs" />
					<input bind:value={terminology.id.meterLabel} placeholder={t('onboarding.termMeterLabel')} class="col-span-2 rounded-lg border border-stone bg-white px-2.5 py-1.5 text-xs" />
				</div>
			</div>
		</div>

		<!-- Configurable Workflow Features & Intake Modules -->
		<div class="space-y-4 rounded-xl border border-stone/80 bg-surface/40 p-4">
			<div>
				<h3 class="text-sm font-semibold text-ink">{t('blueprints.featureTogglesTitle')}</h3>
				<p class="text-[11px] text-slate-600">{t('blueprints.featureTogglesSubtitle')}</p>
			</div>

			<div class="grid gap-2.5 sm:grid-cols-2 text-xs text-ink">
				<label class="flex items-center gap-2 rounded-lg border border-stone/70 bg-white px-3 py-2">
					<input type="checkbox" bind:checked={featureConfig.showPreRepairChecklist} class="rounded border-stone text-claytile" />
					<span>{t('blueprints.showPreRepairChecklist')}</span>
				</label>
				<label class="flex items-center gap-2 rounded-lg border border-stone/70 bg-white px-3 py-2">
					<input type="checkbox" bind:checked={featureConfig.showPhysicalConditionNotes} class="rounded border-stone text-claytile" />
					<span>{t('blueprints.showPhysicalConditionNotes')}</span>
				</label>
				<label class="flex items-center gap-2 rounded-lg border border-stone/70 bg-white px-3 py-2">
					<input type="checkbox" bind:checked={featureConfig.showAccessories} class="rounded border-stone text-claytile" />
					<span>{t('blueprints.showAccessories')}</span>
				</label>
				<label class="flex items-center gap-2 rounded-lg border border-stone/70 bg-white px-3 py-2">
					<input
						type="checkbox"
						checked={featureConfig.showSecurityCredentials}
						onchange={(e) => {
							const checked = (e.currentTarget as HTMLInputElement).checked;
							featureConfig.showSecurityCredentials = checked;
							if (checked && featureConfig.securityCredentialMode === 'None') {
								featureConfig.securityCredentialMode = 'PasscodeOnly';
							}
						}}
						class="rounded border-stone text-claytile"
					/>
					<span>{t('blueprints.showSecurityCredentials')}</span>
				</label>
				<label class="flex items-center gap-2 rounded-lg border border-stone/70 bg-white px-3 py-2">
					<input type="checkbox" bind:checked={featureConfig.showWarrantySection} class="rounded border-stone text-claytile" />
					<span>{t('blueprints.showWarrantySection')}</span>
				</label>
				<label class="flex items-center gap-2 rounded-lg border border-stone/70 bg-white px-3 py-2">
					<input type="checkbox" bind:checked={featureConfig.showBillingAndPayments} class="rounded border-stone text-claytile" />
					<span>{t('blueprints.showBillingAndPayments')}</span>
				</label>
				<label class="flex items-center gap-2 rounded-lg border border-stone/70 bg-white px-3 py-2">
					<input type="checkbox" bind:checked={featureConfig.showCustomerApprovals} class="rounded border-stone text-claytile" />
					<span>{t('blueprints.showCustomerApprovals')}</span>
				</label>
				<label class="flex items-center gap-2 rounded-lg border border-stone/70 bg-white px-3 py-2">
					<input type="checkbox" bind:checked={featureConfig.enableFieldService} class="rounded border-stone text-claytile" />
					<span>{t('blueprints.enableFieldService')}</span>
				</label>
				<label class="flex items-center gap-2 rounded-lg border border-stone/70 bg-white px-3 py-2">
					<input type="checkbox" bind:checked={featureConfig.enablePreventiveMaintenance} class="rounded border-stone text-claytile" />
					<span>{t('blueprints.enablePreventiveMaintenance')}</span>
				</label>
				<label class="flex flex-col gap-1 rounded-lg border border-stone/70 bg-white px-3 py-2">
					<span class="text-[10px] font-semibold uppercase tracking-wide text-slate-500">{t('blueprints.defaultServiceLocationType')}</span>
					<select bind:value={featureConfig.defaultServiceLocationType} class="rounded border border-stone bg-surface px-2 py-1 text-xs">
						<option value="InShop">{t('fieldService.inShop')}</option>
						<option value="OnSite">{t('fieldService.onSite')}</option>
						<option value="PickupDelivery">{t('fieldService.pickupDelivery')}</option>
					</select>
				</label>
			</div>

			{#if featureConfig.showSecurityCredentials}
				<div class="rounded-lg border border-stone/70 bg-white p-3 space-y-1.5">
					<span class="text-[11px] font-semibold uppercase tracking-wide text-slate-500">{t('blueprints.securityCredentialMode')}</span>
					<select bind:value={featureConfig.securityCredentialMode} class="w-full sm:w-96 rounded-lg border border-stone bg-surface px-2.5 py-1.5 text-xs">
						<option value="PasscodeOnly">{t('blueprints.securityModePasscodeOnly')}</option>
						<option value="PasscodeAndPattern">{t('blueprints.securityModePasscodeAndPattern')}</option>
					</select>
				</div>
			{/if}

			{#if featureConfig.showAccessories}
				<div class="grid gap-3 md:grid-cols-2 rounded-lg border border-stone/70 bg-white p-3">
					<label class="space-y-1">
						<span class="text-[11px] font-semibold text-slate-600">{t('blueprints.quickAccessoriesEn')}</span>
						<input bind:value={quickAccessoriesEnText} class="w-full rounded-lg border border-stone bg-surface px-2.5 py-1.5 text-xs" />
					</label>
					<label class="space-y-1">
						<span class="text-[11px] font-semibold text-slate-600">{t('blueprints.quickAccessoriesId')}</span>
						<input bind:value={quickAccessoriesIdText} class="w-full rounded-lg border border-stone bg-surface px-2.5 py-1.5 text-xs" />
					</label>
					<p class="md:col-span-2 text-[11px] text-slate-500">{t('blueprints.quickAccessoriesHint')}</p>
				</div>
			{/if}

			<div class="rounded-lg border border-stone/70 bg-white p-3 space-y-2">
				<span class="text-[11px] font-semibold uppercase tracking-wide text-slate-500 block">{t('blueprints.attachmentTypes')}</span>
				<div class="flex flex-wrap gap-1.5">
					{#each allAttachmentTypes as attType}
						<label class="inline-flex cursor-pointer items-center gap-1.5 rounded-full border px-2.5 py-1 text-[11px] font-medium transition {featureConfig.attachmentTypes.includes(attType) ? 'border-claytile bg-claytile/10 text-ink' : 'border-stone bg-surface text-slate-500'}">
							<input
								type="checkbox"
								checked={featureConfig.attachmentTypes.includes(attType)}
								onchange={() => toggleAttachmentType(attType)}
								class="sr-only"
							/>
							<span>{t(`tickets.attachmentType.${attType}`)}</span>
						</label>
					{/each}
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
