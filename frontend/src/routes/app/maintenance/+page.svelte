<script lang="ts">
	import { enhance } from '$app/forms';
	import PageHeader from '$lib/components/PageHeader.svelte';
	import { can, permissions } from '$lib/auth/permissions';
	import { createTranslator, formatDate, resolveTerminology } from '$lib/i18n';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();
	const t = $derived(createTranslator(data.locale));
	const terms = $derived(resolveTerminology(data.user.workspace.terminology, data.locale));
	const canWrite = $derived(can(data.user.permissions, permissions.maintenanceWrite));
	const canDelete = $derived(can(data.user.permissions, permissions.maintenanceDelete));

	let showCreateForm = $state(false);
	let triggerType = $state<'Calendar' | 'Meter' | 'Both'>('Calendar');
</script>

<div class="space-y-6">
	<PageHeader
		items={[
			{ label: t('nav.dashboard'), href: '/app' },
			{ label: t('nav.maintenance') }
		]}
		title={t('maintenance.title')}
		subtitle={t('maintenance.subtitle')}
	>
		{#snippet actions()}
			{#if canWrite}
				<form method="POST" action="?/runDueCheck" use:enhance>
					<button
						type="submit"
						class="rounded-xl border border-stone bg-white px-3.5 py-2 text-xs font-semibold text-ink transition hover:bg-ash"
					>
						{t('maintenance.runDueCheck')}
					</button>
				</form>
				<button
					type="button"
					onclick={() => (showCreateForm = !showCreateForm)}
					class="rounded-xl bg-ink px-4 py-2 text-xs font-semibold text-surface transition hover:bg-claytile"
				>
					+ {t('maintenance.newSchedule')}
				</button>
			{/if}
		{/snippet}
	</PageHeader>

	<!-- Filter tabs -->
	<div class="flex items-center gap-2">
		<a
			href="/app/maintenance"
			class="rounded-lg border px-3 py-1.5 text-xs font-semibold transition {!data.dueOnly ? 'border-ink bg-ink text-surface' : 'border-stone bg-white text-slate-600 hover:bg-ash'}"
		>
			{t('maintenance.filterAll')}
		</a>
		<a
			href="/app/maintenance?dueOnly=true"
			class="rounded-lg border px-3 py-1.5 text-xs font-semibold transition {data.dueOnly ? 'border-claytile bg-claytile text-white' : 'border-stone bg-white text-slate-600 hover:bg-ash'}"
		>
			{t('maintenance.filterDue')}
		</a>
	</div>

	{#if showCreateForm && canWrite}
		<form
			method="POST"
			action="?/create"
			use:enhance={() => {
				return async ({ result, update }) => {
					if (result.type === 'success') showCreateForm = false;
					await update();
				};
			}}
			class="space-y-4 rounded-2xl border border-stone bg-white p-5 shadow-xs"
		>
			<h2 class="text-sm font-semibold text-ink">{t('maintenance.newSchedule')}</h2>
			<div class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
				<div>
					<label for="pmTitle" class="mb-1 block text-xs font-semibold text-slate-600">{t('maintenance.scheduleTitle')}</label>
					<input id="pmTitle" name="title" required class="w-full rounded-lg border border-stone px-3 py-2 text-sm" />
				</div>
				<div>
					<label for="pmAsset" class="mb-1 block text-xs font-semibold text-slate-600">{terms.assetSingular}</label>
					<select id="pmAsset" name="serviceAssetId" required class="w-full rounded-lg border border-stone bg-white px-3 py-2 text-sm">
						<option value="">—</option>
						{#each data.assets as asset (asset.id)}
							<option value={asset.id}>{asset.brand} {asset.model} {asset.serialNumber ? `(${asset.serialNumber})` : ''} — {asset.customerName ?? ''}</option>
						{/each}
					</select>
				</div>
				<div>
					<label for="pmTemplate" class="mb-1 block text-xs font-semibold text-slate-600">{t('maintenance.template')}</label>
					<select id="pmTemplate" name="serviceTemplateId" class="w-full rounded-lg border border-stone bg-white px-3 py-2 text-sm">
						<option value="">—</option>
						{#each data.templates as tpl (tpl.id)}
							<option value={tpl.id}>{tpl.name}</option>
						{/each}
					</select>
				</div>
				<div>
					<label for="pmTrigger" class="mb-1 block text-xs font-semibold text-slate-600">{t('maintenance.triggerType')}</label>
					<select id="pmTrigger" name="triggerType" bind:value={triggerType} class="w-full rounded-lg border border-stone bg-white px-3 py-2 text-sm">
						<option value="Calendar">{t('maintenance.triggerCalendar')}</option>
						<option value="Meter">{t('maintenance.triggerMeter')}</option>
						<option value="Both">{t('maintenance.triggerBoth')}</option>
					</select>
				</div>
				{#if triggerType === 'Calendar' || triggerType === 'Both'}
					<div class="grid grid-cols-2 gap-2">
						<div>
							<label for="pmIntVal" class="mb-1 block text-xs font-semibold text-slate-600">{t('maintenance.intervalValue')}</label>
							<input id="pmIntVal" name="intervalValue" type="number" min="1" value="3" class="w-full rounded-lg border border-stone px-3 py-2 text-sm" />
						</div>
						<div>
							<label for="pmIntUnit" class="mb-1 block text-xs font-semibold text-slate-600">{t('maintenance.intervalUnit')}</label>
							<select id="pmIntUnit" name="intervalUnit" class="w-full rounded-lg border border-stone bg-white px-3 py-2 text-sm">
								<option value="Days">{t('maintenance.unitDays')}</option>
								<option value="Weeks">{t('maintenance.unitWeeks')}</option>
								<option value="Months" selected>{t('maintenance.unitMonths')}</option>
							</select>
						</div>
					</div>
					<div>
						<label for="pmNextDue" class="mb-1 block text-xs font-semibold text-slate-600">{t('maintenance.nextDueAt')}</label>
						<input id="pmNextDue" name="nextDueAt" type="date" class="w-full rounded-lg border border-stone px-3 py-2 text-sm" />
					</div>
				{/if}
				{#if triggerType === 'Meter' || triggerType === 'Both'}
					<div>
						<label for="pmMeterInt" class="mb-1 block text-xs font-semibold text-slate-600">{t('maintenance.meterInterval')}</label>
						<input id="pmMeterInt" name="meterInterval" type="number" step="any" min="1" placeholder="5000" class="w-full rounded-lg border border-stone px-3 py-2 text-sm" />
					</div>
					<div>
						<label for="pmNextMeter" class="mb-1 block text-xs font-semibold text-slate-600">{t('maintenance.nextDueMeter')}</label>
						<input id="pmNextMeter" name="nextDueMeter" type="number" step="any" class="w-full rounded-lg border border-stone px-3 py-2 text-sm" />
					</div>
				{/if}
				<div>
					<label for="pmLoc" class="mb-1 block text-xs font-semibold text-slate-600">{t('fieldService.locationType')}</label>
					<select id="pmLoc" name="defaultLocationType" class="w-full rounded-lg border border-stone bg-white px-3 py-2 text-sm">
						<option value="InShop">{t('fieldService.inShop')}</option>
						<option value="OnSite">{t('fieldService.onSite')}</option>
						<option value="PickupDelivery">{t('fieldService.pickupDelivery')}</option>
					</select>
				</div>
				<div>
					<label for="pmLead" class="mb-1 block text-xs font-semibold text-slate-600">{t('maintenance.leadTimeDays')}</label>
					<input id="pmLead" name="leadTimeDays" type="number" min="1" value="7" class="w-full rounded-lg border border-stone px-3 py-2 text-sm" />
				</div>
			</div>
			<label class="flex items-center gap-2 text-sm text-ink">
				<input type="checkbox" name="autoCreateTicket" class="h-4 w-4 rounded border-stone text-claytile" />
				<span>{t('maintenance.autoCreateTicket')}</span>
			</label>
			<div class="flex justify-end gap-2">
				<button type="button" onclick={() => (showCreateForm = false)} class="rounded-lg border border-stone px-3.5 py-1.5 text-xs font-semibold text-slate-600">{t('common.cancel')}</button>
				<button type="submit" class="rounded-lg bg-claytile px-4 py-1.5 text-xs font-semibold text-white">{t('common.save')}</button>
			</div>
		</form>
	{/if}

	{#if data.schedules.length === 0}
		<div class="rounded-2xl border border-dashed border-stone bg-white/70 p-10 text-center text-sm text-slate-500">
			{t('maintenance.empty')}
		</div>
	{:else}
		<div class="grid gap-4 md:grid-cols-2">
			{#each data.schedules as sched (sched.id)}
				<div class="flex flex-col justify-between rounded-2xl border border-stone bg-white p-5 shadow-xs">
					<div>
						<div class="flex items-start justify-between gap-3">
							<div>
								<h3 class="text-sm font-bold text-ink">{sched.title}</h3>
								<p class="mt-0.5 text-xs text-slate-600">
									{sched.assetDisplayName}
									{#if sched.assetIdentifier}<span class="font-mono text-slate-400"> · {sched.assetIdentifier}</span>{/if}
									<span> · {sched.customerName}</span>
								</p>
							</div>
							<span class="shrink-0 rounded-full px-2.5 py-0.5 text-[11px] font-semibold {sched.isDue
								? 'bg-red-100 text-red-700'
								: sched.isUpcoming
									? 'bg-amber-100 text-amber-800'
									: 'bg-emerald-50 text-emerald-700'}">
								{sched.isDue ? t('maintenance.badgeDue') : sched.isUpcoming ? t('maintenance.badgeUpcoming') : t('maintenance.badgeScheduled')}
							</span>
						</div>

						<div class="mt-3 grid grid-cols-2 gap-2 rounded-xl bg-surface p-3 text-xs">
							<div>
								<span class="text-slate-500">{t('maintenance.nextDueAt')}:</span>
								<div class="font-semibold text-ink">{sched.nextDueAt ? formatDate(sched.nextDueAt, data.locale) : '—'}</div>
							</div>
							<div>
								<span class="text-slate-500">{t('maintenance.nextDueMeter')}:</span>
								<div class="font-semibold text-ink">
									{sched.nextDueMeter != null ? `${sched.currentMeterReading ?? 0} / ${sched.nextDueMeter} ${sched.meterUnit ?? ''}` : '—'}
								</div>
							</div>
						</div>

						{#if sched.lastGeneratedTicketNumber}
							<div class="mt-2 text-xs text-slate-500">
								{terms.ticketSingular}: <a href="/app/tickets/{sched.lastGeneratedTicketId}" class="font-semibold text-claytile underline">{sched.lastGeneratedTicketNumber}</a>
							</div>
						{/if}
					</div>

					{#if canWrite || canDelete}
						<div class="mt-4 flex flex-wrap items-center justify-end gap-2 border-t border-stone/70 pt-3">
							{#if canWrite}
								<form method="POST" action="?/generateTicket" use:enhance>
									<input type="hidden" name="id" value={sched.id} />
									<button type="submit" class="rounded-lg bg-ink px-3 py-1.5 text-xs font-semibold text-surface hover:bg-claytile">
										{t('maintenance.generateNow')}
									</button>
								</form>
								<form method="POST" action="?/completeCycle" use:enhance>
									<input type="hidden" name="id" value={sched.id} />
									<button type="submit" class="rounded-lg border border-stone bg-white px-3 py-1.5 text-xs font-semibold text-ink hover:bg-ash">
										{t('maintenance.markCompleted')}
									</button>
								</form>
							{/if}
							{#if canDelete}
								<form method="POST" action="?/delete" use:enhance>
									<input type="hidden" name="id" value={sched.id} />
									<button type="submit" class="rounded-lg border border-red-200 px-2.5 py-1.5 text-xs font-semibold text-red-600 hover:bg-red-50">
										{t('common.delete')}
									</button>
								</form>
							{/if}
						</div>
					{/if}
				</div>
			{/each}
		</div>
	{/if}
</div>
