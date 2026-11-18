<script lang="ts">
	import { tick } from 'svelte';
	import type { Locale } from '$lib/i18n';

	let {
		name = '',
		value = $bindable(''),
		placeholder = '0',
		prefix = '',
		disabled = false,
		required = false,
		class: className = '',
		inputClass = '',
		prefixClass = '',
		locale = 'id' as Locale,
		currency = 'IDR',
		allowDecimals = false,
		allowNegative = false,
		tone = 'default',
		form = undefined,
		id = undefined,
		oninput = undefined,
		onchange = undefined
	}: {
		name?: string;
		value?: number | string | null;
		placeholder?: string;
		prefix?: string;
		disabled?: boolean;
		required?: boolean;
		class?: string;
		inputClass?: string;
		prefixClass?: string;
		locale?: Locale;
		currency?: string;
		allowDecimals?: boolean;
		allowNegative?: boolean;
		tone?: 'default' | 'positive' | 'negative';
		form?: string;
		id?: string;
		oninput?: (e: Event) => void;
		onchange?: (e: Event) => void;
	} = $props();

	// Thousand and decimal separators based on locale
	const thousandSep = $derived(locale === 'id' ? '.' : ',');
	const decimalSep = $derived(locale === 'id' ? ',' : '.');

	let inputEl = $state<HTMLInputElement | null>(null);
	let displayValue = $state('');

	export async function focus() {
		await tick();
		if (!inputEl) return;
		inputEl.focus();
		const len = inputEl.value.length;
		inputEl.setSelectionRange(len, len);
	}

	function cleanToNumeric(val: string): string {
		if (!val) return '';

		let isNeg = false;
		let working = val;
		if (allowNegative) {
			const hasPlus = working.includes('+');
			const hasMinus = working.includes('-');
			isNeg = hasMinus && !hasPlus;
			working = working.replace(/[+-]/g, '');
		}

		const sign = isNeg ? '-' : '';

		if (!allowDecimals) {
			const digits = working.replace(/\D/g, '');
			if (!digits) return sign;
			return `${sign}${digits}`;
		}

		// If decimals allowed, preserve digits and decimal point
		// Replace locale decimal separator with standard '.'
		let normalized = working;
		if (decimalSep !== '.') {
			normalized = normalized.split(thousandSep).join('').replace(decimalSep, '.');
		} else {
			normalized = normalized.split(thousandSep).join('');
		}
		// Keep only digits and first decimal point
		const parts = normalized.split('.');
		if (parts.length > 1) {
			const intPart = parts[0].replace(/\D/g, '');
			const decPart = parts.slice(1).join('').replace(/\D/g, '');
			return intPart ? `${sign}${intPart}.${decPart}` : `${sign}0.${decPart}`;
		}
		const digits = normalized.replace(/\D/g, '');
		if (!digits) return sign;
		return `${sign}${digits}`;
	}

	function formatDisplay(raw: string | number | null | undefined): string {
		if (raw === null || raw === undefined || raw === '') return '';
		const str = String(raw).trim();
		if (!str) return '';

		const isNeg = allowNegative && str.startsWith('-');
		const sign = isNeg ? '-' : '';
		const unsigned = isNeg ? str.slice(1) : str;

		if (!allowDecimals) {
			const digits = unsigned.replace(/\D/g, '');
			if (!digits) return sign;
			return `${sign}${digits.replace(/\B(?=(\d{3})+(?!\d))/g, thousandSep)}`;
		}

		// Split on standard decimal point '.' or locale decimal separator
		const sep = unsigned.includes(decimalSep) ? decimalSep : '.';
		const parts = unsigned.split(sep);
		const intDigits = parts[0].replace(/\D/g, '');
		if (!intDigits && parts.length === 1) {
			return sign;
		}
		const formattedInt = (intDigits || '0').replace(/\B(?=(\d{3})+(?!\d))/g, thousandSep);

		if (parts.length > 1) {
			const decDigits = parts[1].replace(/\D/g, '');
			return `${sign}${formattedInt}${decimalSep}${decDigits}`;
		}
		return `${sign}${formattedInt}`;
	}

	// Keep displayValue synchronized when external value changes
	$effect(() => {
		const currentRawInDisplay = cleanToNumeric(displayValue);
		const incomingRaw = value === null || value === undefined ? '' : String(value);

		if (incomingRaw !== currentRawInDisplay) {
			displayValue = formatDisplay(incomingRaw);
		}
	});

	function handleInput(e: Event) {
		const target = e.target as HTMLInputElement;
		const currentVal = target.value;
		const cursor = target.selectionStart ?? currentVal.length;

		// Count meaningful digits (and decimal points) before cursor
		let meaningfulCharsBeforeCursor = 0;
		for (let i = 0; i < cursor; i++) {
			const ch = currentVal[i];
			if (/\d/.test(ch) || (allowDecimals && (ch === decimalSep || ch === '.'))) {
				meaningfulCharsBeforeCursor++;
			}
		}

		const rawNumeric = cleanToNumeric(currentVal);
		const formatted = formatDisplay(rawNumeric);

		displayValue = formatted;
		target.value = formatted;
		value = rawNumeric;

		// Restore cursor position accurately
		let newCursor = 0;
		let seenMeaningful = 0;
		for (let i = 0; i < formatted.length; i++) {
			const ch = formatted[i];
			if (/\d/.test(ch) || (allowDecimals && ch === decimalSep)) {
				seenMeaningful++;
			}
			if (seenMeaningful === meaningfulCharsBeforeCursor && meaningfulCharsBeforeCursor > 0) {
				newCursor = i + 1;
				break;
			}
		}
		if (meaningfulCharsBeforeCursor === 0) {
			newCursor = formatted.startsWith('-') ? 1 : 0;
		} else if (seenMeaningful < meaningfulCharsBeforeCursor) {
			newCursor = formatted.length;
		}

		target.setSelectionRange(newCursor, newCursor);
		oninput?.(e);
	}

	function handleKeydown(e: KeyboardEvent) {
		const target = e.target as HTMLInputElement;
		if (target.selectionStart === target.selectionEnd) {
			const pos = target.selectionStart ?? 0;
			if (e.key === 'Backspace' && pos > 0 && target.value[pos - 1] === thousandSep) {
				e.preventDefault();
				const val = target.value;
				const newVal = val.slice(0, pos - 2) + val.slice(pos);
				target.value = newVal;
				target.setSelectionRange(pos - 2, pos - 2);
				target.dispatchEvent(new Event('input', { bubbles: true }));
			} else if (e.key === 'Delete' && pos < target.value.length && target.value[pos] === thousandSep) {
				e.preventDefault();
				const val = target.value;
				const newVal = val.slice(0, pos) + val.slice(pos + 2);
				target.value = newVal;
				target.setSelectionRange(pos, pos);
				target.dispatchEvent(new Event('input', { bubbles: true }));
			}
		}
	}

	function handleBlur(e: FocusEvent) {
		onchange?.(e);
	}

	const rawValueForSubmit = $derived.by(() => {
		const cleaned = cleanToNumeric(displayValue);
		return cleaned === '-' ? '' : cleaned;
	});

	const hasCustomPadding = $derived(
		/(^|\s)p[xytrbl]?-/.test(inputClass)
	);
</script>

<div class="currency-field relative flex items-stretch {disabled ? 'is-disabled' : ''} {tone === 'negative' ? 'is-negative' : tone === 'positive' ? 'is-positive' : ''} {className}">
	{#if prefix}
		<span class="currency-prefix inline-flex items-center px-sm border-r border-stone text-body-sm font-mono select-none shrink-0 {prefixClass}">
			{prefix}
		</span>
	{/if}
	<input
		bind:this={inputEl}
		type="text"
		inputmode={allowNegative ? 'text' : (allowDecimals ? 'decimal' : 'numeric')}
		value={displayValue}
		{id}
		{placeholder}
		{disabled}
		{required}
		{form}
		class="currency-native-input w-full text-body-sm {hasCustomPadding ? '' : 'p-sm'} {inputClass}"
		oninput={handleInput}
		onkeydown={handleKeydown}
		onblur={handleBlur}
	/>
	{#if name}
		<input type="hidden" {name} {form} value={rawValueForSubmit} />
	{/if}
</div>

<style>
	.currency-field {
		border: 1px solid var(--color-stone, #e6dfd5);
		border-radius: var(--radius-input, 7px);
		background-color: #ffffff;
		transition: border-color 0.2s ease-in-out, box-shadow 0.2s ease-in-out;
		overflow: hidden;
	}

	.currency-field:focus-within {
		border-color: var(--color-action-blue, #b45039);
		box-shadow: 0 0 0 3px rgba(180, 80, 57, 0.2);
	}

	.currency-field.is-disabled {
		background-color: var(--color-ash, #f4f0ea);
		cursor: not-allowed;
	}

	.currency-field .currency-prefix {
		background-color: var(--color-ash, #f4f0ea);
		color: var(--color-secondary, #6d6255);
	}

	.currency-field.is-negative .currency-prefix {
		background-color: var(--color-error-container, #fce8e6);
		color: var(--color-error, #8f2824);
	}

	.currency-field.is-positive .currency-prefix {
		background-color: #ecfdf5;
		color: var(--color-success, #1b5e39);
	}

	.currency-field input.currency-native-input {
		border: 0;
		border-radius: 0;
		box-shadow: none;
		background-color: transparent;
		outline: none;
		color: var(--color-ink, #1c1815);
	}

	.currency-field.is-negative input.currency-native-input {
		color: var(--color-error, #8f2824);
	}

	.currency-field.is-positive input.currency-native-input {
		color: var(--color-success, #1b5e39);
	}

	.currency-field.is-disabled input.currency-native-input {
		color: var(--color-lead, #b5a897);
		cursor: not-allowed;
	}

	.currency-field input.currency-native-input:focus {
		border: 0;
		box-shadow: none;
	}
</style>

