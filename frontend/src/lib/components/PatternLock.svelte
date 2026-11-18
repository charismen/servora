<script lang="ts">
	let {
		value = $bindable(''),
		readonly = false,
		size = 200,
		clearLabel = 'Clear'
	}: {
		value?: string;
		readonly?: boolean;
		size?: number;
		clearLabel?: string;
	} = $props();

	// Coordinates for the 3x3 grid dots inside a viewBox of 300x300
	const DOT_COORDS: Record<number, { x: number; y: number }> = {
		1: { x: 50, y: 50 },
		2: { x: 150, y: 50 },
		3: { x: 250, y: 50 },
		4: { x: 50, y: 150 },
		5: { x: 150, y: 150 },
		6: { x: 250, y: 150 },
		7: { x: 50, y: 250 },
		8: { x: 150, y: 250 },
		9: { x: 250, y: 250 }
	};

	const dots = [1, 2, 3, 4, 5, 6, 7, 8, 9];

	const selectedPoints = $derived(
		value
			? value
					.split('-')
					.map((v) => parseInt(v.trim(), 10))
					.filter((v) => v >= 1 && v <= 9)
			: []
	);

	function togglePoint(dot: number) {
		if (readonly) return;
		const current = [...selectedPoints];
		const index = current.indexOf(dot);
		if (index >= 0) {
			// If already clicked, truncate up to this point or remove it if it's the last one
			if (index === current.length - 1) {
				current.pop();
			} else {
				current.splice(index + 1);
			}
		} else {
			current.push(dot);
		}
		value = current.join('-');
	}

	function clear() {
		if (readonly) return;
		value = '';
	}

	const pathD = $derived.by(() => {
		if (selectedPoints.length < 2) return '';
		return selectedPoints
			.map((pt, i) => {
				const coord = DOT_COORDS[pt];
				return `${i === 0 ? 'M' : 'L'} ${coord.x} ${coord.y}`;
			})
			.join(' ');
	});
</script>

<div class="inline-flex flex-col items-center gap-2 select-none">
	<div
		class="relative rounded-xl border border-stone bg-ash/40 p-2 shadow-inner"
		style="width: {size}px; height: {size}px;"
	>
		<svg
			viewBox="0 0 300 300"
			class="w-full h-full"
			xmlns="http://www.w3.org/2000/svg"
		>
			<!-- Connecting Line -->
			{#if pathD}
				<path
					d={pathD}
					fill="none"
					stroke="#0284c7"
					stroke-width="8"
					stroke-linecap="round"
					stroke-linejoin="round"
					class="transition-all duration-150"
				/>
			{/if}

			<!-- Grid Dots -->
			{#each dots as dot}
				{@const coord = DOT_COORDS[dot]}
				{@const orderIndex = selectedPoints.indexOf(dot)}
				{@const isSelected = orderIndex >= 0}
				<!-- Outer target ring -->
				<!-- svelte-ignore a11y_click_events_have_key_events -->
				<!-- svelte-ignore a11y_no_static_element_interactions -->
				<g
					class="{readonly ? 'cursor-default' : 'cursor-pointer hover:opacity-80'}"
					onclick={() => togglePoint(dot)}
				>
					<circle
						cx={coord.x}
						cy={coord.y}
						r="30"
						fill="transparent"
					/>
					<circle
						cx={coord.x}
						cy={coord.y}
						r={isSelected ? '22' : '14'}
						fill={isSelected ? '#0284c7' : '#cbd5e1'}
						stroke={isSelected ? '#38bdf8' : '#94a3b8'}
						stroke-width={isSelected ? '4' : '2'}
						class="transition-all duration-150"
					/>
					{#if isSelected}
						<text
							x={coord.x}
							y={coord.y + 5}
							text-anchor="middle"
							fill="#ffffff"
							font-size="16"
							font-weight="bold"
						>
							{orderIndex + 1}
						</text>
					{/if}
				</g>
			{/each}
		</svg>
	</div>

	{#if !readonly}
		<div class="flex items-center gap-2 text-xs">
			{#if selectedPoints.length > 0}
				<span class="font-mono text-secondary font-semibold">
					{value}
				</span>
				<button
					type="button"
					onclick={clear}
					class="px-2 py-0.5 rounded border border-error/30 bg-error/10 text-error hover:bg-error/20 font-semibold cursor-pointer"
				>
					{clearLabel}
				</button>
			{:else}
				<span class="text-overcast italic">1-2-3 / 4-5-6 / 7-8-9</span>
			{/if}
		</div>
	{/if}
</div>

