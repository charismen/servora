<script lang="ts">
	import type { Action } from 'svelte/action';

	let {
		options = [],
		value = $bindable(''),
		placeholder = '',
		class: className = '',
		buttonClass = '',
		name = '',
		onchange,
		label,
		searchable = false,
		searchPlaceholder = '',
		noOptionsLabel = '',
		align = 'left',
		size = 'default',
		disabled = false
	}: {
		options: { value: string; label: string }[];
		value?: string;
		placeholder?: string;
		class?: string;
		buttonClass?: string;
		name?: string;
		onchange?: (val: string) => void;
		label?: string;
		searchable?: boolean;
		searchPlaceholder?: string;
		noOptionsLabel?: string;
		align?: 'left' | 'right';
		size?: 'default' | 'sm';
		disabled?: boolean;
	} = $props();

	let isOpen = $state(false);
	let searchQuery = $state('');

	let selectedLabel = $derived(
		options.find(opt => opt.value === value)?.label ?? placeholder
	);

	let filteredOptions = $derived(
		searchQuery
			? options.filter(opt => opt.label.toLowerCase().includes(searchQuery.toLowerCase()))
			: options
	);

	$effect(() => {
		if (!isOpen) {
			searchQuery = '';
		}
	});

	function selectOption(optValue: string) {
		value = optValue;
		isOpen = false;
		if (onchange) {
			onchange(optValue);
		}
	}

	// Click outside action
	const clickOutside: Action<HTMLElement, void> = (node) => {
		const handleClick = (event: MouseEvent) => {
			if (node && !node.contains(event.target as Node) && !event.defaultPrevented) {
				isOpen = false;
			}
		};

		document.addEventListener('click', handleClick, true);

		return {
			destroy() {
				document.removeEventListener('click', handleClick, true);
			}
		};
	};
</script>

<div class="relative w-full min-w-0 {className} {isOpen ? 'z-40' : ''}" use:clickOutside>
	<input type="hidden" {name} {value} />

	<!-- Trigger button -->
	<button
		type="button"
		{disabled}
		onclick={(e) => {
			if (disabled) return;
			e.stopPropagation();
			isOpen = !isOpen;
		}}
		class="w-full min-w-0 flex items-center justify-between gap-1.5 border border-stone bg-white rounded-lg text-left text-primary hover:border-outline focus:ring-3 focus:ring-focus-blue/20 outline-none transition-all select-none disabled:bg-ash/50 disabled:text-overcast disabled:border-stone/60 disabled:cursor-not-allowed cursor-pointer {size === 'sm' ? 'h-8 px-2.5 py-1 text-xs font-medium' : 'h-10 px-3 py-2 text-body-sm'} {buttonClass}"
	>
		<span class="truncate min-w-0">{label ?? selectedLabel}</span>
		<span class="material-symbols-outlined shrink-0 text-secondary transition-transform duration-200 {size === 'sm' ? 'text-[16px]' : 'text-[18px]'} {isOpen ? 'rotate-180' : ''}">
			keyboard_arrow_down
		</span>
	</button>

	{#if isOpen}
		<!-- Dropdown List Container (ponytail: native select dropdown panel replacement) -->
		<!-- ponytail: right-0 when align='right'; left-0 when align='left' -->
		<div class="absolute {size === 'sm' ? 'top-9' : 'top-11'} {align === 'right' ? 'right-0 left-auto' : 'left-0 right-auto'} z-50 w-full min-w-[160px] max-w-[calc(100vw-1rem)] bg-white border border-stone rounded-xl shadow-xl p-1.5 flex flex-col select-none transition-all">
			{#if searchable}
				<div class="px-2 py-1.5 border-b border-stone mb-1 flex items-center gap-xs">
					<span class="material-symbols-outlined text-[16px] text-overcast">search</span>
					<input
						type="text"
						bind:value={searchQuery}
						placeholder={searchPlaceholder}
						class="w-full text-xs bg-transparent outline-none text-primary"
						onclick={(e) => e.stopPropagation()}
						onkeydown={(e) => {
							if (e.key === ' ') e.stopPropagation();
						}}
					/>
				</div>
			{/if}

			<div class="max-h-60 overflow-y-auto space-y-0.5 pr-0.5">
				{#each filteredOptions as option}
					{@const isSelected = value === option.value}
					<button
						type="button"
						onclick={() => selectOption(option.value)}
						class="w-full flex items-center justify-between px-3 py-2 rounded-lg text-left text-xs font-semibold transition-all cursor-pointer
							{isSelected ? 'bg-ash text-primary font-bold' : 'text-secondary hover:bg-ash/50 hover:text-primary'}"
					>
						<span class="truncate">{option.label}</span>
						{#if isSelected}
							<span class="material-symbols-outlined text-primary text-[16px]">check</span>
						{/if}
					</button>
				{:else}
					<p class="px-3 py-2 text-xs text-overcast">{noOptionsLabel}</p>
				{/each}
			</div>
		</div>
	{/if}
</div>
