<script lang="ts">
	import type { Snippet } from 'svelte';
	import Breadcrumbs from '$lib/components/Breadcrumbs.svelte';

	type BreadcrumbItem = {
		label: string;
		href?: string;
	};

	let {
		items,
		title,
		subtitle,
		actions
	}: {
		items: BreadcrumbItem[];
		title: string;
		subtitle?: string;
		actions?: Snippet;
	} = $props();
</script>

<div class="flex flex-col gap-xs sm:gap-sm">
	<Breadcrumbs {items} />
	<div class="flex flex-wrap items-start justify-between gap-sm sm:gap-md">
		<div class="min-w-[260px] sm:min-w-[320px] flex-1 max-w-full">
			<h2 class="font-headline-sm text-headline-sm text-primary font-bold leading-tight mb-0.5">{title}</h2>
			{#if subtitle}
				<p class="text-body-sm text-secondary leading-normal">{subtitle}</p>
			{/if}
		</div>
		{#if actions}
			<div class="flex flex-wrap items-center gap-xs sm:gap-sm justify-start sm:justify-end shrink-0 max-w-full">
				{@render actions()}
			</div>
		{/if}
	</div>
</div>
