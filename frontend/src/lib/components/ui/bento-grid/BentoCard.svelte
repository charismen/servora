<script lang="ts">
	import type { Snippet } from 'svelte';

	let {
		name,
		description,
		Icon,
		background,
		href,
		cta = 'Learn more',
		class: className = '',
		children
	}: {
		name: string;
		description: string;
		Icon?: any; // Icon string (material symbol name) or Svelte Component
		background?: Snippet; // Background graphic / animation snippet
		href?: string;
		cta?: string;
		class?: string;
		children?: Snippet;
	} = $props();
</script>

<div
	data-bento
	class="group relative col-span-3 flex flex-col justify-between overflow-hidden rounded-3xl bg-white dark:bg-black border border-black/10 dark:border-white/10 [box-shadow:0_0_0_1px_rgba(0,0,0,0.03),0_2px_4px_rgba(0,0,0,0.05),0_12px_24px_rgba(0,0,0,0.05)] transition-all duration-300 hover:shadow-xl {className}"
>
	{#if background}
		<div class="absolute inset-0 z-0">
			{@render background()}
		</div>
	{/if}

	<div class="pointer-events-none z-10 flex transform-gpu flex-col gap-1 p-6 transition-all duration-300 group-hover:-translate-y-10">
		{#if Icon}
			{#if typeof Icon === 'string'}
				<span class="material-symbols-outlined h-12 w-12 text-neutral-700 dark:text-neutral-300 text-[24px] flex items-center justify-center rounded-lg bg-neutral-100 dark:bg-neutral-800 transition-all duration-300 group-hover:bg-neutral-200">
					{Icon}
				</span>
			{:else}
				<div class="h-12 w-12 text-neutral-700 dark:text-neutral-300 flex items-center justify-center rounded-lg bg-neutral-100 dark:bg-neutral-800 transition-all duration-300 group-hover:bg-neutral-200">
					<Icon class="h-6 w-6" />
				</div>
			{/if}
		{/if}
		<h3 class="text-xl font-semibold text-neutral-700 dark:text-neutral-300 mt-4">
			{name}
		</h3>
		<p class="max-w-[32rem] text-neutral-400 text-sm">
			{description}
		</p>
	</div>

	<div class="pointer-events-none absolute bottom-0 flex w-full translate-y-10 transform-gpu flex-row items-center p-4 opacity-0 transition-all duration-300 group-hover:translate-y-0 group-hover:opacity-100 z-20">
		<a
			href={href}
			class="pointer-events-auto inline-flex items-center gap-1 text-sm font-semibold text-neutral-700 dark:text-neutral-300 hover:underline"
		>
			{cta}
			<span class="material-symbols-outlined text-[16px] transition-transform duration-300 group-hover:translate-x-1">arrow_forward</span>
		</a>
	</div>
	
	<div class="pointer-events-none absolute inset-0 transform-gpu transition-all duration-300 group-hover:bg-black/[0.03] dark:group-hover:bg-white/[0.03] z-10"></div>
</div>
