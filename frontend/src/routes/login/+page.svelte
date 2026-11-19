<script lang="ts">
	import type { ActionData, PageData } from './$types';
	import { enhance } from '$app/forms';
	import { createTranslator, locales } from '$lib/i18n';
	import { formatEnhanceUnexpectedError } from '$lib/utils/http';
	import { page } from '$app/stores';
	import { textReveal } from '$lib/components/textReveal';
	import Dropdown from '$lib/components/ui/dropdown/Dropdown.svelte';
	import logo from '$lib/assets/favicon.svg';

	let { data, form }: { data: PageData; form: ActionData } = $props();
	const t = $derived(createTranslator(data.locale));

	let localeFormEl: HTMLFormElement | undefined = $state();
	let email = $state('');
	let password = $state('');
	let showPassword = $state(false);
	let isSubmitting = $state(false);
	let unexpectedError = $state('');
	const displayError = $derived(unexpectedError || form?.message || '');

	$effect(() => {
		if (form?.email) {
			email = form.email;
		}
	});

	function fillDemoAccount(account: { email: string; defaultPassword?: string }) {
		email = account.email;
		password = account.defaultPassword ?? '';
	}
</script>

<svelte:head>
	<title>{t('auth.cardTitle')} · {t('brand.name')}</title>
</svelte:head>

<div class="login-screen">
	<!-- Hero / Brand Section (Left on desktop, below form on mobile) -->
	<section class="login-hero" aria-label={t('app.title')}>
		<div class="login-brand">
			<a href="/" class="login-brand-mark bg-transparent! p-0 w-11 h-11" title={t('auth.backToHome')}>
				<img src={logo} alt={t('brand.logoAlt')} class="w-full h-full logo-group" />
			</a>
			<div>
				<strong>{t('brand.name')}</strong>
				<span>{t('brand.tagline')}</span>
			</div>
		</div>

		<div class="login-copy-block">
			<h1 use:textReveal={{ startDelay: 0.15, stagger: 0.04 }}>{t('auth.title')}</h1>
			<p use:textReveal={{ startDelay: 0.4, stagger: 0.02 }}>{t('auth.subtitle')}</p>
		</div>

		<div class="login-proof-grid" aria-label={t('auth.proofLabel')}>
			<div>
				<strong>{t('auth.proofJwtLabel')}</strong>
				<span>{t('auth.proofSecure')}</span>
			</div>
			<div>
				<strong>24/7</strong>
				<span>{t('auth.proofTracking')}</span>
			</div>
			<div>
				<strong>{t('auth.proofIdLabel')}</strong>
				<span>{t('auth.proofLocale')}</span>
			</div>
		</div>
	</section>

	<!-- Main / Login Form Section (Right on desktop, top on mobile) -->
	<main class="login-main">
		<div class="login-tools">
			<a
				href="/"
				class="text-xs font-medium text-metal hover:text-ink transition-colors inline-flex items-center gap-1.5"
			>
				<span class="material-symbols-outlined text-[16px]">arrow_back</span>
				<span>{t('auth.backToHome')}</span>
			</a>

			<form bind:this={localeFormEl} method="POST" action="/locale" class="shrink-0">
				<input type="hidden" name="redirectTo" value={$page.url.pathname + $page.url.search} />
				<label class="sr-only" for="login-locale">{t('locale.label')}</label>
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
		</div>

		<section class="login-card" aria-labelledby="login-heading">
			<div class="login-card-header">
				<h2 id="login-heading">{t('auth.cardTitle')}</h2>
				<p>{t('auth.cardSubtitle')}</p>
			</div>

			<!-- Quick-fill demo credentials helper (Development only, strictly omitted in Production) -->
			{#if data.isDev && data.demoAccounts && data.demoAccounts.length > 0}
				<div class="mb-5 flex flex-wrap items-center gap-2 justify-start">
					{#each data.demoAccounts as account (account.userId)}
						<button
							type="button"
							class="inline-flex items-center gap-1.5 px-2.5 py-1 text-xs font-medium text-metal hover:text-ink bg-ash hover:bg-stone/60 border border-stone/80 rounded-full transition-colors cursor-pointer"
							onclick={() => fillDemoAccount(account)}
							title={`${account.name} (${account.role}) · ${account.email}`}
						>
							<span class="material-symbols-outlined text-[13px]">
								{account.email === 'admin@example.com' ? 'key' : 'badge'}
							</span>
							<span>{account.name}</span>
							<span class="text-[10px] text-secondary">({account.role})</span>
						</button>
					{/each}
				</div>
			{/if}

			<form
				class="login-form"
				method="POST"
				use:enhance={() => {
					isSubmitting = true;
					unexpectedError = '';
					return async ({ result, update }) => {
						try {
							if (result.type === 'error') {
								unexpectedError = formatEnhanceUnexpectedError(result.error, t);
								return;
							}
							await update();
						} finally {
							isSubmitting = false;
						}
					};
				}}
			>
				<input type="hidden" name="redirectTo" value={form?.redirectTo ?? data.redirectTo} />

				{#if displayError}
					<div class="form-error flex items-center gap-2" role="alert">
						<span class="material-symbols-outlined text-[16px] shrink-0">error</span>
						<span>{displayError}</span>
					</div>
				{/if}

				<div class="field">
					<label for="email">{t('auth.email')}</label>
					<input
						id="email"
						name="email"
						bind:value={email}
						type="email"
						placeholder={t('auth.emailPlaceholder')}
						autocomplete="email"
						required
					/>
				</div>

				<div class="field">
					<div class="login-label-row">
						<label for="password">{t('auth.password')}</label>
						<a href="#forgot" onclick={(event) => event.preventDefault()}>{t('auth.forgot')}</a>
					</div>
					<div class="relative flex items-center">
						<input
							id="password"
							name="password"
							bind:value={password}
							type={showPassword ? 'text' : 'password'}
							placeholder="••••••••"
							autocomplete="current-password"
							class="w-full pr-10"
							required
						/>
						<button
							type="button"
							class="absolute right-2.5 text-secondary hover:text-ink cursor-pointer p-0.5"
							onclick={() => (showPassword = !showPassword)}
							title={showPassword ? 'Hide password' : 'Show password'}
							tabindex="-1"
						>
							<span class="material-symbols-outlined text-[18px]">
								{showPassword ? 'visibility_off' : 'visibility'}
							</span>
						</button>
					</div>
				</div>

				<button class="button login-submit flex items-center justify-center gap-2" type="submit" disabled={isSubmitting}>
					{#if isSubmitting}
						<span class="material-symbols-outlined text-[16px] animate-spin">progress_activity</span>
					{/if}
					<span>{t('auth.submit')}</span>
				</button>
			</form>

			<p class="login-help">
				{t('auth.help')} <a href="#help" onclick={(event) => event.preventDefault()}>{t('auth.contactAdmin')}</a>
			</p>
		</section>

		<footer class="login-footer">
			<span>© {new Date().getFullYear()} {t('brand.name')}.</span>
			<a href="#status" onclick={(event) => event.preventDefault()}>{t('auth.status')}</a>
		</footer>
	</main>
</div>
