<script lang="ts">
	import PageHeader from '$lib/components/PageHeader.svelte';
	import DatePicker from '$lib/components/ui/date-picker/DatePicker.svelte';
	import Dropdown from '$lib/components/ui/dropdown/Dropdown.svelte';
	import { createTranslator, formatCurrency, formatDate } from '$lib/i18n';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();
	const t = $derived(createTranslator(data.locale));
	const item = $derived(data.detail.item);
	const stock = $derived(data.detail.stock);
	const pricing = $derived(data.detail.pricing);
	const currencyCode = $derived(data.shopProfile?.currencyCode ?? 'IDR');
	const movementTypes = ['StockIn', 'Adjustment', 'Reserve', 'Release', 'Consume', 'Return'];
</script>

<div class="space-y-lg pb-xl">
	<PageHeader
		items={[{ label: t('common.console'), href: '/app' }, { label: t('nav.inventory'), href: '/app/inventory' }, { label: item.name }]}
		title={item.name}
		subtitle={`${item.sku} · ${item.categoryName ?? t('inventory.noCategory')}`}
	>
		{#snippet actions()}
		<a class="button secondary" href={`/app/inventory/${item.id}/label?from=detail`}>{t('inventory.printLabel')}</a>
		<a class="button secondary" href="/app/inventory">{t('inventory.detail.back')}</a>
		{/snippet}
	</PageHeader>

	<div class="grid grid-cols-1 lg:grid-cols-[1fr_360px] gap-lg">
		<div class="space-y-lg">
			<section data-bento class="bg-white border border-stone rounded-lg p-lg">
				<h3 class="font-subheading text-subheading font-semibold text-primary mb-md">{t('inventory.detail.stockSummary')}</h3>
				<div class="grid grid-cols-2 md:grid-cols-4 gap-md text-body-sm">
					<div><span class="text-secondary">{t('inventory.detail.onHand')}</span><strong class="block text-primary text-headline-sm">{stock.onHandQuantity}</strong></div>
					<div><span class="text-secondary">{t('inventory.detail.reserved')}</span><strong class="block text-primary text-headline-sm">{stock.reservedQuantity}</strong></div>
					<div><span class="text-secondary">{t('inventory.detail.available')}</span><strong class="block text-success text-headline-sm">{stock.availableQuantity}</strong></div>
					<div><span class="text-secondary">{t('inventory.reorder')}</span><strong class="block {stock.isLowStock ? 'text-error' : 'text-primary'} text-headline-sm">{stock.reorderLevel}</strong></div>
				</div>
			</section>

			<section data-bento class="bg-white border border-stone rounded-lg p-lg">
				<h3 class="font-subheading text-subheading font-semibold text-primary mb-md">{t('inventory.detail.pricing')}</h3>
				<div class="grid grid-cols-2 md:grid-cols-3 gap-md text-body-sm">
					<div><span class="text-secondary">{t('inventory.purchasePrice')}</span><strong class="block text-primary">{pricing.purchasePrice == null ? '-' : formatCurrency(pricing.purchasePrice, data.locale, currencyCode)}</strong></div>
					<div><span class="text-secondary">{t('inventory.shippingCost')}</span><strong class="block text-primary">{pricing.shippingCost == null ? '-' : formatCurrency(pricing.shippingCost, data.locale, currencyCode)}</strong></div>
					<div><span class="text-secondary">{t('inventory.serviceFee')}</span><strong class="block text-primary">{pricing.serviceFee == null ? '-' : formatCurrency(pricing.serviceFee, data.locale, currencyCode)}</strong></div>
					<div><span class="text-secondary">{t('inventory.marginPercent')}</span><strong class="block text-primary">{pricing.marginPercent ?? '-'}%</strong></div>
					<div><span class="text-secondary">{t('inventory.taxPercent')}</span><strong class="block text-primary">{pricing.taxPercent ?? '-'}%</strong></div>
					<div><span class="text-secondary">{t('inventory.salePrice')}</span><strong class="block text-primary">{formatCurrency(pricing.fixedSalePrice, data.locale, currencyCode)}</strong></div>
					<div><span class="text-secondary">{t('inventory.priceBeforeTax')}</span><strong class="block text-primary">{pricing.priceBeforeTax == null ? '-' : formatCurrency(pricing.priceBeforeTax, data.locale, currencyCode)}</strong></div>
					<div><span class="text-secondary">{t('inventory.taxAmount')}</span><strong class="block text-primary">{pricing.taxAmount == null ? '-' : formatCurrency(pricing.taxAmount, data.locale, currencyCode)}</strong></div>
					<div><span class="text-secondary">{t('inventory.priceAfterTax')}</span><strong class="block text-primary">{pricing.priceAfterTax == null ? '-' : formatCurrency(pricing.priceAfterTax, data.locale, currencyCode)}</strong></div>
				</div>
			</section>

			<section data-bento class="bg-white border border-stone rounded-lg !overflow-visible relative">
				<div class="p-lg border-b border-stone flex flex-wrap items-end justify-between gap-md">
					<div>
						<h3 class="font-subheading text-subheading font-semibold text-primary">{t('inventory.detail.movements')}</h3>
						<p class="text-body-sm text-secondary">{t('inventory.detail.movementsSubtitle')}</p>
					</div>
					<a class="button secondary" href={data.exportUrl}>{t('inventory.detail.exportMovements')}</a>
				</div>
				<form method="GET" class="p-md bg-ash border-b border-stone flex flex-wrap gap-sm items-end !overflow-visible relative z-30">
					<div class="space-y-xs min-w-[160px]">
						<span class="block text-body-sm text-secondary">{t('inventory.import.action')}</span>
						<Dropdown
							name="type"
							value={data.filters.type ?? ''}
							options={[
								{ value: '', label: t('inventory.detail.allTypes') },
								...movementTypes.map((type) => ({ value: type, label: type }))
							]}
						/>
					</div>
					<div class="space-y-xs min-w-[175px]">
						<span class="block text-body-sm text-secondary">{t('reports.from')}</span>
						<DatePicker name="from" value={data.filters.from ?? ''} placeholder={t('reports.from')} clearable />
					</div>
					<div class="space-y-xs min-w-[175px]">
						<span class="block text-body-sm text-secondary">{t('reports.to')}</span>
						<DatePicker name="to" value={data.filters.to ?? ''} placeholder={t('reports.to')} clearable />
					</div>
					<button class="button" type="submit">{t('reports.apply')}</button>
				</form>
				<div class="overflow-x-auto">
					<table class="w-full text-left text-body-sm">
						<tbody class="divide-y divide-stone">
							{#each data.movements as movement}
								<tr>
									<td class="px-lg py-sm font-semibold text-primary">{movement.type}</td>
									<td class="px-lg py-sm text-secondary">{movement.quantity}</td>
									<td class="px-lg py-sm text-secondary">
										{#if movement.serviceTicketId && movement.ticketNumber}
											<a class="font-medium text-primary hover:underline" href={`/app/tickets/${movement.serviceTicketId}`}>{movement.ticketNumber}</a>
										{:else}
											{movement.ticketNumber ?? '-'}
										{/if}
									</td>
									<td class="px-lg py-sm text-secondary">{movement.reason ?? '-'}</td>
									<td class="px-lg py-sm text-right text-secondary">{formatDate(movement.createdAt, data.locale, { dateStyle: 'medium', timeStyle: 'short' })}</td>
								</tr>
							{/each}
							{#if data.movements.length === 0}
								<tr><td colspan="5" class="px-lg py-lg text-center text-secondary">{t('inventory.detail.noMovements')}</td></tr>
							{/if}
						</tbody>
					</table>
				</div>
			</section>
		</div>

		<aside class="space-y-lg">
			<section data-bento class="bg-white border border-stone rounded-lg p-lg">
				<h3 class="font-subheading text-subheading font-semibold text-primary mb-md">{t('inventory.detail.itemInfo')}</h3>
				<div class="space-y-sm text-body-sm">
					<p><span class="text-secondary">{t('inventory.itemType')}</span><strong class="block text-primary">{t(`inventory.itemType.${item.itemType}`)}</strong></p>
					<p><span class="text-secondary">{t('inventory.condition')}</span><strong class="block text-primary">{t(`inventory.condition.${item.condition}`)}</strong></p>
					<p><span class="text-secondary">{t('inventory.unit')}</span><strong class="block text-primary">{item.unit}</strong></p>
					<p><span class="text-secondary">{t('inventory.supplier')}</span><strong class="block text-primary">{item.supplierName ?? '-'}</strong></p>
					<p><span class="text-secondary">{t('inventory.barcodeValue')}</span><strong class="block text-primary">{item.barcodeValue ?? item.sku}</strong></p>
					<p><span class="text-secondary">{t('inventory.location')}</span><strong class="block text-primary">{item.locationNote ?? '-'}</strong></p>
					<p><span class="text-secondary">{t('inventory.quality')}</span><strong class="block text-primary">{item.quality ?? '-'}</strong></p>
					<p><span class="text-secondary">{t('inventory.warrantyDays')}</span><strong class="block text-primary">{item.warrantyDays ?? '-'}</strong></p>
				</div>
			</section>

			<section data-bento class="bg-white border border-stone rounded-lg p-lg">
				<h3 class="font-subheading text-subheading font-semibold text-primary mb-md">{t('inventory.detail.linkedTickets')}</h3>
				<div class="space-y-sm">
					{#each data.detail.linkedTickets as ticket}
						<a class="block rounded-lg border border-stone p-sm hover:bg-ash" href={`/app/tickets/${ticket.ticketId}`}>
							<strong class="text-primary">{ticket.ticketNumber}</strong>
							<p class="text-body-sm text-secondary">{ticket.status} · {ticket.quantity}</p>
						</a>
					{/each}
					{#if data.detail.linkedTickets.length === 0}
						<p class="text-body-sm text-secondary">{t('inventory.detail.noLinkedTickets')}</p>
					{/if}
				</div>
			</section>
		</aside>
	</div>
</div>
