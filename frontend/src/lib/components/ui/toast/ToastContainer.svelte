<script lang="ts">
	import { toast } from '$lib/stores/toast.svelte';
	import ToastItem from './ToastItem.svelte';
</script>

{#if toast.toasts.length > 0}
	<div
		class="fixed bottom-5 right-5 sm:bottom-6 sm:right-6 z-50 flex flex-col-reverse gap-2.5 w-[calc(100vw-2.5rem)] sm:w-[380px] pointer-events-none"
		aria-label="Notifications"
	>
		{#each toast.toasts as item (item.id)}
			<ToastItem {item} onDismiss={(id) => toast.dismiss(id)} />
		{/each}
	</div>
{/if}
