<script lang="ts">
	import { createTranslator } from '$lib/i18n';
	import logo from '$lib/assets/favicon.svg';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();
	const t = $derived(createTranslator(data.locale));
</script>

<svelte:head>
	<title>{t('auth.verifyEmailTitle')} · {t('brand.name')}</title>
</svelte:head>

<div class="min-h-screen bg-[#faf8f5] text-[#1c1815] flex flex-col items-center justify-center px-4 py-12">
	<div class="w-full max-w-md space-y-6">
		<div class="flex items-center justify-center gap-2.5">
			<img src={logo} alt={t('brand.logoAlt')} class="w-9 h-9" />
			<span class="font-bold text-lg tracking-tight">{t('brand.name')}</span>
		</div>

		<section class="rounded-2xl border border-[#e6dfd5] bg-white p-6 sm:p-8 shadow-2xs space-y-5 text-center">
			{#if data.verified}
				<div class="mx-auto w-12 h-12 rounded-full bg-emerald-100 text-emerald-700 flex items-center justify-center">
					<span class="material-symbols-outlined text-[24px]">verified</span>
				</div>
				<div>
					<h1 class="text-xl font-bold tracking-tight text-[#1c1815]">{t('auth.verifyEmailSuccessTitle')}</h1>
					<p class="text-sm text-[#6b6158] mt-1">{t('auth.verifyEmailSuccessLogin')}</p>
				</div>
				<a
					href="/login?verified=success"
					class="w-full inline-flex items-center justify-center rounded-xl bg-[#1c1815] px-4 py-2.5 text-sm font-semibold text-white hover:opacity-90 transition"
				>
					{t('auth.twoFactorBackToLogin')}
				</a>
			{:else}
				<div class="mx-auto w-12 h-12 rounded-full bg-red-100 text-red-700 flex items-center justify-center">
					<span class="material-symbols-outlined text-[24px]">error</span>
				</div>
				<div>
					<h1 class="text-xl font-bold tracking-tight text-[#1c1815]">{t('auth.verifyEmailFailedTitle')}</h1>
					<p class="text-sm text-[#6b6158] mt-1">
						{data.errorMessage ?? t('auth.resetInvalidLink')}
					</p>
				</div>
				<a
					href="/login"
					class="w-full inline-flex items-center justify-center rounded-xl bg-[#1c1815] px-4 py-2.5 text-sm font-semibold text-white hover:opacity-90 transition"
				>
					{t('auth.twoFactorBackToLogin')}
				</a>
			{/if}
		</section>
	</div>
</div>
