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
	const t = $derived(createTranslator(activeLocale));
	const resolvedPlaceholder = $derived(placeholder || t('picker.selectTime'));

	let isOpen = $state(false);
	let draftText = $state('');
	let triggerEl: HTMLButtonElement | undefined = $state();
	let popoverEl: HTMLDivElement | undefined = $state();
	let hourListEl: HTMLDivElement | undefined = $state();
	let minuteListEl: HTMLDivElement | undefined = $state();
	let popoverStyle = $state('');

	const hours = Array.from({ length: 24 }, (_, i) => String(i).padStart(2, '0'));
	const minutes = Array.from({ length: 60 }, (_, i) => String(i).padStart(2, '0'));

	function normalizeTime(raw: string): string {
		const match = /^(\d{1,2}):(\d{2})$/.exec(raw.trim());
		if (!match) return '';
		const h = Number(match[1]);
		const m = Number(match[2]);
		if (h < 0 || h > 23 || m < 0 || m > 59) return '';
		return `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}`;
	}

	const normalizedValue = $derived(normalizeTime(value));
	const selectedHour = $derived(normalizedValue ? normalizedValue.slice(0, 2) : '');
	const selectedMinute = $derived(normalizedValue ? normalizedValue.slice(3, 5) : '');

	$effect(() => {
		draftText = normalizedValue;
	});

	function updatePosition() {
		if (!triggerEl) return;
		const rect = triggerEl.getBoundingClientRect();
		const popWidth = Math.min(232, window.innerWidth - 16);
		const popHeight = popoverEl?.offsetHeight || 296;
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

	function scrollSelectedIntoView() {
		if (hourListEl && selectedHour) {
			const el = hourListEl.querySelector(`[data-hour="${selectedHour}"]`) as HTMLElement | null;
			el?.scrollIntoView({ block: 'center' });
		}
		if (minuteListEl && selectedMinute) {
			const el = minuteListEl.querySelector(`[data-minute="${selectedMinute}"]`) as HTMLElement | null;
			el?.scrollIntoView({ block: 'center' });
		}
	}

	async function toggleOpen() {
		if (disabled) return;
		isOpen = !isOpen;
		if (isOpen) {
			draftText = normalizedValue;
			await tick();
			updatePosition();
			scrollSelectedIntoView();
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

	function commitTime(next: string) {
		value = next;
		draftText = next;
		onchange?.(next);
	}

	function selectHour(h: string) {
		const m = selectedMinute || '00';
		commitTime(`${h}:${m}`);
	}

	function selectMinute(m: string) {
		const h = selectedHour || '09';
		commitTime(`${h}:${m}`);
	}

	function selectNow() {
		const now = new Date();
		const h = String(now.getHours()).padStart(2, '0');
		const m = String(now.getMinutes()).padStart(2, '0');
		commitTime(`${h}:${m}`);
		isOpen = false;
	}

	function clearTime(e: MouseEvent | undefined = undefined) {
		e?.stopPropagation();
		commitTime('');
		isOpen = false;
	}

	function applyDraftInput() {
		const parsed = normalizeTime(draftText);
		if (parsed) {
			commitTime(parsed);
		} else {
			draftText = normalizedValue;
		}
	}

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
			value={normalizedValue}
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
		<input type="hidden" {name} value={normalizedValue} />
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
		<span class="truncate font-mono tabular-nums {normalizedValue ? 'text-primary' : 'text-overcast font-sans'}">
			{normalizedValue || resolvedPlaceholder}
		</span>
		<span class="flex items-center gap-1 shrink-0">
			{#if clearable && normalizedValue && !required && !disabled}
				<span
					role="button"
					tabindex="-1"
					aria-label={t('picker.clear')}
					title={t('picker.clear')}
					onclick={clearTime}
					onkeydown={(e) => {
						if (e.key === 'Enter' || e.key === ' ') {
							e.preventDefault();
							clearTime();
						}
					}}
					class="inline-flex items-center justify-center rounded-full p-0.5 text-secondary hover:text-ink hover:bg-ash transition-colors"
				>
					<span class="material-symbols-outlined text-[15px]">close</span>
				</span>
			{/if}
			<span class="material-symbols-outlined text-secondary {size === 'sm' ? 'text-[16px]' : 'text-[18px]'}">
				schedule
			</span>
		</span>
	</button>

	{#if isOpen}
		<div
			bind:this={popoverEl}
			style={popoverStyle}
			role="dialog"
			aria-label={resolvedPlaceholder}
			class="z-[80] max-w-[calc(100vw-1rem)] bg-white border border-stone rounded-xl shadow-xl p-3 select-none space-y-2.5"
		>
			<!-- Direct HH:mm input -->
			<div class="flex items-center gap-2">
				<input
					type="text"
					inputmode="numeric"
					maxlength="5"
					bind:value={draftText}
					placeholder="09:00"
					onblur={applyDraftInput}
					onkeydown={(e) => {
						if (e.key === 'Enter') {
							e.preventDefault();
							applyDraftInput();
							isOpen = false;
						}
					}}
					class="w-full h-8 rounded-input border border-stone bg-ash/40 px-2.5 text-center font-mono text-body-sm text-ink tabular-nums focus:bg-white focus:border-ink focus:outline-none"
				/>
			</div>

			<!-- Hour & Minute Columns -->
			<div class="grid grid-cols-2 gap-2">
				<div class="space-y-1">
					<span class="block text-center text-[10px] font-bold text-overcast uppercase tracking-wider">
						{t('picker.hour')}
					</span>
					<div
						bind:this={hourListEl}
						class="h-44 overflow-y-auto custom-scrollbar rounded-lg border border-stone/70 bg-surface-container-low/40 p-1 space-y-0.5"
					>
						{#each hours as h}
							<button
								type="button"
								data-hour={h}
								onclick={() => selectHour(h)}
								class="w-full h-7 rounded-md text-xs font-mono font-semibold tabular-nums transition-colors cursor-pointer {selectedHour === h
									? 'bg-action-blue text-white shadow-2xs'
									: 'text-ink hover:bg-ash'}"
							>
								{h}
							</button>
						{/each}
					</div>
				</div>

				<div class="space-y-1">
					<span class="block text-center text-[10px] font-bold text-overcast uppercase tracking-wider">
						{t('picker.minute')}
					</span>
					<div
						bind:this={minuteListEl}
						class="h-44 overflow-y-auto custom-scrollbar rounded-lg border border-stone/70 bg-surface-container-low/40 p-1 space-y-0.5"
					>
						{#each minutes as m}
							<button
								type="button"
								data-minute={m}
								onclick={() => selectMinute(m)}
								class="w-full h-7 rounded-md text-xs font-mono font-semibold tabular-nums transition-colors cursor-pointer {selectedMinute === m
									? 'bg-action-blue text-white shadow-2xs'
									: 'text-ink hover:bg-ash'}"
							>
								{m}
							</button>
						{/each}
					</div>
				</div>
			</div>

			<!-- Footer Actions -->
			<div class="pt-2 border-t border-stone/70 flex items-center justify-between gap-2">
				<button
					type="button"
					onclick={selectNow}
					class="text-xs font-semibold text-action-blue hover:underline cursor-pointer px-1.5 py-0.5 rounded"
				>
					{t('picker.now')}
				</button>
				<div class="flex items-center gap-2">
					{#if !required && normalizedValue}
						<button
							type="button"
							onclick={() => clearTime()}
							class="text-xs font-medium text-secondary hover:text-ink cursor-pointer px-1.5 py-0.5 rounded hover:bg-ash transition-colors"
						>
							{t('picker.clear')}
						</button>
					{/if}
					<button
						type="button"
						onclick={() => {
							applyDraftInput();
							isOpen = false;
						}}
						class="text-xs font-semibold text-ink bg-ash hover:bg-stone/50 cursor-pointer px-2 py-0.5 rounded transition-colors"
					>
						{t('picker.done')}
					</button>
				</div>
			</div>
		</div>
	{/if}
</div>
