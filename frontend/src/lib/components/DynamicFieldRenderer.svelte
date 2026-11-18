<script lang="ts">
	import type { Locale } from '$lib/i18n';
	import type { CustomFieldDefinition } from '$lib/types/blueprints';

	let {
		fields = [],
		values = $bindable<Record<number, string>>({}),
		locale = 'en',
		namePrefix = 'customField',
		disabled = false
	}: {
		fields: CustomFieldDefinition[];
		values?: Record<number, string>;
		locale: Locale;
		namePrefix?: string;
		disabled?: boolean;
	} = $props();

	function fieldLabel(field: CustomFieldDefinition) {
		return locale === 'id' ? field.labelId || field.labelEn : field.labelEn || field.labelId;
	}
</script>

{#if fields.length > 0}
	<div class="grid gap-4 sm:grid-cols-2">
		{#each fields as field (field.id)}
			<div class={field.fieldType === 'Multiline' ? 'sm:col-span-2' : ''}>
				{#if field.fieldType === 'Boolean'}
					<label class="flex items-center gap-2.5 rounded-lg border border-stone bg-white px-3.5 py-2.5 text-sm font-medium text-ink">
						<input
							type="checkbox"
							name="{namePrefix}_{field.id}"
							checked={values[field.id] === 'true'}
							onchange={(e) => {
								values[field.id] = (e.currentTarget as HTMLInputElement).checked ? 'true' : 'false';
							}}
							{disabled}
							class="h-4 w-4 rounded border-stone text-claytile focus:ring-claytile"
						/>
						<span>{fieldLabel(field)}{#if field.isRequired}<span class="text-red-600"> *</span>{/if}</span>
					</label>
				{:else}
					<label for="{namePrefix}_{field.id}" class="mb-1.5 block text-xs font-semibold uppercase tracking-wide text-slate-600">
						{fieldLabel(field)}{#if field.isRequired}<span class="text-red-600"> *</span>{/if}
					</label>

					{#if field.fieldType === 'Select'}
						<select
							id="{namePrefix}_{field.id}"
							name="{namePrefix}_{field.id}"
							value={values[field.id] ?? ''}
							onchange={(e) => {
								values[field.id] = (e.currentTarget as HTMLSelectElement).value;
							}}
							required={field.isRequired}
							{disabled}
							class="w-full rounded-lg border border-stone bg-white px-3 py-2 text-sm text-ink focus:border-claytile focus:outline-none"
						>
							<option value="">—</option>
							{#each field.options as opt}
								<option value={opt}>{opt}</option>
							{/each}
						</select>
					{:else if field.fieldType === 'Multiline'}
						<textarea
							id="{namePrefix}_{field.id}"
							name="{namePrefix}_{field.id}"
							rows="3"
							value={values[field.id] ?? ''}
							oninput={(e) => {
								values[field.id] = (e.currentTarget as HTMLTextAreaElement).value;
							}}
							required={field.isRequired}
							{disabled}
							class="w-full rounded-lg border border-stone bg-white px-3 py-2 text-sm text-ink focus:border-claytile focus:outline-none"
						></textarea>
					{:else if field.fieldType === 'Number'}
						<input
							id="{namePrefix}_{field.id}"
							name="{namePrefix}_{field.id}"
							type="number"
							step="any"
							value={values[field.id] ?? ''}
							oninput={(e) => {
								values[field.id] = (e.currentTarget as HTMLInputElement).value;
							}}
							required={field.isRequired}
							{disabled}
							class="w-full rounded-lg border border-stone bg-white px-3 py-2 text-sm text-ink focus:border-claytile focus:outline-none"
						/>
					{:else if field.fieldType === 'Date'}
						<input
							id="{namePrefix}_{field.id}"
							name="{namePrefix}_{field.id}"
							type="date"
							value={values[field.id] ?? ''}
							oninput={(e) => {
								values[field.id] = (e.currentTarget as HTMLInputElement).value;
							}}
							required={field.isRequired}
							{disabled}
							class="w-full rounded-lg border border-stone bg-white px-3 py-2 text-sm text-ink focus:border-claytile focus:outline-none"
						/>
					{:else}
						<input
							id="{namePrefix}_{field.id}"
							name="{namePrefix}_{field.id}"
							type="text"
							value={values[field.id] ?? ''}
							oninput={(e) => {
								values[field.id] = (e.currentTarget as HTMLInputElement).value;
							}}
							required={field.isRequired}
							{disabled}
							class="w-full rounded-lg border border-stone bg-white px-3 py-2 text-sm text-ink focus:border-claytile focus:outline-none"
						/>
					{/if}
				{/if}
			</div>
		{/each}
	</div>
{/if}
