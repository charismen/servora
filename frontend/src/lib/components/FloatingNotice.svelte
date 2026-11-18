<script lang="ts">
	import { fly } from 'svelte/transition';
	import { quintOut } from 'svelte/easing';

	type Variant = 'default' | 'success' | 'error' | 'warning' | 'info';

	interface ActionButton {
		label: string;
		onClick: () => void;
		variant?: 'default' | 'outline' | 'ghost';
	}

	let {
		message,
		title,
		variant = 'info',
		dismissLabel = 'Dismiss',
		actions,
		highlightTitle = false,
		onDismiss
	}: {
		message: string;
		title?: string;
		variant?: Variant;
		dismissLabel?: string;
		actions?: ActionButton;
		highlightTitle?: boolean;
		onDismiss?: () => void;
	} = $props();

	const variantStyles = {
		default: 'border-stone bg-white/95 text-ink shadow-slate-950/5',
		info: 'border-stone bg-white/95 text-ink shadow-slate-950/5',
		success: 'border-emerald-200 bg-white/95 text-emerald-950 shadow-emerald-950/5',
		error: 'border-rose-200 bg-white/95 text-rose-950 shadow-rose-950/5',
		warning: 'border-amber-200 bg-white/95 text-amber-950 shadow-amber-950/5'
	};

	const iconStyles = {
		default: 'bg-ash text-secondary border-stone/80',
		info: 'bg-ash text-secondary border-stone/80',
		success: 'bg-emerald-50 text-emerald-700 border-emerald-200/80',
		error: 'bg-rose-50 text-rose-700 border-rose-200/80',
		warning: 'bg-amber-50 text-amber-800 border-amber-200/80'
	};

	const iconNames = {
		default: 'info',
		info: 'info',
		success: 'check_circle',
		error: 'error',
		warning: 'warning'
	};
</script>

<div
	transition:fly={{ y: -12, duration: 240, easing: quintOut }}
	class="fixed top-18 sm:top-20 right-4 sm:right-6 z-50 pointer-events-auto flex items-start gap-3 w-[calc(100vw-2rem)] sm:w-[380px] p-3.5 rounded-xl border shadow-lg backdrop-blur-md select-none {variantStyles[variant]}"
	role={variant === 'error' ? 'alert' : 'status'}
	aria-live={variant === 'error' ? 'assertive' : 'polite'}
>
	<div class="w-7 h-7 rounded-lg border flex items-center justify-center shrink-0 mt-0.5 shadow-2xs {iconStyles[variant]}">
		<span class="material-symbols-outlined text-[17px] leading-none">
			{iconNames[variant]}
		</span>
	</div>

	<div class="flex-1 min-w-0 pt-0.5 space-y-0.5">
		{#if title}
			<h4 class="text-body-sm font-semibold leading-tight {highlightTitle ? 'text-emerald-700' : 'text-ink'}">
				{title}
			</h4>
		{/if}
		<p class="text-body-sm text-secondary leading-snug break-words">{message}</p>
	</div>

	<div class="flex items-center gap-1.5 shrink-0 ml-1">
		{#if actions}
			<button
				type="button"
				class="px-2.5 py-1 rounded text-caption font-semibold border border-stone bg-white hover:bg-ash cursor-pointer shadow-2xs transition-colors"
				onclick={() => {
					actions?.onClick();
					onDismiss?.();
				}}
			>
				{actions.label}
			</button>
		{/if}

		{#if onDismiss}
			<button
				type="button"
				onclick={onDismiss}
				class="shrink-0 p-1 rounded-full text-secondary hover:text-ink hover:bg-stone/30 transition-colors cursor-pointer"
				aria-label={dismissLabel}
			>
				<span class="material-symbols-outlined text-[16px] block">close</span>
			</button>
		{/if}
	</div>
</div>
