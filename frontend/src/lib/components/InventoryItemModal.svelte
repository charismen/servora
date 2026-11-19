<script lang="ts">
	import { enhance } from '$app/forms';
	import { tick } from 'svelte';
	import { fade } from 'svelte/transition';
	import CurrencyInput from '$lib/components/CurrencyInput.svelte';
	import Dropdown from '$lib/components/ui/dropdown/Dropdown.svelte';
	import { createTranslator, formatCurrency, type Locale } from '$lib/i18n';
	import type { InventoryCategory, InventoryItem } from '$lib/types/inventory';

	const ITEM_TYPES = [
		'SparePart',
		'Accessory',
		'ServiceConsumable',
		'Equipment',
		'ToolOrSupply',
		'RefurbishedUnit',
		'SecondhandPart',
		'SecondhandPcBuild',
		'SecondhandLaptop'
	] as const;
	const CONDITIONS = ['New', 'Used', 'Refurbished', 'ForParts'] as const;

	let {
		open = false,
		item = null,
		categories = [],
		locale = 'en' as Locale,
		currencyCode = 'IDR',
		defaultTaxPercent = 11,
		roundingIncrement = 1000,
		defaultUnit = 'pcs',
		action = '?/saveItem',
		onclose
	}: {
		open: boolean;
		item?: InventoryItem | null;
		categories: InventoryCategory[];
		locale: Locale;
		currencyCode: string;
		defaultTaxPercent?: number;
		roundingIncrement?: number;
		defaultUnit?: string;
		action?: string;
		onclose: () => void;
	} = $props();

	const t = $derived(createTranslator(locale));
	const currencySymbol = $derived(
		currencyCode === 'IDR'
			? 'Rp'
			: currencyCode === 'MYR'
				? 'RM'
				: currencyCode === 'SGD'
					? 'S$'
					: currencyCode === 'EUR'
						? '€'
						: currencyCode === 'GBP'
							? '£'
							: currencyCode === 'AUD'
								? 'A$'
								: '$'
	);
	const stepRounding = $derived(Math.max(1, Number(roundingIncrement) || 1000));
	const isEdit = $derived(Boolean(item));

	let skuInput = $state<HTMLInputElement | null>(null);
	let submitting = $state(false);
	let errorMessage = $state<string | null>(null);
	let previousFocus: HTMLElement | null = null;
	let initializedKey = '';

	let draft = $state({
		sku: '',
		name: '',
		categoryId: '',
		itemType: 'SparePart',
		condition: 'New',
		purchasePrice: 0 as number | string,
		shippingCost: 0 as number | string,
		serviceFee: 0 as number | string,
		marginPercent: 0 as number | string,
		taxPercent: 11 as number | string,
		unit: 'pcs',
		reorderLevel: 0 as number | string,
		locationNote: '',
		barcodeValue: '',
		supplierName: '',
		quality: '',
		warrantyDays: '' as number | string,
		description: '',
		isActive: true
	});

	let useManualSalePrice = $state(false);
	let manualSalePrice = $state<number | string>(0);

	function computePricing(purchase: unknown, shipping: unknown, service: unknown, margin: unknown, tax: unknown) {
		const p = Math.max(0, Number(purchase) || 0);
		const s = Math.max(0, Number(shipping) || 0);
		const f = Math.max(0, Number(service) || 0);
		const m = Math.max(0, Number(margin) || 0);
		const tx = Math.max(0, Number(tax) || 0);
		const hpp = p + s;
		const marginAmount = (p * m) / 100;
		const priceBeforeTax = hpp + f + marginAmount;
		const taxAmount = (priceBeforeTax * tx) / 100;
		const priceAfterTax = priceBeforeTax + taxAmount;
		const calculatedSalePrice = Math.round(priceAfterTax / stepRounding) * stepRounding;
		return { hpp, marginAmount, priceBeforeTax, taxAmount, priceAfterTax, calculatedSalePrice };
	}

	const preview = $derived(
		computePricing(draft.purchasePrice, draft.shippingCost, draft.serviceFee, draft.marginPercent, draft.taxPercent)
	);
	const effectiveSalePrice = $derived(
		useManualSalePrice ? Math.max(0, Number(manualSalePrice) || 0) : preview.calculatedSalePrice
	);

	const categoryOptions = $derived.by(() => {
		const active = categories.filter((c) => c.isActive);
		const options = [
			{ value: '', label: t('inventory.noCategory') },
			...active.map((c) => ({ value: String(c.id), label: c.name }))
		];
		if (item?.categoryId && !active.some((c) => c.id === item.categoryId)) {
			const existing = categories.find((c) => c.id === item.categoryId);
			const fallbackName = existing?.name ?? item.categoryName;
			if (fallbackName) {
				options.push({ value: String(item.categoryId), label: `${fallbackName} (${t('inventory.inactive')})` });
			}
		}
		return options;
	});

	function resetFromTarget(target: InventoryItem | null) {
		errorMessage = null;
		submitting = false;
		if (!target) {
			draft = {
				sku: '',
				name: '',
				categoryId: '',
				itemType: ITEM_TYPES[0],
				condition: CONDITIONS[0],
				purchasePrice: 0,
				shippingCost: 0,
				serviceFee: 0,
				marginPercent: 0,
				taxPercent: defaultTaxPercent,
				unit: defaultUnit || 'pcs',
				reorderLevel: 0,
				locationNote: '',
				barcodeValue: '',
				supplierName: '',
				quality: '',
				warrantyDays: '',
				description: '',
				isActive: true
			};
			useManualSalePrice = false;
			manualSalePrice = 0;
			return;
		}

		const purchase = target.purchasePrice ?? target.costPrice ?? 0;
		const shipping = target.shippingCost ?? 0;
		const service = target.serviceFee ?? 0;
		const margin = target.marginPercent ?? 0;
		const tax = target.taxPercent ?? defaultTaxPercent;

		draft = {
			sku: target.sku,
			name: target.name,
			categoryId: target.categoryId != null ? String(target.categoryId) : '',
			itemType: ITEM_TYPES.includes(target.itemType as (typeof ITEM_TYPES)[number]) ? target.itemType : ITEM_TYPES[0],
			condition: CONDITIONS.includes(target.condition as (typeof CONDITIONS)[number]) ? target.condition : CONDITIONS[0],
			purchasePrice: purchase,
			shippingCost: shipping,
			serviceFee: service,
			marginPercent: margin,
			taxPercent: tax,
			unit: target.unit || defaultUnit || 'pcs',
			reorderLevel: target.reorderLevel ?? 0,
			locationNote: target.locationNote ?? '',
			barcodeValue: target.barcodeValue ?? '',
			supplierName: target.supplierName ?? '',
			quality: target.quality ?? '',
			warrantyDays: target.warrantyDays ?? '',
			description: target.description ?? '',
			isActive: target.isActive
		};

		const storedSalePrice = target.fixedSalePrice ?? target.salePrice ?? 0;
		const calculated = computePricing(purchase, shipping, service, margin, tax).calculatedSalePrice;
		useManualSalePrice = Math.abs(storedSalePrice - calculated) > 0.01;
		manualSalePrice = storedSalePrice;
	}

	$effect(() => {
		if (open) {
			const nextKey = item ? `edit:${item.id}:${item.updatedAt ?? ''}` : 'add';
			if (initializedKey !== nextKey) {
				if (!initializedKey && typeof document !== 'undefined') {
					previousFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null;
				}
				initializedKey = nextKey;
				resetFromTarget(item ?? null);
				tick().then(() => skuInput?.focus());
			}
		} else if (initializedKey) {
			initializedKey = '';
			errorMessage = null;
			submitting = false;
			previousFocus?.focus?.();
			previousFocus = null;
		}
	});

	function handleClose() {
		if (submitting) return;
		onclose();
	}

	function handleKeydown(event: KeyboardEvent) {
		if (open && event.key === 'Escape' && !submitting) {
			event.preventDefault();
			onclose();
		}
	}

	function toggleManualPrice(checked: boolean) {
		useManualSalePrice = checked;
		if (checked && (!manualSalePrice || Number(manualSalePrice) <= 0)) {
			manualSalePrice = preview.calculatedSalePrice;
		}
	}
</script>

<svelte:window onkeydown={handleKeydown} />

{#if open}
	<div
		transition:fade={{ duration: 150 }}
		class="fixed inset-0 z-50 bg-black/60 backdrop-blur-xs flex items-center justify-center p-4 overflow-y-auto"
		onclick={(e) => { if (e.target === e.currentTarget) handleClose(); }}
		role="presentation"
	>
		<div
			class="bg-white border border-stone rounded-2xl shadow-2xl max-w-3xl w-full max-h-[92vh] flex flex-col overflow-hidden my-auto"
			role="dialog"
			aria-modal="true"
			aria-labelledby="inventory-item-modal-title"
		>
			<div class="px-6 py-4 border-b border-stone flex items-center justify-between bg-surface-container-low shrink-0">
				<div class="min-w-0">
					<h3 id="inventory-item-modal-title" class="font-subheading text-[17px] font-bold text-ink truncate">
						{isEdit ? t('inventory.editItem') : t('inventory.addItem')}
					</h3>
					<p class="text-xs text-secondary mt-0.5">
						{isEdit ? t('inventory.editSubtitle') : t('inventory.subtitle')}
					</p>
				</div>
				<button
					type="button"
					class="w-8 h-8 rounded-lg hover:bg-ash text-secondary hover:text-ink flex items-center justify-center cursor-pointer transition-colors disabled:opacity-50 shrink-0"
					disabled={submitting}
					onclick={handleClose}
					aria-label={t('common.close')}
				>
					<span class="material-symbols-outlined text-[20px]" aria-hidden="true">close</span>
				</button>
			</div>

			<form
				method="POST"
				{action}
				use:enhance={() => {
					submitting = true;
					errorMessage = null;
					return async ({ result, update }) => {
						submitting = false;
						if (result.type === 'failure') {
							const data = result.data as { message?: string; messageKey?: string } | undefined;
							if (data?.messageKey) {
								const translated = t(data.messageKey);
								errorMessage = translated !== data.messageKey ? translated : data.message ?? t('common.error');
							} else {
								errorMessage = data?.message ?? t('common.error');
							}
							await update({ reset: false });
							return;
						}
						onclose();
						await update();
					};
				}}
				class="flex-1 overflow-y-auto p-6 space-y-5 custom-scrollbar"
			>
				{#if item}
					<input type="hidden" name="id" value={item.id} />
					<input type="hidden" name="updatedAt" value={item.updatedAt ?? ''} />
				{:else}
					<input type="hidden" name="isActive" value="on" />
				{/if}
				<input type="hidden" name="costPrice" value={draft.purchasePrice} />
				<input type="hidden" name="salePrice" value={effectiveSalePrice} />
				<input type="hidden" name="fixedSalePrice" value={useManualSalePrice ? manualSalePrice : ''} />

				{#if errorMessage}
					<div role="alert" class="rounded-xl border border-error/30 bg-error/10 px-4 py-3 text-body-sm text-error flex items-start gap-2.5">
						<span class="material-symbols-outlined text-[18px] shrink-0 mt-0.5" aria-hidden="true">error</span>
						<span class="font-medium">{errorMessage}</span>
					</div>
				{/if}

				{#if item}
					<div class="rounded-xl border border-stone bg-ash/50 px-4 py-3 flex flex-wrap items-center justify-between gap-3">
						<div class="flex flex-wrap items-center gap-4 text-xs font-mono tabular-nums">
							<div>
								<span class="text-secondary">{t('inventory.detail.onHand')}:</span>
								<strong class="text-ink ml-1">{item.onHandQuantity} {item.unit}</strong>
							</div>
							<div>
								<span class="text-secondary">{t('inventory.detail.reserved')}:</span>
								<strong class="text-ink ml-1">{item.reservedQuantity} {item.unit}</strong>
							</div>
							<div>
								<span class="text-secondary">{t('inventory.detail.available')}:</span>
								<strong class="text-emerald-700 ml-1">{item.availableQuantity} {item.unit}</strong>
							</div>
						</div>
						<span class="text-xs text-secondary">{t('inventory.editStockHint')}</span>
					</div>
				{/if}

				<!-- Identity & Classification -->
				<div class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-4">
					<label class="space-y-1 block">
						<span class="font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('inventory.sku')} *</span>
						<input
							bind:this={skuInput}
							name="sku"
							bind:value={draft.sku}
							required
							maxlength="80"
							class="w-full border border-stone rounded-input px-3 py-2 text-body-sm font-mono text-ink bg-white focus:border-ink focus:ring-1 focus:ring-ink/10 focus:outline-none"
							placeholder={t('inventory.placeholder.sku')}
						/>
					</label>
					<label class="space-y-1 sm:col-span-2 block">
						<span class="font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('inventory.name')} *</span>
						<input
							name="name"
							bind:value={draft.name}
							required
							maxlength="180"
							class="w-full border border-stone rounded-input px-3 py-2 text-body-sm text-ink bg-white focus:border-ink focus:ring-1 focus:ring-ink/10 focus:outline-none"
							placeholder={t('inventory.placeholder.name')}
						/>
					</label>
					<div class="space-y-1">
						<span class="font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('inventory.category')}</span>
						<Dropdown
							name="categoryId"
							bind:value={draft.categoryId}
							searchable
							searchPlaceholder={t('common.search')}
							noOptionsLabel={t('common.noOptions')}
							options={categoryOptions}
						/>
					</div>
					<div class="space-y-1">
						<span class="font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('inventory.itemType')}</span>
						<Dropdown
							name="itemType"
							bind:value={draft.itemType}
							options={ITEM_TYPES.map((type) => ({ value: type, label: t(`inventory.itemType.${type}`) }))}
						/>
					</div>
					<div class="space-y-1">
						<span class="font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('inventory.condition')}</span>
						<Dropdown
							name="condition"
							bind:value={draft.condition}
							options={CONDITIONS.map((condition) => ({ value: condition, label: t(`inventory.condition.${condition}`) }))}
						/>
					</div>
				</div>

				<!-- Pricing & Cost Calculation -->
				<div class="bg-ash/50 border border-stone rounded-xl p-4 space-y-4">
					<div class="flex flex-wrap items-center justify-between gap-2">
						<h4 class="font-label-caps text-ink text-[12px] font-bold uppercase tracking-wider">{t('inventory.detail.pricing')}</h4>
						<label class="inline-flex items-center gap-2 text-xs text-ink font-medium cursor-pointer select-none">
							<input
								type="checkbox"
								checked={useManualSalePrice}
								onchange={(e) => toggleManualPrice((e.currentTarget as HTMLInputElement).checked)}
								class="rounded border-stone text-ink focus:ring-ink"
							/>
							<span>{t('inventory.manualSalePrice')}</span>
						</label>
					</div>

					<div class="grid grid-cols-2 sm:grid-cols-3 gap-3">
						<div class="space-y-1">
							<span class="font-label-caps text-secondary text-[10px] font-bold uppercase">{t('inventory.purchasePrice')}</span>
							<CurrencyInput
								name="purchasePrice"
								prefix={currencySymbol}
								{locale}
								currency={currencyCode}
								bind:value={draft.purchasePrice}
							/>
						</div>
						<div class="space-y-1">
							<span class="font-label-caps text-secondary text-[10px] font-bold uppercase">{t('inventory.shippingCost')}</span>
							<CurrencyInput
								name="shippingCost"
								prefix={currencySymbol}
								{locale}
								currency={currencyCode}
								bind:value={draft.shippingCost}
							/>
						</div>
						<div class="space-y-1">
							<span class="font-label-caps text-secondary text-[10px] font-bold uppercase">{t('inventory.serviceFee')}</span>
							<CurrencyInput
								name="serviceFee"
								prefix={currencySymbol}
								{locale}
								currency={currencyCode}
								bind:value={draft.serviceFee}
							/>
						</div>
						<label class="space-y-1 block">
							<span class="font-label-caps text-secondary text-[10px] font-bold uppercase">{t('inventory.marginPercent')}</span>
							<input
								name="marginPercent"
								type="number"
								min="0"
								step="0.01"
								bind:value={draft.marginPercent}
								class="w-full h-10 border border-stone rounded-input px-3 py-2 text-body-sm font-mono text-ink focus:border-ink focus:outline-none bg-white"
							/>
						</label>
						<label class="space-y-1 block">
							<span class="font-label-caps text-secondary text-[10px] font-bold uppercase">{t('inventory.taxPercent')}</span>
							<input
								name="taxPercent"
								type="number"
								min="0"
								step="0.01"
								bind:value={draft.taxPercent}
								class="w-full h-10 border border-stone rounded-input px-3 py-2 text-body-sm font-mono text-ink focus:border-ink focus:outline-none bg-white"
							/>
						</label>
						<div class="space-y-1">
							<div class="flex items-center justify-between gap-1">
								<span class="font-label-caps text-ink text-[10px] font-bold uppercase">{t('inventory.salePrice')}</span>
								{#if !useManualSalePrice}
									<span class="text-[10px] text-secondary">{t('inventory.autoCalculated')}</span>
								{/if}
							</div>
							{#if useManualSalePrice}
								<CurrencyInput
									prefix={currencySymbol}
									{locale}
									currency={currencyCode}
									bind:value={manualSalePrice}
								/>
							{:else}
								<div class="h-10 px-3 border border-stone rounded-input bg-white font-mono font-bold text-ink text-body-sm flex items-center tabular-nums">
									{formatCurrency(preview.calculatedSalePrice, locale, currencyCode)}
								</div>
							{/if}
						</div>
					</div>

					{#if useManualSalePrice}
						<p class="text-xs text-secondary">{t('inventory.manualSalePriceHint')}</p>
					{/if}

					<div class="grid grid-cols-2 sm:grid-cols-4 gap-2 pt-2 border-t border-stone/60 text-xs font-mono tabular-nums text-secondary">
						<div>{t('inventory.hpp')}: <strong class="text-ink">{formatCurrency(preview.hpp, locale, currencyCode)}</strong></div>
						<div>{t('inventory.marginAmount')}: <strong class="text-ink">{formatCurrency(preview.marginAmount, locale, currencyCode)}</strong></div>
						<div>{t('inventory.taxAmount')}: <strong class="text-ink">{formatCurrency(preview.taxAmount, locale, currencyCode)}</strong></div>
						<div>{t('inventory.salePrice')}: <strong class="text-emerald-700 font-bold">{formatCurrency(effectiveSalePrice, locale, currencyCode)}</strong></div>
					</div>
				</div>

				<!-- Stock Limits & Physical Attributes -->
				<div class="grid grid-cols-2 sm:grid-cols-4 gap-4">
					<label class="space-y-1 block">
						<span class="font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('inventory.unit')}</span>
						<input
							name="unit"
							bind:value={draft.unit}
							maxlength="20"
							class="w-full border border-stone rounded-input px-3 py-2 text-body-sm text-ink bg-white focus:border-ink focus:outline-none"
						/>
					</label>
					<label class="space-y-1 block">
						<span class="font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('inventory.reorder')}</span>
						<input
							name="reorderLevel"
							type="number"
							min="0"
							step="1"
							bind:value={draft.reorderLevel}
							class="w-full border border-stone rounded-input px-3 py-2 text-body-sm font-mono text-ink bg-white focus:border-ink focus:outline-none"
						/>
					</label>
					<label class="space-y-1 block sm:col-span-2">
						<span class="font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('inventory.location')}</span>
						<input
							name="locationNote"
							bind:value={draft.locationNote}
							maxlength="180"
							placeholder={t('inventory.placeholder.location')}
							class="w-full border border-stone rounded-input px-3 py-2 text-body-sm text-ink bg-white focus:border-ink focus:outline-none"
						/>
					</label>
					<label class="space-y-1 block sm:col-span-2">
						<span class="font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('inventory.barcodeValue')}</span>
						<input
							name="barcodeValue"
							bind:value={draft.barcodeValue}
							maxlength="120"
							placeholder={t('inventory.placeholder.barcode')}
							class="w-full border border-stone rounded-input px-3 py-2 text-body-sm font-mono text-ink bg-white focus:border-ink focus:outline-none"
						/>
					</label>
					<label class="space-y-1 block sm:col-span-2">
						<span class="font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('inventory.supplier')}</span>
						<input
							name="supplierName"
							bind:value={draft.supplierName}
							maxlength="180"
							placeholder={t('inventory.placeholder.supplier')}
							class="w-full border border-stone rounded-input px-3 py-2 text-body-sm text-ink bg-white focus:border-ink focus:outline-none"
						/>
					</label>
					<label class="space-y-1 block sm:col-span-2">
						<span class="font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('inventory.quality')}</span>
						<input
							name="quality"
							bind:value={draft.quality}
							maxlength="120"
							placeholder={t('inventory.placeholder.quality')}
							class="w-full border border-stone rounded-input px-3 py-2 text-body-sm text-ink bg-white focus:border-ink focus:outline-none"
						/>
					</label>
					<label class="space-y-1 block sm:col-span-2">
						<span class="font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('inventory.warrantyDays')}</span>
						<input
							name="warrantyDays"
							type="number"
							min="0"
							step="1"
							bind:value={draft.warrantyDays}
							placeholder={t('inventory.placeholder.warrantyDays')}
							class="w-full border border-stone rounded-input px-3 py-2 text-body-sm font-mono text-ink bg-white focus:border-ink focus:outline-none"
						/>
					</label>
					<label class="space-y-1 block col-span-2 sm:col-span-4">
						<span class="font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider">{t('inventory.description')}</span>
						<input
							name="description"
							bind:value={draft.description}
							maxlength="1000"
							placeholder={t('inventory.secondhandHint')}
							class="w-full border border-stone rounded-input px-3 py-2 text-body-sm text-ink bg-white focus:border-ink focus:outline-none"
						/>
					</label>
				</div>

				{#if isEdit}
					<label class="flex items-start gap-3 rounded-xl border border-stone bg-ash/30 px-4 py-3 cursor-pointer">
						<input
							name="isActive"
							type="checkbox"
							value="on"
							bind:checked={draft.isActive}
							class="mt-0.5 rounded border-stone text-ink focus:ring-ink"
						/>
						<span class="space-y-0.5">
							<span class="block text-body-sm font-semibold text-ink">{t('inventory.itemActiveLabel')}</span>
							<span class="block text-xs text-secondary">{t('inventory.itemActiveHint')}</span>
						</span>
					</label>
				{/if}

				<div class="pt-4 border-t border-stone flex items-center justify-end gap-2 shrink-0">
					<button
						type="button"
						class="px-4 py-2 rounded-button border border-stone bg-white text-secondary hover:text-ink hover:bg-ash font-medium text-body-sm transition-all cursor-pointer disabled:opacity-50"
						disabled={submitting}
						onclick={handleClose}
					>
						{t('common.cancel')}
					</button>
					<button
						type="submit"
						disabled={submitting}
						class="px-5 py-2 rounded-button bg-ink text-white font-medium text-body-sm hover:bg-ink/90 active:scale-95 transition-all shadow-xs cursor-pointer inline-flex items-center gap-1.5 disabled:opacity-60 disabled:cursor-not-allowed"
					>
						{#if submitting}
							<span class="material-symbols-outlined text-[16px] animate-spin" aria-hidden="true">progress_activity</span>
							<span>{t('common.saving')}</span>
						{:else}
							<span>{isEdit ? t('inventory.saveChanges') : t('inventory.addItem')}</span>
						{/if}
					</button>
				</div>
			</form>
		</div>
	</div>
{/if}
