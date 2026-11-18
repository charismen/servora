<script lang="ts">
	import { untrack } from 'svelte';
	import { createTranslator, type Locale } from '$lib/i18n';
	import type {
		InspectionChecklistTemplate,
		InspectionItemStatus,
		TicketInspectionDto
	} from '$lib/types/blueprints';

	export type InspectionEntry = {
		checklistTemplateId: number | null;
		label: string;
		status: InspectionItemStatus;
		notes: string;
		isRequired?: boolean;
	};

	let {
		templates = [],
		existingItems = [],
		entries = $bindable<InspectionEntry[]>([]),
		locale = 'en',
		compactHeader = false
	}: {
		templates?: InspectionChecklistTemplate[];
		existingItems?: TicketInspectionDto[];
		entries?: InspectionEntry[];
		locale: Locale;
		compactHeader?: boolean;
	} = $props();

	const t = $derived(createTranslator(locale));
	let seededKey = $state('');

	function buildInitialEntries(
		tpls: InspectionChecklistTemplate[],
		existing: TicketInspectionDto[],
		loc: Locale
	): InspectionEntry[] {
		const byTemplateId = new Map<number, TicketInspectionDto>();
		const byLabel = new Map<string, TicketInspectionDto>();
		for (const item of existing) {
			if (item.checklistTemplateId != null) {
				byTemplateId.set(item.checklistTemplateId, item);
			} else {
				byLabel.set(item.label.toLowerCase(), item);
			}
		}

		const merged: InspectionEntry[] = tpls.map((tpl) => {
			const localizedLabel = loc === 'id' ? tpl.labelId || tpl.labelEn : tpl.labelEn || tpl.labelId;
			const match = byTemplateId.get(tpl.id) ?? byLabel.get(localizedLabel.toLowerCase());
			return {
				checklistTemplateId: tpl.id,
				label: localizedLabel,
				status: (match?.status as InspectionItemStatus) || 'Unchecked',
				notes: match?.notes ?? '',
				isRequired: tpl.isRequired
			};
		});

		const activeTemplateIds = new Set(tpls.map((tpl) => tpl.id));
		for (const item of existing) {
			const alreadyIncluded =
				(item.checklistTemplateId != null && activeTemplateIds.has(item.checklistTemplateId)) ||
				merged.some((entry) => entry.label.toLowerCase() === item.label.toLowerCase());
			if (!alreadyIncluded) {
				merged.push({
					checklistTemplateId: item.checklistTemplateId,
					label: item.label,
					status: (item.status as InspectionItemStatus) || 'Unchecked',
					notes: item.notes ?? ''
				});
			}
		}

		return merged;
	}

	$effect(() => {
		const key = `${locale}:${templates.map((t) => t.id).join(',')}:${existingItems.map((e) => `${e.id}:${e.status}`).join(',')}`;
		if (key !== seededKey && (templates.length > 0 || existingItems.length > 0)) {
			seededKey = key;
			untrack(() => {
				if (entries.length === 0 || existingItems.length > 0) {
					entries = buildInitialEntries(templates, existingItems, locale);
				}
			});
		}
	});

	const statuses: {
		value: InspectionItemStatus;
		symbol: string;
		labelKey: string;
		activeClass: string;
	}[] = [
		{
			value: 'Pass',
			symbol: '✓',
			labelKey: 'tickets.inspection.pass',
			activeClass: 'bg-emerald-600 text-white shadow-xs'
		},
		{
			value: 'Issue',
			symbol: '✕',
			labelKey: 'tickets.inspection.fail',
			activeClass: 'bg-rose-600 text-white shadow-xs'
		},
		{
			value: 'Unchecked',
			symbol: '?',
			labelKey: 'tickets.inspection.untested',
			activeClass: 'bg-slate-700 text-white shadow-xs'
		},
		{
			value: 'NotApplicable',
			symbol: '-',
			labelKey: 'tickets.inspection.na',
			activeClass: 'bg-stone-500 text-white shadow-xs'
		}
	];
</script>

{#if entries.length > 0}
	<div class="space-y-4">
		<div class="flex flex-col gap-2 {compactHeader ? '' : 'border-b border-stone pb-3'} sm:flex-row sm:items-start sm:justify-between">
			{#if !compactHeader}
				<div>
					<h3 class="font-headline-sm text-headline-sm text-ink font-semibold">
						{t('tickets.inspection.title')}
					</h3>
					<p class="text-caption text-slate-600 mt-0.5">
						{t('tickets.inspection.subtitle')}
					</p>
				</div>
			{/if}
			<div class="flex flex-wrap items-center gap-3 text-xs text-slate-600">
				<span class="inline-flex items-center gap-1.5">
					<span class="h-2.5 w-2.5 rounded-full bg-emerald-600"></span>
					{t('tickets.inspection.pass')}
				</span>
				<span class="inline-flex items-center gap-1.5">
					<span class="h-2.5 w-2.5 rounded-full bg-rose-600"></span>
					{t('tickets.inspection.fail')}
				</span>
				<span class="inline-flex items-center gap-1.5">
					<span class="h-2.5 w-2.5 rounded-full bg-slate-700"></span>
					{t('tickets.inspection.untested')}
				</span>
				<span class="inline-flex items-center gap-1.5">
					<span class="h-2.5 w-2.5 rounded-full bg-stone-400"></span>
					{t('tickets.inspection.na')}
				</span>
			</div>
		</div>

		<div class="grid grid-cols-1 gap-3 md:grid-cols-2">
			{#each entries as item, idx (`${item.checklistTemplateId ?? 'hist'}-${item.label}`)}
				<div class="rounded-xl border border-stone/80 bg-white p-3.5 shadow-2xs space-y-2.5">
					<div class="flex items-center justify-between gap-2">
						<span class="text-sm font-semibold text-ink">
							{item.label}
							{#if item.isRequired}
								<span class="text-claytile">*</span>
							{/if}
						</span>
						<div class="inline-flex shrink-0 items-center rounded-lg border border-stone bg-ash/50 p-0.5">
							{#each statuses as opt}
								<button
									type="button"
									title={t(opt.labelKey)}
									aria-label={`${item.label}: ${t(opt.labelKey)}`}
									onclick={() => {
										entries[idx].status = opt.value;
									}}
									class="flex h-6 w-6 items-center justify-center rounded-md text-xs font-bold transition {item.status === opt.value
										? opt.activeClass
										: 'text-slate-600 hover:bg-white hover:text-ink'}"
								>
									{opt.symbol}
								</button>
							{/each}
						</div>
					</div>
					<input
						type="text"
						bind:value={entries[idx].notes}
						placeholder={t('tickets.inspection.notesPlaceholder')}
						class="w-full rounded-lg border border-stone/70 bg-surface/60 px-2.5 py-1.5 text-xs text-ink placeholder:text-slate-400 focus:border-claytile focus:bg-white focus:outline-none"
					/>
				</div>
			{/each}
		</div>
	</div>
	<input type="hidden" name="inspectionsJson" value={JSON.stringify(entries)} />
{/if}
