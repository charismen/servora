<script lang="ts">
	import { enhance } from '$app/forms';
	import { createTranslator } from '$lib/i18n';
	import logo from '$lib/assets/favicon.svg';
	import type { ActionData, PageData } from './$types';

	let { data, form }: { data: PageData; form: ActionData } = $props();
	const t = $derived(createTranslator(data.locale));
	let submitting = $state(false);
</script>

<svelte:head>
	<title>{t('auth.forgotTitle')} · {t('brand.name')}</title>
</svelte:head>

<div class="min-h-screen bg-[#faf8f5] text-[#1c1815] flex flex-col items-center justify-center px-4 py-12">
	<div class="w-full max-w-md space-y-6">
		<div class="flex items-center justify-center gap-2.5">
			<img src={logo} alt={t('brand.logoAlt')} class="w-9 h-9" />
			<span class="font-bold text-lg tracking-tight">{t('brand.name')}</span>
		</div>

		<section class="rounded-2xl border border-[#e6dfd5] bg-white p-6 sm:p-8 shadow-2xs space-y-5">
			<div>
				<h1 class="text-xl font-bold tracking-tight text-[#1c1815]">{t('auth.forgotTitle')}</h1>
				<p class="text-sm text-[#6b6158] mt-1">{t('auth.forgotSubtitle')}</p>
			</div>

			{#if form?.sent}
				<div class="rounded-xl border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-900 space-y-1">
					<p class="font-semibold">{t('auth.forgotSentTitle')}</p>
					<p class="text-xs text-emerald-800">{t('auth.forgotSentBody')}</p>
				</div>
				<a
					href="/login"
					class="w-full inline-flex items-center justify-center rounded-xl bg-[#1c1815] px-4 py-2.5 text-sm font-semibold text-white hover:opacity-90 transition"
				>
					{t('auth.twoFactorBackToLogin')}
				</a>
			{:else}
				{#if form?.message || form?.messageKey}
					<div class="rounded-xl border border-red-200 bg-red-50 px-3.5 py-2.5 text-xs text-red-800" role="alert">
						{form.message ?? t(form.messageKey ?? 'common.error')}
					</div>
				{/if}

				<form
					method="POST"
					class="space-y-4"
					use:enhance={() => {
						submitting = true;
						return async ({ update }) => {
							await update();
							submitting = false;
						};
					}}
				>
					<div class="space-y-1.5">
						<label for="forgot-email" class="block text-xs font-semibold uppercase tracking-wider text-[#6b6158]">
							{t('auth.email')}
						</label>
						<input
							id="forgot-email"
							name="email"
							type="email"
							value={form?.email ?? ''}
							placeholder={t('auth.emailPlaceholder')}
							required
							class="w-full h-11 rounded-xl border border-[#e6dfd5] bg-[#faf8f5]/40 px-3.5 text-sm focus:outline-none focus:ring-2 focus:ring-[#b45039]/30 focus:border-[#b45039]"
						/>
					</div>

					<button
						type="submit"
						disabled={submitting}
						class="w-full inline-flex items-center justify-center gap-2 rounded-xl bg-[#1c1815] px-4 py-2.5 text-sm font-semibold text-white hover:opacity-90 transition cursor-pointer disabled:opacity-60"
					>
						{#if submitting}
							<span class="material-symbols-outlined text-[16px] animate-spin">progress_activity</span>
						{/if}
						<span>{t('auth.forgotSubmit')}</span>
					</button>
				</form>

				<div class="pt-3 border-t border-[#e6dfd5] text-center">
					<a href="/login" class="text-xs font-medium text-[#6b6158] hover:text-[#1c1815]">
						{t('auth.twoFactorBackToLogin')}
					</a>
				</div>
			{/if}
		</section>
	</div>
</div>
