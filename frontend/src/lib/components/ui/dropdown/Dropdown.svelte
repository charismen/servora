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
	let triggerEl = $state<HTMLButtonElement | null>(null);
	let menuEl = $state<HTMLDivElement | null>(null);
	let menuStyle = $state('');

	let selectedLabel = $derived(
		options.find((opt) => opt.value === value)?.label ?? placeholder
	);

	let filteredOptions = $derived(
		searchQuery
			? options.filter((opt) => opt.label.toLowerCase().includes(searchQuery.toLowerCase()))
			: options
	);

	function updateMenuPosition() {
		if (!triggerEl || typeof window === 'undefined') return;
		const rect = triggerEl.getBoundingClientRect();
		const viewportW = window.innerWidth;
		const viewportH = window.innerHeight;
		const margin = 8;
		const gap = 4;

		const width = Math.min(Math.max(rect.width, 160), viewportW - margin * 2);
		let left = align === 'right' ? rect.right - width : rect.left;
		left = Math.max(margin, Math.min(left, viewportW - width - margin));

		const spaceBelow = viewportH - rect.bottom - margin - gap;
		const spaceAbove = rect.top - margin - gap;
		const estimatedHeight = Math.min(options.length * 36 + (searchable ? 48 : 16), 280);
		const openUpward = spaceBelow < Math.min(estimatedHeight, 200) && spaceAbove > spaceBelow;

		if (openUpward) {
			const bottom = viewportH - rect.top + gap;
			menuStyle = `position:fixed;left:${left}px;bottom:${bottom}px;width:${width}px;z-index:9999;`;
		} else {
			const top = rect.bottom + gap;
			menuStyle = `position:fixed;left:${left}px;top:${top}px;width:${width}px;z-index:9999;`;
		}
	}

	$effect(() => {
		if (!isOpen) {
			searchQuery = '';
			return;
		}

		updateMenuPosition();

		const handleReposition = () => updateMenuPosition();
		const handlePointerDown = (event: MouseEvent) => {
			const target = event.target as Node | null;
			if (!target) return;
			if (triggerEl?.contains(target) || menuEl?.contains(target)) return;
			isOpen = false;
		};
		const handleKeydown = (event: KeyboardEvent) => {
			if (event.key === 'Escape') {
				event.preventDefault();
				event.stopPropagation();
				isOpen = false;
				triggerEl?.focus();
			}
		};

		window.addEventListener('resize', handleReposition);
		window.addEventListener('scroll', handleReposition, true);
		document.addEventListener('mousedown', handlePointerDown, true);
		window.addEventListener('keydown', handleKeydown, true);

		return () => {
			window.removeEventListener('resize', handleReposition);
			window.removeEventListener('scroll', handleReposition, true);
			document.removeEventListener('mousedown', handlePointerDown, true);
			window.removeEventListener('keydown', handleKeydown, true);
		};
	});

	function selectOption(optValue: string) {
		value = optValue;
		isOpen = false;
		if (onchange) {
			onchange(optValue);
		}
	}

	// Portal action: moves menu element into document.body so parent overflow:hidden/auto never clips it
	const portal: Action<HTMLElement, void> = (node) => {
		if (typeof document !== 'undefined') {
			document.body.appendChild(node);
		}
		return {
			destroy() {
				if (node.parentNode) {
					node.parentNode.removeChild(node);
				}
			}
		};
	};
</script>

<div class="relative w-full min-w-0 {className}">
	{#if name}
		<input type="hidden" {name} {value} />
	{/if}

	<button
		bind:this={triggerEl}
		type="button"
		{disabled}
		aria-haspopup="listbox"
		aria-expanded={isOpen}
		onclick={(e) => {
			if (disabled) return;
			e.stopPropagation();
			isOpen = !isOpen;
		}}
		class="w-full min-w-0 flex items-center justify-between gap-1.5 border border-stone bg-white rounded-lg text-left text-primary hover:border-outline focus:ring-3 focus:ring-focus-blue/20 outline-none transition-all select-none disabled:bg-ash/50 disabled:text-overcast disabled:border-stone/60 disabled:cursor-not-allowed cursor-pointer {size === 'sm' ? 'h-8 px-2.5 py-1 text-xs font-medium' : 'h-10 px-3 py-2 text-body-sm'} {buttonClass}"
	>
		<span class="truncate min-w-0">{label ?? selectedLabel}</span>
		<span class="material-symbols-outlined shrink-0 text-secondary transition-transform duration-200 {size === 'sm' ? 'text-[16px]' : 'text-[18px]'} {isOpen ? 'rotate-180' : ''}" aria-hidden="true">
			keyboard_arrow_down
		</span>
	</button>

	{#if isOpen}
		<div
			bind:this={menuEl}
			use:portal
			style={menuStyle}
			role="listbox"
			class="bg-white border border-stone rounded-xl shadow-2xl p-1.5 flex flex-col select-none"
		>
			{#if searchable}
				<div class="px-2 py-1.5 border-b border-stone mb-1 flex items-center gap-xs">
					<span class="material-symbols-outlined text-[16px] text-overcast" aria-hidden="true">search</span>
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

			<div class="max-h-60 overflow-y-auto space-y-0.5 pr-0.5 custom-scrollbar">
				{#each filteredOptions as option}
					{@const isSelected = value === option.value}
					<button
						type="button"
						role="option"
						aria-selected={isSelected}
						onclick={() => selectOption(option.value)}
						class="w-full flex items-center justify-between gap-2 px-3 py-2 rounded-lg text-left text-xs font-semibold transition-all cursor-pointer
							{isSelected ? 'bg-ash text-primary font-bold' : 'text-secondary hover:bg-ash/50 hover:text-primary'}"
					>
						<span class="truncate">{option.label}</span>
						{#if isSelected}
							<span class="material-symbols-outlined text-primary text-[16px] shrink-0" aria-hidden="true">check</span>
						{/if}
					</button>
				{:else}
					<p class="px-3 py-2 text-xs text-overcast">{noOptionsLabel}</p>
				{/each}
			</div>
		</div>
	{/if}
</div>
