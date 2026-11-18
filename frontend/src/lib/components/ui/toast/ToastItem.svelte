<script lang="ts">
	import { fly, fade } from 'svelte/transition';
	import { quintOut } from 'svelte/easing';
	import type { ToastItem as ToastItemType } from '$lib/stores/toast.svelte';

	let {
		item,
		onDismiss
	}: {
		item: ToastItemType;
		onDismiss: (id: string) => void;
	} = $props();

	const variantStyles = {
		success: 'border-emerald-200 bg-white/95 text-emerald-950 shadow-emerald-950/5',
		error: 'border-rose-200 bg-white/95 text-rose-950 shadow-rose-950/5',
		warning: 'border-amber-200 bg-white/95 text-amber-950 shadow-amber-950/5',
		info: 'border-stone bg-white/95 text-ink shadow-slate-950/5'
	};

	const iconStyles = {
		success: 'bg-emerald-50 text-emerald-700 border-emerald-200/80',
		error: 'bg-rose-50 text-rose-700 border-rose-200/80',
		warning: 'bg-amber-50 text-amber-800 border-amber-200/80',
		info: 'bg-ash text-secondary border-stone/80'
	};

	const iconNames = {
		success: 'check_circle',
		error: 'error',
		warning: 'warning',
		info: 'info'
	};
</script>

<div
	transition:fly={{ y: 16, duration: 240, easing: quintOut }}
	class="pointer-events-auto flex items-start gap-3 rounded-xl border p-3.5 shadow-lg backdrop-blur-md transition-all select-none {variantStyles[item.variant]}"
	role={item.variant === 'error' ? 'alert' : 'status'}
	aria-live={item.variant === 'error' ? 'assertive' : 'polite'}
>
	<!-- Semantic status icon badge -->
	<div class="w-7 h-7 rounded-lg border flex items-center justify-center shrink-0 mt-0.5 shadow-2xs {iconStyles[item.variant]}">
		<span class="material-symbols-outlined text-[17px] leading-none">
			{iconNames[item.variant]}
		</span>
	</div>

	<!-- Text content -->
	<div class="flex-1 min-w-0 pt-0.5 space-y-0.5">
		{#if item.title}
			<h4 class="text-body-sm font-semibold leading-tight text-ink">
				{item.title}
			</h4>
		{/if}
		<p class="text-body-sm text-secondary leading-snug break-words">
			{item.message}
		</p>
	</div>

	<!-- Dismiss button -->
	{#if item.dismissible}
		<button
			type="button"
			class="shrink-0 p-1 rounded-full text-secondary hover:text-ink hover:bg-stone/30 transition-colors cursor-pointer"
			onclick={() => onDismiss(item.id)}
			aria-label="Dismiss"
		>
			<span class="material-symbols-outlined text-[16px] block">close</span>
		</button>
	{/if}
</div>
