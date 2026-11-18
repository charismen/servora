/**
 * Utility functions for sanitizing and formatting phone/WhatsApp numbers.
 * Removes spaces, dashes, parentheses, and dots pasted from external apps/contacts.
 * Automatically converts leading domestic '0' (or raw '8...') to international country code (default +62).
 */

export function sanitizePhoneNumber(val: string | null | undefined, defaultCountryCode = '+62'): string {
	if (!val) return '';
	val = val.trim();
	if (!val) return '';

	// Clean out any whitespace (including Unicode NBSP, zero-width, tabs)
	val = val.replace(/[\s\u00A0\u202F\u200B\uFEFF]+/g, '');

	const hasPlus = val.startsWith('+');
	const digits = val.replace(/\D/g, '');

	if (hasPlus && !digits) {
		return '+';
	}
	if (!digits) {
		return '';
	}

	const cc = defaultCountryCode.startsWith('+') ? defaultCountryCode : `+${defaultCountryCode}`;
	const rawCc = cc.replace(/\D/g, '');

	// If starts with '0' (e.g. 0823-0167-0863 or 0857 90784739), replace leading 0 with country code
	if (val.startsWith('0') || (!hasPlus && digits.startsWith('0'))) {
		return `${cc}${digits.slice(1)}`;
	}

	// If it already had a leading plus, keep the plus and clean digits (e.g. +62 823-0167-0863 or +1 555-234-5678)
	if (hasPlus) {
		return `+${digits}`;
	}

	// If it starts with raw country code digits without plus (e.g. 6282301670863)
	if (digits.startsWith(rawCc)) {
		return `+${digits}`;
	}

	// If it starts with common Indonesian mobile prefix '8' without 0 or + (e.g. 823-0167-0863)
	if (digits.startsWith('8') && digits.length >= 8) {
		return `${cc}${digits}`;
	}

	return `+${digits}`;
}

/**
 * Event handler for real-time phone input sanitization on <input type="tel">
 */
export function handlePhoneInput(e: Event, defaultCountryCode = '+62'): void {
	const input = e.currentTarget as HTMLInputElement;
	if (!input) return;
	const original = input.value;
	const sanitized = sanitizePhoneNumber(original, defaultCountryCode);
	if (original !== sanitized) {
		input.value = sanitized;
	}
}

/**
 * Event handler for paste events on phone inputs, stripping spaces/dashes immediately
 */
export function handlePhonePaste(e: ClipboardEvent, defaultCountryCode = '+62'): void {
	const clipboardData = e.clipboardData;
	if (!clipboardData) return;
	const text = clipboardData.getData('text');
	if (!text) return;

	e.preventDefault();
	const sanitized = sanitizePhoneNumber(text, defaultCountryCode);
	const input = e.currentTarget as HTMLInputElement;
	if (!input) return;

	const start = input.selectionStart ?? 0;
	const end = input.selectionEnd ?? input.value.length;
	const nextValue = input.value.slice(0, start) + sanitized + input.value.slice(end);
	const finalSanitized = sanitizePhoneNumber(nextValue, defaultCountryCode);

	input.value = finalSanitized;
	input.dispatchEvent(new Event('input', { bubbles: true }));
}
