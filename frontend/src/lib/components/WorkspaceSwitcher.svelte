<script lang="ts">
	import { invalidateAll } from '$app/navigation';
	import { createTranslator, type Locale } from '$lib/i18n';
	import type { WorkspaceContext, WorkspaceSummary } from '$lib/types/auth';
	import { toast } from '$lib/stores/toast.svelte';

	let {
		currentWorkspace,
		accessibleWorkspaces = [],
		locale = 'en',
		compact = false,
		canManage = false
	}: {
		currentWorkspace: WorkspaceContext;
		accessibleWorkspaces: WorkspaceSummary[];
		locale: Locale;
		compact?: boolean;
		canManage?: boolean;
	} = $props();

	const t = $derived(createTranslator(locale));
	let open = $state(false);
	let switching = $state(false);

	async function switchWorkspace(workspaceId: number) {
		if (workspaceId === currentWorkspace.id || switching) {
			open = false;
			return;
		}
		switching = true;
		try {
			const res = await fetch('/app/switch-workspace', {
				method: 'POST',
				headers: { 'Content-Type': 'application/json' },
				body: JSON.stringify({ workspaceId })
			});
			if (!res.ok) {
				toast.error(t('common.error'));
				return;
			}
			toast.success(t('workspaces.switched'));
			open = false;
			await invalidateAll();
			window.location.reload();
		} catch {
			toast.error(t('common.error'));
		} finally {
			switching = false;
		}
	}

	function modeBadgeClass(mode: string) {
		if (mode === 'InternalMaintenance') return 'bg-indigo-50 text-indigo-700 border-indigo-200';
		if (mode === 'Hybrid') return 'bg-amber-50 text-amber-800 border-amber-200';
		return 'bg-emerald-50 text-emerald-700 border-emerald-200';
	}
</script>

<div class="relative">
	<button
		type="button"
		onclick={() => (open = !open)}
		disabled={switching}
		aria-expanded={open}
		aria-haspopup="listbox"
		class="flex w-full items-center {compact ? 'justify-center px-1.5 py-1.5' : 'justify-between gap-2 px-2.5 py-2'} rounded-[10px] border border-stone bg-white text-left transition-colors hover:border-claytile/50 hover:bg-ash/50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-claytile/45 cursor-pointer"
		title={compact ? `${currentWorkspace.name} — ${t('workspaces.switch')}` : t('workspaces.switch')}
	>
		<div class="flex min-w-0 items-center {compact ? 'justify-center' : 'gap-2.5'}">
			<div class="flex h-7 w-7 shrink-0 items-center justify-center rounded-lg bg-ink text-xs font-bold text-surface">
				{currentWorkspace.name.slice(0, 2).toUpperCase()}
			</div>
			{#if !compact}
				<div class="min-w-0 flex-1">
					<div class="truncate text-xs font-semibold text-ink leading-tight">{currentWorkspace.name}</div>
					<div class="truncate text-[10.5px] text-secondary mt-0.5 leading-tight">{currentWorkspace.organizationName}</div>
				</div>
			{/if}
		</div>
		{#if !compact}
			<span class="material-symbols-outlined text-[18px] shrink-0 text-secondary transition-transform duration-150 {open ? 'rotate-180' : ''}">
				expand_more
			</span>
		{/if}
	</button>

	{#if open}
		<div class="absolute left-0 top-full z-50 mt-1.5 w-64 rounded-xl border border-stone bg-white p-1.5 shadow-lg">
			<div class="px-2.5 py-1.5 text-[10px] font-semibold uppercase tracking-wider text-secondary">
				{t('workspaces.switch')}
			</div>
			<div class="max-h-60 space-y-1 overflow-y-auto">
				{#each accessibleWorkspaces as ws (ws.id)}
					<button
						type="button"
						onclick={() => switchWorkspace(ws.id)}
						class="flex w-full items-center justify-between gap-2 rounded-lg px-2.5 py-2 text-left text-xs transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-claytile/45 cursor-pointer {ws.id === currentWorkspace.id
							? 'bg-ash font-semibold text-ink'
							: 'text-ink/85 hover:bg-ash/50'}"
					>
						<div class="min-w-0 flex-1">
							<div class="truncate font-medium text-ink">{ws.name}</div>
							<span class="mt-0.5 inline-block rounded border px-1.5 py-0.5 text-[10px] font-medium {modeBadgeClass(ws.operatingMode)}">
								{t(`workspaces.mode.${ws.operatingMode}`)}
							</span>
						</div>
						{#if ws.id === currentWorkspace.id}
							<span class="h-2 w-2 shrink-0 rounded-full bg-claytile"></span>
						{/if}
					</button>
				{/each}
			</div>

			{#if canManage}
				<div class="mt-1.5 border-t border-stone pt-1.5">
					<a
						href="/app/onboarding?mode=create"
						onclick={() => (open = false)}
						class="flex items-center gap-2 rounded-lg px-2.5 py-1.5 text-xs font-medium text-claytile transition hover:bg-ash/60"
					>
						<span>+ {t('workspaces.createNew')}</span>
					</a>
				</div>
			{/if}
		</div>
	{/if}
</div>
