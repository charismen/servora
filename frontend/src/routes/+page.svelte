<script lang="ts">
	import { goto } from '$app/navigation';
	import { page } from '$app/stores';
	import { createTranslator, locales } from '$lib/i18n';
	import { magnetic } from '$lib/components/magnetic';
	import Dropdown from '$lib/components/ui/dropdown/Dropdown.svelte';
	import logo from '$lib/assets/favicon.svg';

	let { data } = $props();
	const t = $derived(createTranslator(data.locale));

	let localeFormEl: HTMLFormElement | undefined = $state();
	let trackingQuery = $state('');
	let trackingError = $state('');

	function handleTrackingSubmit(e: SubmitEvent) {
		e.preventDefault();
		const trimmed = trackingQuery.trim();
		if (!trimmed) {
			trackingError = t('home.trackError');
			return;
		}
		trackingError = '';
		goto(`/track/${encodeURIComponent(trimmed)}`);
	}
</script>

<svelte:head>
	<title>{t('brand.name')} · {t('home.title')}</title>
</svelte:head>

<div class="min-h-screen w-full bg-surface text-ink flex flex-col font-sans selection:bg-focus-blue/30 selection:text-ink">
	<!-- Subtle ambient structural grid -->
	<div
		class="fixed inset-0 bg-[linear-gradient(to_right,var(--color-stone)_1px,transparent_1px),linear-gradient(to_bottom,var(--color-stone)_1px,transparent_1px)] bg-[size:32px_32px] opacity-40 [mask-image:radial-gradient(ellipse_70%_60%_at_50%_0%,#000_60%,transparent_100%)] pointer-events-none"
		aria-hidden="true"
	></div>

	<!-- Header Navigation -->
	<header class="w-full max-w-7xl mx-auto px-md sm:px-lg h-20 flex items-center justify-between z-10 relative">
		<div class="flex items-center gap-sm min-w-0">
			<img src={logo} alt={t('brand.logoAlt')} class="w-9 h-9 object-contain shrink-0 logo-group" />
			<div class="flex flex-col min-w-0">
				<strong class="font-bold text-ink leading-tight tracking-tight text-body">{t('brand.name')}</strong>
				<span class="text-[11px] text-metal tracking-wide uppercase mt-0.5 truncate hidden sm:inline-block">
					{t('brand.tagline')}
				</span>
			</div>
		</div>

		<div class="flex items-center gap-xs sm:gap-md shrink-0">
			<!-- Language Picker using shared Dropdown component -->
			<form bind:this={localeFormEl} method="POST" action="/locale" class="inline-block">
				<input type="hidden" name="redirectTo" value={$page.url.pathname + $page.url.search} />
				<label class="sr-only" for="landing-locale-select">{t('locale.label')}</label>
				<Dropdown
					name="locale"
					value={data.locale}
					options={locales.map(l => ({ value: l.code, label: l.label }))}
					onchange={() => setTimeout(() => localeFormEl?.requestSubmit(), 0)}
					size="sm"
					class="w-36 sm:w-40 shrink-0"
					align="right"
				/>
			</form>

			<a
				use:magnetic
				href="/login"
				class="px-4 py-2 bg-ink hover:opacity-90 active:scale-95 text-white font-medium text-xs rounded-button transition-all shadow-sm flex items-center gap-1.5"
			>
				<span class="material-symbols-outlined text-[16px]">lock</span>
				<span>{t('auth.submit')}</span>
			</a>
		</div>
	</header>

	<!-- Main Hero & Gateway Content -->
	<main class="flex-1 max-w-6xl w-full mx-auto px-md sm:px-lg pt-8 pb-16 z-10 flex flex-col items-center">
		<!-- Hero Section -->
		<section class="w-full text-center max-w-4xl pt-6 pb-12 flex flex-col items-center">
			<h1 class="font-serif text-4xl sm:text-5xl md:text-6xl font-normal tracking-tight text-ink leading-[1.08] max-w-3xl">
				{t('home.title')}
			</h1>

			<p class="mt-5 text-secondary text-base sm:text-lg max-w-2xl leading-relaxed font-normal">
				{t('home.subtitle')}
			</p>

			<!-- Quick Primary Actions -->
			<div class="mt-8 flex flex-wrap items-center justify-center gap-3">
				<a
					use:magnetic
					href="/app"
					class="px-6 h-12 bg-ink hover:bg-black active:scale-[0.98] text-white font-medium text-body-sm rounded-button flex items-center justify-center shadow-md transition-all gap-2"
				>
					<span class="material-symbols-outlined text-[18px]">space_dashboard</span>
					<span>{t('home.openWorkspace')}</span>
				</a>
				<a
					use:magnetic
					href="/login"
					class="px-6 h-12 bg-white border border-stone hover:border-slate active:scale-[0.98] text-ink font-medium text-body-sm rounded-button flex items-center justify-center shadow-2xs transition-all gap-2"
				>
					<span class="material-symbols-outlined text-[18px]">login</span>
					<span>{t('auth.submit')}</span>
				</a>
			</div>
		</section>

		<!-- Public Repair Status Lookup Card -->
		<section class="w-full max-w-2xl mt-2 mb-14" aria-labelledby="tracking-lookup-heading">
			<div class="bg-white border border-stone rounded-cards p-6 sm:p-7 shadow-xs">
				<div class="flex items-center gap-3 mb-2">
					<div class="w-8 h-8 rounded-full bg-ash flex items-center justify-center text-ink shrink-0">
						<span class="material-symbols-outlined text-[18px]">search_check</span>
					</div>
					<div>
						<h2 id="tracking-lookup-heading" class="font-bold text-body text-ink leading-tight">
							{t('home.trackTitle')}
						</h2>
						<p class="text-xs text-metal mt-0.5">
							{t('home.trackHelp')}
						</p>
					</div>
				</div>

				<form onsubmit={handleTrackingSubmit} class="mt-4 flex flex-col sm:flex-row gap-2.5">
					<div class="relative flex-1">
						<span class="absolute left-3.5 top-1/2 -translate-y-1/2 text-metal material-symbols-outlined text-[18px] pointer-events-none">
							tag
						</span>
						<input
							type="text"
							bind:value={trackingQuery}
							placeholder={t('home.trackPlaceholder')}
							class="w-full h-11 pl-10 pr-3.5 bg-ash/50 border border-stone focus:bg-white focus:border-ink rounded-input text-body-sm text-ink outline-none transition-colors"
							aria-label={t('home.trackPlaceholder')}
						/>
					</div>
					<button
						type="submit"
						class="h-11 px-5 bg-ink hover:opacity-90 active:scale-[0.98] text-white font-medium text-body-sm rounded-button shrink-0 transition-all shadow-2xs flex items-center justify-center gap-1.5"
					>
						<span>{t('home.trackButton')}</span>
						<span class="material-symbols-outlined text-[16px]">arrow_forward</span>
					</button>
				</form>

				{#if trackingError}
					<p class="mt-2 text-xs text-danger font-medium flex items-center gap-1">
						<span class="material-symbols-outlined text-[14px]">error</span>
						{trackingError}
					</p>
				{/if}
			</div>
		</section>

		<!-- Bento Feature Highlights Grid -->
		<section class="w-full grid grid-cols-1 md:grid-cols-3 gap-5 text-left" aria-label={t('auth.proofLabel')}>
			<!-- Feature 1: Intake & Custody (Emphasized) -->
			<div class="rounded-cards border border-stone bg-white p-6 shadow-2xs hover:border-slate transition-all flex flex-col justify-between">
				<div>
					<div class="w-10 h-10 rounded-lg bg-ash flex items-center justify-center text-ink mb-4">
						<span class="material-symbols-outlined text-[22px]">add_a_photo</span>
					</div>
					<h3 class="font-bold text-body text-ink mb-1.5">{t('home.featureIntakeTitle')}</h3>
					<p class="text-body-sm text-secondary leading-relaxed">{t('home.featureIntakeBody')}</p>
				</div>
				<div class="mt-6 pt-4 border-t border-stone/60 flex items-center gap-2 text-xs text-metal">
					<span class="material-symbols-outlined text-[16px]">verified</span>
					<span>{t('home.featureIntakeFoot')}</span>
				</div>
			</div>

			<!-- Feature 2: Kanban Flow -->
			<div class="rounded-cards border border-stone bg-white p-6 shadow-2xs hover:border-slate transition-all flex flex-col justify-between">
				<div>
					<div class="w-10 h-10 rounded-lg bg-ash flex items-center justify-center text-ink mb-4">
						<span class="material-symbols-outlined text-[22px]">view_kanban</span>
					</div>
					<h3 class="font-bold text-body text-ink mb-1.5">{t('home.featureKanbanTitle')}</h3>
					<p class="text-body-sm text-secondary leading-relaxed">{t('home.featureKanbanBody')}</p>
				</div>
				<div class="mt-6 pt-4 border-t border-stone/60 flex items-center gap-2 text-xs text-metal">
					<span class="material-symbols-outlined text-[16px]">tune</span>
					<span>{t('home.featureKanbanFoot')}</span>
				</div>
			</div>

			<!-- Feature 3: Real-Time Inventory -->
			<div class="rounded-cards border border-stone bg-white p-6 shadow-2xs hover:border-slate transition-all flex flex-col justify-between">
				<div>
					<div class="w-10 h-10 rounded-lg bg-ash flex items-center justify-center text-ink mb-4">
						<span class="material-symbols-outlined text-[22px]">inventory_2</span>
					</div>
					<h3 class="font-bold text-body text-ink mb-1.5">{t('home.featureInventoryTitle')}</h3>
					<p class="text-body-sm text-secondary leading-relaxed">{t('home.featureInventoryBody')}</p>
				</div>
				<div class="mt-6 pt-4 border-t border-stone/60 flex items-center gap-2 text-xs text-metal">
					<span class="material-symbols-outlined text-[16px]">inventory</span>
					<span>{t('home.featureInventoryFoot')}</span>
				</div>
			</div>
		</section>
	</main>

	<!-- Footer -->
	<footer class="w-full max-w-7xl mx-auto px-md sm:px-lg py-8 border-t border-stone flex flex-col sm:flex-row items-center justify-between gap-3 text-xs text-secondary z-10">
		<div class="flex items-center gap-2">
			<img src={logo} alt="" class="w-4 h-4 object-contain opacity-70" />
			<span>© {new Date().getFullYear()} {t('brand.name')}.</span>
		</div>
		<div class="flex items-center gap-5">
			<a class="hover:text-ink transition-colors" href="/login">{t('auth.submit')}</a>
			<a class="hover:text-ink transition-colors" href="/app">{t('home.openWorkspace')}</a>
		</div>
	</footer>
</div>
