<script lang="ts">
	import { createTranslator, formatCurrency } from '$lib/i18n';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();
	const t = $derived(createTranslator(data.locale));
	const item = $derived(data.detail.item);
	const currencyCode = $derived(data.shopProfile?.currencyCode ?? 'IDR');
	const code = $derived(item.barcodeValue || item.sku);
	const barcode = $derived(buildCode39(code));
	let width = $state(140);
	let height = $state(90);

	type Bar = { x: number; width: number };
	const code39: Record<string, string> = {
		'0': 'nnnwwnwnn', '1': 'wnnwnnnnw', '2': 'nnwwnnnnw', '3': 'wnwwnnnnn', '4': 'nnnwwnnnw',
		'5': 'wnnwwnnnn', '6': 'nnwwwnnnn', '7': 'nnnwnnwnw', '8': 'wnnwnnwnn', '9': 'nnwwnnwnn',
		A: 'wnnnnwnnw', B: 'nnwnnwnnw', C: 'wnwnnwnnn', D: 'nnnnwwnnw', E: 'wnnnwwnnn',
		F: 'nnwnwwnnn', G: 'nnnnnwwnw', H: 'wnnnnwwnn', I: 'nnwnnwwnn', J: 'nnnnwwwnn',
		K: 'wnnnnnnww', L: 'nnwnnnnww', M: 'wnwnnnnwn', N: 'nnnnwnnww', O: 'wnnnwnnwn',
		P: 'nnwnwnnwn', Q: 'nnnnnnwww', R: 'wnnnnnwwn', S: 'nnwnnnwwn', T: 'nnnnwnwwn',
		U: 'wwnnnnnnw', V: 'nwwnnnnnw', W: 'wwwnnnnnn', X: 'nwnnwnnnw', Y: 'wwnnwnnnn',
		Z: 'nwwnwnnnn', '-': 'nwnnnnwnw', '.': 'wwnnnnwnn', ' ': 'nwwnnnwnn', '$': 'nwnwnwnnn',
		'/': 'nwnwnnnwn', '+': 'nwnnnwnwn', '%': 'nnnwnwnwn', '*': 'nwnnwnwnn'
	};

	function buildCode39(value: string) {
		const normalized = value.toUpperCase().replace(/[^0-9A-Z .$/+%-]/g, '-');
		const bars: Bar[] = [];
		let x = 0;

		for (const char of `*${normalized}*`) {
			for (const [index, unit] of [...code39[char]].entries()) {
				const partWidth = unit === 'w' ? 3 : 1;
				if (index % 2 === 0) bars.push({ x, width: partWidth });
				x += partWidth;
			}
			x += 1;
		}

		return { bars, width: x };
	}
</script>

<div class="min-h-screen bg-ash p-lg print:bg-white print:p-0">
	<div class="mb-md flex flex-wrap items-center gap-md print:hidden">
		<div class="flex gap-sm">
			<button class="button" type="button" onclick={() => window.print()}>{t('inventory.printLabel')}</button>
			<a class="button secondary" href={data.backHref}>{t('inventory.detail.back')}</a>
		</div>
		
		<div class="flex flex-wrap items-center gap-md text-body-sm font-semibold text-secondary">
			<label class="flex items-center gap-xs">
				<span>{t('inventory.labelWidthMm')}</span>
				<input type="number" bind:value={width} min="30" max="200" class="w-16 border border-slate rounded-[7px] p-1 text-center font-mono text-primary bg-white focus:outline-none focus:ring-1 focus:ring-focus-blue" />
			</label>
			<label class="flex items-center gap-xs">
				<span>{t('inventory.labelHeightMm')}</span>
				<input type="number" bind:value={height} min="20" max="200" class="w-16 border border-slate rounded-[7px] p-1 text-center font-mono text-primary bg-white focus:outline-none focus:ring-1 focus:ring-focus-blue" />
			</label>
		</div>
	</div>

	<section 
		class="label bg-white border border-stone rounded-lg p-lg shadow-sm print:shadow-none"
		style={`width: ${width}mm; min-height: ${height}mm;`}
	>
		<div class="flex items-start justify-between gap-md border-b border-stone pb-sm">
			<div class="min-w-0">
				<p class="text-[10px] uppercase tracking-widest text-secondary">{t('nav.inventory')}</p>
				<h1 class="text-[18px] font-bold text-primary leading-tight">{item.name}</h1>
			</div>
			<strong class="shrink-0 rounded border border-stone px-sm py-xs text-[12px] text-primary">{item.sku}</strong>
		</div>

		<div class="grid flex-1 grid-cols-[1fr_1.35fr] gap-md py-md">
			<div class="flex flex-col items-center justify-center rounded border border-stone p-sm text-center">
				<svg class="barcode mx-auto mb-xs h-16 w-full" viewBox={`0 0 ${barcode.width} 48`} preserveAspectRatio="none" role="img" aria-label={`${t('inventory.barcode')} ${code}`}>
					{#each barcode.bars as bar}
						<rect x={bar.x} y="0" width={bar.width} height="48"></rect>
					{/each}
				</svg>
				<p class="break-all font-mono text-[15px] font-bold tracking-widest text-primary">{code}</p>
			</div>

			<div class="details-grid grid grid-cols-2 gap-xs text-[11px] text-secondary">
				<p><span>{t('inventory.itemType')}</span><strong>{t(`inventory.itemType.${item.itemType}`)}</strong></p>
				<p><span>{t('inventory.condition')}</span><strong>{t(`inventory.condition.${item.condition}`)}</strong></p>
				<p><span>{t('inventory.supplier')}</span><strong>{item.supplierName || '-'}</strong></p>
				<p><span>{t('inventory.barcodeValue')}</span><strong>{item.barcodeValue || '-'}</strong></p>
				<p><span>{t('inventory.location')}</span><strong>{item.locationNote || '-'}</strong></p>
				<p><span>{t('inventory.quality')}</span><strong>{item.quality || '-'}</strong></p>
				<p><span>{t('inventory.warrantyDays')}</span><strong>{item.warrantyDays ?? '-'}</strong></p>
				<p><span>{t('inventory.salePrice')}</span><strong>{formatCurrency(item.fixedSalePrice, data.locale, currencyCode)}</strong></p>
				<p class="col-span-2"><span>{t('inventory.secondhandSpecs')}</span><strong>{item.description || '-'}</strong></p>
			</div>
		</div>
	</section>
</div>

{@html `<style>
	@media print {
		/* Use default printer paper layout (A4/Letter) with standard page margins */
		@page {
			margin: 10mm;
		}

		/* Reset root viewport dimensions to occupy standard sheet width */
		html, body {
			width: 100% !important;
			height: auto !important;
			margin: 0 !important;
			padding: 0 !important;
			overflow: visible !important;
			background: #ffffff !important;
		}

		/* Disable layout controls and navigation elements */
		aside, header, footer, .print\:hidden {
			display: none !important;
		}

		/* Reset main layout wrappers to flow naturally on standard paper */
		main, main > div, .min-h-screen {
			margin: 0 !important;
			padding: 0 !important;
			width: 100% !important;
			min-height: 0 !important;
			background: #ffffff !important;
		}

		.label {
			border: 1px dashed #b5bdc9 !important; /* crop/cutting helper boundary line */
			border-radius: 6px !important;
			box-shadow: none !important;
			padding: 5mm !important;
			width: ${width}mm !important;
			min-height: ${height}mm !important;
			box-sizing: border-box !important;
			margin: 0 !important;
			display: flex !important;
			flex-direction: column !important;
			background: #ffffff !important;
			overflow: visible !important;
			page-break-inside: avoid !important;
		}

		/* Scale typography and graphical lines to prevent overflow */
		.label h1 {
			font-size: 14px !important;
			line-height: 1.2 !important;
		}
		.label .py-md {
			padding-top: 1mm !important;
			padding-bottom: 1mm !important;
		}
		.label .barcode {
			height: 12mm !important;
			margin-bottom: 2px !important;
		}
		.label p {
			margin-bottom: 2px !important;
		}
	}
</style>`}

<style>
	.label {
		max-width: 100%;
		display: flex;
		flex-direction: column;
		justify-content: space-between;
		box-sizing: border-box;
	}

	.barcode rect {
		fill: #111827;
	}

	p strong {
		display: block;
		color: #111827;
		overflow-wrap: anywhere;
	}

	.details-grid p {
		border-bottom: 1px solid #e5e7eb;
		padding-bottom: 4px;
	}
</style>
