<script lang="ts">
	import { page } from '$app/stores';
	import { createTranslator, type Locale } from '$lib/i18n';

	type BreadcrumbItem = {
		label: string;
		href?: string;
	};

	let { items }: { items: BreadcrumbItem[] } = $props();
	const t = $derived(createTranslator(($page.data.locale ?? 'en') as Locale));
</script>

<nav class="flex flex-wrap items-center gap-1.5 text-[11px] font-bold text-secondary uppercase tracking-widest font-label-caps mb-xs select-none" aria-label={t('common.breadcrumb')}>
	{#each items as item, index}
		{#if index > 0}
			<span class="text-overcast/60 text-[10px] mx-0.5">/</span>
		{/if}

		{#if item.href && index < items.length - 1}
			<a href={item.href} class="hover:text-primary transition-colors duration-150">{item.label}</a>
		{:else}
			<span class="text-primary/95">{item.label}</span>
		{/if}
	{/each}
</nav>
