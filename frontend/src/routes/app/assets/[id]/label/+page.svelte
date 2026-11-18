<script lang="ts">
	import { page } from '$app/stores';
	import { createTranslator } from '$lib/i18n';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();
	const t = $derived(createTranslator(data.locale));
	const asset = $derived(data.asset);
	const brandName = $derived(asset.companyName || data.user?.shop?.name || t('brand.name'));
	const origin = $derived($page.url.origin);
	const scanUrl = $derived(`${origin}/app/assets/${asset.id}?token=${asset.publicToken}`);
	const qrUrl = $derived(`https://api.qrserver.com/v1/create-qr-code/?size=160x160&data=${encodeURIComponent(scanUrl)}`);

	let width = $state(90);
	let height = $state(55);

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
		const normalized = (value || 'AST').toUpperCase().replace(/[^0-9A-Z .$/+%-]/g, '-');
		const bars: Bar[] = [];
		let x = 0;

		for (const char of `*${normalized}*`) {
			const pattern = code39[char] ?? code39['-'];
			for (const [index, unit] of [...pattern].entries()) {
				const partWidth = unit === 'w' ? 3 : 1;
				if (index % 2 === 0) bars.push({ x, width: partWidth });
				x += partWidth;
			}
			x += 1;
		}

		return { bars, width: x };
	}

	const barcode = $derived(buildCode39(asset.assetTag));
</script>

<svelte:head>
	<title>{asset.assetTag} · {t('assets.label.title')}</title>
</svelte:head>

<div class="min-h-screen bg-ash/60 p-6 print:bg-white print:p-0">
	<!-- Control Bar (hidden when printing) -->
	<div class="mb-6 flex flex-wrap items-center justify-between gap-4 print:hidden max-w-[600px] mx-auto bg-white p-4 rounded-xl border border-stone shadow-2xs">
		<div class="flex items-center gap-2">
			<button
				class="h-9 px-4 rounded-button font-medium bg-primary text-white hover:opacity-90 shadow-2xs text-body-sm cursor-pointer transition-opacity inline-flex items-center gap-1.5"
				type="button"
				onclick={() => window.print()}
			>
				<span class="material-symbols-outlined text-[18px]">print</span>
				<span>{t('assets.label.print')}</span>
			</button>
			<a
				class="h-9 px-3 rounded-button border border-stone bg-white text-secondary hover:text-primary hover:bg-ash text-body-sm font-medium shadow-2xs inline-flex items-center gap-1"
				href={data.backHref}
			>
				<span class="material-symbols-outlined text-[18px]">arrow_back</span>
				<span>{t('assets.detail.back')}</span>
			</a>
		</div>

		<div class="flex items-center gap-3 text-caption font-semibold text-secondary">
			<label for="label-width" class="flex items-center gap-1.5 cursor-pointer">
				<span>Width (mm):</span>
				<input
					id="label-width"
					type="number"
					bind:value={width}
					min="40"
					max="200"
					class="w-16 border border-stone rounded-md px-2 py-1 text-center font-mono text-primary bg-white focus:outline-none focus:ring-1 focus:ring-primary"
				/>
			</label>
			<label for="label-height" class="flex items-center gap-1.5 cursor-pointer">
				<span>Height (mm):</span>
				<input
					id="label-height"
					type="number"
					bind:value={height}
					min="30"
					max="150"
					class="w-16 border border-stone rounded-md px-2 py-1 text-center font-mono text-primary bg-white focus:outline-none focus:ring-1 focus:ring-primary"
				/>
			</label>
		</div>
	</div>

	<!-- Printable Asset Tag Sticker Stage -->
	<div class="flex justify-center print:block overflow-x-auto max-w-full pb-4">
		<section
			class="bg-white border-2 border-stone-800 rounded-lg p-3.5 shadow-md print:shadow-none print:border-stone-900 flex flex-col justify-between overflow-hidden"
			style={`width: ${width}mm; min-height: ${height}mm;`}
		>
			<!-- Header -->
			<div class="border-b border-stone-700 pb-1.5 flex items-start justify-between gap-2">
				<div class="min-w-0">
					<p class="text-[9px] font-bold uppercase tracking-widest text-secondary truncate">{brandName}</p>
					<h1 class="text-[14px] font-bold text-primary leading-tight line-clamp-1">{asset.name}</h1>
				</div>
				<span class="shrink-0 font-mono text-[13px] font-black tracking-wider bg-ash px-2 py-0.5 rounded border border-stone-400">
					{asset.assetTag}
				</span>
			</div>

			<!-- Middle: QR Code + Meta Info -->
			<div class="grid grid-cols-[1fr_80px] gap-2 py-2 items-center">
				<div class="space-y-1 text-[10px] text-secondary">
					{#if asset.categoryName}
						<div class="truncate"><span class="font-bold text-primary">{t('assets.category')}:</span> {asset.categoryName}</div>
					{/if}
					{#if asset.brand || asset.model}
						<div class="truncate"><span class="font-bold text-primary">{t('assets.brand')}:</span> {[asset.brand, asset.model].filter(Boolean).join(' ')}</div>
					{/if}
					{#if asset.serialNumber}
						<div class="truncate font-mono"><span class="font-bold text-primary">S/N:</span> {asset.serialNumber}</div>
					{/if}
					{#if asset.location}
						<div class="truncate"><span class="font-bold text-primary">{t('assets.location')}:</span> {asset.location}</div>
					{/if}
					{#if asset.assignedUserName}
						<div class="truncate"><span class="font-bold text-primary">{t('assets.custodian')}:</span> {asset.assignedUserName}</div>
					{/if}
				</div>

				<div class="flex flex-col items-center justify-center">
					<img class="h-[72px] w-[72px] object-contain" src={qrUrl} alt={asset.assetTag} />
					<span class="text-[8px] font-mono text-secondary mt-0.5 text-center leading-tight">SCAN ASSET</span>
				</div>
			</div>

			<!-- Footer: Code39 Barcode -->
			<div class="border-t border-dashed border-stone-400 pt-1 text-center">
				<svg class="barcode mx-auto h-7 w-full max-w-[200px]" viewBox={`0 0 ${barcode.width} 32`} preserveAspectRatio="none" role="img" aria-label={`Barcode ${asset.assetTag}`}>
					{#each barcode.bars as bar}
						<rect x={bar.x} y="0" width={bar.width} height="32" fill="currentColor"></rect>
					{/each}
				</svg>
				<p class="font-mono text-[10px] font-bold tracking-widest text-primary mt-0.5">{asset.assetTag}</p>
			</div>
		</section>
	</div>
</div>

<style>
	@media print {
		@page {
			margin: 4mm;
		}
		:global(body) {
			background: white !important;
		}
	}
</style>
