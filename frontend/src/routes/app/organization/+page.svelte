<script lang="ts">
	import { invalidateAll } from '$app/navigation';
	import PageHeader from '$lib/components/PageHeader.svelte';
	import { createTranslator, formatCurrency, resolveBlueprintLabel } from '$lib/i18n';
	import { toast } from '$lib/stores/toast.svelte';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();
	const t = $derived(createTranslator(data.locale));

	async function switchTo(workspaceId: number) {
		const res = await fetch('/app/switch-workspace', {
			method: 'POST',
			headers: { 'Content-Type': 'application/json' },
			body: JSON.stringify({ workspaceId })
		});
		if (res.ok) {
			toast.success(t('workspaces.switched'));
			await invalidateAll();
			window.location.href = '/app';
		} else {
			toast.error(t('common.error'));
		}
	}
</script>

<div class="space-y-6">
	<PageHeader
		items={[
			{ label: t('nav.dashboard'), href: '/app' },
			{ label: t('organization.title') }
		]}
		title={data.summary?.organizationName ?? t('organization.title')}
		subtitle={t('organization.subtitle')}
	>
		{#snippet actions()}
			<a
				href="/app/onboarding?mode=create"
				class="rounded-xl bg-ink px-4 py-2 text-xs font-semibold text-surface transition hover:bg-claytile"
			>
				+ {t('workspaces.createNew')}
			</a>
		{/snippet}
	</PageHeader>

	{#if data.summary}
		<div class="grid gap-3 sm:grid-cols-2 lg:grid-cols-6">
			{#each [
				{ label: t('organization.totalWorkspaces'), value: data.summary.totalWorkspaces },
				{ label: t('organization.totalOpenTickets'), value: data.summary.totalOpenTickets },
				{ label: t('organization.totalOverdue'), value: data.summary.totalOverdueTickets },
				{ label: t('organization.totalCompletedToday'), value: data.summary.totalCompletedToday },
				{ label: t('organization.totalAssets'), value: data.summary.totalServiceAssets },
				{ label: t('organization.totalDuePm'), value: data.summary.totalDueMaintenanceSchedules }
			] as stat}
				<div class="rounded-2xl border border-stone bg-white p-4 shadow-xs">
					<div class="text-[11px] font-semibold uppercase tracking-wide text-secondary">{stat.label}</div>
					<div class="mt-1 text-2xl font-bold tabular-nums text-ink">{stat.value}</div>
				</div>
			{/each}
		</div>

		<div class="rounded-2xl border border-stone bg-white p-5 shadow-xs">
			<h2 class="mb-4 text-sm font-semibold text-ink">{t('organization.workspaceBreakdown')}</h2>
			<div class="overflow-x-auto">
				<table class="w-full text-left text-xs">
					<thead class="border-b border-stone text-[11px] uppercase tracking-wide text-secondary">
						<tr>
							<th class="pb-2.5 pr-4">{t('onboarding.workspaceName')}</th>
							<th class="pb-2.5 pr-4">{t('onboarding.step2')}</th>
							<th class="pb-2.5 pr-4">{t('organization.totalOpenTickets')}</th>
							<th class="pb-2.5 pr-4">{t('organization.totalOverdue')}</th>
							<th class="pb-2.5 pr-4">{t('organization.totalAssets')}</th>
							<th class="pb-2.5 pr-4">{t('organization.totalDuePm')}</th>
							<th class="pb-2.5 pr-4">{t('organization.revenuePaid')}</th>
							<th class="pb-2.5 text-right"></th>
						</tr>
					</thead>
					<tbody class="divide-y divide-stone/60 tabular-nums">
						{#each data.summary.workspaces as ws (ws.workspaceId)}
							<tr>
								<td class="py-3 pr-4">
									<div class="font-semibold text-ink">{ws.name}</div>
									<div class="mt-0.5 text-[11.5px] text-secondary">{resolveBlueprintLabel(ws.industryBlueprintKey, data.locale)}</div>
								</td>
								<td class="py-3 pr-4">
									<span class="rounded-full bg-ash px-2 py-0.5 text-[11px] font-medium text-ink">
										{t(`workspaces.mode.${ws.operatingMode}`)}
									</span>
								</td>
								<td class="py-3 pr-4 font-semibold text-ink">{ws.openTickets}</td>
								<td class="py-3 pr-4 font-semibold {ws.overdueTickets > 0 ? 'text-red-600' : 'text-slate-600'}">{ws.overdueTickets}</td>
								<td class="py-3 pr-4 text-slate-700">{ws.activeServiceAssets}</td>
								<td class="py-3 pr-4 font-semibold {ws.dueMaintenanceSchedules > 0 ? 'text-amber-700' : 'text-slate-600'}">{ws.dueMaintenanceSchedules}</td>
								<td class="py-3 pr-4 font-medium text-ink">{formatCurrency(ws.revenuePaid, data.locale, ws.currencyCode)}</td>
								<td class="py-3 text-right">
									<button
										type="button"
										onclick={() => switchTo(ws.workspaceId)}
										class="rounded-lg border border-stone px-3 py-1 text-xs font-semibold text-ink transition hover:border-claytile hover:text-claytile"
									>
										{t('organization.switchToWorkspace')}
									</button>
								</td>
							</tr>
						{/each}
					</tbody>
				</table>
			</div>
		</div>
	{:else}
		<div class="rounded-2xl border border-stone bg-white p-8 text-center text-sm text-slate-500">
			{t('common.noOptions')}
		</div>
	{/if}
</div>
