<script lang="ts">
	let {
		variant = 'neutral',
		label,
		dot = true,
		class: className = ''
	}: {
		variant?: 'success' | 'warning' | 'error' | 'info' | 'neutral';
		label: string;
		dot?: boolean;
		class?: string;
	} = $props();

	const variantStyles = {
		success: {
			badge: 'bg-emerald-500/10 border-emerald-300 text-emerald-800',
			dot: 'bg-emerald-600'
		},
		warning: {
			badge: 'bg-amber-500/10 border-amber-300 text-amber-800',
			dot: 'bg-amber-600'
		},
		error: {
			badge: 'bg-rose-500/10 border-rose-300 text-rose-800',
			dot: 'bg-rose-600'
		},
		info: {
			badge: 'bg-blue-500/10 border-blue-300 text-blue-800',
			dot: 'bg-blue-600'
		},
		neutral: {
			badge: 'bg-ash/80 border-stone text-secondary',
			dot: 'bg-metal'
		}
	};

	const currentStyle = $derived(variantStyles[variant] || variantStyles.neutral);
</script>

<span
	class="inline-flex items-center gap-1.5 h-6 px-2.5 rounded border text-[11px] font-label-caps font-bold uppercase tracking-wider shrink-0 select-none {currentStyle.badge} {className}"
>
	{#if dot}
		<span class="w-1.5 h-1.5 rounded-full shrink-0 {currentStyle.dot}"></span>
	{/if}
	<span class="truncate">{label}</span>
</span>
