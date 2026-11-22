<script lang="ts">
	import { enhance } from '$app/forms';
	import PageHeader from '$lib/components/PageHeader.svelte';
	import { createTranslator } from '$lib/i18n';
	import { toast } from '$lib/stores/toast.svelte';
	import { formatEnhanceUnexpectedError } from '$lib/utils/http';
	import type { ActionData, PageData } from './$types';

	let { data, form }: { data: PageData; form: ActionData } = $props();
	const t = $derived(createTranslator(data.locale));
	const initial = $derived(form?.settings ?? data.settings);

	let provider = $state('None');
	let environment = $state('Sandbox');
	let isEnabled = $state(false);
	let apiKey = $state('');
	let clearApiKey = $state(false);
	let webhookSecret = $state('');
	let clearWebhookSecret = $state(false);
	let defaultLinkExpiryHours = $state(24);
	let enablePublicTrackingPayment = $state(false);
	let requirePhoneVerificationForPublicPayment = $state(true);

	let saving = $state(false);
	let testing = $state(false);
	let rotating = $state(false);
	let copiedWebhook = $state(false);

	$effect(() => {
		provider = initial.provider || 'None';
		environment = initial.environment || 'Sandbox';
		isEnabled = initial.isEnabled;
		defaultLinkExpiryHours = initial.defaultLinkExpiryHours || 24;
		enablePublicTrackingPayment = initial.enablePublicTrackingPayment;
		requirePhoneVerificationForPublicPayment = initial.requirePhoneVerificationForPublicPayment;
		apiKey = '';
		clearApiKey = false;
		webhookSecret = '';
		clearWebhookSecret = false;
	});

	async function copyWebhookUrl() {
		if (!initial.webhookUrl || !navigator.clipboard) return;
		await navigator.clipboard.writeText(initial.webhookUrl);
		copiedWebhook = true;
		toast.success(t('settings.paymentGatewayCopied'));
		setTimeout(() => {
			copiedWebhook = false;
		}, 1800);
	}
</script>

<div class="space-y-lg pb-xl max-w-4xl">
	<PageHeader
		items={[
			{ label: t('common.console'), href: '/app' },
			{ label: t('nav.settings'), href: '/app/settings' },
			{ label: t('settings.paymentGateway') }
		]}
		title={t('settings.paymentGateway')}
		subtitle={t('settings.paymentGatewaySubtitle')}
	/>

	{#if !initial.workspaceEligible}
		<div class="rounded-xl border border-amber-300 bg-amber-50/80 px-4 py-3 text-body-sm text-amber-900 flex items-start gap-2.5">
			<span class="material-symbols-outlined text-[20px] text-amber-700 shrink-0 mt-0.5">info</span>
			<div>
				<strong class="font-semibold">{t('settings.paymentGatewayIneligibleBanner')}</strong>
				<span class="ml-1">{initial.workspaceEligibilityReason}</span>
			</div>
		</div>
	{/if}

	{#if !initial.canStoreSecrets}
		<div class="rounded-xl border border-rose-300 bg-rose-50/80 px-4 py-3 text-body-sm text-rose-900 flex items-start gap-2.5">
			<span class="material-symbols-outlined text-[20px] text-rose-700 shrink-0 mt-0.5">warning</span>
			<span>{t('settings.paymentGatewayEncryptionMissingBanner')}</span>
		</div>
	{:else if !initial.encryptionKeyConfigured}
		<div class="rounded-xl border border-stone bg-ash/60 px-4 py-2.5 text-caption text-secondary flex items-start gap-2">
			<span class="material-symbols-outlined text-[18px] text-secondary shrink-0 mt-0.5">shield</span>
			<span>{t('settings.paymentGatewayDevKeyNotice')}</span>
		</div>
	{/if}

	{#if form?.message}
		<div
			class="rounded-xl border px-4 py-3 text-body-sm {form.ok
				? 'border-emerald-200 bg-emerald-50 text-emerald-800'
				: 'border-rose-200 bg-rose-50 text-rose-800'}"
		>
			{form.message}
		</div>
	{/if}

	{#if form?.testResult}
		<div
			class="rounded-xl border px-4 py-3 text-body-sm flex items-center gap-2 {form.testResult.success
				? 'border-emerald-200 bg-emerald-50 text-emerald-800'
				: 'border-rose-200 bg-rose-50 text-rose-800'}"
		>
			<span class="material-symbols-outlined text-[18px]">
				{form.testResult.success ? 'check_circle' : 'error'}
			</span>
			<span>{form.testResult.message}</span>
		</div>
	{/if}

	<form
		method="POST"
		action="?/save"
		class="rounded-xl border border-stone bg-white p-md sm:p-lg shadow-2xs space-y-lg"
		use:enhance={() => {
			saving = true;
			return async ({ result, update }) => {
				try {
					if (result.type === 'error') {
						toast.error(formatEnhanceUnexpectedError(result.error, t));
						return;
					}
					await update({ reset: false });
				} finally {
					saving = false;
				}
			};
		}}
	>
		<!-- Provider & Environment -->
		<div class="grid grid-cols-1 md:grid-cols-2 gap-md">
			<div class="space-y-1.5">
				<label
					for="provider"
					class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block"
				>
					{t('settings.paymentGatewayProviderLabel')}
				</label>
				<select
					id="provider"
					name="provider"
					bind:value={provider}
					class="w-full h-10 rounded-input border border-stone bg-white px-3 text-body-sm text-primary focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary shadow-2xs"
				>
					<option value="None">{t('settings.paymentGatewayProviderNone')}</option>
					<option value="Xendit">{t('settings.paymentGatewayProviderXendit')}</option>
					<option value="Stripe">{t('settings.paymentGatewayProviderStripe')}</option>
				</select>
			</div>

			<div class="space-y-1.5">
				<label
					for="environment"
					class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block"
				>
					{t('settings.paymentGatewayEnvLabel')}
				</label>
				<select
					id="environment"
					name="environment"
					bind:value={environment}
					disabled={provider === 'None'}
					class="w-full h-10 rounded-input border border-stone bg-white px-3 text-body-sm text-primary focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary shadow-2xs disabled:bg-ash/50 disabled:text-secondary"
				>
					<option value="Sandbox">{t('settings.paymentGatewayEnvSandbox')}</option>
					<option value="Live">{t('settings.paymentGatewayEnvLive')}</option>
				</select>
			</div>
		</div>

		<!-- Master Enable Toggle -->
		<label
			class="flex items-start gap-sm rounded-xl border border-stone bg-ash/30 p-md text-body-sm text-secondary hover:bg-ash/60 transition-colors cursor-pointer select-none"
		>
			<input
				type="checkbox"
				name="isEnabled"
				bind:checked={isEnabled}
				disabled={provider === 'None' || !initial.workspaceEligible}
				class="mt-0.5 h-4 w-4 rounded border-stone text-primary focus:ring-primary"
			/>
			<div>
				<span class="font-semibold text-primary block">{t('settings.paymentGatewayEnableToggle')}</span>
				<span class="text-caption text-secondary block mt-0.5">{t('settings.paymentGatewayEnableHint')}</span>
			</div>
		</label>

		{#if provider !== 'None'}
			<!-- Encrypted Credentials -->
			<div class="border-t border-stone pt-lg space-y-md">
				<div class="space-y-1.5">
					<div class="flex flex-wrap items-center justify-between gap-2">
						<label
							for="apiKey"
							class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block"
						>
							{t('settings.paymentGatewayApiKey')}
						</label>
						{#if initial.hasApiKey}
							<span class="text-caption font-mono text-emerald-700 bg-emerald-50 border border-emerald-200 px-2 py-0.5 rounded-full">
								{t('settings.paymentGatewaySavedKeyHint')} ••••{initial.apiKeyLast4}
							</span>
						{/if}
					</div>
					<input
						id="apiKey"
						name="apiKey"
						type="password"
						autocomplete="new-password"
						bind:value={apiKey}
						placeholder={t('settings.paymentGatewayApiKeyPlaceholder')}
						class="w-full h-10 rounded-input border border-stone bg-white px-3 text-body-sm font-mono text-primary focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary shadow-2xs"
					/>
					{#if initial.hasApiKey}
						<label class="inline-flex items-center gap-1.5 text-caption text-secondary cursor-pointer select-none">
							<input type="checkbox" name="clearApiKey" bind:checked={clearApiKey} class="rounded border-stone" />
							<span>{t('settings.paymentGatewayClearKey')}</span>
						</label>
					{/if}
				</div>

				<div class="space-y-1.5">
					<div class="flex flex-wrap items-center justify-between gap-2">
						<label
							for="webhookSecret"
							class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block"
						>
							{t('settings.paymentGatewayWebhookSecret')}
						</label>
						{#if initial.hasWebhookSecret}
							<span class="text-caption font-mono text-emerald-700 bg-emerald-50 border border-emerald-200 px-2 py-0.5 rounded-full">
								{t('settings.paymentGatewaySavedKeyHint')} ••••{initial.webhookSecretLast4}
							</span>
						{/if}
					</div>
					<input
						id="webhookSecret"
						name="webhookSecret"
						type="password"
						autocomplete="new-password"
						bind:value={webhookSecret}
						placeholder={t('settings.paymentGatewayWebhookSecretPlaceholder')}
						class="w-full h-10 rounded-input border border-stone bg-white px-3 text-body-sm font-mono text-primary focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary shadow-2xs"
					/>
					{#if initial.hasWebhookSecret}
						<label class="inline-flex items-center gap-1.5 text-caption text-secondary cursor-pointer select-none">
							<input type="checkbox" name="clearWebhookSecret" bind:checked={clearWebhookSecret} class="rounded border-stone" />
							<span>{t('settings.paymentGatewayClearKey')}</span>
						</label>
					{/if}
				</div>

				<div class="space-y-1.5 max-w-xs">
					<label
						for="defaultLinkExpiryHours"
						class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block"
					>
						{t('settings.paymentGatewayExpiryHours')}
					</label>
					<input
						id="defaultLinkExpiryHours"
						name="defaultLinkExpiryHours"
						type="number"
						min="1"
						max="168"
						bind:value={defaultLinkExpiryHours}
						class="w-full h-10 rounded-input border border-stone bg-white px-3 text-body-sm font-mono text-primary focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary shadow-2xs"
					/>
					<p class="text-[11px] text-secondary m-0">{t('settings.paymentGatewayExpiryHint')}</p>
				</div>
			</div>

			<!-- Public Tracking Checkout Toggles -->
			<div class="border-t border-stone pt-lg space-y-sm">
				<span class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block">
					{t('settings.paymentGatewayPublicSection')}
				</span>

				<label
					class="flex items-start gap-sm rounded-xl border border-stone bg-ash/30 p-md text-body-sm text-secondary hover:bg-ash/60 transition-colors cursor-pointer select-none"
				>
					<input
						type="checkbox"
						name="enablePublicTrackingPayment"
						bind:checked={enablePublicTrackingPayment}
						disabled={!isEnabled}
						class="mt-0.5 h-4 w-4 rounded border-stone text-primary focus:ring-primary"
					/>
					<div>
						<span class="font-semibold text-primary block">{t('settings.paymentGatewayEnablePublic')}</span>
						<span class="text-caption text-secondary block mt-0.5">{t('settings.paymentGatewayEnablePublicHint')}</span>
					</div>
				</label>

				<label
					class="flex items-start gap-sm rounded-xl border border-stone bg-ash/30 p-md text-body-sm text-secondary hover:bg-ash/60 transition-colors cursor-pointer select-none"
				>
					<input
						type="checkbox"
						name="requirePhoneVerificationForPublicPayment"
						bind:checked={requirePhoneVerificationForPublicPayment}
						disabled={!enablePublicTrackingPayment}
						class="mt-0.5 h-4 w-4 rounded border-stone text-primary focus:ring-primary"
					/>
					<div>
						<span class="font-semibold text-primary block">{t('settings.paymentGatewayRequirePhone')}</span>
						<span class="text-caption text-secondary block mt-0.5">{t('settings.paymentGatewayRequirePhoneHint')}</span>
					</div>
				</label>
			</div>
		{:else}
			<input type="hidden" name="defaultLinkExpiryHours" value={defaultLinkExpiryHours} />
			{#if requirePhoneVerificationForPublicPayment}
				<input type="hidden" name="requirePhoneVerificationForPublicPayment" value="on" />
			{/if}
		{/if}

		<!-- Actions -->
		<div class="flex flex-wrap items-center justify-between gap-sm border-t border-stone pt-md">
			<a
				href="/app/settings"
				class="h-10 px-4 rounded-button border border-stone bg-white text-secondary hover:text-primary hover:bg-ash text-body-sm font-medium inline-flex items-center justify-center transition-colors"
			>
				{t('settings.cancel')}
			</a>
			<button
				type="submit"
				disabled={saving}
				class="h-10 px-5 rounded-button font-medium bg-ink text-white hover:bg-ink/90 shadow-xs text-body-sm cursor-pointer inline-flex items-center gap-1.5 disabled:opacity-60"
			>
				<span class="material-symbols-outlined text-[18px]">check</span>
				<span>{saving ? t('settings.saving') : t('settings.save')}</span>
			</button>
		</div>
	</form>

	<!-- Webhook URL & Connection Verification Card -->
	<section class="rounded-xl border border-stone bg-white p-md sm:p-lg shadow-2xs space-y-md">
		<div class="flex flex-wrap items-start justify-between gap-3">
			<div>
				<h3 class="font-subheading text-subheading font-semibold text-primary m-0">
					{t('settings.paymentGatewayWebhookUrlLabel')}
				</h3>
				<p class="text-caption text-secondary mt-0.5 m-0">{t('settings.paymentGatewayWebhookUrlHint')}</p>
			</div>
		</div>

		<div class="flex flex-col sm:flex-row gap-2">
			<input
				type="text"
				readonly
				value={initial.webhookUrl}
				class="flex-1 h-10 rounded-input border border-stone bg-ash/40 px-3 text-caption font-mono text-primary select-all"
			/>
			<button
				type="button"
				onclick={copyWebhookUrl}
				class="h-10 px-4 rounded-button border border-stone bg-white text-primary hover:bg-ash text-body-sm font-medium inline-flex items-center justify-center gap-1.5 cursor-pointer shrink-0"
			>
				<span class="material-symbols-outlined text-[17px]">{copiedWebhook ? 'check' : 'content_copy'}</span>
				<span>{copiedWebhook ? t('settings.paymentGatewayCopied') : t('settings.paymentGatewayCopyUrl')}</span>
			</button>
		</div>

		<div class="flex flex-wrap items-center gap-3 pt-2 border-t border-stone/70">
			{#if initial.provider !== 'None' && initial.hasApiKey}
				<form
					method="POST"
					action="?/testConnection"
					use:enhance={() => {
						testing = true;
						return async ({ result, update }) => {
							try {
								if (result.type === 'error') {
									toast.error(formatEnhanceUnexpectedError(result.error, t));
									return;
								}
								await update({ reset: false });
							} finally {
								testing = false;
							}
						};
					}}
				>
					<button
						type="submit"
						disabled={testing}
						class="h-9 px-3.5 rounded-button border border-stone bg-white text-primary hover:bg-ash text-caption font-semibold inline-flex items-center gap-1.5 cursor-pointer disabled:opacity-60"
					>
						<span class="material-symbols-outlined text-[16px]">wifi_tethering</span>
						<span>{testing ? t('settings.paymentGatewayTesting') : t('settings.paymentGatewayTestConnection')}</span>
					</button>
				</form>
			{/if}

			<form
				method="POST"
				action="?/rotateWebhook"
				onsubmit={(e) => {
					if (!confirm(t('settings.paymentGatewayRotateConfirm'))) {
						e.preventDefault();
					}
				}}
				use:enhance={() => {
					rotating = true;
					return async ({ result, update }) => {
						try {
							if (result.type === 'error') {
								toast.error(formatEnhanceUnexpectedError(result.error, t));
								return;
							}
							await update({ reset: false });
						} finally {
							rotating = false;
						}
					};
				}}
			>
				<button
					type="submit"
					disabled={rotating}
					class="h-9 px-3.5 rounded-button border border-rose-200 bg-white text-rose-700 hover:bg-rose-50 text-caption font-semibold inline-flex items-center gap-1.5 cursor-pointer disabled:opacity-60"
				>
					<span class="material-symbols-outlined text-[16px]">sync_lock</span>
					<span>{t('settings.paymentGatewayRotateWebhook')}</span>
				</button>
			</form>
		</div>
	</section>
</div>
