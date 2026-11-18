<script lang="ts">
	import { onMount } from 'svelte';

	let { circleSize = 400, circleClassName = 'bg-white' }: { circleSize?: number; circleClassName?: string } = $props();

	let containerEl: HTMLElement | null = $state(null);
	let x = $state(0);
	let y = $state(0);
	let hovered = $state(false);

	onMount(() => {
		const parent = containerEl?.parentElement;
		if (!parent) return;

		const handleMouseMove = (e: MouseEvent) => {
			const rect = parent.getBoundingClientRect();
			x = e.clientX - rect.left;
			y = e.clientY - rect.top;
		};

		const handleMouseEnter = () => {
			hovered = true;
		};

		const handleMouseLeave = () => {
			hovered = false;
		};

		parent.addEventListener('mousemove', handleMouseMove);
		parent.addEventListener('mouseenter', handleMouseEnter);
		parent.addEventListener('mouseleave', handleMouseLeave);

		// Ensure parent container is positioned relatively and clips overflow
		if (getComputedStyle(parent).position === 'static') {
			parent.style.position = 'relative';
		}
		parent.style.overflow = 'hidden';

		return () => {
			parent.removeEventListener('mousemove', handleMouseMove);
			parent.removeEventListener('mouseenter', handleMouseEnter);
			parent.removeEventListener('mouseleave', handleMouseLeave);
		};
	});
</script>

<div
	bind:this={containerEl}
	class="absolute inset-0 pointer-events-none z-0 overflow-hidden {circleClassName}"
	style="
		clip-path: circle({hovered ? circleSize / 2 : 0}px at {x}px {y}px);
		transition: clip-path 0.2s cubic-bezier(0.25, 1, 0.5, 1);
	"
></div>
