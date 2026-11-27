import type { InventoryTaxMode } from '$lib/types/settings';

export type TaxPreset = {
	id: string;
	percent: number;
	labelKey: string;
	fallbackLabel?: string;
};

export const INDONESIAN_TAX_PRESETS: TaxPreset[] = [
	{ id: 'pkp', percent: 11, labelKey: 'inventory.taxPreset.pkp' },
	{ id: 'umkm', percent: 0.5, labelKey: 'inventory.taxPreset.umkm' },
	{ id: 'nonPkp', percent: 0, labelKey: 'inventory.taxPreset.nonPkp' }
];

export const INTERNATIONAL_TAX_PRESETS: TaxPreset[] = [
	{ id: 'exempt', percent: 0, labelKey: 'inventory.taxPreset.exempt' },
	{ id: 'rate5', percent: 5, labelKey: 'inventory.taxPreset.rate5' },
	{ id: 'rate10', percent: 10, labelKey: 'inventory.taxPreset.rate10' },
	{ id: 'rate15', percent: 15, labelKey: 'inventory.taxPreset.rate15' },
	{ id: 'rate20', percent: 20, labelKey: 'inventory.taxPreset.rate20' }
];

export function getTaxPresets(currencyCode: string | null | undefined): TaxPreset[] {
	return currencyCode === 'IDR' ? INDONESIAN_TAX_PRESETS : INTERNATIONAL_TAX_PRESETS;
}

export function normalizeTaxMode(mode: string | null | undefined): InventoryTaxMode {
	return mode === 'Multiplier' ? 'Multiplier' : 'Percentage';
}

export function taxInputFromPercent(percent: number, mode: InventoryTaxMode): number {
	const safe = Math.max(0, Number.isFinite(percent) ? percent : 0);
	if (mode === 'Multiplier') {
		return Number((1 + safe / 100).toFixed(4));
	}
	return Number(safe.toFixed(4));
}

export function taxPercentFromInput(raw: number | string, mode: InventoryTaxMode): number {
	const parsed = Number(raw);
	if (!Number.isFinite(parsed)) return 0;
	if (mode === 'Multiplier') {
		return Math.max(0, Number(((parsed - 1) * 100).toFixed(4)));
	}
	return Math.max(0, Number(parsed.toFixed(4)));
}

export function isPresetActive(currentPercent: number, presetPercent: number): boolean {
	return Math.abs(currentPercent - presetPercent) < 0.005;
}

export function formatTaxRateSummary(percent: number, mode: InventoryTaxMode): string {
	const safe = Math.max(0, Number.isFinite(percent) ? percent : 0);
	const pct = Number(safe.toFixed(2));
	const mult = Number((1 + safe / 100).toFixed(4));
	return mode === 'Multiplier' ? `×${mult} (${pct}%)` : `${pct}% (×${mult})`;
}
