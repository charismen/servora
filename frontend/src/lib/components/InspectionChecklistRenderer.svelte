<script lang="ts">
	import { createTranslator, type Locale } from '$lib/i18n';
	import type { InspectionChecklistTemplate, InspectionItemStatus } from '$lib/types/blueprints';

	export type InspectionEntry = {
		checklistTemplateId: number;
		label: string;
		status: InspectionItemStatus;
		notes: string;
	};

	let {
		templates = [],
		entries = $bindable<InspectionEntry[]>([]),
		locale = 'en'
	}: {
		templates: InspectionChecklistTemplate[];
		entries?: InspectionEntry[];
		locale: Locale;
	} = $props();

	const t = $derived(createTranslator(locale));

	$effect(() => {
		if (templates.length > 0 && entries.length === 0) {
			entries = templates.map((tpl) => ({
				checklistTemplateId: tpl.id,
				label: locale === 'id' ? tpl.labelId || tpl.labelEn : tpl.labelEn || tpl.labelId,
				status: 'Unchecked' as InspectionItemStatus,
				notes: ''
			}));
		}
	});

	const statuses: { value: InspectionItemStatus; labelKey: string; activeClass: string }[] = [
		{ value: 'Pass', labelKey: 'inspection.pass', activeClass: 'bg-emerald-600 text-white border-emerald-600' },
		{ value: 'Issue', labelKey: 'inspection.issue', activeClass: 'bg-amber-600 text-white border-amber-600' },
		{ value: 'NotApplicable', labelKey: 'inspection.na', activeClass: 'bg-slate-600 text-white border-slate-600' }
	];
</script>

{#if entries.length > 0}
	<div class="space-y-2.5">
		{#each entries as item, idx (item.checklistTemplateId)}
			<div class="flex flex-col gap-2 rounded-lg border border-stone bg-white p-3 sm:flex-row sm:items-center sm:justify-between">
				<div class="min-w-0 flex-1">
					<div class="text-sm font-medium text-ink">{item.label}</div>
					<input
						type="text"
						bind:value={entries[idx].notes}
						placeholder={t('inspection.notesPlaceholder')}
						class="mt-1.5 w-full rounded border border-stone/70 bg-ash/40 px-2.5 py-1 text-xs text-ink placeholder:text-slate-400 focus:border-claytile focus:bg-white focus:outline-none"
					/>
				</div>
				<div class="flex shrink-0 items-center gap-1.5">
					{#each statuses as opt}
						<button
							type="button"
							onclick={() => {
								entries[idx].status = entries[idx].status === opt.value ? 'Unchecked' : opt.value;
							}}
							class="rounded-md border px-2.5 py-1 text-xs font-semibold transition {item.status === opt.value
								? opt.activeClass
								: 'border-stone bg-surface text-slate-600 hover:bg-ash'}"
						>
							{t(opt.labelKey)}
						</button>
					{/each}
				</div>
			</div>
		{/each}
	</div>
	<input type="hidden" name="inspectionsJson" value={JSON.stringify(entries)} />
{/if}
