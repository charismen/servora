<script lang="ts">
	import { page } from '$app/stores';
	import { createTranslator, type Locale } from '$lib/i18n';
	import { tick } from 'svelte';
	import type { Action } from 'svelte/action';

	let {
		value = $bindable(''),
		placeholder,
		class: className = '',
		buttonClass = '',
		name = '',
		id,
		required = false,
		disabled = false,
		clearable = false,
		size = 'default',
		align = 'left',
		locale: localeProp,
		onchange
	}: {
		value?: string;
		placeholder?: string;
		class?: string;
		buttonClass?: string;
		name?: string;
		id?: string;
		required?: boolean;
		disabled?: boolean;
		clearable?: boolean;
		size?: 'default' | 'sm';
		align?: 'left' | 'right';
		locale?: Locale;
		onchange?: (val: string) => void;
	} = $props();

	const activeLocale = $derived<Locale>(
		localeProp ?? (($page.data?.locale === 'id' ? 'id' : 'en') as Locale)
	);
	const intlLocale = $derived(activeLocale === 'id' ? 'id-ID' : 'en-US');
	const t = $derived(createTranslator(activeLocale));
	const resolvedPlaceholder = $derived(placeholder || t('picker.selectDate'));

	let isOpen = $state(false);
	let current = $state(new Date());
	let triggerEl: HTMLButtonElement | undefined = $state();
	let popoverEl: HTMLDivElement | undefined = $state();
	let popoverStyle = $state('');

	function parseYmd(dateStr: string): Date | null {
		if (!dateStr) return null;
		const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(dateStr.trim());
		if (match) {
			const y = Number(match[1]);
			const m = Number(match[2]);
			const d = Number(match[3]);
			const parsed = new Date(y, m - 1, d);
			if (parsed.getFullYear() === y && parsed.getMonth() === m - 1 && parsed.getDate() === d) {
				return parsed;
			}
		}
		const fallback = new Date(dateStr);
		return Number.isNaN(fallback.getTime()) ? null : fallback;
	}

	function toYmd(date: Date): string {
		const y = date.getFullYear();
		const m = String(date.getMonth() + 1).padStart(2, '0');
		const d = String(date.getDate()).padStart(2, '0');
		return `${y}-${m}-${d}`;
	}

	$effect(() => {
		const parsed = parseYmd(value);
		if (parsed) {
			current = new Date(parsed.getFullYear(), parsed.getMonth(), 1);
		}
	});

	const displayMonth = $derived(current.getMonth());
	const displayYear = $derived(current.getFullYear());

	const monthLabel = $derived(
		new Intl.DateTimeFormat(intlLocale, { month: 'long', year: 'numeric' }).format(
			new Date(displayYear, displayMonth, 1)
		)
	);

	// Sunday (Jan 4, 2026) through Saturday (Jan 10, 2026)
	const weekdays = $derived.by(() => {
		const fmt = new Intl.DateTimeFormat(intlLocale, { weekday: 'short' });
		return Array.from({ length: 7 }, (_, i) => fmt.format(new Date(2026, 0, 4 + i)).slice(0, 2));
	});

	const daysGrid = $derived.by(() => {
		const firstDayIndex = new Date(displayYear, displayMonth, 1).getDay();
		const daysInMonth = new Date(displayYear, displayMonth + 1, 0).getDate();

		const grid: (Date | null)[] = [];
		for (let i = 0; i < firstDayIndex; i++) {
			grid.push(null);
		}
		for (let d = 1; d <= daysInMonth; d++) {
			grid.push(new Date(displayYear, displayMonth, d));
		}
		return grid;
	});

	function updatePosition() {
		if (!triggerEl) return;
		const rect = triggerEl.getBoundingClientRect();
		const popWidth = Math.min(288, window.innerWidth - 16);
		const popHeight = popoverEl?.offsetHeight || 332;
		const gap = 6;

		let left = align === 'right' ? rect.right - popWidth : rect.left;
		left = Math.max(8, Math.min(left, window.innerWidth - popWidth - 8));

		const spaceBelow = window.innerHeight - rect.bottom;
		const spaceAbove = rect.top;
		let top = rect.bottom + gap;
		if (spaceBelow < popHeight + gap && spaceAbove > popHeight + gap) {
			top = Math.max(8, rect.top - popHeight - gap);
		} else {
			top = Math.min(top, Math.max(8, window.innerHeight - popHeight - 8));
		}

		popoverStyle = `position: fixed; top: ${Math.round(top)}px; left: ${Math.round(left)}px; width: ${popWidth}px;`;
	}

	async function toggleOpen() {
		if (disabled) return;
		isOpen = !isOpen;
		if (isOpen) {
			await tick();
			updatePosition();
		}
	}

	$effect(() => {
		if (!isOpen) return;
		const handleReposition = () => updatePosition();
		const handleKeyDown = (e: KeyboardEvent) => {
			if (e.key === 'Escape') {
				e.stopPropagation();
				isOpen = false;
				triggerEl?.focus();
			}
		};
		window.addEventListener('scroll', handleReposition, true);
		window.addEventListener('resize', handleReposition);
		window.addEventListener('keydown', handleKeyDown, true);
		return () => {
			window.removeEventListener('scroll', handleReposition, true);
			window.removeEventListener('resize', handleReposition);
			window.removeEventListener('keydown', handleKeyDown, true);
		};
	});

	function prevMonth() {
		current = new Date(displayYear, displayMonth - 1, 1);
	}

	function nextMonth() {
		current = new Date(displayYear, displayMonth + 1, 1);
	}

	function selectDay(date: Date) {
		const formatted = toYmd(date);
		value = formatted;
		isOpen = false;
		onchange?.(formatted);
	}

	function selectToday() {
		const today = new Date();
		current = new Date(today.getFullYear(), today.getMonth(), 1);
		selectDay(today);
	}

	function clearDate(e: MouseEvent | undefined = undefined) {
		e?.stopPropagation();
		value = '';
		isOpen = false;
		onchange?.('');
	}

	function formatDateStr(dateStr: string) {
		const date = parseYmd(dateStr);
		if (!date) return '';
		return new Intl.DateTimeFormat(intlLocale, { dateStyle: 'medium' }).format(date);
	}

	const selectedYmd = $derived.by(() => {
		const parsed = parseYmd(value);
		return parsed ? toYmd(parsed) : '';
	});
	const todayYmd = $derived(toYmd(new Date()));

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

<div class="relative w-full {className} {isOpen ? 'z-50' : ''}" use:clickOutside>
	{#if required}
		<input
			type="text"
			{name}
			value={selectedYmd}
			required
			tabindex="-1"
			aria-hidden="true"
			class="sr-only"
			oninvalid={(e) => {
				e.preventDefault();
				triggerEl?.focus();
				if (!isOpen) void toggleOpen();
			}}
		/>
	{:else}
		<input type="hidden" {name} value={selectedYmd} />
	{/if}

	<button
		bind:this={triggerEl}
		{id}
		type="button"
		{disabled}
		aria-haspopup="dialog"
		aria-expanded={isOpen}
		onclick={(e) => {
			e.stopPropagation();
			void toggleOpen();
		}}
		class="w-full flex items-center justify-between gap-2 border border-stone bg-white rounded-lg text-left text-primary hover:border-outline focus:ring-3 focus:ring-focus-blue/20 outline-none transition-all select-none disabled:bg-ash/50 disabled:text-overcast disabled:border-stone/60 disabled:cursor-not-allowed cursor-pointer {size === 'sm'
			? 'h-8 px-2.5 py-1 text-xs font-medium'
			: 'h-10 px-3 py-2 text-body-sm'} {isOpen ? 'border-outline ring-3 ring-focus-blue/20' : ''} {buttonClass}"
	>
		<span class="truncate tabular-nums {selectedYmd ? 'text-primary' : 'text-overcast'}">
			{selectedYmd ? formatDateStr(selectedYmd) : resolvedPlaceholder}
		</span>
		<span class="flex items-center gap-1 shrink-0">
			{#if clearable && selectedYmd && !required && !disabled}
				<span
					role="button"
					tabindex="-1"
					aria-label={t('picker.clear')}
					title={t('picker.clear')}
					onclick={clearDate}
					onkeydown={(e) => {
						if (e.key === 'Enter' || e.key === ' ') {
							e.preventDefault();
							clearDate();
						}
					}}
					class="inline-flex items-center justify-center rounded-full p-0.5 text-secondary hover:text-ink hover:bg-ash transition-colors"
				>
					<span class="material-symbols-outlined text-[15px]">close</span>
				</span>
			{/if}
			<span class="material-symbols-outlined text-secondary {size === 'sm' ? 'text-[16px]' : 'text-[18px]'}">
				calendar_month
			</span>
		</span>
	</button>

	{#if isOpen}
		<div
			bind:this={popoverEl}
			style={popoverStyle}
			role="dialog"
			aria-label={resolvedPlaceholder}
			class="z-[80] max-w-[calc(100vw-1rem)] bg-white border border-stone rounded-xl shadow-xl p-3 select-none"
		>
			<!-- Header -->
			<div class="flex items-center justify-between mb-3">
				<button
					type="button"
					onclick={prevMonth}
					aria-label={t('picker.prevMonth')}
					class="h-7 w-7 flex items-center justify-center rounded-lg border border-stone hover:bg-ash transition-colors cursor-pointer text-secondary hover:text-ink"
				>
					<span class="material-symbols-outlined text-[18px]">chevron_left</span>
				</button>
				<span class="text-xs font-bold text-primary tracking-wide">
					{monthLabel}
				</span>
				<button
					type="button"
					onclick={nextMonth}
					aria-label={t('picker.nextMonth')}
					class="h-7 w-7 flex items-center justify-center rounded-lg border border-stone hover:bg-ash transition-colors cursor-pointer text-secondary hover:text-ink"
				>
					<span class="material-symbols-outlined text-[18px]">chevron_right</span>
				</button>
			</div>

			<!-- Weekdays -->
			<div class="grid grid-cols-7 gap-y-1 text-center mb-1">
				{#each weekdays as day}
					<span class="text-[10px] font-bold text-overcast uppercase tracking-wider">{day}</span>
				{/each}
			</div>

			<!-- Days Grid -->
			<div class="grid grid-cols-7 gap-y-1 text-center">
				{#each daysGrid as date}
					{#if date === null}
						<span></span>
					{:else}
						{@const cellYmd = toYmd(date)}
						{@const isSelected = selectedYmd === cellYmd}
						{@const isToday = todayYmd === cellYmd}
						<button
							type="button"
							onclick={() => selectDay(date)}
							class="h-8 w-8 mx-auto flex items-center justify-center rounded-lg text-xs font-semibold tabular-nums transition-all cursor-pointer
								{isSelected ? 'bg-action-blue !text-white shadow-sm font-bold' : 'text-ink hover:bg-ash'}
								{isToday && !isSelected ? 'border border-action-blue text-action-blue bg-action-blue/10 font-bold' : ''}"
						>
							{date.getDate()}
						</button>
					{/if}
				{/each}
			</div>

			<!-- Footer Actions -->
			<div class="mt-2.5 pt-2 border-t border-stone/70 flex items-center justify-between gap-2">
				<button
					type="button"
					onclick={selectToday}
					class="text-xs font-semibold text-action-blue hover:underline cursor-pointer px-1.5 py-0.5 rounded"
				>
					{t('picker.today')}
				</button>
				{#if !required && selectedYmd}
					<button
						type="button"
						onclick={() => clearDate()}
						class="text-xs font-medium text-secondary hover:text-ink cursor-pointer px-1.5 py-0.5 rounded hover:bg-ash transition-colors"
					>
						{t('picker.clear')}
					</button>
				{/if}
			</div>
		</div>
	{/if}
</div>
