<script lang="ts">
	import { enhance } from '$app/forms';
	import { untrack } from 'svelte';
	import PageHeader from '$lib/components/PageHeader.svelte';
	import { createTranslator } from '$lib/i18n';
	import type { OperatingMode, TerminologyConfig } from '$lib/types/auth';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();
	const t = $derived(createTranslator(data.locale));
	const isCreate = $derived(data.wizardMode === 'create');

	let step = $state(1);
	let organizationName = $state(untrack(() => data.user.organization?.name ?? 'Servora Operations Group'));
	let organizationLogoUrl = $state(untrack(() => data.user.organization?.logoUrl ?? ''));
	let workspaceName = $state(untrack(() => (data.wizardMode === 'create' ? '' : data.user.workspace.name)));
	let workspaceSlug = $state(untrack(() => (data.wizardMode === 'create' ? '' : data.user.workspace.slug)));
	let phoneNumber = $state(untrack(() => data.shopProfile?.phoneNumber ?? ''));
	let address = $state(untrack(() => data.shopProfile?.address ?? ''));
	let defaultLocale = $state(untrack(() => data.user.workspace.defaultLocale || 'en'));
	let timeZone = $state(untrack(() => data.user.workspace.timeZone || 'Asia/Jakarta'));
	let currencyCode = $state(untrack(() => data.user.workspace.currencyCode || 'IDR'));
	let operatingMode = $state<OperatingMode>(untrack(() => data.user.workspace.operatingMode || 'Commercial'));
	let selectedBlueprintKey = $state(untrack(() => data.user.workspace.industryBlueprintKey || 'it_computer'));
	let replaceStatuses = $state(true);
	let replaceFieldsAndChecklists = $state(true);
	let seedSampleData = $state(true);
	let submitting = $state(false);

	const selectedBlueprint = $derived(
		data.blueprints.find((b) => b.key === selectedBlueprintKey) ?? data.blueprints[0]
	);

	let terminology = $state<TerminologyConfig>(
		untrack(() =>
			structuredClone(
				data.user.workspace.terminology ?? {
					en: {
						assetSingular: 'Device',
						assetPlural: 'Devices',
						ticketSingular: 'Service Ticket',
						ticketPlural: 'Service Tickets',
						requesterSingular: 'Customer',
						requesterPlural: 'Customers',
						primaryIdentifierLabel: 'Serial Number',
						secondaryIdentifierLabel: 'Asset Label',
						meterLabel: 'Usage Meter'
					},
					id: {
						assetSingular: 'Perangkat',
						assetPlural: 'Perangkat',
						ticketSingular: 'Tiket Servis',
						ticketPlural: 'Tiket Servis',
						requesterSingular: 'Pelanggan',
						requesterPlural: 'Pelanggan',
						primaryIdentifierLabel: 'Nomor Seri',
						secondaryIdentifierLabel: 'Label Aset',
						meterLabel: 'Meter Pemakaian'
					}
				}
			)
		)
	);

	function selectBlueprint(key: string) {
		selectedBlueprintKey = key;
		const bp = data.blueprints.find((b) => b.key === key);
		if (bp) {
			terminology = structuredClone(bp.terminology);
			if (isCreate) {
				operatingMode = bp.recommendedMode;
			}
		}
	}

	const modes: { value: OperatingMode; descKey: string }[] = [
		{ value: 'Commercial', descKey: 'onboarding.modeCommercialDesc' },
		{ value: 'InternalMaintenance', descKey: 'onboarding.modeInternalDesc' },
		{ value: 'Hybrid', descKey: 'onboarding.modeHybridDesc' }
	];
</script>

<div class="space-y-6">
	<PageHeader
		items={[
			{ label: t('nav.dashboard'), href: '/app' },
			{ label: isCreate ? t('onboarding.createTitle') : t('onboarding.title') }
		]}
		title={isCreate ? t('onboarding.createTitle') : t('onboarding.title')}
		subtitle={t('onboarding.subtitle')}
	/>

	<!-- Stepper -->
	<div class="grid grid-cols-2 gap-2 sm:grid-cols-4">
		{#each [
			{ num: 1, label: t('onboarding.step1') },
			{ num: 2, label: t('onboarding.step2') },
			{ num: 3, label: t('onboarding.step3') },
			{ num: 4, label: t('onboarding.step4') }
		] as item}
			<button
				type="button"
				onclick={() => (step = item.num)}
				class="flex items-center gap-2.5 rounded-xl border px-3.5 py-2.5 text-left text-xs font-semibold transition {step === item.num
					? 'border-claytile bg-claytile/10 text-ink'
					: step > item.num
						? 'border-emerald-200 bg-emerald-50/70 text-emerald-800'
						: 'border-stone bg-white text-slate-500 hover:bg-ash/50'}"
			>
				<span class="flex h-6 w-6 shrink-0 items-center justify-center rounded-full text-[11px] font-bold {step === item.num ? 'bg-claytile text-white' : step > item.num ? 'bg-emerald-600 text-white' : 'bg-ash text-slate-600'}">
					{item.num}
				</span>
				<span class="truncate">{item.label}</span>
			</button>
		{/each}
	</div>

	<form
		method="POST"
		action="?/submit"
		use:enhance={() => {
			submitting = true;
			return async ({ update }) => {
				submitting = false;
				await update();
			};
		}}
		class="rounded-2xl border border-stone bg-white p-6 shadow-xs"
	>
		<input type="hidden" name="wizardMode" value={data.wizardMode} />
		<input type="hidden" name="operatingMode" value={operatingMode} />
		<input type="hidden" name="industryBlueprintKey" value={selectedBlueprintKey} />
		<input type="hidden" name="terminologyJson" value={JSON.stringify(terminology)} />
		<input type="hidden" name="organizationName" value={organizationName} />
		<input type="hidden" name="organizationLogoUrl" value={organizationLogoUrl} />
		<input type="hidden" name="workspaceName" value={workspaceName} />
		<input type="hidden" name="workspaceSlug" value={workspaceSlug} />
		<input type="hidden" name="phoneNumber" value={phoneNumber} />
		<input type="hidden" name="address" value={address} />
		<input type="hidden" name="defaultLocale" value={defaultLocale} />
		<input type="hidden" name="timeZone" value={timeZone} />
		<input type="hidden" name="currencyCode" value={currencyCode} />

		{#if step === 1}
			<div class="space-y-5">
				<h2 class="text-base font-semibold text-ink">{t('onboarding.step1')}</h2>
				<div class="grid gap-4 sm:grid-cols-2">
					{#if !isCreate}
						<div>
							<label for="orgName" class="mb-1.5 block text-xs font-semibold uppercase tracking-wide text-slate-600">{t('onboarding.orgName')}</label>
							<input id="orgName" bind:value={organizationName} required class="w-full rounded-lg border border-stone px-3 py-2 text-sm text-ink focus:border-claytile focus:outline-none" />
						</div>
						<div>
							<label for="orgLogo" class="mb-1.5 block text-xs font-semibold uppercase tracking-wide text-slate-600">{t('onboarding.orgLogo')}</label>
							<input id="orgLogo" bind:value={organizationLogoUrl} placeholder="https://..." class="w-full rounded-lg border border-stone px-3 py-2 text-sm text-ink focus:border-claytile focus:outline-none" />
						</div>
					{/if}
					<div>
						<label for="wsName" class="mb-1.5 block text-xs font-semibold uppercase tracking-wide text-slate-600">{t('onboarding.workspaceName')}</label>
						<input id="wsName" bind:value={workspaceName} required class="w-full rounded-lg border border-stone px-3 py-2 text-sm text-ink focus:border-claytile focus:outline-none" />
					</div>
					<div>
						<label for="wsSlug" class="mb-1.5 block text-xs font-semibold uppercase tracking-wide text-slate-600">{t('onboarding.workspaceSlug')}</label>
						<input id="wsSlug" bind:value={workspaceSlug} class="w-full rounded-lg border border-stone px-3 py-2 text-sm text-ink focus:border-claytile focus:outline-none" />
					</div>
					<div>
						<label for="wsPhone" class="mb-1.5 block text-xs font-semibold uppercase tracking-wide text-slate-600">{t('onboarding.phone')}</label>
						<input id="wsPhone" bind:value={phoneNumber} class="w-full rounded-lg border border-stone px-3 py-2 text-sm text-ink focus:border-claytile focus:outline-none" />
					</div>
					<div>
						<label for="wsAddr" class="mb-1.5 block text-xs font-semibold uppercase tracking-wide text-slate-600">{t('onboarding.address')}</label>
						<input id="wsAddr" bind:value={address} class="w-full rounded-lg border border-stone px-3 py-2 text-sm text-ink focus:border-claytile focus:outline-none" />
					</div>
					<div>
						<label for="wsLocale" class="mb-1.5 block text-xs font-semibold uppercase tracking-wide text-slate-600">{t('onboarding.locale')}</label>
						<select id="wsLocale" bind:value={defaultLocale} class="w-full rounded-lg border border-stone bg-white px-3 py-2 text-sm text-ink">
							<option value="en">English (en)</option>
							<option value="id">Bahasa Indonesia (id)</option>
						</select>
					</div>
					<div class="grid grid-cols-2 gap-3">
						<div>
							<label for="wsTz" class="mb-1.5 block text-xs font-semibold uppercase tracking-wide text-slate-600">{t('onboarding.timezone')}</label>
							<input id="wsTz" bind:value={timeZone} class="w-full rounded-lg border border-stone px-3 py-2 text-sm text-ink" />
						</div>
						<div>
							<label for="wsCurrency" class="mb-1.5 block text-xs font-semibold uppercase tracking-wide text-slate-600">{t('onboarding.currency')}</label>
							<select id="wsCurrency" bind:value={currencyCode} class="w-full rounded-lg border border-stone bg-white px-3 py-2 text-sm text-ink">
								<option value="IDR">IDR</option>
								<option value="USD">USD</option>
								<option value="SGD">SGD</option>
								<option value="EUR">EUR</option>
							</select>
						</div>
					</div>
				</div>
			</div>
		{:else if step === 2}
			<div class="space-y-4">
				<h2 class="text-base font-semibold text-ink">{t('onboarding.step2')}</h2>
				<div class="grid gap-4 md:grid-cols-3">
					{#each modes as mode}
						<button
							type="button"
							onclick={() => (operatingMode = mode.value)}
							class="flex flex-col rounded-xl border p-4 text-left transition {operatingMode === mode.value
								? 'border-claytile bg-claytile/5 ring-2 ring-claytile/20'
								: 'border-stone bg-surface hover:border-claytile/40'}"
						>
							<div class="flex items-center justify-between">
								<span class="text-sm font-bold text-ink">{t(`workspaces.mode.${mode.value}`)}</span>
								<span class="h-3.5 w-3.5 rounded-full border {operatingMode === mode.value ? 'border-claytile bg-claytile' : 'border-slate-300'}"></span>
							</div>
							<p class="mt-2 text-xs leading-relaxed text-slate-600">{t(mode.descKey)}</p>
						</button>
					{/each}
				</div>
			</div>
		{:else if step === 3}
			<div class="space-y-6">
				<h2 class="text-base font-semibold text-ink">{t('onboarding.step3')}</h2>
				<div class="grid gap-3 md:grid-cols-3">
					{#each data.blueprints as bp (bp.key)}
						<button
							type="button"
							onclick={() => selectBlueprint(bp.key)}
							class="flex flex-col rounded-xl border p-4 text-left transition {selectedBlueprintKey === bp.key
								? 'border-claytile bg-claytile/5 ring-2 ring-claytile/20'
								: 'border-stone bg-surface hover:border-claytile/40'}"
						>
							<div class="flex items-center justify-between gap-2">
								<span class="text-sm font-bold text-ink">{data.locale === 'id' ? bp.nameId : bp.nameEn}</span>
								<span class="rounded bg-ash px-1.5 py-0.5 text-[10px] font-semibold text-slate-600">{bp.defaultTicketPrefix}</span>
							</div>
							<p class="mt-1.5 text-xs leading-relaxed text-slate-600">{data.locale === 'id' ? bp.descriptionId : bp.descriptionEn}</p>
							<div class="mt-3 flex flex-wrap gap-1">
								{#each bp.statuses.slice(0, 4) as st}
									<span class="rounded-full px-2 py-0.5 text-[10px] font-medium text-white" style="background-color: {st.color}">{st.name}</span>
								{/each}
							</div>
						</button>
					{/each}
				</div>

				{#if selectedBlueprint}
					<div class="grid gap-4 rounded-xl border border-stone bg-surface/70 p-4 md:grid-cols-3">
						<div>
							<div class="text-xs font-semibold uppercase tracking-wide text-slate-500">{t('onboarding.blueprintPreviewStatuses')}</div>
							<div class="mt-2 flex flex-wrap gap-1.5">
								{#each selectedBlueprint.statuses as st}
									<span class="rounded-full px-2 py-0.5 text-xs font-medium text-white" style="background-color: {st.color}">{st.name}</span>
								{/each}
							</div>
						</div>
						<div>
							<div class="text-xs font-semibold uppercase tracking-wide text-slate-500">{t('onboarding.blueprintPreviewFields')}</div>
							<ul class="mt-2 space-y-1 text-xs text-slate-700">
								{#each selectedBlueprint.fields as f}
									<li>• {data.locale === 'id' ? f.labelId : f.labelEn} <span class="text-slate-400">({f.scope} · {f.fieldType})</span></li>
								{/each}
							</ul>
						</div>
						<div>
							<div class="text-xs font-semibold uppercase tracking-wide text-slate-500">{t('onboarding.blueprintPreviewChecklists')}</div>
							<ul class="mt-2 space-y-1 text-xs text-slate-700">
								{#each selectedBlueprint.checklists as c}
									<li>• {data.locale === 'id' ? c.labelId : c.labelEn}</li>
								{/each}
							</ul>
						</div>
					</div>
				{/if}

				<!-- Editable Terminology -->
				<div class="space-y-3 rounded-xl border border-stone p-4">
					<h3 class="text-sm font-semibold text-ink">{t('onboarding.terminologyCustomize')}</h3>
					<div class="grid gap-4 md:grid-cols-2">
						<div class="space-y-2.5">
							<div class="text-xs font-semibold text-claytile">{t('onboarding.terminologyEn')}</div>
							<div class="grid grid-cols-2 gap-2">
								<input bind:value={terminology.en.assetSingular} placeholder={t('onboarding.termAssetSingular')} class="rounded-lg border border-stone px-2.5 py-1.5 text-xs" />
								<input bind:value={terminology.en.assetPlural} placeholder={t('onboarding.termAssetPlural')} class="rounded-lg border border-stone px-2.5 py-1.5 text-xs" />
								<input bind:value={terminology.en.ticketSingular} placeholder={t('onboarding.termTicketSingular')} class="rounded-lg border border-stone px-2.5 py-1.5 text-xs" />
								<input bind:value={terminology.en.ticketPlural} placeholder={t('onboarding.termTicketPlural')} class="rounded-lg border border-stone px-2.5 py-1.5 text-xs" />
								<input bind:value={terminology.en.requesterSingular} placeholder={t('onboarding.termRequesterSingular')} class="rounded-lg border border-stone px-2.5 py-1.5 text-xs" />
								<input bind:value={terminology.en.requesterPlural} placeholder={t('onboarding.termRequesterPlural')} class="rounded-lg border border-stone px-2.5 py-1.5 text-xs" />
							</div>
						</div>
						<div class="space-y-2.5">
							<div class="text-xs font-semibold text-claytile">{t('onboarding.terminologyId')}</div>
							<div class="grid grid-cols-2 gap-2">
								<input bind:value={terminology.id.assetSingular} placeholder={t('onboarding.termAssetSingular')} class="rounded-lg border border-stone px-2.5 py-1.5 text-xs" />
								<input bind:value={terminology.id.assetPlural} placeholder={t('onboarding.termAssetPlural')} class="rounded-lg border border-stone px-2.5 py-1.5 text-xs" />
								<input bind:value={terminology.id.ticketSingular} placeholder={t('onboarding.termTicketSingular')} class="rounded-lg border border-stone px-2.5 py-1.5 text-xs" />
								<input bind:value={terminology.id.ticketPlural} placeholder={t('onboarding.termTicketPlural')} class="rounded-lg border border-stone px-2.5 py-1.5 text-xs" />
								<input bind:value={terminology.id.requesterSingular} placeholder={t('onboarding.termRequesterSingular')} class="rounded-lg border border-stone px-2.5 py-1.5 text-xs" />
								<input bind:value={terminology.id.requesterPlural} placeholder={t('onboarding.termRequesterPlural')} class="rounded-lg border border-stone px-2.5 py-1.5 text-xs" />
							</div>
						</div>
					</div>
				</div>
			</div>
		{:else}
			<div class="space-y-5">
				<h2 class="text-base font-semibold text-ink">{t('onboarding.step4')}</h2>
				<div class="grid gap-3 rounded-xl border border-stone bg-surface p-4 text-sm sm:grid-cols-2">
					<div><span class="text-slate-500">{t('onboarding.workspaceName')}:</span> <strong class="text-ink">{workspaceName || '—'}</strong></div>
					<div><span class="text-slate-500">{t('onboarding.step2')}:</span> <strong class="text-ink">{t(`workspaces.mode.${operatingMode}`)}</strong></div>
					<div><span class="text-slate-500">{t('onboarding.step3')}:</span> <strong class="text-ink">{selectedBlueprint ? (data.locale === 'id' ? selectedBlueprint.nameId : selectedBlueprint.nameEn) : selectedBlueprintKey}</strong></div>
					<div><span class="text-slate-500">{t('onboarding.currency')} / {t('onboarding.timezone')}:</span> <strong class="text-ink">{currencyCode} · {timeZone}</strong></div>
				</div>

				<div class="space-y-2.5">
					{#if !isCreate}
						<label class="flex items-center gap-2.5 text-sm text-ink">
							<input type="checkbox" name="replaceStatuses" bind:checked={replaceStatuses} class="h-4 w-4 rounded border-stone text-claytile" />
							<span>{t('onboarding.replaceStatuses')}</span>
						</label>
						<label class="flex items-center gap-2.5 text-sm text-ink">
							<input type="checkbox" name="replaceFieldsAndChecklists" bind:checked={replaceFieldsAndChecklists} class="h-4 w-4 rounded border-stone text-claytile" />
							<span>{t('onboarding.replaceFields')}</span>
						</label>
					{/if}
					<label class="flex items-center gap-2.5 text-sm text-ink">
						<input type="checkbox" name="seedSampleData" bind:checked={seedSampleData} class="h-4 w-4 rounded border-stone text-claytile" />
						<span>{t('onboarding.seedSampleData')}</span>
					</label>
				</div>
			</div>
		{/if}

		<!-- Footer Controls -->
		<div class="mt-6 flex items-center justify-between border-t border-stone pt-4">
			<button
				type="button"
				disabled={step === 1}
				onclick={() => (step = Math.max(1, step - 1))}
				class="rounded-lg border border-stone px-4 py-2 text-sm font-medium text-ink transition hover:bg-ash disabled:opacity-40"
			>
				{t('common.previous')}
			</button>

			{#if step < 4}
				<button
					type="button"
					onclick={() => (step = Math.min(4, step + 1))}
					class="rounded-lg bg-ink px-5 py-2 text-sm font-semibold text-surface transition hover:bg-claytile"
				>
					{t('common.next')}
				</button>
			{:else}
				<button
					type="submit"
					disabled={submitting || !workspaceName.trim()}
					class="rounded-lg bg-claytile px-5 py-2 text-sm font-semibold text-white transition hover:opacity-90 disabled:opacity-50"
				>
					{isCreate ? t('onboarding.createWorkspaceBtn') : t('onboarding.launchWorkspace')}
				</button>
			{/if}
		</div>
	</form>
</div>
