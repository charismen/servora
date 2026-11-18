<script lang="ts">
	let {
		page = 1,
		pageSize = 10,
		totalCount = 0,
		hrefBuilder = (p: number) => `?page=${p}`,
		class: className = '',
		showingLabel = '',
		toLabel = '',
		ofLabel = '',
		itemsLabel = '',
		previousLabel = '',
		nextLabel = ''
	}: {
		page?: number;
		pageSize?: number;
		totalCount?: number;
		hrefBuilder?: (p: number) => string;
		class?: string;
		showingLabel?: string;
		toLabel?: string;
		ofLabel?: string;
		itemsLabel?: string;
		previousLabel?: string;
		nextLabel?: string;
	} = $props();

	let startItem = $derived(totalCount === 0 ? 0 : (page - 1) * pageSize + 1);
	let endItem = $derived(Math.min(page * pageSize, totalCount));
</script>

<div class="flex flex-col sm:flex-row items-center justify-between w-full gap-sm py-sm px-md border-t border-stone bg-ash/50 select-none {className}">
	<div class="font-body-sm text-body-sm text-secondary">
		{showingLabel} <span class="font-bold text-primary">{startItem}</span> {toLabel} <span class="font-bold text-primary">{endItem}</span> {ofLabel} <span class="font-bold text-primary">{totalCount}</span> {itemsLabel}
	</div>
	<div class="flex items-center gap-xs">
		<!-- Previous Button -->
		{#if page > 1}
			<a
				href={hrefBuilder(page - 1)}
				class="h-9 px-sm flex items-center justify-center rounded-lg border border-stone bg-white font-body-sm text-body-sm font-semibold text-secondary hover:bg-ash hover:text-primary transition-all cursor-pointer"
			>
				<span class="material-symbols-outlined text-[18px] mr-1">chevron_left</span>
				<span>{previousLabel}</span>
			</a>
		{:else}
			<button
				disabled
				class="h-9 px-sm flex items-center justify-center rounded-lg border border-stone bg-white font-body-sm text-body-sm font-semibold text-secondary opacity-40 cursor-not-allowed"
			>
				<span class="material-symbols-outlined text-[18px] mr-1">chevron_left</span>
				<span>{previousLabel}</span>
			</button>
		{/if}

		<!-- Next Button -->
		{#if page * pageSize < totalCount}
			<a
				href={hrefBuilder(page + 1)}
				class="h-9 px-sm flex items-center justify-center rounded-lg border border-stone bg-white font-body-sm text-body-sm font-semibold text-secondary hover:bg-ash hover:text-primary transition-all cursor-pointer"
			>
				<span>{nextLabel}</span>
				<span class="material-symbols-outlined text-[18px] ml-1">chevron_right</span>
			</a>
		{:else}
			<button
				disabled
				class="h-9 px-sm flex items-center justify-center rounded-lg border border-stone bg-white font-body-sm text-body-sm font-semibold text-secondary opacity-40 cursor-not-allowed"
			>
				<span>{nextLabel}</span>
				<span class="material-symbols-outlined text-[18px] ml-1">chevron_right</span>
			</button>
		{/if}
	</div>
</div>
