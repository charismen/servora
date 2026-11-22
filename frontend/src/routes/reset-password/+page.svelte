<script lang="ts">
	import { enhance } from '$app/forms';
	import { createTranslator } from '$lib/i18n';
	import logo from '$lib/assets/favicon.svg';
	import type { ActionData, PageData } from './$types';

	let { data, form }: { data: PageData; form: ActionData } = $props();
	const t = $derived(createTranslator(data.locale));
	let submitting = $state(false);
	const emailValue = $derived(form?.email ?? data.email);
	const tokenValue = $derived(form?.token ?? data.token);
</script>

<svelte:head>
	<title>{t('auth.resetTitle')} · {t('brand.name')}</title>
</svelte:head>

<div class="min-h-screen bg-[#faf8f5] text-[#1c1815] flex flex-col items-center justify-center px-4 py-12">
	<div class="w-full max-w-md space-y-6">
		<div class="flex items-center justify-center gap-2.5">
			<img src={logo} alt={t('brand.logoAlt')} class="w-9 h-9" />
			<span class="font-bold text-lg tracking-tight">{t('brand.name')}</span>
		</div>

		<section class="rounded-2xl border border-[#e6dfd5] bg-white p-6 sm:p-8 shadow-2xs space-y-5">
			<div>
				<h1 class="text-xl font-bold tracking-tight text-[#1c1815]">{t('auth.resetTitle')}</h1>
				<p class="text-sm text-[#6b6158] mt-1">{t('auth.resetSubtitle')}</p>
			</div>

			{#if !tokenValue || !emailValue}
				<div class="rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900">
					{t('auth.resetInvalidLink')}
				</div>
				<a
					href="/forgot-password"
					class="w-full inline-flex items-center justify-center rounded-xl bg-[#1c1815] px-4 py-2.5 text-sm font-semibold text-white hover:opacity-90 transition"
				>
					{t('auth.forgotSubmit')}
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
					<input type="hidden" name="email" value={emailValue} />
					<input type="hidden" name="token" value={tokenValue} />

					<div class="rounded-xl bg-[#f4f0ea] px-3.5 py-2.5 text-xs text-[#6b6158]">
						{t('auth.email')}: <strong class="text-[#1c1815] font-mono">{emailValue}</strong>
					</div>

					<div class="space-y-1.5">
						<label for="newPassword" class="block text-xs font-semibold uppercase tracking-wider text-[#6b6158]">
							{t('auth.newPassword')}
						</label>
						<input
							id="newPassword"
							name="newPassword"
							type="password"
							minlength="6"
							required
							autocomplete="new-password"
							class="w-full h-11 rounded-xl border border-[#e6dfd5] bg-[#faf8f5]/40 px-3.5 text-sm focus:outline-none focus:ring-2 focus:ring-[#b45039]/30 focus:border-[#b45039]"
						/>
					</div>

					<div class="space-y-1.5">
						<label for="confirmPassword" class="block text-xs font-semibold uppercase tracking-wider text-[#6b6158]">
							{t('auth.confirmPassword')}
						</label>
						<input
							id="confirmPassword"
							name="confirmPassword"
							type="password"
							minlength="6"
							required
							autocomplete="new-password"
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
						<span>{t('auth.resetSubmit')}</span>
					</button>
				</form>
			{/if}
		</section>
	</div>
</div>
