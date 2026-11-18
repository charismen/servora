<script lang="ts">
	import type { Snippet } from 'svelte';

	let { children, class: className = '' }: { children: Snippet; class?: string } = $props();
</script>

<div class="grid w-full auto-rows-[22rem] grid-cols-3 gap-4 {className}">
	{@render children()}
</div>
