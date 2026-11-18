<script lang="ts">
	import type { Snippet } from 'svelte';

	let {
		status = '',
		color = 'slate',
		pulse = true,
		title = '',
		class: className = '',
		children
	}: {
		status?: string;
		color?: 'green' | 'blue' | 'orange' | 'red' | 'purple' | 'slate';
		pulse?: boolean;
		title?: string;
		class?: string;
		children?: Snippet;
	} = $props();

	// Map status values to appropriate color themes
	const statusColors: Record<string, 'green' | 'blue' | 'orange' | 'red' | 'purple' | 'slate'> = {
		received: 'slate',
		new: 'blue',
		inspection: 'blue',
		diagnosing: 'orange',
		repairing: 'orange',
		'in progress': 'purple',
		'waiting parts': 'orange',
		'waiting sparepart': 'orange',
		'waiting approval': 'orange',
		'ready for pickup': 'blue',
		completed: 'green',
		'picked up': 'green',
		resolved: 'green',
		done: 'green',
		cancelled: 'red',
		failed: 'red',
		unpaid: 'red',
		paid: 'green',
		waived: 'green',
		partial: 'orange',
		banned: 'red',
		active: 'green',
		inactive: 'slate'
	};

	// Determine active theme based on status or color override
	const activeColor = $derived(
		status ? (statusColors[status.trim().toLowerCase()] ?? color) : color
	);

	// Theme style mappings (ponytail: native Tailwind glow shadows & border ratios)
	const colorMap = {
		green: {
			bg: 'bg-emerald-500/10',
			text: 'text-emerald-600 dark:text-emerald-400',
			border: 'border-emerald-500/25',
			glow: 'shadow-[0_0_8px_rgba(16,185,129,0.15)]',
			dot: 'bg-emerald-500'
		},
		blue: {
			bg: 'bg-blue-500/10',
			text: 'text-blue-600 dark:text-blue-400',
			border: 'border-blue-500/25',
			glow: 'shadow-[0_0_8px_rgba(59,130,246,0.15)]',
			dot: 'bg-blue-500'
		},
		orange: {
			bg: 'bg-amber-500/10',
			text: 'text-amber-600 dark:text-amber-400',
			border: 'border-amber-500/25',
			glow: 'shadow-[0_0_8px_rgba(245,158,11,0.15)]',
			dot: 'bg-amber-500'
		},
		red: {
			bg: 'bg-rose-500/10',
			text: 'text-rose-600 dark:text-rose-400',
			border: 'border-rose-500/25',
			glow: 'shadow-[0_0_8px_rgba(244,63,94,0.15)]',
			dot: 'bg-rose-500'
		},
		purple: {
			bg: 'bg-violet-500/10',
			text: 'text-violet-600 dark:text-violet-400',
			border: 'border-violet-500/25',
			glow: 'shadow-[0_0_8px_rgba(139,92,246,0.15)]',
			dot: 'bg-violet-500'
		},
		slate: {
			bg: 'bg-slate-500/10',
			text: 'text-slate-600 dark:text-slate-400',
			border: 'border-slate-500/25',
			glow: 'shadow-[0_0_8px_rgba(107,114,128,0.1)]',
			dot: 'bg-slate-500'
		}
	};

	const resolved = $derived(colorMap[activeColor]);
</script>

<div
	{title}
	class="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full border text-[11px] font-bold uppercase tracking-wider transition-all duration-300 {resolved.bg} {resolved.text} {resolved.border} {resolved.glow} {className}"
>
	{#if pulse}
		<span class="relative flex h-2 w-2 shrink-0">
			<span class="animate-ping absolute inline-flex h-full w-full rounded-full opacity-75 {resolved.dot}"></span>
			<span class="relative inline-flex rounded-full h-2 w-2 {resolved.dot}"></span>
		</span>
	{/if}
	{#if children}
		{@render children()}
	{/if}
</div>
