<script lang="ts">
	import favicon from '$lib/assets/favicon.svg';
	import '../app.css';
	import { page } from '$app/stores';
	import { createTranslator, locales } from '$lib/i18n';

	let { children, data } = $props();
	const t = $derived(createTranslator(data.locale));
</script>

<svelte:head>
	<link rel="icon" href={favicon} />
	<title>{t('app.title')}</title>
</svelte:head>

{#if $page.url.pathname.startsWith('/app') || $page.url.pathname.startsWith('/track/') || $page.url.pathname === '/login' || $page.url.pathname === '/'}
	{@render children()}
{:else}
	<div class="app-shell">
		<header class="topbar">
			<a class="brand" href="/">
				{t('brand.name')}
				<span>{t('brand.tagline')}</span>
			</a>

			<nav class="topnav" aria-label={t('nav.primary')}>
				<a href="/app">{t('nav.workspace')}</a>
				<a href="/login">{t('nav.login')}</a>
				<form method="POST" action="/locale">
					<input type="hidden" name="redirectTo" value={$page.url.pathname + $page.url.search} />
					<select name="locale" aria-label={t('locale.label')} onchange={(event) => event.currentTarget.form?.requestSubmit()}>
						{#each locales as locale}
							<option value={locale.code} selected={locale.code === data.locale}>{locale.label}</option>
						{/each}
					</select>
				</form>
			</nav>
		</header>

		<main>
			{@render children()}
		</main>
	</div>
{/if}
